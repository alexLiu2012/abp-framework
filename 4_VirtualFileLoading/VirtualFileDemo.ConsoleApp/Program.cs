using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System;
using Volo.Abp;
using Volo.Abp.VirtualFileSystem;

namespace VirtualFileDemo.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var application = AbpApplicationFactory.Create<MainModule>(options =>
            {
            });

            application.Initialize();

            var fileProvider = application.ServiceProvider.GetRequiredService<IVirtualFileProvider>();

            /* load resource file in this assembly */
            Console.WriteLine("loading from this assembly");
            var thisManifestFile = fileProvider.GetFileInfo("/settings/manifest_setting.json");            
            Console.WriteLine(thisManifestFile.ReadAsString());
            var thisPhysicalFile = fileProvider.GetFileInfo("physical_setting.json");
            Console.WriteLine(thisPhysicalFile.ReadAsString());
            Console.WriteLine("-----------------------------------------------------------------------------");

            /* load resource file from dependent assembly*/
            Console.WriteLine("loading from dependent assembly");
            var baseManifestFile = fileProvider.GetFileInfo("/settings/base_manifest_setting.json");
            Console.WriteLine(baseManifestFile.ReadAsString());
            var basePhysicalFile = fileProvider.GetFileInfo("base_physical_setting.json");
            Console.WriteLine(basePhysicalFile.ReadAsString());
            Console.WriteLine("-----------------------------------------------------------------------------");

            /* get resource file conflict */
            Console.WriteLine("loading conflict");
            var conflictManifestFile = fileProvider.GetFileInfo("/settings/conflict_manifest_setting.json");
            Console.WriteLine(conflictManifestFile.ReadAsString());
            var conflictPhysiclaFile = fileProvider.GetFileInfo("conflict_physical_setting.json");
            Console.WriteLine(conflictPhysiclaFile.ReadAsString());
            Console.WriteLine("-----------------------------------------------------------------------------");            
        }
    }
}
