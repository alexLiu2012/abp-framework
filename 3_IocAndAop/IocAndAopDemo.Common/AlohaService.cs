using System;
using Volo.Abp.DependencyInjection;

namespace IocAndAopDemo.Common
{
    public class AlohaService : IAlohaService, ITransientDependency
    {
        public void Greeting()
        {
            Console.WriteLine("aloha from aloha service");
        }
    }
}
