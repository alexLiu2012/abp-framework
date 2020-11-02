using IocAndAopDemo.Common;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;

namespace IocAndAopDemo.InterceptService
{
    class Program
    {
        static void Main(string[] args)
        {            
            var application = AbpApplicationFactory.Create<MainModule>(options =>
            {
                options.UseAutofac();
            });
            
            application.Initialize();

            var hello = application.ServiceProvider.GetRequiredService<IHelloService>();
            hello.Greeting();

            var aloha = application.ServiceProvider.GetRequiredService<IAlohaService>();
            aloha.Greeting();
        }
    }
}
