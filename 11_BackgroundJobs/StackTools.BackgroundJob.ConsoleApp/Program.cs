using Microsoft.Extensions.DependencyInjection;
using System;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;

namespace StackTools.BackgroundJob.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = AbpApplicationFactory.Create<MainModule>(options => options.UseAutofac());
            app.Initialize();

            var manager = app.ServiceProvider.GetRequiredService<IBackgroundJobManager>();

            manager.EnqueueAsync(new MyJobArgs()
            {
                Name = "my job args",
                Description = "this is from an enqueued background job with args"
            });

            Console.ReadLine();
        }
    }
}
