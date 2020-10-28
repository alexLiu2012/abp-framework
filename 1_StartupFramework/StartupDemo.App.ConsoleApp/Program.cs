using Volo.Abp;

namespace StartupDemo.App.ConsoleBasic
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var application = AbpApplicationFactory.Create<MainModule>())
            {
                application.Initialize();
            }
        }
    }
}
