using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace StackTools.AbpBackgroundWork.ConsoleApp
{
    public class AsyncPeriodBackgroundWorker3 : AsyncPeriodicBackgroundWorkerBase
    {
        public AsyncPeriodBackgroundWorker3(AbpTimer timer, IServiceScopeFactory serviceScopeFactory) : base(timer, serviceScopeFactory)
        {
            if(timer.Period == 0)
            {
                timer.Period = 2000;
            }
        }
        protected override Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
        {
            Console.WriteLine("from async background worker 3");
            return Task.CompletedTask;
        }
    }
}
