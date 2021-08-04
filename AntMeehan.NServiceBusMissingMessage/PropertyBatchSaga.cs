using AntMeehan.NServiceBusMissingMessage.Events;
using log4net;
using NServiceBus;
using System.Configuration;
using System;
using System.Threading.Tasks;
using AntMeehan.NServiceBusMissingMessage.Commands;
using System.Collections.Generic;
using static Raven.Client.Constants;
using System.Linq;

namespace AntMeehan.NServiceBusMissingMessage
{
    public class PropertyBatchSaga : Saga<PropertyBatchSaga.State>,
        IAmStartedByMessages<PropertyAssignedBatchEvent>,
        IHandleMessages<PropertyAddedToBatchEvent>,
        IHandleTimeouts<PropertyBatchSaga.BatchTimeout>
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(PropertyBatchSaga));

        public class State : ContainSagaData
        {
            public int BatchId { get; set; }
            public bool TimeoutRequested { get; set; }
            public List<Guid> OutstandingUpdates { get; set; }
            public bool TimeoutRaised { get; set; }

            public State()
            {
                OutstandingUpdates = new List<Guid>();
            }

        }

        protected override void ConfigureHowToFindSaga(SagaPropertyMapper<State> mapper)
        {
            mapper.ConfigureMapping<PropertyAssignedBatchEvent>(x => x.BatchId).ToSaga(x => x.BatchId);
            mapper.ConfigureMapping<PropertyAddedToBatchEvent>(x => x.BatchId).ToSaga(x => x.BatchId);
        }

        public async Task Handle(PropertyAssignedBatchEvent message, IMessageHandlerContext context)
        {
            var updateId = Guid.NewGuid();
            Data.OutstandingUpdates.Add(updateId);

            _log.Info($"Batch assigned {message.PropertyId}, sending AddPropertyToBatch ({updateId})");

            await context.Send(new AddPropertyToBatchCommand()
            {
                PropertyId = message.PropertyId,
                BatchId =  message.BatchId,
                UpdateId = updateId,
            });

            await EnsureTimeout(context);
        }

        public Task Handle(PropertyAddedToBatchEvent message, IMessageHandlerContext context)
        {
            _log.Info($"Property {message.PropertyId} added to batch ({message.UpdateId})");
            if (Data.OutstandingUpdates.Remove(message.UpdateId))
            {
                _log.Info($"Batch {Data.BatchId} has been updated with Update {message.UpdateId}");
            }
            else
            {
                _log.Warn($"Batch {Data.BatchId} does not contain Update {message.UpdateId}, unable to remove from outstanding");
            }

            if (Data.TimeoutRaised && !Data.OutstandingUpdates.Any())
            {
                _log.Info($"Batch {Data.BatchId} ready for generation because of last outstanding batch update completed after timeout");
                var session = context.SynchronizedStorageSession.RavenSession();
                session.Delete(Documents.ActiveBatch.CalculateId());

                MarkAsComplete();
            }

            return Task.CompletedTask;
        }

        private async Task EnsureTimeout(IMessageHandlerContext context)
        {
            if (!Data.TimeoutRequested)
            {
                await RequestTimeout<BatchTimeout>(context, TimeSpan.FromSeconds(60));
                Data.TimeoutRequested = true;
            }
        }

        public Task Timeout(BatchTimeout state, IMessageHandlerContext context)
        {
            Data.TimeoutRaised = true;
            _log.Info("Batch timed out");
            if (Data.OutstandingUpdates.Any())
            {
                _log.Warn($"There are still outsanding updates!");
            }
            else
            {
                _log.Info($"Batch {Data.BatchId} ready for generation because of timeout and no outstanding batch updates");
                var session = context.SynchronizedStorageSession.RavenSession();
                session.Delete(Documents.ActiveBatch.CalculateId());

                MarkAsComplete();

            }



            return Task.CompletedTask;
        }

        

        public class BatchTimeout
        {

        }
    }
}
