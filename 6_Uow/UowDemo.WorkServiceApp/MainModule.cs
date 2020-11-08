using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace UowDemo.WorkServiceApp
{
    [DependsOn(typeof(AbpUnitOfWorkModule))]
    public class MainModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddHostedService<Worker>();
        }
    }
}