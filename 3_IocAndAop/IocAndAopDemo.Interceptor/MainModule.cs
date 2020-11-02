using IocAndAopDemo.Common;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace IocAndAopDemo.InterceptService
{
    [DependsOn(typeof(AbpAutofacModule))]
    [DependsOn(typeof(CommonModule))]
    public class MainModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.OnRegistred(HelloInterceptor.Register);
            context.Services.OnRegistred(AlohaInterceptor.Register);                        
        }
    }
}
