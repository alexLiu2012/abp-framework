using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Modularity;

namespace StackTools.BackgroundJob.ConsoleApp
{
    [DependsOn(typeof(AbpBackgroundJobsModule))]
    public class MainModule : AbpModule
    {
        public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
        {
            var jobWorker = context.ServiceProvider.GetRequiredService<IBackgroundJobWorker>();
        }
    }
}
