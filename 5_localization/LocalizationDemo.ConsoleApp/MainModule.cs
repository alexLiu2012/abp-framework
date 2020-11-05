using Volo.Abp.VirtualFileSystem;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;

namespace LocalizationDemo.ConsoleApp
{
    [DependsOn(typeof(AbpLocalizationModule))]    
    public class MainModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            // add resource directory
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.AddEmbedded<MainModule>("LocalizationDemo.ConsoleApp");
            });

            Configure<AbpLocalizationOptions>(options =>
            {
                options.Resources.Add<MyBaseResource>().AddVirtualJson("/baseResources");
                options.Resources.Add<MyResource>().AddVirtualJson("/resources");
            });
        }
    }
}
