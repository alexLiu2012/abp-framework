using StartupDemo.Common;
using Volo.Abp.Modularity;

namespace StartupDemo.App.ConsoleBasic
{
    [DependsOn(typeof(HelloWorldModule))]
    public class MainModule : AbpModule
    {
    }
}
