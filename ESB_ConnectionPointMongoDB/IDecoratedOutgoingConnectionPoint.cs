using ESB_ConnectionPoints.PluginsInterfaces;
using System;
using System.Threading;
using System.Threading.Tasks;


namespace ESB_ConnectionPointMongoDB
{
    public interface IDecoratedOutgoingConnectionPoint : IStandartOutgoingConnectionPoint, IOutgoingConnectionPoint, IConnectionPoint, IDisposable
    {
        void StartListener(
          IMessageSource messageSource,
          IMessageReplyHandler replyHandler,
          CancellationToken ct);

        bool CanProcess(Message message);

        bool IsReady();

        Task Process(
          Message message,
          IMessageSource messageSource,
          IMessageReplyHandler replyHandler,
          CancellationToken ct);
    }
}
