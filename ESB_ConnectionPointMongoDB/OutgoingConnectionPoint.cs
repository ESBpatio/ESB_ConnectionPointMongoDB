using ESB_ConnectionPoints.PluginsInterfaces;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NJsonSchema.Validation;
using NJsonSchema;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System.Globalization;

namespace ESB_ConnectionPointMongoDB
{
    public class OutgoingConnectionPoint : IDecoratedOutgoingConnectionPoint, IStandartOutgoingConnectionPoint, IOutgoingConnectionPoint, IConnectionPoint, IDisposable
    {
        private string connectionSettings;
        private string login;
        private string password;
        private string dbName;
        private int countThread;
        private bool isDebug;
        private string DailyTimeClearDB;
        private MongoClient Client;
        private readonly List<Guid> messagesInProcessing = new List<Guid>();
        private readonly Dictionary<string, RequestSettings> requestsSettings = new Dictionary<string, RequestSettings>();
        private readonly ILogger mainLogger;
        private readonly IMessageFactory messageFactory;

        public OutgoingConnectionPoint(string jsonSettings, IServiceLocator serviceLocator)
        {
            this.mainLogger = serviceLocator.GetLogger(this.GetType());
            this.messageFactory = serviceLocator.GetMessageFactory();
            this.ParseSettings(jsonSettings);
        }

        public void Initialize()
        {
            Client = ConnectionDB();
            Timer();
        }

        private void ParseSettings(string jsonSettings)
        {
            if (string.IsNullOrEmpty(jsonSettings))
            {
                throw new ArgumentException("Не задан параметр <jsonSettings>");
            }
            JObject jObject;
            try
            {
                jObject = JObject.Parse(jsonSettings);
            }
            catch (Exception ex)
            {

                throw new Exception("Не удалось разобрать строку настроек JSON! " + ex.Message);
            }

            connectionSettings = JsonUtils.StringValue(jObject, "connectionSettings", "");
            login = JsonUtils.StringValue(jObject, "login", "");
            password = JsonUtils.StringValue(jObject, "password", "");
            countThread = JsonUtils.IntValue(jObject, "threadCount", 1);
            isDebug = JsonUtils.BoolValue(jObject, "isDebug", false);
            dbName = JsonUtils.StringValue(jObject, "dbName", "");
            DailyTimeClearDB = JsonUtils.StringValue(jObject, "DailyTimeClearDB", "00:00:00");
            if (string.IsNullOrEmpty(dbName))
                throw new ArgumentException("Не задан параметр <dbName>");
            try
            {
                foreach (JToken selectToken in jObject.SelectTokens("Requests[*]"))
                {
                    RequestSettings requestSettings = new RequestSettings(selectToken);
                    this.requestsSettings.Add(requestSettings.ClassId, requestSettings);
                }
            }
            catch (Exception ex)
            {

                throw new Exception("Не удалось получить настройки запросов! " + ex.Message);
            }
        }

        private MongoClient ConnectionDB()
        {
            if(string.IsNullOrEmpty(login) && string.IsNullOrEmpty(password))
            {
                
                return new MongoClient("mongodb://" + this.connectionSettings + "/?readPreference=primary&appname=MongoDB%20Compass&ssl=false");
            }
            else
            {
                return new MongoClient("mongodb://" + login + ":" + password + "@" + this.connectionSettings + "/?authSource=admin&readPreference=primary&appname=MongoDB%20Compass&ssl=false");
            }
        }

