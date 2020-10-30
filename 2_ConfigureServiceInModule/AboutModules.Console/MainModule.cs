using AboutModules.Demo;
using Volo.Abp.Modularity;

namespace AboutModules.ConsoleApp
{
    [DependsOn(typeof(SomeModule))]
    public class MainModule : AbpModule
    {
    }
}