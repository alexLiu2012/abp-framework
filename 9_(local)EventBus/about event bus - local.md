## about event bus

#### 1. concept

abp框架实现了event_bus，分为进程内的 local bus 和 微服务的 distributed bus

* `AbpEventBusModule`模块注册 event handler

  ```c#
  public class AbpEventBusModule : AbpModule
  {
      public override void PreConfigureServices(ServiceConfigurationContext context)
      {
          AddEventHandlers(context.Services);
      }
      
      private static void AddEventHandlers(IServiceCollection services)
      {
          // ...
          // 使用了 autofac 
          services.OnRegistred(context =>
          {
              if(ReflectionHelper.IsAssignableToGenericType(context.ImplementationType, typeof(ILocalEventHandler<>)))
              {
                  // 添加 local event handler
                  localHandlers.Add(context.ImplementationType);                
              }
              else if(ReflectionHelper.IsAssignableToGenericType(context.ImplementationType, typeof(IDistributedEventHandler<>)))
              {
                  // 添加 distributed event handler
                  distributedHandlers.Add(context.ImplementationType);
              }
          });
          
          // 注册 local event options
          services.Configure<AbpLocalEventBusOptions>(options =>
          {
              options.Handlers.AddIfNotContains(localHandlers);                              
          });
          // 注册 distributed event options
          services.Configure<AbpDistributedEventBusOptions(options =>
          {
              options.Handler.AddIfNotContains(distributedHandlers);
          });
      }
  }
  
  ```

##### 1.1 local event