        private void InsertMessage(Message message, ObjectCollection objectCollection, IMessageSource messageSource, IMongoCollection<ObjectCollection> collection)
        {
            DateTime startOperation = DateTime.Now;

            objectCollection.Body = message.Body;
            objectCollection.ClassId = message.ClassId;
            objectCollection.Id = message.Id;
            objectCollection.Type = message.Type;
            objectCollection.PackageNumber = int.Parse(messageUtils.getPropeties(message.Properties, "packageNumber"));
            objectCollection.Name = requestsSettings[message.ClassId].ObjectName;
            objectCollection.Date = message.CreationTime.AddHours(3);
            collection.InsertOne(objectCollection);
            if (isDebug)
                mainLogger.Debug((DateTime.Now - startOperation).ToString());
        }

          
        private void GetMessage(Message message, IMongoCollection<ObjectCollection> collection, IMessageReplyHandler replyHandler)
        {
            string jsonRequest = Encoding.UTF8.GetString(message.Body);
            JObject jRequest;
            try
            {
                jRequest = JObject.Parse(jsonRequest);
            }
            catch (Exception ex)
            {

                throw new Exception("Не удалось разобрать строку запроса сообщения JSON! " + ex.Message);
            }
            if (requestsSettings[message.ClassId].NeedSendResult)
            {
                CreateMessageToClient(message, replyHandler, jRequest, collection);
            }
            foreach (JToken selectToken in jRequest.SelectTokens("Requests[*]"))
            {
                string result = selectToken.ToString();
                if (message.ClassId == "456")
                {
                    result = result.Replace("p", "P");
                }
                BsonDocument filter = BsonDocument.Parse(result);
                List<ObjectCollection> document = collection.Find(filter).ToList();

                foreach (var item in document)
                {
                    Message messageReply = new Message
                    {
                        Id = Guid.NewGuid(),
                        Body = item.Body,
                        ClassId =  requestsSettings[message.ClassId].ReplyClassId,
                        Type = item.Type
                    };
                    replyHandler.HandleReplyMessage(messageReply);
                    Thread.Sleep((int)requestsSettings[message.ClassId].Delay * 1000);
                }
            }
        }

        public void CreateMessageToClient(Message message , IMessageReplyHandler messageReply,JObject jObject, IMongoCollection<ObjectCollection> collection)
        {
            List<string> packetsFound = new List<string>();
            List<string> packetsNotFound = new List<string>();

            foreach (JToken selectToken in jObject.SelectTokens("Requests[*]"))
            {
                string result = selectToken.ToString();
                if (message.ClassId == "456")
                {
                    result = result.Replace("p", "P");
                }
                BsonDocument filter = BsonDocument.Parse(result);
                List<ObjectCollection> document = collection.Find(filter).ToList();

                if (document.Count > 0)
                {
                    packetsFound.Add(JsonUtils.StringValue(selectToken, "PackageNumber"));
                }
                else
                {
                    packetsNotFound.Add(JsonUtils.StringValue(selectToken, "PackageNumber"));
                }
            }

                JObject rss = new JObject(
                new JProperty("packetsFound", packetsFound.ToArray()),
                new JProperty("packetsNotFound" , packetsNotFound.ToArray())
                );
            Message messageToClient = new Message
            {
                Id = Guid.NewGuid(),
                ClassId =  requestsSettings[message.ClassId].ReplyClassId,
                Receiver = message.Source,
                CorrelationId = message.Id,
                Type = "replyToClient",
                Body = Encoding.UTF8.GetBytes(rss.ToString())
            };
            messageToClient.SetPropertyWithValue("HTTP_StatusCode", "200");
            messageToClient.SetPropertyWithValue("HTTP_HDR_content-type", "application/json");
            messageReply.HandleReplyMessage(messageToClient);
        }

        public enum GetOperation
        {
            all,
            from,
            period
        }

        private void ProcessMessage(
          Message message,
          IMessageSource messageSource,
          IMessageReplyHandler replyHandler,
          CancellationToken ct)
        {
            try
            {
                bool flag = false;
                foreach (KeyValuePair<string, RequestSettings> requestSetting in this.requestsSettings)
                {
                    if (requestSetting.Key.Equals(message.ClassId))
                        flag = true;
                }
                if (!flag && message.Type.ToLower() != "blackhole")
                    throw new Exception("Не найдены настройки для обработки класса сообщения " + message.ClassId + " !");
                IMongoDatabase database = Client.GetDatabase(this.dbName);
                IMongoCollection<ObjectCollection> collection = database.GetCollection<ObjectCollection>(requestsSettings[message.ClassId].CollectionName);
                if (message.Type != "GET" && message.Type != "blackhole")
                {
                    InsertMessage(message, new ObjectCollection(), messageSource, collection);
                }
                else if (message.Type == "GET")
                {
                    GetMessage(message, collection, replyHandler);
                }
                CompletePeeklock(mainLogger, messageSource, message);
            }
            catch (Exception ex)
            {

                CompletePeeklock(mainLogger, messageSource, message, MessageHandlingError.UnknowError, ex.Message);
            }
        }

