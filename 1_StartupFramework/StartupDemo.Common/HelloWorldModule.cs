using System;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace StartupDemo.Common
{
    public class HelloWorldModule : AbpModule
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            Console.WriteLine($"hello from {nameof(HelloWorldModule)}");
        }
    }
}
