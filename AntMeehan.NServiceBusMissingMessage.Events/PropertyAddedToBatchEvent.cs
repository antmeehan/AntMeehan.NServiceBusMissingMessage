using NServiceBus;
using System;

namespace AntMeehan.NServiceBusMissingMessage.Events
{
    public class PropertyAddedToBatchEvent : IEvent
    {
        public string PropertyId { get; set; }
        public int BatchId { get; set; }
        public Guid UpdateId { get; set; }
    }
}
