using StartupDemo.Common;
using Volo.Abp.Modularity;

namespace StartupDemo.App.ConsoleWithConfiguration
{
    [DependsOn(typeof(HelloWorldModule))]
    public class MainModule : AbpModule
    {
    }
}
