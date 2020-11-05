using Volo.Abp.Localization;

namespace LocalizationDemo.ConsoleApp
{
    [LocalizationResourceName("the_resource")]
    [InheritResource(typeof(MyBaseResource))]
    public class MyResource
    {
    }
}
