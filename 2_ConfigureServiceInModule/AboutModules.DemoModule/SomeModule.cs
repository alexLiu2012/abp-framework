using AboutModules.DemoDepends;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AboutModules.Demo
{
    [DependsOn(typeof(SomeDependsModule))]
    public class SomeModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            PreConfigure<SomePreOptions>(options =>
            {
                options.PreName = "some pre option";
                options.PreValue = "some pre value";
            });
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var preOptions = context.Services.ExecutePreConfiguredActions<SomePreOptions>();

            Configure<SomeOptions>(options =>
            {
                options.Name = preOptions.PreName + "acted";
                options.Value = preOptions.PreValue + "acted";
            });
        }
    }
}
