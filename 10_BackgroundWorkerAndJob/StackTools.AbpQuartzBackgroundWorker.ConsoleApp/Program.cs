using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.BackgroundWorkers.Quartz;

namespace StackTools.AbpQuartzBackgroundWorker.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = AbpApplicationFactory.Create<MainModule>(options => options.UseAutofac());
            app.Initialize();

            var manager = app.ServiceProvider.GetRequiredService<IBackgroundWorkerManager>() as QuartzBackgroundWorkerManager;

            // quartz background worker registered automatically,
            // can be disabled in worker or options
            

            // extend method with IScheduler, to do in future ...

            Console.WriteLine("press p to pause all worker");
            var input = Console.ReadLine();

            if(input == "p")
            {
                manager.Stop("hello");
            }

            Console.WriteLine("press any key to quit");
            Console.ReadLine();
        }
    }
}
