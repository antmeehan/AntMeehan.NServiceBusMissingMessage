using AntMeehan.NServiceBusMissingMessage.Commands;
using AntMeehan.NServiceBusMissingMessage.Events;
using log4net;
using NServiceBus;
using Raven.Client.Documents;
using System.Threading.Tasks;

namespace AntMeehan.NServiceBusMissingMessage
{
    public class PropertyCommandHandler : IHandleMessages<PropertyCommand>
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(PropertyCommandHandler));
        private readonly IDocumentStore _documentStore;

        public PropertyCommandHandler(IDocumentStore documentStore)
        {
            _documentStore = documentStore;
        }

        public async Task Handle(PropertyCommand message, IMessageHandlerContext context)
        {
            _log.Info($"Got PropertyCommand for {message.PropertyId}, raising {nameof(PropertyAssignedBatchEvent)}");

            Documents.ActiveBatch activeBatch;
            using (var session = _documentStore.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;
                activeBatch = await session.LoadAsync<Documents.ActiveBatch>(Documents.ActiveBatch.CalculateId()).ConfigureAwait(false);
                if (activeBatch == null)
                {
                    var count = new Documents.BatchCount();
                    await session.StoreAsync(count);
                    await session.SaveChangesAsync();

                    activeBatch = new Documents.ActiveBatch()
                    {
                        BatchId = count.GetBatchId(),
                    };
                    await session.StoreAsync(activeBatch, string.Empty, activeBatch.Id).ConfigureAwait(false);
                    await session.SaveChangesAsync();
                }
            }


            await context.Publish(new PropertyAssignedBatchEvent()
            {
                PropertyId = message.PropertyId,
                BatchId = activeBatch.BatchId,
            });
        }
    }
}
