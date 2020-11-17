using System;
using System.Collections.Generic;
using System.Text;

using Volo.Abp.EventBus;
using Volo.Abp.Modularity;

namespace StackTools.LocalEventBus.ConsoleApp
{    
    [DependsOn(typeof(AbpEventBusModule))]
    public class MainModule : AbpModule
    {
    }
}
