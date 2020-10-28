## about abp startup

#### 1. concepts

* `IAbpApplication`是abp框架的生命周期容器抽象

  ```c#
  public interface IApplication : IModuleContainer, IDisposable
  {
      Type StartupModuleType { get; }
      
      IServiceCollection Services { get; }
      IServiceProvider ServiceProvider { get; }
      
      void Shutdown();
  }
  
  ```

  从定义可以看出：

  * `Services`：同`asp.net core`，“服务集合”，保存了框架中注册的服务
  * `ServiceProvider`：同`asp.net core`，“服务提供者”，保存了可以解析的服务
  * `StartupModuleType`：用于链式加载`module`
  * `Shutdown()`用于结束abp框架生命周期

* abp框架不依赖于DI的具体实现，所以又派生出两个接口

  * `IAbpApplicationWithInternalServiceProvider`，使用内置DI实现，即`Microsoft.Extensions.DependencyInjection`

    ```c#
    public interface IAbpApplicationWithInternalServiceProvider : IAbpApplication
    {
        void Initialize();
    }
    
    ```

  * `IAbpApplicationWithExternalServiceProvider`，使用外部DI实现

    ```c#
    public interface IAbpApplicationWithExternalServiceProvider : IAbpApplication
    {
        void Initialize();
    }
    
    ```

  从定义可以看出

  * `Initialize()`用于初始化框架生命周期（启动）

* abp框架不能自启动，必须寄宿与一个可执行程序中

  * `console application`，手动启动和停止
  * `web application`，由`IHost`启动和停止

* abp框架实现了上述接口

  * `AbpApplicationBase`定义了`IAbpApplication`的抽象实现

    ```c#
    public abstract class AbpApplicationBase : IAbpApplication
    {
        internal AbpApplicationBase(
            [NotNull] Type startupModuleType,
            [NotNull] IServiceCollection services,
            [CanBeNull] Action<AbpApplicationCreationOptions> optionsAction)
        {
            // ...
        }
    }
    
    ```

  * `AbpApplicationWithInternalServiceProvider`使用内部DI实现

    ```c#
    public class AbpApplicationWithInternalServiceProvider : 
    	AbpApplicationBase,
    	IAbpApplicationWithInternalServiceProvider
    {
        public AbpApplicationWithInternalServiceProvider(
            [NotNull] Type startupModuleType,
            new ServiceCollection(),// 使用microsoft.extensions.DI
            [CanBeNull] Action<AbpApplicationCreationOptions> optionsAction)
        {
            // ...
        }        
    }
    
    ```

  * `AbpApplicationWithExternalServiceProvider`使用外部DI实现

    ```c#
    public class AbpApplicationWithExternalServiceProvider : 
    	AbpApplicationBase,
    	IAbpApplicationWithExternalServiceProvider
    {
        public AbpApplicationWithInternalServiceProvider(
            [NotNull] Type startupModuleType,
            [NotNull] IServiceCollection services,	// 使用外部DI
            [CanBeNull] Action<AbpApplicationCreationOptions> optionsAction)
        {
            // ...
        }        
    }
    
    ```

* 创建`IAbpApplication`

  * `AbpApplicationFactory`定义了创建`IAbpApplication`的静态工厂

  ```c#
  public static class AbpApplicationFactory
  {
      public static IAbpApplicationWithInternalServiceProvider Create<TStartupModule>(
          [CanBeNull] Action<AbpApplicationCreationOptions> optionsAction)
      {
          // ...
      }
      
      public static IAbpApplicationWithExternalServiceProvider Create<TStartupModule>(
          [NotNull] IServiceCollection services, 
          [CanBeNull] Action<AbpApplicationCreationOptions> optionsAction)
      {
          // ...
      }
  }
  
  ```

  * `IServiceCollection`的扩展方法

  ```c#
  public static IAbpApplicationWithExternalServiceProvider AddApplication<TStartupModule>(
      [NotNull] this IServiceCollection services, 
      [CanBeNull] Action<AbpApplicationCreationOptions> optionsAction = null)
  {
      // ...
  }
             
  ```

  从定义可以看出，通过`IServiceCollection`扩展方法只能创建external`IAbpApplication`，以为`IAbpApplication`必须接管注册它的`IServiceCollection`build的`IServiceProvider`

#### 2. in deep

