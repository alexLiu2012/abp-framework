using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StartupDemo.Common;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace StartupDemo.App.ConsoleWithSerilog
{
    [DependsOn(typeof(HelloWorldModule))]
    public class MainModule : AbpModule
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var logger = context.ServiceProvider.GetRequiredService<ILogger<MainModule>>();

            logger.LogInformation("a log information from main module");           
        }
    }
}
