using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StartupDemo.Common;
using Volo.Abp;
using Volo.Abp.Modularity;


namespace StartupDemo.App.WorkerServiceWithConfiguration
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

            var originValue = config["originKey"];
            var currentValue = config["currentKey"];

            System.Console.WriteLine($"origin: {originValue}, current: {currentValue}");
        }
    }
}
