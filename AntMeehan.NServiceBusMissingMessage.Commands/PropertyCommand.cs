using NServiceBus;

namespace AntMeehan.NServiceBusMissingMessage.Commands
{
    public class PropertyCommand : ICommand
    {
        public string PropertyId { get; set; }
    }
}
