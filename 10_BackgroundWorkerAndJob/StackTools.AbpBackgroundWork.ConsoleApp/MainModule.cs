using Volo.Abp.Modularity;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp;
using Volo.Abp.Autofac;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.Threading;

namespace StackTools.AbpBackgroundWork.ConsoleApp
{
    //[DependsOn(typeof(AbpAutofacModule))]
    [DependsOn(typeof(AbpBackgroundWorkersModule))]
    
    public class MainModule : AbpModule
    {        
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var factory = context.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var timer = context.ServiceProvider.GetRequiredService<AbpTimer>();
            var worker2 = context.ServiceProvider.GetRequiredService<PeriodBackgroundWorker2>();
            var worker3 = context.ServiceProvider.GetRequiredService<AsyncPeriodBackgroundWorker3>();

            context.AddBackgroundWorker<PeriodBackgroundWorker2>();
            context.AddBackgroundWorker<AsyncPeriodBackgroundWorker3>();
        }
    }
}
