using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using Volo.Abp;

namespace LocalizationDemo.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var application = AbpApplicationFactory.Create<MainModule>();

            application.Initialize();
          
            var singletonSvc = application.ServiceProvider.GetRequiredService<MyService>();

            Console.WriteLine("output in de");
            CultureInfo.CurrentUICulture = new CultureInfo("de");
            singletonSvc.Greeting();
            Console.WriteLine("-----------------------------");

            Console.WriteLine("output in en");
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            singletonSvc.Greeting();
            Console.WriteLine("-----------------------------");
        }
    }
}
