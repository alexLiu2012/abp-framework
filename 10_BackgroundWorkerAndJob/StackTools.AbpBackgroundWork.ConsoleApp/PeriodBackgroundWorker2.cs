using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace StackTools.AbpBackgroundWork.ConsoleApp
{
    public class PeriodBackgroundWorker2 : PeriodicBackgroundWorkerBase
    {
        public PeriodBackgroundWorker2(AbpTimer timer, IServiceScopeFactory factory) : base(timer, factory)
        {
            if(timer.Period == 0)
            {
                timer.Period = 2000;
            }
        }
        protected override void DoWork(PeriodicBackgroundWorkerContext workerContext)
        {
            Console.WriteLine("periodic worker 2 is started");           
        }
    }
}
