using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace StackTools.BackgroundJob.ConsoleApp
{
    public class MyJob : BackgroundJob<MyJobArgs>, ITransientDependency
    {
        public override void Execute(MyJobArgs args)
        {
            Console.WriteLine($"from my job, name {args.Name}, desp {args.Description}");
        }
    }
}
