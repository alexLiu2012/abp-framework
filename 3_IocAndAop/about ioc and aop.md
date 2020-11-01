## about ioc and aop in abp

#### 1. concept

##### 1.1 ioc

`IAbpApplication`在创建（调用构造函数）时会注册模块中的服务

除了调用模块`ConfigureService()`方法注册服务（手动注册）外，

还可以按照约定自动注册（规约注册）

```c#
public abstract class AbpApplicationBase
{
    protected virtual void ConfigureServices()
    {
        // ...
        services.AddAssembly(module.Type.Assembly);
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

* `IConventionalRegistrar`的具象实例实现了注册服务的方法

  ```c#
  public abstract class ConventionalRegistrarBase 
  {
      public virtual void AddAssembly(IServiceCollection servcies, Assembly assembly)
      {        
          var types = AssemblyHelper.GetAllTypes(assembly).Where(/**/);
          
          AddTypes(services, types);
      }
      
      public virtual void AddTypes(IServiceCollection services, params Type[] types)
      {
          foreach(var type in types)
          {
              AddType(services, type);
          }
      }        
  }
  
  ```

  ```c#
  public class DefaultConventionalRegistrar : ConventionalRegistrarBase
  {
      public override void AddType(IServiceCollection services, Type type)
      {        
          // disable conventional register
          if(IsconventionalRegistrationDisabled(type))
          {            
              return;
          }                
              
          // get service lifetime
          var dependencyAttribute = GetDependencyAttributeOrNull(type):
          var lifetime = GetLifetimeOrNull(type, dependencyAttribute);
          if(lifetime == null)
          {
              return;
          }
          
          // get exposed service type
          var exposedServiceTypes = ExposedServiceExplorer.GetExposedServices(type);
          // 可手动配置 exposed service
          TriggerServiceExposing(services, type, exposedServiceTypes);
          
          // 注册服务
          foreach(var exposedServiceType in exposedServiceTypes)
          {
              var serviceDescriptor = CreateServiceDescriptor(/**/);
              
              if(dependencyAttribute?.ReplaceServices == true)
              {               
                  service.Replace(serviceDescriptor);		// replace register
              }
              else if(dependencyAttribute?.TryRegister == true)
              {
                  services.TryAdd(serviceDescriptor);		// try register
              }
              else
              {
                  service.Add(servcieDescriptor);			// register
              }
          }       
      }
  }
  
  ```

  * 获取待注册的生命周期

    abp框架支持

    * 特性标注生命周期
    * 实现特定生命周期接口

    ```c#
    public class DefaultConventionalRegistrar
    {
        // ...        
        var dependencyAttribute = GetDependencyAttributeOrNull(type):
        var lifetime = GetLifetimeOrNull(type, dependencyAttribute);
        if(lifetime == null)
        {
            return;
        }    
        // ...
    }
    
    ```

    ```c#
    public class DefaultConventionalRegistrar
    {    
        // 返回属性标注的生命周期
        protected virtual ServiceLifetime? GetLifeTimeOrNull(
            Type type, 
            [CanBeNull] DependencyAttribute dependencyAttribute)
            {
                return dependencyAttribute?
                    .Lifetime ?? GetServiceLifetimeFromClassHierarcy(type);
            }
    }
    ```

    ```c#
    public class DefaultConventionalRegistrar
    { 
        // 返回实现特定接口的生命周期
        protected virtual ServiceLifetime? GetServiceLifetimeFromClassHierarcy(Type type)
        {
            if (typeof(ITransientDependency).GetTypeInfo().IsAssignableFrom(type))
            {            
                return ServiceLifetime.Transient;
            }
            
            if (typeof(ISingletonDependency).GetTypeInfo().IsAssignableFrom(type))
            {
                return ServiceLifetime.Singleton;
            }
            
            if (typeof(IScopedDependency).GetTypeInfo().IsAssignableFrom(type))
            {
                return ServiceLifetime.Scoped;
            }
            
            return null;
    }
    
    ```

  * 获取服务契约类型（暴露类型）

    ```c#
    public class DefaultConventionalRegistrar
    {
        // 通过 IExposedServiceProvider
        var exposedServiceTypes = ExposedServiceExplorer.GetExposedServices(type);
        
        // 通过 ExposingServiceContext
        // 在模块 pre configure service 中配置
        TriggerServiceExposing(services, type, exposedServiceTypes);
    }
    
    ```

    abp框架可以

    * 通过`IExposedServiceTypeProvider`指定（特性标注或约定）

      ```c#
      public static class ExposedServiceExplorer
      {
          public static List<Type> GetExposedServices(Type type)
          {
              return type
                  .GetCustomerAttributes(true)
                  .OfType<IExposedServiceTypeProvider>()
                  // 如果没有标记属性，则使用默认属性，
                  // 即 includeDefaults=true，includeSelf=true
                  .DefaultIfEmpty(DefaultExposeServicesAttribute)
                  // 通过IExposedServiceProvider.Get方法获取服务契约类型
                  .SelectMany(p => p.GetExposedServiceTypes(type))
                  .Distinct()
                  .ToList();
          }
      }
      
      ```

      ```c#
      public static class ExposedServiceExplor
      {
          // 没有显式指定属性时，使用默认值
          private static readonly ExposeServiceAttribute DefaultExposeServicesAttribute =
              new ExposeServiceAttribute()
          	{
              	IncludeDefaults = true,
              	IncludeSelf = true        
          	}
      }
      
      ```

      `ExposeServiceAttribute`是`IExposedServiceTypeProvider`的实现，同时也是特性

      ```c#
      public class ExpsoeServiceAttribute : Attribute, IExposedServiceTypeProvider
      {
          public Type[] ServiceTypes { get; }
          public bool IncludeDefaults { get;set; }
          public bool IncludeSelf { get;set; }
          
          public Type[] GetExposedServiceTypes(Type targetType)
          {
              // ...
              
              if(IncludeDefaults)		// 加载默认（约定）契约类型
              {
                  foreach(var type in GetDefaultServices(targetType))
                  {
                      serviceList.AddInNotContains(type);
                  }
              }
              
              if(IncludeSelf)			// 加载自身作为契约类型
              {
                  serviceList.AddInNotContains(targetType);
              }
              
              return serviceList.ToArray();
          }   
          
          // 加载默认（约定）契约类型
          private static List<Type> GetDefaultServices(Type type)
          {
              var serviceTypes = new List<Type>();
              
              foreach(var interfaceType in type.GetTypeInfo().GetInterfaces())
              {
                  var interfaceName = interfaceType.Name;
                  
                  // 获取接口名称
                  if(interfaceName.StartWith("I"))
                  {
                      interfaceName = interfaceName.Right(interfaceName.Length -1);
                  }
                  
                  // 如果服务名包含接口名，则暴露该接口
                  // 即接口名是服务名的子串
                  if(type.Name.EndWith(interfaceName))
                  {
                      serviceTypes.Add(interface)
                  }
              }
          }
      }
      
      ```

    * 通过`OnExposingServiceContext`指定

      ```c#
      public abstract class ConventionalRegistrarBase
      {
          protected virtual void TriggerServiceExposing(IServiceCollection services, Type implementationType, List<Type> serviceTypes)
          {
              var exposeActions = services.GetExposingActionList();
              
              if(exposeActions.Any())
              {
                  var args = new OnServiceExposingContext(implementationType, serviceTypes);
                  foreach(var action in exposeActions)
                  {
                      action(args);
                  }
              }
          }
      }
      
      ```

      * `OnServiceExposingContext`是服务和它暴露的契约类型（集合）对的封装

      * 在模块的`PreConfigureService()`方法中配置context，通过扩展方法

        ```c#
        public static class ServiceCollectionRegistrationActionExtensions
        {
            public static void OnExposing(this IServiceCollection services, Action<IOnServiceExposingContext exposeAction)
            {
                GetOrCreateExposingList(services).Add(exposeAction);
            }
        }
        
        ```

  * 注册服务

    ```c#
    public class DefaultConventionalRegistrar
    {
        foreach(var exposedServiceType in exposedServiceTypes)
        {
            // create servcie descriptor
            var serviceDescriptor = CreateServiceDescriptor(/**/);
            
            if(dependencyAttribute?.ReplaceServices == true)
            {               
                service.Replace(serviceDescriptor);		// replace register
            }
            else if(dependencyAttribute?.TryRegister == true)
            {
                services.TryAdd(serviceDescriptor);		// try register
            }
            else
            {
                service.Add(servcieDescriptor);			// register
            }
        }       
    }
    
    ```

    ```c#
    public class DefaultConventionalRegistrar
    {
        protected virtual ServiceDescriptor CreateServiceDescriptor(
        	Type implementType,
        	Type exposingServiceType,
        	List<Type> allExposingServiceTypes,
        	ServiceLifetime lifetime)
        {
            if(lifetime.IsIn(ServiceLifetime.Singleton, ServiceLifetime.Scope))
            {
                // 重定向 implementationType 的超类（派生类）类型
                var redirectedType = GetRedirectedTypeOrNull(/* */);
                            
                if(redirectedType != null)
                {
                    return ServiceDescriptor.Describe(
                    	exposingServiceType,
                    	provider => provider.GetService(redirectedType),
                    	lifetime);
                }            
            }
            
            return ServiceDescriptor.Describe(
            	exposingServiceType,
            	implementationType,
            	lifetime);
        }
    }
    
    ```

    * 重定向超类（派生类）类型

      ```c#
      public class DefaultConventionalRegistrar
      {
          protected virtual Type GetRedirectedTypeOrNull(
          	Type implementationType,
          	Type exposingServiceType,
          	List<Type> allExposingServiceTypes)
          {
              if(allExposingServiceTypes.Count < 2)
              {
                  return null;
              }
              if(exposingServiceType == implementationType)
              {
                  return null;
              }
              if(allExposingServiceTypes.Contains(implementationType))
              {
                  return implementationType;
              }
              
              return allExposingServiceTypes.FirstOrDefault(
              	t => t != exposingServiceType && exposingServiceType.IsAssignableFrom(t));
          }
      }
      
      ```

##### 1.2 aop

abp框架扩展了服务的拦截器功能

* 添加拦截器

  * 通过`OnRegistered()`扩展方法添加`OnServiceRegistrationContext` 

  ```c#
  public static class ServiceCollectionRegistrationActionExtensions
  {
      public static void OnRegistred(this IServiceCollection services, Action<IOnServiceRegistredContext> registrationContext)
      {
          GetOrCreateRegistrationActionList(services).Add(registrationAction);
      }
  }
  
  ```

  * `OnServiceRegistredContext`包含拦截器集合

  ```c#
  public class OnServiceRegistredContext : IOnServiceRegistredContext
  {
      public virtual ITypList<IAbpInterceptor> Interceptors { get; }
      public virtual Type ServiceType { get; }
      public virtual Type ImplementationType { get; }
      // ...
  }
  
  ```

  ```c#
  public abstract class AbpInterceptor : IAbpInterceptor
  {
      public abstract Task InterceptAsync(IAbpMethodInvocation invocation);
  }
  
  ```

  ```c#
  public interface IAbpMethodInvocation
  {
      // abp定义的 invocation
  }
  ```

  * 适配成Castle.Core的动态代理

    * 适配 castle_interceptor

    ```c#
    public class CastleAsyncAbpInterceptorAdapter<IInterceptor> : AsyncInterceptorBase where IInterceptor : IAbpInterceptor
    {
        private readonly TInterceptor _abpInterceptor;
        
        public CastleAsyncAbpInterceptorAdapter(IInterceptor abpInterceptor)
        {
            _abpInterceptor = abpInterceptor;
        }
        
        protected override async Task InterceptAsync(/**/) 
        {
            // _abpInterceptor.InterceptAsync(...)
        }
        protected override async Task<TResult> InterceptAsync<TResult>(/**/) 
        {
            // _abpInterceptor.InterceptAsync(...)
        }
    }
    
    ```

    * 适配 castle_methodInvocation

    ```c#
    public class CastleAbpMethodInvocationAdapter : CastleAbpMethodInvocationAdapterBase, IAbpMethodInvation
    {
        protected IInvocationProceedInfo ProceedInfo { get; }
        protected Func<IInvocation, IInvocationProceedInfo, Task> Proceed { get; }
        
        public CastleAbpMethodInvocationAdapter(
        	IInvocation invocation,
        	IInvocationProceedInfo proceedInfo,
        	Func<...> proceed) : base(invocation)
        {
            ProceedInfo = proceedInfo;
            Proceed = proceed;
        }
        
        public override async Task ProceedAsync()
        {
            // Proceed(invocation, ProceedInfo)
        }
    }
    
    ```

    * 适配 castle_determination_interceptor

    ```c#
    public class AbpAsyncDeterminationInterceptor<TInterceptor> : AsyncDeterminationInterceptor where TInterceptor : IAbpInterceptor
    {
        // adapt a castle_interceptor
        // base(castle_interceptor)
    }
    
    ```

##### 1.3 autofac

abp框架不依赖ioc实现，默认使用microsoft.extensions.dependencyInjection。

但是使用autofac可以扩展更多功能，例如属性注入、拦截器

* 模块依赖`AbpAutofacModule`

  ```c#
  [DependsOn(typeof(AbpCastleCoreModule))]
  public class AbpAutofacModule : AbpModule
  {    
  }
  ```

  ```c#
  public class AbpCastleCoreModule : AbpModule
  {
      public override void ConfigureServices(ServiceCnofigurationContext context)
      {
          context.Services.AddTransient(typeof(AbpAsyncDeterminationInterceptor<>));
      }
  }
  ```

* 注入abp_autofac_service_factory

  * 创建`IAbpApplication`时调用`AbpApplicationCreationOptions`的扩展方法

  ```c#
  public static class AbpAutofacAbpApplicationCreationOptionsExtensions
  {
      // 创建abp_application时选择使用autofac
      public static void UseAutofac(this AbpApplicationCreationOptions options)
      {
          options.Services.AddAutofacServiceProviderFactory();
      }    
  }
  
  ```

  ```c#
  public static class AbpAutofacAbpApplicationCreationOptionsExtensions
  {    
      public static AbpAutofacServiceProviderFactory AddAutofacServiceProviderFactory(this IServiceCollection services)
      {
          return services.AddAutofacServiceProviderFactory(new ContainerBuilder());
      }
      
      public static AbpAutofacServiceProviderFactory AddAutofacServiceProviderFactory(this IServiceCollection services, ContainerBuilder containerBuilder)
      {
          var factory = new AbpAutofacServiceProviderFactory(containerBuilder);
          
          services.AddObjectAccessor(containerBuilder);
          services.AddSingleton(IServiceProviderFactory<ContainerBuilder> factory);
          return factory;        
      }    
  }
  
  ```

  * 构建`IHost`是调用`IHostBuilder`的扩展方法

  ```c#
  public static class AbpAutofacHostBuilderExtensions
  {
      public static IHostBuilder UseAutofac(this IHostBuilder hostBuilder)
      {
          var containerBuilder = new ContainerBuilder();
          
          return hostBuilder.ConfigureServices((_, services) =>
                 {
                     services.AddObjectAccessor(containerBuilder)                          
                 })
              .UseServiceProviderFactory(new AbpAutofacServiceProviderFactory(containerBuilder));            	        
      }        
  }
  
  ```

* 其实质是注入autofac_service_provider

  ```c#
  public class AbpAutofacServiceProviderFactory : IServiceProviderFactory<ContainerBuilder>
  {
      private readonly ContainerBuilder _builder;
      private IServiceCollection _services;
      
      public AbpAutofacServiceProviderFactory(ContainerBuilder builder)
      {
          _builder = builder;
      }
      
      public ContainerBuilder CreateBuilder(IServiceCollection services)
      {
          _services = services;
          _bulider.Populate(services);	// 引入autofac
          return _builder;            
      }
      
      // ...
  }
  
  ```

  ```c#
  public static class AutofacRegistration
  {
      public static void Populate(
      	this ContainerBuilder builder,
      	IServiceCollection services)
      {
          // 使用autofac作为service_provider
          builder.RegisterType<AutofacServiceProvider>().As<IServiceProvider>();
          // 使用autofac提供service_scope
          builder.RegisterType<AutofacServiceScopeFactory>().As<IServiceScopeFactory>();
          // 在autofac中注册服务        
          Register(builder, services);
      }
  }
              
  ```

  * 向autofac注册服务

  ```c#
  public static class AutofacRegistration
  {
      public static void Register(
      	ContainerBuilder builder,
      	IServiceCollection services)
      {
          var moduleContainer = services.GetSingletonInstance<IModuleContainer>();
          var registrationActionList = services.GetRegistrationActionList();
          
          foreach(var service in services)
          {
              if(service.ImplementationType != null)
              {
                  // ...
                  builder
                      .Registertype(service.ImplementationType)
                      .As(service.ServiceType)
                      .ConfiugrationLifecylce(service.Lifetime)
                      .ConfigureAbpConventions(moduleContainer, registrationActionList);
              }
              else if(service.ImplementationFactory != null)
              {
                  // ...
                  registration
                      .ConfigureLifecycle(service.Lifetime)
                      .CreateRegistration();
                  builder.RegisterComponent(registration);
              }
              else
              {
                  builder
                      .RegisterInstance(service.ImplementationInstance)
                      .As(service.ServiceType)
                      .ConfigureLifecycle(service.Lifetime);
              }
          }
      }
  }
  
  ```

  * 扩展“属性注入”和“拦截器”功能

    只对“接口，实现”方式注册的服务有效，

    注册服务工厂、服务本身，不能使用扩展的功能

  ```c#
  public static class AbpRegistrationBuilderExtensions
  {
      public static IRegistrationBuilder<...> ConfigureAbpConventions<...>(
      	this IRegistrationBuilder<...> registrationBuilder,
      	IModuleContainer moduleContainer,
      	ServiceRegistrationActionList registrationActionList)
      {
          var serviceType = ...;
          if(serviceType == null)
          {
              return registrationBuilder;
          }
          
          var implementationType = ...;
          if(implementationType == null)
          {
              return registrationBuilder;
          }
          
          // 属性注入
          registrationBuilder = registrationBuilder.EnablePropertyInjection(moudleContainer);
          // 拦截器
          registrationBuilder = registrationBuilder.InvokeRegistrationActions(registrationActionList);
          return registrationBuilder;
      }
  }
  
  ```

  

#### 2. how to use

##### 2.1 注册服务

* 手动注册

  在module的`ConfigureService()`方法中注册

* 约定注册

  * 定义class

    * 实现服务注册接口`ISingletonDependency`、`IScopedDependency`、`ITransientDependency`
    * 实现上述接口的派生接口，如`IDomainService`
    * 继承实现了上述接口的基类，如`Repository`

  * 标记class特性

    ```c#
    [Dependency(Lifetime, TryRegister, ReplaceService)]
    ```

* 契约服务类型（exposed service）

  ```c#
  [ExposeServices(Type, includeDefault, includeSelf)]
  ```

  * 如果没有显式标注特性，框架使用默认特性，即`includeDefault，includeSelf`为`true`
  * 如果标记了特性，但没有显式传入`includeDefault, includeSelf`的值，则默认为`false`

##### 2.2 属性注入

* 添加`AbpAutofacModule`的依赖
* `UseAutofac()`方法

##### 2.3 使用拦截器

* 添加`AbpAutofacModule`的依赖

* 在module的`PreConfigureService()`方法中注册拦截器Action

  ```c#
  public class MyModule : AbpModule
  {
      public override void PreConfigureService(ServiceConfigurationContext context)
      {
          context.Services.OnRegistred(...)
          {
              // ...
          }
      }
  }
  
  ```

  





