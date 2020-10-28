using Volo.Abp.Modularity;
using StartupDemo.Common;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Microsoft.Extensions.Configuration;

namespace StartupDemo.App.WorkerServiceBasic
{
    [DependsOn(typeof(HelloWorldModule))]
    public class MainModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddHostedService<Worker>();
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var config = context.ServiceProvider.GetRequiredService<IConfiguration>();
           
            System.Console.WriteLine(config["demoKey"]);
        }
    }
}
