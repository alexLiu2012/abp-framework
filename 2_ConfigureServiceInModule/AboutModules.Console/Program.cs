using AboutModules.Demo;
using System;
using Volo.Abp;
using Microsoft.Extensions.DependencyInjection;

namespace AboutModules.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var application = AbpApplicationFactory.Create<MainModule>())
            {
                application.Initialize();

                var someServcie = application.ServiceProvider.GetRequiredService<SomeServcie>();
                someServcie.Show();
            }

            Console.ReadLine();
        }
    }
}
