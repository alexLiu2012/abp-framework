using Microsoft.Extensions.Localization;
using System;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;
using Volo.Abp.Localization.Resources.AbpLocalization;

namespace LocalizationDemo.ConsoleApp
{
    public class MyService : ISingletonDependency
    {
        private IStringLocalizer<MyResource> _mylocalizer;
        private IStringLocalizer<DefaultResource> _defLocalizer;
        private IStringLocalizer<AbpLocalizationResource> _abpLocalizer;

        public MyService(IStringLocalizer<MyResource> myLocalizer, IStringLocalizer<DefaultResource> defLocalizer, IStringLocalizer<AbpLocalizationResource> abpLocalizer)                        
        {            
            _mylocalizer = myLocalizer;

            _defLocalizer = defLocalizer;
            _abpLocalizer = abpLocalizer;
        }

        public void Greeting()
        {
            var defs = _defLocalizer.GetAllStrings();
            var abps = _abpLocalizer.GetAllStrings();

            var a = _mylocalizer.GetAllStrings();

            Console.WriteLine($"hello in my language: { _mylocalizer["hello"] }");
            Console.WriteLine($"aloha in my language: { _mylocalizer["aloha"] }");
        }
    }
}
