using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESB_ConnectionPointMongoDB
{
    public class RequestSettings
    {
        public string CollectionName { get;  set; }
        public string ObjectName { get;  set; }
        public string ClassId { get; set; }
        public bool IsDebugMode { get;  set; }
        public int DayDataStorage { get; set; }
        public string ReplyClassId { get; set; }
        public int Delay { get; set; }
        public bool NeedSendResult { get; set; }

        public RequestSettings(JToken jToken)
        {
            this.CollectionName = jToken.StringValue("collectionName", "notSorted");
            this.IsDebugMode = jToken.BoolValue("isDebugMode", false);
            this.ObjectName = jToken.StringValue("name", "unknown");
            this.ClassId = jToken.StringValue("classId");
            this.ReplyClassId = jToken.StringValue("replyClassId", "555");
            this.DayDataStorage = jToken.IntValue("dayDataStorage", 30);
            this.Delay = jToken.IntValue("delay");
            this.NeedSendResult = jToken.BoolValue("needSendResult");
        }
    }
}
