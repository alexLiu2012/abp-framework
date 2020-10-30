using AboutModules.DemoDepends;
using Microsoft.Extensions.Options;
using System;
using Volo.Abp.DependencyInjection;

namespace AboutModules.Demo
{
    public class SomeServcie : ISingletonDependency
    {
        private readonly SomeDependsService _dependService;
        private readonly SomeOptions _options;

        public SomeServcie(
            SomeDependsService dependService,
            IOptions<SomeOptions> options)
        {
            _dependService = dependService;
            _options = options.Value;
        }

        public void Show()
        {
            _dependService.Greeting();
            Console.WriteLine(_options.Name + "----" + _options.Value);
        }
    }
}