        public void Run(
          IMessageSource messageSource,
          IMessageReplyHandler replyHandler,
          CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {

                while (!ct.IsCancellationRequested && this.messagesInProcessing.Count < this.countThread)
                {
                    Message message = (Message)null;
                    try
                    {
                        message = messageSource.PeekLockMessage(ct, 10000);
                    }
                    catch (Exception ex)
                    {

                        this.mainLogger.Error("При получении сообщения из очереди произошла ошибка ", ex);
                    }

                    if (!(message == (Message)null) && !this.messagesInProcessing.Contains(message.Id) )
                    {
                        if (this.countThread > 1)
                            Task.Factory.StartNew((Action)(() => this.ProcessMessage(message, messageSource, replyHandler, ct)));
                        else
                            this.ProcessMessage(message, messageSource, replyHandler, ct);
                    }
                }
                ct.WaitHandle.WaitOne(10);
            }
        }

        private void AbandonPeeklock(
          ILogger logger,
          IMessageSource messageSource,
          Message message,
          string debugMessage = "")
        {
            messageSource.AbandonPeekLock(message.Id);
            this.messagesInProcessing.Remove(message.Id);
            if (string.IsNullOrEmpty(debugMessage))
                return;
            logger.Debug("Сообщение возвращено в очередь: " + debugMessage);
        }

        private void CompletePeeklock(
          ILogger logger,
          IMessageSource messageSource,
          Message message,
          MessageHandlingError messageHandlingError,
          string errorMessage)
        {
            messageSource.CompletePeekLock(message.Id, messageHandlingError, errorMessage);
            this.messagesInProcessing.Remove(message.Id);
            logger.Error("Сообщение не обработано и помещено в архив: " + errorMessage);
        }

        private void CompletePeeklock(ILogger logger, IMessageSource messageSource, Message message)
        {
            messageSource.CompletePeekLock(message.Id);
            this.messagesInProcessing.Remove(message.Id);
            logger.Debug("Сообщение обработано");
        }

        public bool IsReady()
        {
            return this.messagesInProcessing.Count < this.countThread;
        }

        public bool CanProcess(Message message)
        {
            return true;
        }

        Task IDecoratedOutgoingConnectionPoint.Process(
          Message message,
          IMessageSource messageSource,
          IMessageReplyHandler replyHandler,
          CancellationToken ct)
        {
            if (this.countThread > 1)
                return Task.Factory.StartNew((Action)(() => this.ProcessMessage(message, messageSource, replyHandler, ct)));
            this.ProcessMessage(message, messageSource, replyHandler, ct);
            return (Task)null;
        }

        public void Cleanup()
        {
        }

        public void Dispose()
        {
        }

        public void StartListener(
          IMessageSource messageSource,
          IMessageReplyHandler replyHandler,
          CancellationToken ct)
        {
        }

        public void CleanCollection()
        {
            var database = Client.GetDatabase(this.dbName);
            foreach (var item in requestsSettings)
            {
                var collection = database.GetCollection<ObjectCollection>(item.Value.CollectionName);
                var result = collection.DeleteMany(p => p.Date <= DateTime.Now.AddDays(-item.Value.DayDataStorage));
                mainLogger.Debug("Очистка коллекции " + item.Value.CollectionName + " выполнена успешна. Удалено : " + result.DeletedCount);
            }
        }

        public void Timer()
        {
            var timeParts = DailyTimeClearDB.Split(new char[1] { ':' });

            var dateNow = DateTime.Now;
            var date = new DateTime(dateNow.Year, dateNow.Month, dateNow.Day,
                       int.Parse(timeParts[0]), int.Parse(timeParts[1]), int.Parse(timeParts[2]));
            TimeSpan ts;
            if (date > dateNow)
                ts = date - dateNow;
            else
            {
                date = date.AddDays(1);
                ts = date - dateNow;
            }

            //waits certan time and run the code
            Task.Delay(ts).ContinueWith((x) => CleanCollection());
        }
    }
}
