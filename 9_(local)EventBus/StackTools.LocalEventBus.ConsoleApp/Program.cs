using Microsoft.Extensions.DependencyInjection;
using System;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace StackTools.LocalEventBus.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = AbpApplicationFactory.Create<MainModule>(options => options.UseAutofac());
            app.Initialize();

            var services = app.ServiceProvider;
            var eventBus = services.GetRequiredService<ILocalEventBus>();

            // publish event will be handled by the auto registrered handlers
            // which had been registered with IxxxDependency
            eventBus.PublishAsync(new TheAEvent()
            {
                Name = "A event",
                Description = "a demo of A event published"
            });

            // manual registrered handler
            eventBus.Subscribe(new TheAEvent2Handler());
            eventBus.PublishAsync(new TheAEvent()
            {
                Name = "another A event",
                Description = "this is another event of event A published"
            });

            Console.WriteLine("----------------------------");

            var service = services.GetRequiredService<MyService>();
            service.SendInfo("hello world");            

            Console.ReadLine();
        }
    }
}
