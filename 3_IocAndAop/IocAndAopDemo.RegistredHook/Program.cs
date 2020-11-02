using IocAndAopDemo.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using Volo.Abp;

namespace IocAndAopDemo.RegistredHook
{
    class Program
    {
        static void Main(string[] args)
        {
            var application = AbpApplicationFactory.Create<MainModule>(options =>
            {
                // with autofac, OnRegistred() will just inject the registred action but no execution
                options.UseAutofac();
            });

            application.Initialize();

            var hello = application.ServiceProvider.GetRequiredService<HelloService>();
            hello.Greeting();            
        }
    }
}
