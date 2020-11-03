using System.IO;
using VirtualFileDemo.Common;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace VirtualFileDemo.ConsoleApp
{
    [DependsOn(typeof(AbpVirtualFileSystemModule))]
    [DependsOn(typeof(LibraryModule))]
    public class MainModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {                
                // 注册 manifest resource
                // 引用 ms fileprovider embedded 包
                // 添加 manifest=true 属性
                options.FileSets.AddEmbedded<MainModule>();

                // 注册 physical resource
                options.FileSets.AddPhysical(Path.Combine(Directory.GetCurrentDirectory(), "settings"));
            });
        }
    }
}
