using IocAndAopDemo.Common;
using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DynamicProxy;

namespace IocAndAopDemo.InterceptService
{
    public class HelloInterceptor : AbpInterceptor, ITransientDependency
    {
        public override async Task InterceptAsync(IAbpMethodInvocation invocation)
        {
            Console.WriteLine("in hello interceptor");
            await invocation.ProceedAsync();
            Console.WriteLine("out of hello interceptor");
        }

        public static void Register(IOnServiceRegistredContext registrationContext)
        {
            if (registrationContext.ImplementationType == typeof(HelloService))
            {
                registrationContext.Interceptors.TryAdd<HelloInterceptor>();
            }
        }
    }
}
