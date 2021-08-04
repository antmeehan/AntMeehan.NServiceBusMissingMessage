using NServiceBus;
using System;

namespace AntMeehan.NServiceBusMissingMessage.Commands
{
    public class AddPropertyToBatchCommand : ICommand
    {
        public string PropertyId { get; set; }
        public int BatchId { get; set; }
        public Guid UpdateId { get; set; }
    }
}
