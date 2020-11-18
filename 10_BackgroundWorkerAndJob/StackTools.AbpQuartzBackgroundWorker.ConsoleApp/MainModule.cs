using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers.Quartz;
using Volo.Abp.Modularity;

namespace StackTools.AbpQuartzBackgroundWorker.ConsoleApp
{    
    [DependsOn(typeof(AbpBackgroundWorkersQuartzModule))]
    public class MainModule : AbpModule
    {       
    }
}
