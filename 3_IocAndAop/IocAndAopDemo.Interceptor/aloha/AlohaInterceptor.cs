using IocAndAopDemo.Common;
using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DynamicProxy;

namespace IocAndAopDemo.InterceptService
{
    public class AlohaInterceptor : AbpInterceptor, ITransientDependency
    {        
        public override async Task InterceptAsync(IAbpMethodInvocation invocation)
        {
            Console.WriteLine("in the aloha interceptor");
            await invocation.ProceedAsync();
            Console.WriteLine("out of aloha interceptor");
        }

        public static void Register(IOnServiceRegistredContext registrationContext)
        {
            if (registrationContext.ImplementationType == typeof(AlohaService))
            {
                registrationContext.Interceptors.TryAdd<AlohaInterceptor>();
            }
        }
    }

    
}
