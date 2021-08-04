using AntMeehan.NServiceBusMissingMessage.Commands;
using AntMeehan.NServiceBusMissingMessage.Events;
using log4net;
using NServiceBus;
using Raven.Client.Documents;
using System;
using System.Threading.Tasks;

namespace AntMeehan.NServiceBusMissingMessage
{
    public class AddPropertyToBatchCommandHandler : IHandleMessages<AddPropertyToBatchCommand>
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(AddPropertyToBatchCommandHandler));
        private readonly IDocumentStore _documentStore;

        public AddPropertyToBatchCommandHandler(IDocumentStore documentStore)
        {
            _documentStore = documentStore;
        }

        public async Task Handle(AddPropertyToBatchCommand message, IMessageHandlerContext context)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;
                var reportBatch = await session.LoadAsync<Documents.ReportBatch>(Documents.ReportBatch.CalculateId(message.BatchId)) ?? new Documents.ReportBatch() { BatchId = message.BatchId };

                reportBatch.PropertyIds.Add(message.PropertyId);

                await Task.Delay(TimeSpan.FromMilliseconds(200));

                await session.StoreAsync(reportBatch);
                await session.SaveChangesAsync();
            }
            
            _log.Info($"Got AddPropertyToBatchCommand ({message.UpdateId}), raising {nameof(PropertyAddedToBatchEvent)}");
            await context.Publish(new PropertyAddedToBatchEvent()
            {
                PropertyId = message.PropertyId,
                BatchId = message.BatchId,
                UpdateId = message.UpdateId,
            });
        }
    }
}
