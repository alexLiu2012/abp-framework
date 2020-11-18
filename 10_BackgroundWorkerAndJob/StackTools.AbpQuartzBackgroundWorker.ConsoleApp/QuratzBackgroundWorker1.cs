using Microsoft.Extensions.DependencyInjection;
using Quartz;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.BackgroundWorkers.Quartz;
using Volo.Abp.DependencyInjection;

namespace StackTools.AbpQuartzBackgroundWorker.ConsoleApp
{    
    [Dependency(ServiceLifetime.Transient)]
    public class QuartzBackgroundWorker1 : QuartzBackgroundWorkerBase
    {
        public QuartzBackgroundWorker1()
        {
            JobDetail = JobBuilder
                .Create<QuartzBackgroundWorker1>()
                .WithIdentity("worker 1")
                .Build();

            Trigger = TriggerBuilder
                .Create()
                .WithIdentity("worker 1")
                .WithSimpleSchedule(builder =>
                    builder.WithIntervalInSeconds(5)
                    .RepeatForever())
                .StartNow()
                .Build();

            AutoRegister = true;
                
        }

        public override Task Execute(IJobExecutionContext context)
        {
            Console.WriteLine("this is from quartz background job");
            return Task.CompletedTask;
        }
    }
}
