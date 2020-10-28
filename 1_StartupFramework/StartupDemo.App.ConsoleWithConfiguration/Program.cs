using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using Volo.Abp;

namespace StartupDemo.App.ConsoleWithConfiguration
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var application = AbpApplicationFactory.Create<MainModule>(options =>
            {
                options.Configuration.BasePath = Directory.GetCurrentDirectory();
                options.Configuration.FileName = "configuration";
            }))
            {
                application.Initialize();

                var config = application.ServiceProvider.GetRequiredService<IConfiguration>();
                Console.WriteLine(config["name"]);
            }
        }
    }
}
