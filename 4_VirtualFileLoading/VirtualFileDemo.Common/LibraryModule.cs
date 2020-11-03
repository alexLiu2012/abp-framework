using System;
using System.IO;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace VirtualFileDemo.Common
{
    [DependsOn(typeof(AbpVirtualFileSystemModule))]
    public class LibraryModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                // add manifest
                options.FileSets.AddEmbedded<LibraryModule>();

                // add physical
                options.FileSets.AddPhysical(Path.Combine(Directory.GetCurrentDirectory(), "settings"));
            });
        }
    }
}
