using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace StackTools.LocalEventBus.ConsoleApp
{
    public class TheAEventHandler : ILocalEventHandler<TheAEvent>/*, ITransientDependency*/
    {
        public Task HandleEventAsync(TheAEvent eventData)
        {
            Console.WriteLine($"got something from A event: {eventData.Name}, {eventData.Description}");
            return Task.CompletedTask;
        }
    }
}
