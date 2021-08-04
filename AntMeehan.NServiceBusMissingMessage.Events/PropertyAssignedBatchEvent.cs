using NServiceBus;

namespace AntMeehan.NServiceBusMissingMessage.Events
{
    public class PropertyAssignedBatchEvent : IEvent
    {
        public string PropertyId {  get; set; }
        public int BatchId { get; set;  }
    }
}
