using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.EventBus;

namespace StackTools.LocalEventBus.ConsoleApp
{
    public class TheAEvent2Handler : ILocalEventHandler<TheAEvent>
    {
        public Task HandleEventAsync(TheAEvent eventData)
        {
            Console.WriteLine($"this is from event2 handler: {eventData.Name}, {eventData.Description}");
            return Task.CompletedTask;
        }
    }
}
