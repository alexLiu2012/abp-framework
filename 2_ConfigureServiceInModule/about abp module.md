## about abp module and configure services

#### 1. concept

abp框架采用模块化设计，模块隔离自身服务与逻辑，实现“高内聚”

模块内注册的服务将被abp框架链式注册，

其所在程序集内的服务也按照约定自动注册，可以disable自动注册

* `AbpModule`是模块的抽象基类，任何模块都需要继承它

  ```c#
  public abstract class AbpModule : 
  	IAbpModule,
  	IPreConfigureServices,
  	IPostConfigureServices
  	IOnPreApplicationInitialization,
  	IOnApplicationInitialization,
  	IOnPostApplicationInitialzation,
  	IOnApplicationShutdown
  {
      protected internal bool SkipAutoServiceRegistration { get; protected set;}    
      protected internal ServiceConfigurationContext ServiceConfigurationContext { get; internal set; }
      
      /* 注册服务 */
      public virtual void PreConfigureServices(ServiceConfigurationContext context) {}
      public virtual void ConfigureServices(ServiceConfigurationContext context) {}
      public virtual void PostConfigureServices(ServicesConfigurationContext context) {}
      
      /* 配置生命周期动作 */
      public virtual void OnPreApplicationInitialization(ApplicationInitializationContext context) {}
      public virtual void OnApplicationInitialization(ApplicationInitializationContext context) {}
      public virtual void OnPostApplicationInitialization(ApplicationInitializationContext context) {}
      public virtual void OnApplicationShutdown(ApplicationShutdownContext context) {}
      
  }
  
  ```

  从定义可以看出，

  * `AbpModule`内聚了服务注册，并扩展出不同的时间节点
  * `AbpModule`可以配置生命周期钩子

* 使用`[DependsOn(typeof(ModuleName))]`标记模块间的依赖

  ```c#
  [DependsOn(typeof(ADependentModule))]
  public class MyModule : AbpModule
  {
      // ...
  }
  
  ```

* `AbpApplicationBase`在创建（调用构造函数）时加载所有依赖的模块

  `IModuleLoader`用于加载所有依赖的模块

  * 注册`IModuleLoader`的实例`ModuleLoader`

    ```c#
    public abstract class AbpApplicationBase
    {
        internal AbpApplicationBase(/* */)
        {
            // ...           
            services.AddCoreAbpServices(this, options); // 加载abp core services        
            // ...
        }
    }
    
    ```

    ```c#
    internal static class InternalServiceCollectionExtensions
    {
        internal static void AddCoreAbpServices(
        	this IServiceCollection services,
        	IAbpApplication abpApplication,
        	AbpApplicationCreationOptions options)
        {
            // ...        
            var moduleLoader = new ModuleLoader();
            services.TryAddSingleton<IModuleLoader>(moduleLoader);	// 注册IModuleLoader的实例        
            // ...
        }
    }
    
    ```

  * 解析`IModuleLoader`并调用`LoadModules()`方法

    ```c#
    public abstract class AbpApplicationBase
    {    
        internal AbpApplicationBase(/* */)
        {
            // ...        
            Modules = LoadModules(services, options);
            // ...
        }        
        
        protected virtual IReadOnlyList<IAbpModuleDescriptor> LoadModules(
            IserviceCollection services,
        	AbpApplicationCreationOptions options)
        {
            // 解析IModuleLoader并调用其Load...方法
            return services.GetSingletonIstance<IModuleLoader>()
                .LoadModules(services, StartupModuleType, options.PlugInSources);
        }
    }
    
    ```

    ```c#
    public class ModuleLoader : IModuleLoader
    {    
        public IAbpModuleDescriptor[] LoadModules(
        	IServiceCollection services,
        	Type startupModuleType,
        	PlugInSourceList plugInSources)
        {
            // ...
            var modules = GetDescriptors(services, startupModuleType, plugInSource);
            modules = SortByDependency(modules, statupModuleType);
            
            return modules.ToArray();
        }        
    }
    
    ```

  * 保存加载的模块（descriptor）的集合

    ```c#
    public abstract class AbpApplicationBase
    {
        // ...    
        public IReadOnlyList<IAbpModuleDescriptor> Modules { get; }
        // ...
    }
    
    ```

    ```c#
    public interface IAbpModuleDescriptor
    {
        Type Type { get; }
        Assembly Assembly { get; }
        IAbpModule Instance { get; }
        bool IsLoadedAsPlugIn { get; }
        IReadOnlyList<IAbpModuleDescriptor> Dependencies { get; }
    }
    
    ```

