using System;
using Volo.Abp.DependencyInjection;

namespace AboutModules.DemoDepends
{
    public class SomeDependsService : ISingletonDependency
    {
        public void Greeting()
        {
            Console.WriteLine("hello from dependent service");
        }
    }
}
