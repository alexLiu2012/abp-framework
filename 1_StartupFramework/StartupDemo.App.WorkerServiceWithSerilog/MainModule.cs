using Microsoft.Extensions.DependencyInjection;
using StartupDemo.Common;
using Volo.Abp.Modularity;

namespace StartupDemo.App.WorkerServiceWithSerilog
{
    [DependsOn(typeof(HelloWorldModule))]
    public class MainModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddHostedService<Worker>();
        }
    }
}
