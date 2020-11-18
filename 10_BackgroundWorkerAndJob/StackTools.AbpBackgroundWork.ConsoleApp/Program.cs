using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;

namespace StackTools.AbpBackgroundWork.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = AbpApplicationFactory.Create<MainModule>(/*options => options.UseAutofac()*/);
            app.Initialize();            

            // manual register background worker 1
            var manager = app.ServiceProvider.GetRequiredService<IBackgroundWorkerManager>();            
            manager.Add(new BackgroundWorker1());

            // register periodic and async periodic worker 2/3 in module -> OnApplicationInitialization()
            
            Console.ReadLine();
        }
    }
}
