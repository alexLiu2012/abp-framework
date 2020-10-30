using Volo.Abp.Modularity;

namespace AboutModules.DemoDepends
{
    public class SomeDependsModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<SomeDependsModuleOptions>(options =>
            {
                options.Name = "hello depends";
                options.Value = "from some depends";
            });
        }
    }
}
