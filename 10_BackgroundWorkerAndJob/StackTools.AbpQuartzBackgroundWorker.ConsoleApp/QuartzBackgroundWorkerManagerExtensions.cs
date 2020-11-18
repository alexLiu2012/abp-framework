using Quartz;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Volo.Abp.BackgroundWorkers.Quartz;

namespace StackTools.AbpQuartzBackgroundWorker.ConsoleApp
{
    public static class QuartzBackgroundWorkerManagerExtensions
    {
        public static void Stop(this QuartzBackgroundWorkerManager manager, string name)
        {
            var schedulerField = manager.GetType().GetField("_scheduler", BindingFlags.Instance | BindingFlags.NonPublic);
            var scheduler = (IScheduler) schedulerField.GetValue(manager);

            scheduler.PauseAll();
            Console.WriteLine("all worker stopped");
        }
    }
}
