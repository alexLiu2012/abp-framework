using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Volo.Abp;

namespace StartupDemo.App.WorkerServiceWithSerilog
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // configure log
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()                        
            .WriteTo.Console()
            .CreateLogger();

            try
            {
                Log.Information("Starting web host");

                var host = CreateHostBuilder(args).Build();
                var services = host.Services;

                var application = services.GetRequiredService<IAbpApplicationWithExternalServiceProvider>();
                application.Initialize(services);
                
                host.Run();               
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");                
            }
            finally
            {
                Log.CloseAndFlush();
            }           
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddApplication<MainModule>();
                })
                .UseSerilog();
    }
}
