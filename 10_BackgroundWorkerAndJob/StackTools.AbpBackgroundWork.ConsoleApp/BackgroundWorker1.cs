using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.BackgroundWorkers;

namespace StackTools.AbpBackgroundWork.ConsoleApp
{
    public class BackgroundWorker1 : IBackgroundWorker
    {
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("background worker 1 is started");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("background worker 1 is stopped");
            return Task.CompletedTask;
        }
    }
}
