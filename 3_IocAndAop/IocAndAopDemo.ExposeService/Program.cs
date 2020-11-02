using IocAndAopDemo.Common;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;

namespace IocAndAopDemo.ExposeService
{
    class Program
    {
        static void Main(string[] args)
        {
            var application = AbpApplicationFactory.Create<MainModule>(options =>
            {

            });

            application.Initialize();

            var hello_interface = application.ServiceProvider.GetRequiredService<IHelloService>();
            hello_interface.Greeting();

            var hello_class = application.ServiceProvider.GetRequiredService<HelloService>();
            hello_class.Greeting();

            var hello_nihao = application.ServiceProvider.GetRequiredService<INiHao>();
            hello_nihao.SayHi();
            
        }
    }
}