* `AbpApplicationCreationOptios`定义了静态工厂创建`IAbpApplication`的运行时选项

  ```c#
  public class AbpApplicationCreationOptions
  {
      public AbpConfigurationBuilderOptions Configuration { get; }
      public PlugInSourceList PlugInSources { get; }
      public IServiceCollection Servcies { get; }    
  }
  
  ```

  其中，

  * `Services`在创建框架时注入服务，`Autofac`就是这样注入的

  * `AbpConfigurationBuilderOptions`定义了构建配置的选项

    ```c#
    public class AbpConfigurationBuilderOptions
    {
        public Assembly UserSecretAssembly { get;set; }
        public string UserSecrectsId { get;set; }
        public string FileName { get;set; } = "appsettings";
        public string EnvironmentName { get;set; }
        public string BasePath { get;set; }
        public string EnvironmentVariablesPrefix { get;set; }
        public string[] CommandLineArgs { get;set; }
    }
    
    ```

  * `PlugInSourceList`???

* 使用Autofac作为DI实现

  * Autofac（源码）

    `AutofacServiceProviderFactory`定义了创建`AutofacServiceProvider`的工厂，

    `AutofactServiceProvider`实现了microsoft.extensions.DI的`IServiceProvider`接口

  * abp

    `AbpAutofacServiceProviderFactory`封装了创建`AutofacServiceProvider`的工厂方法

  * extensions

    实质是注册`AutofacServiceProviderFactory`

    * `IHost`

      ```c#
      IHostBuilder.UseAutofac();
      ```

    * `AbpApplicationCreationOptions`

      ```c#
      AbpApplicationCreationOptions.UseAutofac();
      ```

* 启动（initialize）或停止（shutdown）可以抛出异常

  * `ApplicationInitializationException`
  * `ApplicationShutdownException`

  都派生自`AbpException`

* 预注册服务

  框架在创建时注入了服务

  ```c#
  public abstract class AbpApplicationBase : IAbpApplication
  {
      internal AbpApplicationBase(/* ... */)
      {
          // ...
          services.AddCoreServices();
          services.AddCoreApServices();                                
          // ...
      }
  }
  
  ```

  这些扩展方法定义在`Volo.Abp.Internal`namespace

  * `AddCoreServices`注册基础服务

    ```c#
    internal static void AddCoreServices(this IServiceCollection services)
    {
        services.AddOptions();
        services.AddLogging();
        services.AddLocalization();
    }
    
    ```

    可以看到abp框架注入了`IOptions`，`ILogger`/`ILoggerFactory`，`IStringLocalizer`/`IStringLocalizerFactory`

  * `AddAbpCoreServices`注册abp定义的服务

    ```c#
    internal static void AddCoreAbpServices(this IServiceCollection servcies)
    {
        if(!services.IsAdded<IConfiguration>())
        {
            // add configuration
        }
    }
    ```

    可以看到abp框架包含`IConfiguration`，这是由接管的`IServiceProvider`提供的。

    * 使用`IServiceProvider`中的`IConfiguration`
    * 如果`IServiceProvider`没有注册`IConfiguration`，则使用`AbpApplicationCreationOptions.AbpConfigurationBuilderOptions`中的设置创建`IConfiguration`

  另外同一assembly的`IExceptionNotifier`被自动注入（继承自`ITransientDependency`）

  ```c#
  public class ExceptionNotifier : IExceptionNotifer, ITransientDependency
  {
      public ILogger<ExeptionNotifier> Logger { get;set; }
      
      public virtual async Task NotifyAsync([NotNull] ExceptionNotificationContext context)
      {
          // ...
      }
  }
  
  ```

#### 3. how to startup

##### 3.1 console app

* create framework to run

  `IAbpApplication`寄宿于console_app，

  手动创建、手动启动（initialization）和停止（shutdown）            

* load customized configuration

  通过`AbpApplicationCreationOptions.AbpConfigurationBuildOptions`定义配置来源

  `IAbpApplication`在创建时加载自定义配置

* using serilog

  通过`IServiceCollection`可以增加、替换（扩展方法）向abp框架中注册的服务

##### 3.2 worker service

* create framework to run

  `IAbpApplication`寄宿于`IHost`

  从`IHost.IServiceProvider`解析`IAbpApplication`并手动启动（initialization）

* load customized configuration

  `IHost`将`IServiceProvider`交于`IAbpApplication`

  在`IHost`构建时添加自定义configuration

  `AbpApplicationCreatingOptions.AbpConfigurationBuildOptions`不起作用

* using serilog

  同上，`IAbpApplication`接管`IHost.IServiceProvider`；

  在构建`IHost`时注册SerilogFactory（使用扩展方法`UseSerilog()`）以替换默认服务

  通过`AbpApplicationCreationOptions.IServiceCollection`注册应该也可以？？

##### 3.3 web application

* creat framework to run

  同“worker service”相关

  需要添加`Volo.Abp.AspNetCore.Mvc`模块的依赖

* load customized configuration

  same as "worker service"

* use serilog

  same as "worker Service"