using IocAndAopDemo.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace IocAndAopDemo.RegistredHook
{
    [DependsOn(typeof(AbpAutofacModule))]
    [DependsOn(typeof(CommonModule))]
    public class MainModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.OnRegistred(registration =>
            {
                if (registration.ImplementationType == typeof(HelloService))
                {
                    Console.WriteLine("a hello service registered");
                }
            });
        }
    }
}
