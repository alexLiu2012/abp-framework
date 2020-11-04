## about localization

#### 1. concept

abp框架集成了localization

使用了 microsoft.extensions.localization 并做了扩展，比如加载json文件作为 localizing 资源

* `ILocalizableString`是localizing的统一抽象

  ```c#
  public interface ILocalizableString
  {
      LocalizedString Localize(IStringLocalizerFactory stringLocalizerFactory);
  }
  
  ```

  * `LocalizableString`是实现

  * `FixedLocalizableString`是另一种实现，即 key、value都是自身的 localized_string，

    用于在开发环境使用没有完成的 localized_string（display name）

* 定义和标记 localizing 资源

  * 定义 resource class

    plain class类型，空定义，作为泛型参数在逻辑上聚合相关 localizing_string

    module可以定义自己的 resource_class

    ```c#
    public class MyLocalizationResource
    {    
    }
    ```

  * Json 资源文件定义

    abp可以加载Json文件作为资源文件，框架可以自解析，它的定义如下：

    ```c#
    public class JsonLocalizationFile
    {
        public string Culture { get;set; }
        public Dictionary<string,string> Texts { get; }
        
        public JsonLocalizationFile()
        {
            Texts = new Dictionary<string,string>();
        }
    }
    
    ```

    例子

    ```json
    {
        "culture": "en",
        "texts": {
            "HelloWorld": "Hello World"
        }
    }
    
    ```

  * 特性标记资源

    * 可以使用特性标记 resource_class 名称，从而简化使用

      ```c#
      [LocalizationResourceName("MyLocalization")]
      public class MyLocalizationResource
      {    
      }
      
      ```

    * 可以使用特性标记 resource 的基类，从而继承 base_resource

      ```c#
      [InheritResource(typeof(MyBaseResource))]
      public class MyLocalizationResource
      {    
      }
      
      ```

* 注册`IStringLocalizer`

  在`AbpLocalizationModule`模块中替换 ms 服务

  ```c#
  [DependsOn(typeof(AbpLocalizationAbstractModule))]
  public class AbpLocalizationModule : Module
  {
      public override void ConfigureServices(ServiceConfigurationContext context)
      {
          // 替换IStringLocalizerFactory并注册
          AbpStringLocalizerFactory.Replace(context.Services);        
      }
  }
  
  ```

  `AbpStringLocalizerFactory`是abp框架定义的扩展

  ```c#
  public class AbpStringLocalizerFactory : 
  	IStringLocalizerFactory,
  	IAbpStringLocalizerFactoryWithDefaultResourceSupport
  {
      // 封装 microsoft_resource...factory 作为内部工厂
      public AbpStringLocalizerFactory(
       	ResourceManagerStringLocalizerFactory innerFactory,
         	IOptions<AbpLocalizationOptions> abpLocalizationOptions,
         	IServiceProvider serviceProvider)
      {
          // ...
      }
          
      internal static void Replace(IServiceCollection services)
      {
          // 替换 IStringLocalizerFactory 服务为 abp_localizer_factory        
          services.Replace(
              ServiceDescriptor.Singleton<IStringLocalizerFactory,
              AbpStringLocalizerFactory));
              
          // 注入 microsoft_resource...factory 作为内部工厂
          services.AddSingleton<ResourceManagerStringLocalizerFactory();
      }
  }
  
  ```

* 加载 resource_file

  通过 virtual_file_system加载

  ```c#
  [DependsOn(typeof(AbpLocalizationAbstractModule))]
  public class AbpLocalizationModule : Module
  {
      public override void ConfigureServices(ServiceConfigurationContext context)
      {        
          // 加载 culture_resource_file
          Configure<AbpVirtualFileSystemOptions>(options => 
          {
              options.FileSets.AddEmbedded<AbpLocalizationModule>("Volo.Abp", "Volo/Abp");     
          });                
      }
  }
  
  ```

* 加载 resource

  向`AbpLocalizationOptions`中添加`LocalizationResource`

  ```c#
  [DependsOn(typeof(AbpLocalizationAbstractModule))]
  public class AbpLocalizationModule : Module
  {
      public override void ConfigureServices(ServiceConfigurationContext context)
      {                
          // 向 localization_options 添加 resource
          Configure<AbpLocalizationOptions>(options =>
          {
              options.Resources.Add<DefaultResource>("en");
              options.Resources.Add<AbpLocalizationResource>("en")
                  			 .AddVirtualJson("/Localization/Resources/AbpLocalization");
          });
      }
  }
  
  ```

  * `LocalizationResource`是 resource 的抽象

    ```c#
    public class LocalizationResource
    {
        public Type ResourceType { get; }
        public string ResourceName { get; } 
        public string DefaultCultureName { get;set; }
        public LocalizationResourceContributorList Contributors { get; }
        public List<Type> BaseResourceTypes { get; }
        
        public LocalizationResource(
        	[NotNull] Type resourceType,
        	[CanBeNull] string defaultCultureName = null,
        	[CanBeNull] ILocalizationResourceContributor initialContributor = null)
        {
            ResourceType = resourceType;
            DefaultCultureName = defaultCultureName;
            Contributors = new List<Type>();
            BaseResourceTypes = new LocalizationResourceContributorList();
            
            // 加载特性标记的 resource_name，如果没有标记则为 type_name
            ResourceName = LocalizationResourceNameAttribute.GetName(ResourceType);
            
            // 加载 contributors 
            if(initialContributor != null)
            {
                Contributors.Add(initalContributor);
            }
            
            // 加载 base_resource_type
            AddBaseResourceTypes();
            
            AddBaseResourceTypes();
        }
    }
    
    ```

    * resource_type 泛型参数，标记聚合的 resource_string，用在服务端中

    * resource_name 指定的名称，标记聚合的 resource_string，用在客户端

    * 可以加载 resource 的基类（使用特性标记）

    * Contributors是 localizer_contributor 的集合，最终解析 localizingString

      ```c#
      public class LocalizationResourceContributorList
      {
          public LocalizedString GetOrNull(string cultureName, string name)
          {
              // 获取 localized_string
          }
          
          public void Fil(string cultrueName, Dictionary<string, LocalizedString> dictionary)
          {
              // 添加 localized_string
          }
      }
      ```

    * 添加 virtual_json_contributor

      ```c#
      public static class LocalizationResourceExtensions
      {
          public static LocalizationResource AddVirtualJson(
          	[NotNull] this LocalizationResource localizationResource,
          	[NotNull] string virtualPaht)
          {
              // load virtual json file to LocalizationResource
          }                           
      }
      
      ```

      **注意：json文件的路径必须“/”且不能有“.json"扩展名**

    * 添加 base_type_resource

      ```c#
      public static class LocalizationResourceExtensions
      {    
          public static LocalizationResource AddBaseTypes(
          	[NotNull] this LocalizationResource localizationResource,
              [NotNull] params Type[] types)
          {
              // add base type resource to LocalizationResource
          }                               
      }
      
      ```

#### 2. how to use

* 依赖模块`AbpLocalizationModule`
* 定义resource class
* 定义 resource file
* 在`ConfigureService()`方法中添加 resource
* 注入`IStringLocalizer<T>`并使用



