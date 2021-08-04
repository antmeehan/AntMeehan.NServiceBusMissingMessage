using AntMeehan.NServiceBusMissingMessage.Commands;
using AntMeehan.NServiceBusMissingMessage.Events;
using NServiceBus;
using System.Threading.Tasks;

namespace AntMeehan.NServiceBusMissingMessage
{
    public class AccountEventHandler : IHandleMessages<AccountEvent>
    {        
        public async Task Handle(AccountEvent message, IMessageHandlerContext context)
        {
            foreach (var propertyId in new[] { "PropId1", "PropId2" })
            {
                await context.Send(new PropertyCommand() { PropertyId = propertyId });
            }
        }
    }
}
