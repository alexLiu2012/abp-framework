using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Volo.Abp;

namespace StartupDemo.App.WorkerServiceBasic
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            var services = host.Services;
            
            var application = services.GetRequiredService<IAbpApplicationWithExternalServiceProvider>();
            application.Initialize(services);

            host.Run();            
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddApplication<MainModule>();                    
                });
    }
}
