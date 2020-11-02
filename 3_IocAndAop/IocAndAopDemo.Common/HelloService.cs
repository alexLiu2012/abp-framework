using System;
using Volo.Abp.DependencyInjection;

namespace IocAndAopDemo.Common
{
    public class HelloService : INiHao, IHelloService, ITransientDependency
    {
        public void Greeting()
        {
            Console.WriteLine("hello from hello service");
        }

        public void SayHi()
        {
            Console.WriteLine("hi from iNihao");
        }
    }
}