* `AbpApplicationBase`在创建（调用构造函数）时注册模块中的服务

  * 遍历加载的`IAbpModuleDescriptor`集合，调用instance对应的方法，即`AbpModule`中对应的

    * `void PreConfigureService()`
    * `void ConfigureService()`
    * `void PostConfigureServie()`

    ```c#
    public abstract class AbpApplicationBase
    {
        internal AbpApplicationBase(/* */)
        {
            // ...
            ConfigureServices();
        }
        
        protected virtual void ConfigureServices()
        {
            // ...
            
            // pre_configure services
            foreach(var module in Modules.Where(m => m.Instance is IPreConfigureServices))
            {
                // ...
                ((IPreConfigureServices)module.Instance).PreConfigureServices(context);
                // ...
            }
            
            // configure services
            module.Instance.ConfigureServices(context);
            
            // post_configure services
            ((IPostConfigureServices)module.Instance).PostConfigureService(context);
            
            // ...
        }
    }
    
    ```

  * 按照约定注册程序集内的服务

    ```c#
    public abstract class AbpApplicationBase
    {
        internal AbpApplicationBase(/* */)
        {
            // ...
            ConfigureServices();
        }
        
        protected virtual void ConfigureServices()
        {
            // ...
            foreach(var module in Modules)
            {
                if(module.Instance is AbpModule abpModule)
                {
                    // disable 约定注册
                    if(!abpModule.SkipAutoServiceRegistrar)
                    {
                        Service.AddAssembly(module.Type.Assembly);
                    }
                }
            }
            // ...
        }
    }
    
    ```

    ```c#
    public static class ServiceCollectionConventionRegistratioExtension
    {
        public static IServiceCollection AddAssembly(this IServiceCollection services, Assembly assembly)
        {
            foreach(var registrar in servcies.GetConventionalRegistrar())
            {
                registrar.AddAssembly(servcies, assembly);
            }
            return services;
        }
    }
    
    ```

* `IAbpApplication`启动时（调用`Initialize()`方法）时执行模块中的生命周期钩子

  `IModuleManager`用于启动、停止模块中配置的生命周期钩子

  * `IAbpApplication`实现调用了`AbpApplicationBase`的方法

    ```c#
    internal class AbpApplicationWithInternalServiceProvider
    {
        public void Initialize()
        {
            // ...
            InitializeModules();	// 调用base.Initialize()
        }
    }
    
    ```

    ```c#
    internal class AbpApplicationWithExternalServiceProvider
    {
        public void Initialize()
        {
            // ...
            InitializeModules();	// 调用base.Initialize()
        }
    }
    
    ```

  * `AbpApplicationBase`中定义了具体实现

    ```c#
    protected virtual void InitializeModules()
    {
        using (var scope = ServiceProvider.CreateScope())
        {
            scope.ServiceProvider
                .GetRequiredService<IModuleManager>()
                .InitializeModules(new ApplicationInitializationContext(scope.ServiceProvider));
        }    
    }
    
    ```

    ```c#
    public class ModuleManager : IModuleManager, ISingletonDependency
    {
        public void InitializeModules(ApplicationInitializationContext context)
        {
            foreach(var contributor in _lifecycleContributors)
            {
                foreach(var module in _moduleContainer.Modules)
                {
                    // ...
                    contributor.Initialize(context, module.Instance);
                    // ...
                }
            }
        }
    }
    
    ```

  * `IModuleLifecycleContributor`是模块生命周期的执行者，它控制模块启动（initialize）、停止（shutdown）

    对应不同的生命周期钩子，abp框架定义了不同的执行者

    * pre application initialization

    ```c#
    public class OnPreApplicationInitializationMoudleLifecycleContributor
    {
        public override void Initialize(ApplicationInitializationContext context, IAbpModule module)
        {
            (module as IOnPreApplicationInitialization)?
                .OnPreApplicationInitialization(context);        
        }    
    }
    
    ```

    * application initialization

    ```c#
    public class OnApplicationInitializationModuleLifecycleContributor
    {
        public override void Initialize(ApplicationInitializationContext context, IAbpModule module)
        {
            (module as IOnApplicationInitialization)?
                .OnApplicationInitialization(context);  
        }
    }
    
    ```

    * post application initialization

    ```c#
    public class OnPostApplicationInitializationMoudleLifecycleContributor
    {
        public override void Initialize(ApplicationInitializationContext context, IAbpModule module)
        {
            (module as IOnPostApplicationInitialization)?
                .OnPreApplicationInitialization(context);        
        }    
    }
    
    ```

    * application shutdown

    ```c#
    public class OnApplicationShutdownModuleLifecycleContributor
    {
        public override void Shutdown(ApplicationShutdownContext context, IAbpModule module)
        {
            (module as IApplicationShutdown)?
                .OnApplicationShutdown(context);
        }
    }
    
    ```

#### 2. create module

* 自定义Module

  通过继承`AbpModule`定义Module

* 配置选项

  * 通过`Configure<TOptions>()`方法注册`IOptions<T>`的实例
  * `PreConfigure<TOptions>()`预注册`IOptions<T>`，并可通过`ExecutePreConfiguredAction<T>`获取（解析）`IOptions<T>`的实例

* 注册服务

  * 向`IServiceCollection`中手动注册
  * 定义自动注册的服务

* best practice

  // to do

  