using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Volo.Abp;

namespace StartupDemo.App.ConsoleWithSerilog
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()            
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

            using (var application = AbpApplicationFactory.Create<MainModule>(options =>
            {
                options.Services.AddSingleton<ILoggerFactory>(services => new SerilogLoggerFactory());
            }))
            {
                application.Initialize();                
            }
        }
    }
}
