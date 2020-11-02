using IocAndAopDemo.Common;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace IocAndAopDemo.ExposeService
{
    [DependsOn(typeof(CommonModule))]
    public class MainModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.OnExposing(exposingContext =>
            {
                // 手动指定暴露的服务类型
                // 在application范围内全部有效
                // 可以定义服务，在不同application暴露不同类型
                exposingContext.ExposedTypes.Add(typeof(INiHao));
            });
        }
    }
}