* `LocalEventBus`定义了 local event bus，进程内使用

  ```c#
  [ExposeServices(typeof(ILocalEventBus), typeof(LocalEventBus))]
  public class LocalEventBus : EventBus, ILocalEventBus, ISingletonDependency
  {
      public ILogger<IEventBus> Logger { get;set; }
      protected AbpLocalEventBusOptions Options { get; }
      protected ConcurrentDictionary<Type, List<IEventHandlerFactory>> HandlerFactories { get; }
      public LocalEventBus(
      	IOptions<AbpLocalEventBusOptions> options, 
      	IServiceScopeFactory serviceScopeFactory,
      	ICurrentTenant currentTenant) base (serviceScopeFactory, currentTenant)
      {
          // 注入服务，初始化
          Options = options.Value;
          Logger = NullLogger<LocalEventBus>.Instance;
          HandlerFactories = new ConcurrentDictionary<Type, List<IEventHandlerFactory>>();
          
          // 订阅 options 中的全部handler
          SubscribeHandlers(Options.Handlers);
      }        
  }
  
  ```

  * subscribe event 方法

    通过添加 handler factory 实现订阅

    * 直接添加 factory

      ```c#
      public class LocalEventBus
      {      
          public override IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
          {
              GetOrCreateHandlerFactories(eventType).Locking(factories =>
              	{
                      if(!factory.IsInFactories(factories))
                      {
                          factories.Add(factory);
                      }                                                           
                  });        
              
              return new EventHandlerFactoryUnregistrar(this, eventType, factory);
          }                                
      }
      
      ```

      ```c#
      public abstract class EventBusBase
      {        
          public virtual IDisposable Subscribe<TEvent>(IEventHandlerFactory factory>)
              where TEvent : class =>
                  Subscribe(typeof(TEvent), factory);            
      }
      
      ```

    * 通过 handler 添加 factory

      ```c#
      public abstract class EventBusBase
      {        
          // 泛型方法，factory 创建 THandler 的实例，每次都new，所以叫transient
          public virtual IDisposable Subscribe<TEvent, THandler>() 
              where TEvent : class,
          	where THandler : IEventHandler, new() =>
                  Subscribe(typeof(TEvent), new TransientEventHandlerFactory<THandler>());
          
          // 参数方法，factory 创建 handler 的 wrapper，不变的，所以叫"singleton"
          public virtual IDisposable Subscribe(Type eventType, IEventHandler handler) =>
              Subscribe(eventType, new SingleInstanceHandlerFactory(handler));        
      }
      
      ```

    * 通过 action 添加 factory

      ```c#
      public abstract class EventBusBase
      {    
          public virtual IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) 
              where TEvent : class => 
                  Subscribe(typeof(TEvent), new ActionEventHandler<TEvent>(action));
      }
      ```

    * 添加options中的factory

      ```c#
      public abstract class EventBusBase : IEventBus
      {
          protected virtual SubscribeHandlers(ITypeList<IEventHandler> handlers)
          {
              foreach(var handler in handlers)
              {
                  var interfaces = handler.GetInterfaces();
                  foreach(var @interface in interfaces)
                  {
                      if(!typeof(IEventHandler).GetTypeInfo().IsAssignableFrom(@interface))
                      {
                          continue;	// 过滤 IEventHandler
                      }
                      
                      var genericArgs = @interface.GetGenericArguments();
                      if(generaicArgs.Length == 1)
                      {
                          // 过滤 IEventHandler<T>, 并取得 type
                          // 调用 subscribe 方法，具体实现在派生类中
                          Subscribe(genericArgs[0], new IocEventHandlerFactory(ServiceScopeFactory, handler));                    
                      }
                  }
              }
          }
      }
      ```

  * unsubscribe event 方法

    通过删除handler factory取消订阅

    * 直接删除 factory

      ```c#
      public class LocalEventBus
      {
          public override void Unsubscribe(Type eventType, IEventHandlerFactory factory)
          {
              GetOrCreateHandlerFactories(eventType).Locking(factories => actories.Remove(factory));        
          }
      }
      
      ```

      ```c#
      public abstract class EventBusBase
      {
          public override void Unsubscribe<TEvent>(IEventHandlerFactory factory)
          {
              Unsubscribe(typeof(TEvent), factory);
          }
      }
      ```

    * 通过 handler 删除 factory

      ```c#
      public class LocalEventBus
      {
          public override void Unsubscribe(Type eventType, IEventHandler handler)
          {
               GetOrCreateHandlerFactories(eventType)
                   .Locking(factories =>
                   	{
                          factories.RemoveAll(
                              factory =>
                              // SingleInstanceFactory 是实例封装的 
                              factory is SingleInstanceHandlerFactory &&
                              (factory as SingleInstanceHandlerFactory).HandlerInstance == handler
                          );
                      });
          }
      }
      
      ```

      ```c#
      public abstract class EventBusBase
      {
          public virtual void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler)
          {
              Unsubscribe(typeof(TEVent), handler);
          }
      }
      
      ```

    * 通过 action 删除 factory

      ```c#
      public class LocalEventBus
      {
          public override void Unsubscribe<TEvent>(Func<TEvent,Task> action)
          {        
              Check.NotNull(action, nameof(action));
              
              GetOrCreateHandlerFactories(typeof(TEvent))
                  .Locking(factories =>
                  	{
                          // 遍历所有TEvent对应的factories，
                          // 删除 actionHandler 与 传入参数一致的 factory
                          factories.RemoveAll(
                              factory =>
                              {
                                  var singleInstanceFactory = factory as                                 SingleInstanceHandlerFactory;
                                  if (singleInstanceFactory == null)
                                  {
                                      return false;
                                  }
      
                                  var actionHandler = SingleInstanceFactory.HandlerInstance as ActionEventHandler<TEvent>;
                                  if (actionHandler == null)
                                  {
                                      return false;
                                  }
      
                                  return actionHandler.Action == action;
                              });
                      });
          }
      }
      
      ```

    * 删除全部 factory

      ```c#
      public class LocalEventBus
      {
          public override void UnsubscribeAll(Type eventType)
          {
              GetOrCreateHandlerFactories(eventType).Locking(factories => factories.Clear());
          }
      }
      
      ```

      ```c#
      public abstract class EventBusBase
      {
          public virtual void UnsubscribeAll<TEvent>()
          {
              UnsubscribeAll(typeof(TEvent));
          }
      }
      
      ```

  * publish event 方法

    ```c#
    public class LocalEventBus
    {
        public override virtual Task PublishAsync(Type eventType, object eventData)
        {
            var exceptions = new List<Exception>();        
            await TriggerHandlersAsync(eventType, eventData, exceptions);
            
            // ...
        }
    }
    
    ```

    ```c#
    public abstract class EventBusBase
    {
        public virtual Task PublishAsync<TEvent>(TEvent eventData) where TEvent: class
        {
            return PublishAsync(typeof(TEvent), eventData);
        }
    }
    
    ```

    * trigger handler 方法

      ```c#
      public abstract class EventBusBase
      {
          protected virtual async Task TriggerHandlerAsync(Type eventType, object eventData, List<Exception> exceptions)
          {
              // 遍历 event type 对应的所有 factories，然后 trigger
              foreach (var handlerFactories in GetHandlerFactories(eventType))
              {
                  foreach (var handlerFactory in handlerFactories.EventHandlerFactories)
                  {
                      await TriggerHandlerAsync(handlerFactory, andlerFactories.EventType, eventData, exceptions);
                      }
                  }
              
              // 如果要trigger继承链（实现了IEventDataWithinheritableGenericArgument）
              if(eventType.GetTypeInfo().IsGenericType &&
                 eventType.GetGenericArguments().Length == 1 &&
                     typeof(IEventDataWithInheritableGenericArgument)
                 	       .IsAssignableFrom(eventData))
              {
                  // 获取继承的 baseEventType，baseEventData ...            
                  await PublishAsync(baseEventType, baseEventData);
              }                
          }
      }
      
      ```

      ```c#
      public abstract class EventBusBase
      {
          protected virtual async Task TriggerHandlerAsync(
              IEventHandlerFactory asyncHandlerFactory,
          	Type eventType,
          	object eventData,
          	List<Exception> exceptions)
          {
              using (var eventHandlerWrapper = asyncHandlerFactory.GetHandler())
              {
                  try
                  {
                      var handlerType = eventHandlerWrapper.EventHandler.GetType();
                      
                      // 切换 tenant
                      using(CurrentTenant.Change(GetEventDataTenantId(eventData)))
                      {
                          if(/**/)	// 如果是local event，调用 ILocalEventBus 的方法 ...
                          {
                              await method.Invoke(eventHandlerWrapper.EventHandler, new[] { eventData });                    
                          }
                          else if(/**/)	// 如果是distributed event，调用 IDistributedEventBus 的方法 ...
                          {
                              await method.Invoke(eventHandlerWrapper.EventHandler, new[] { eventData });        
                          }
                          else
                          {
                              throw new AbpException(...);
                          }
                      }
                  }
                  catch(/**/)
                  {
                      // 添加 exception
                  }
              }
          }
      }
      
      ```

  #### 2. how to use

  * 依赖`AbpEventBusModule`，和`AbpAutofac`的nuget包
  * 在`ConfigureService()`方法中向`AbpLocalEventBusOptions`中添加handler
  * 定义 TEvent，THandler
  * 手动添加 THandler
  * publish event

  

