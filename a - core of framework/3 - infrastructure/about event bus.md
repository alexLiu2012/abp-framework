## about event bus

相关程序集：

* Volo.Abp.EventBus
* Volo.Abp.EventBus.RabbitMq
* Volo.Abp.RabbitMq

----

### 1. about

* abp框架实现了event bus
  * 进程内应用的 Local_EventBus
  * 进程间（微服务）应用的 Distributed_EventBus

### 2. details

#### 2.1 event (data)

* event 数据，即发生 event 时描述该 event 的参数
* poco类型

##### 2.1.1 event name 

* distributed event bus 会使用？？

###### 2.1.1.1 event name attribute

```c#
[AttributeUsage(AttributeTargets.Class)]
public class EventNameAttribute : Attribute, IEventNameProvider
{
    public virtual string Name { get; }
    
    public EventNameAttribute([NotNull] string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name));
    }
    
    public static string GetNameOrDefault<TEvent>()
    {
        return GetNameOrDefault(typeof(TEvent));
    }
    
    public static string GetNameOrDefault([NotNull] Type eventType)
    {
        Check.NotNull(eventType, nameof(eventType));
        
        return eventType
            .GetCustomAttributes(true)
            .OfType<IEventNameProvider>()
            .FirstOrDefault()
            ?.GetName(eventType)
            ?? eventType.FullName;
    }
    
    public string GetName(Type eventType)
    {
        return Name;
    }
}

```

###### 2.1.1.2 generic event name 

```c#
[AttributeUsage(AttributeTargets.Class)]
public class GenericEventNameAttribute : Attribute, IEventNameProvider
{
    public string Prefix { get; set; }    
    public string Postfix { get; set; }
    
    public virtual string GetName(Type eventType)
    {
        if (!eventType.IsGenericType)
        {
            throw new AbpException($"Given type is not generic: {eventType.AssemblyQualifiedName}");
        }
        
        var genericArguments = eventType.GetGenericArguments();
        if (genericArguments.Length > 1)
        {
            throw new AbpException($"Given type has more than one generic argument: {eventType.AssemblyQualifiedName}");
        }
        
        var eventName = EventNameAttribute.GetNameOrDefault(genericArguments[0]);
        
        if (!Prefix.IsNullOrEmpty())
        {
            eventName = Prefix + eventName;
        }
        
        if (!Postfix.IsNullOrEmpty())
        {
            eventName = eventName + Postfix;
        }
        
        return eventName;
    }
}

```

###### 2.1.1.3 event name provider

```c#
public interface IEventNameProvider
{
    string GetName(Type eventType);
}

```

##### 2.1.2 may have tenant id

```c#
public interface IEventDataMayHaveTenantId
{    
    bool IsMultiTenant(out Guid? tenantId);
}

```

##### 2.1.3 with inheritable

```c#
public interface IEventDataWithInheritableGenericArgument
{    
    object[] GetConstructorArgs();
}

```

#### 2.2 event handler

* event handler 定义了处理 event 的 handler
* 实现`IEventHandler`（派生）接口，即`ILocalEventHandler`或者`IDistributedEventHandler`

##### 2.2.1 接口

```c#
// 空接口，仅定义类型
public interface IEventHandler
{    
}

```

##### 2.2.2 派生接口

###### 2.2.2.1 local event handler

```c#
public interface ILocalEventHandler<in TEvent> : IEventHandler
{
    Task HandleEventAsync(TEvent eventData);
}

```

###### 2.2.2.2 distributed event handler

```c#
public interface IDistributedEventHandler<in TEvent> : IEventHandler
{    
    Task HandleEventAsync(TEvent eventData);
}

```

##### 2.2.3 实现

```c#
public class ActionEventHandler<TEvent> 
    : ILocalEventHandler<TEvent>, ITransientDependency
{    
    // 注入 handle action    
    public Func<TEvent, Task> Action { get; }
    public ActionEventHandler(Func<TEvent, Task> handler)
    {
        Action = handler;
    }                
    
    public async Task HandleEventAsync(TEvent eventData)
    {
        await Action(eventData);
    }
}

```

#### 2.3 disposed event handler

* 统一抽象

##### 2.3.1 接口

```c#
public interface IEventHandlerDisposeWrapper : IDisposable
{
    IEventHandler EventHandler { get; }
}

```

##### 2.3.2 实现

```c#
public class EventHandlerDisposeWrapper : IEventHandlerDisposeWrapper
{
    // 注入 handler、dispose action
    public IEventHandler EventHandler { get; }    
    private readonly Action _disposeAction;    
    public EventHandlerDisposeWrapper(
        IEventHandler eventHandler, Action disposeAction = null)
    {
        _disposeAction = disposeAction;
        EventHandler = eventHandler;
    }
    
    public void Dispose()
    {
        _disposeAction?.Invoke();
    }
}

```

#### 2.4 event handler factory

##### 2.4.1 接口

```c#
public interface IEventHandlerFactory
{
    IEventHandlerDisposeWrapper GetHandler();    
    bool IsInFactories(List<IEventHandlerFactory> handlerFactories);
}

```

##### 2.4.2 实现

###### 2.4.2.1 ioc factory

* 通过 ioc 创建 handler 

```c#
public class IocEventHandlerFactory : IEventHandlerFactory, IDisposable
{
    // 注入服务
    public Type HandlerType { get; }    
    protected IServiceScopeFactory ScopeFactory { get; }    
    public IocEventHandlerFactory(IServiceScopeFactory scopeFactory, Type handlerType)
    {
        ScopeFactory = scopeFactory;
        HandlerType = handlerType;
    }
        
    public IEventHandlerDisposeWrapper GetHandler()
    {
        var scope = ScopeFactory.CreateScope();
        
        // 从 ioc 中解析 handler，
        // 包裹成 disposed_handler
        return new EventHandlerDisposeWrapper(
            (IEventHandler) scope.ServiceProvider.GetRequiredService(HandlerType),
            () => scope.Dispose());
    }
    
    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories
            .OfType<IocEventHandlerFactory>()
            .Any(f => f.HandlerType == HandlerType);
    }
    
    public void Dispose()
    {        
    }
}

```

###### 2.4.2.2 transient factory

* 直接生成 transient handler

```c#
public class TransientEventHandlerFactory<THandler> 
    : TransientEventHandlerFactory, IEventHandlerFactory
        where THandler : IEventHandler, new()
{
    public TransientEventHandlerFactory()
        : base(typeof(THandler))
    {
    }

    // 直接创建 handler
    protected override IEventHandler CreateHandler()
    {
        return new THandler();
    }
}
       
```

```c#
 public class TransientEventHandlerFactory : IEventHandlerFactory
 {
     // 注入 handlerType
     public Type HandlerType { get; }     
     public TransientEventHandlerFactory(Type handlerType)
     {
         HandlerType = handlerType;
     }
     
	 
     public virtual IEventHandlerDisposeWrapper GetHandler()
     {
         // 直接创建 handler，
         var handler = CreateHandler();
         
         // 并包裹成 disposed_handler
         return new EventHandlerDisposeWrapper(
             handler,
             () => (handler as IDisposable)?.Dispose());
     }

     public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
     {
         return handlerFactories
             .OfType<TransientEventHandlerFactory>()
             .Any(f => f.HandlerType == HandlerType);
     }
     
     // 创建 handler 的方法
     protected virtual IEventHandler CreateHandler()
     {
         return (IEventHandler) Activator.CreateInstance(HandlerType);
     }
 }

```

###### 2.4.2.3 singleton factory

* 生成 singleton handler

```c#
public class SingleInstanceHandlerFactory : IEventHandlerFactory
{
    // 注入 handler 实例
    public IEventHandler HandlerInstance { get; }        
    public SingleInstanceHandlerFactory(IEventHandler handler)
    {
        HandlerInstance = handler;
    }
    
    public IEventHandlerDisposeWrapper GetHandler()
    {
        // 包裹注入的 handler -> disposed_handler
        return new EventHandlerDisposeWrapper(HandlerInstance);
    }
    
    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories
            .OfType<SingleInstanceHandlerFactory>()
            .Any(f => f.HandlerInstance == HandlerInstance);
    }
}

```

#### 2.5 event bus

##### 2.5.1 接口

```c#
public interface IEventBus
{
    /* publish event */
    Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class;        
    Task PublishAsync(Type eventType, object eventData);
    
    /* subscribe event */
    // by action
    IDisposable Subscribe<TEvent>(
        Func<TEvent, Task> action) where TEvent : class;    
    // by tevent and thandler
    IDisposable Subscribe<TEvent, THandler>()
        where TEvent : class where THandler : IEventHandler, new();
    // by event type and event handler
    IDisposable Subscribe(
        Type eventType, IEventHandler handler);   
    // by tevent and handler factory
    IDisposable Subscribe<TEvent>(
        IEventHandlerFactory factory) where TEvent : class;      
    // by event type and handler factory
    IDisposable Subscribe(
        Type eventType, IEventHandlerFactory factory);
    
    /* unsubscribe event */
    // by action
    void Unsubscribe<TEvent>(
        Func<TEvent, Task> action) where TEvent : class;      
    // by tevent and local handler
    // by tevent and thandler???
    void Unsubscribe<TEvent>(
        ILocalEventHandler<TEvent> handler) where TEvent : class;  
    // by event type and event handler
    void Unsubscribe(
        Type eventType, IEventHandler handler);
    // by tevent and handler facory
    void Unsubscribe<TEvent>(
        IEventHandlerFactory factory) where TEvent : class;
    // by event type and handler factory
    void Unsubscribe(
        Type eventType, IEventHandlerFactory factory);              
        
    /* unsubscribe all */
    void UnsubscribeAll<TEvent>() where TEvent : class;
    void UnsubscribeAll(Type eventType);
}

```

##### 2.5.2 抽象基类

###### 2.5.2.1 初始化

```c#
public abstract class EventBusBase : IEventBus
{
    // 注入服务
    protected IServiceScopeFactory ServiceScopeFactory { get; }    
    protected ICurrentTenant CurrentTenant { get; }    
    protected EventBusBase(
        IServiceScopeFactory serviceScopeFactory, ICurrentTenant currentTenant)
    {
        ServiceScopeFactory = serviceScopeFactory;
        CurrentTenant = currentTenant;
    }                        

    /* 获取 eventType 索引的 handler factories 的集合 */
    // 在派生类中实现
    protected abstract IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType);
    // eventType索引的 factories 集合的封装
    protected class EventTypeWithEventHandlerFactories
    {
        public Type EventType { get; }        
        public List<IEventHandlerFactory> EventHandlerFactories { get; }        
        public EventTypeWithEventHandlerFactories(
            Type eventType, List<IEventHandlerFactory> eventHandlerFactories)
        {
            EventType = eventType;
            EventHandlerFactories = eventHandlerFactories;
        }
    }
     
    // tenant ？？
    protected virtual Guid? GetEventDataTenantId(object eventData)
    {
        return eventData switch
        {            
            IMultiTenant multiTenantEventData => multiTenantEventData.TenantId,                
            IEventDataMayHaveTenantId eventDataMayHaveTenantId when eventDataMayHaveTenantId.IsMultiTenant(out var tenantId) => tenantId,            
            _ => CurrentTenant.Id
        };
    }
    
    // Reference from
    // https://blogs.msdn.microsoft.com/benwilli/2017/02/09/an-alternative-to-configureawaitfalse-everywhere/
    protected struct SynchronizationContextRemover : INotifyCompletion
    {
        public bool IsCompleted
        {
            get { return SynchronizationContext.Current == null; }
        }
        
        public void OnCompleted(Action continuation)
        {
            var prevContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                continuation();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevContext);
            }
        }
        
        public SynchronizationContextRemover GetAwaiter()
        {
            return this;
        }
        
        public void GetResult()
        {
        }
    }
}

```

###### 2.5.2.2 subscribe handlers

* 向 event bus 中注册 handlers
* handler 必须注册到 ioc 中

```c#
public abstract class EventBusBase : IEventBus
{
    protected virtual void SubscribeHandlers(ITypeList<IEventHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            var interfaces = handler.GetInterfaces();
            foreach (var @interface in interfaces)
            {
                // 如果传入的 handler 实现了IEventHandler 接口，
                if (!typeof(IEventHandler).GetTypeInfo().IsAssignableFrom(@interface))
                {
                    continue;
                }
                // 使用 handler 的泛型参数<T> 创建 ioc handler factory,
                // 并注册 handler
                // 注意：handler 必须 注册到 ioc 中
                var genericArgs = @interface.GetGenericArguments();
                if (genericArgs.Length == 1)
                {
                    Subscribe(
                        genericArgs[0], 
                        new IocEventHandlerFactory(ServiceScopeFactory, handler));
                }
            }
        }
    }
}

```

###### 2.5.2.3 trigger handler

* 触发 handlers
* 获取要出发的 handlers factory的方法在派生类中定义（GetHandlerFactories）

```c#
public abstract class EventBusBase : IEventBus
{
    // by event type and event data
    public virtual async Task TriggerHandlersAsync(
        Type eventType, object eventData)
    {
        var exceptions = new List<Exception>();        
        await TriggerHandlersAsync(eventType, eventData, exceptions);
        
        if (exceptions.Any())
        {
            if (exceptions.Count == 1)
            {
                exceptions[0].ReThrow();
            }            
            throw new AggregateException( /**/ + eventType, exceptions);
        }
    }
    // by event type and event data with exceptions
    protected virtual async Task TriggerHandlersAsync(
        Type eventType, object eventData, List<Exception> exceptions)
    {
        await new SynchronizationContextRemover();
        
        // 遍历 handler factoryies, 在派生类中创建
        foreach (var handlerFactories in GetHandlerFactories(eventType))
        {
            foreach (var handlerFactory in handlerFactories.EventHandlerFactories)
            {
                await TriggerHandlerAsync(
                    handlerFactory, handlerFactories.EventType, eventData, exceptions);
            }
        }
		
        // 如果 event type 标记了 with_inheritable 特性，
        // 发布基类 event
        if (eventType.GetTypeInfo().IsGenericType &&
            eventType.GetGenericArguments().Length == 1 &&
            typeof(IEventDataWithInheritableGenericArgument).IsAssignableFrom(eventType))
        {
            var genericArg = eventType.GetGenericArguments()[0];
            var baseArg = genericArg.GetTypeInfo().BaseType;
            if (baseArg != null)
            {
                var baseEventType = eventType.GetGenericTypeDefinition()
                    .MakeGenericType(baseArg);
                var constructorArgs = ((IEventDataWithInheritableGenericArgument)eventData)
                    .GetConstructorArgs();
                var baseEventData = Activator.
                    CreateInstance(baseEventType, constructorArgs);
                // 发布基类 event
                await PublishAsync(baseEventType, baseEventData);
            }
        }
    }
    
    // by handler factory, event type, event object with exceptions
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
                
                // 当前 tenant
                using (CurrentTenant.Change(GetEventDataTenantId(eventData)))
                {
                    // 如果是 ILocalEventHandler<T>
                    if (ReflectionHelper.IsAssignableToGenericType(
                        	handlerType, typeof(ILocalEventHandler<>)))
                    {
                        var method = typeof(ILocalEventHandler<>)
                            .MakeGenericType(eventType)
                            .GetMethod(
                            	nameof(ILocalEventHandler<object>.HandleEventAsync),
                            	new[] { eventType });
                        
                        await ((Task)method.Invoke(
                            eventHandlerWrapper.EventHandler, new[] { eventData }));
                    }
                    // 如果是 IDistributedEventHandler<T>
                    else if (ReflectionHelper.IsAssignableToGenericType(
                        		handlerType, typeof(IDistributedEventHandler<>)))
                    {
                        var method = typeof(IDistributedEventHandler<>)
                            .MakeGenericType(eventType)
                            .GetMethod(
                            	nameof(IDistributedEventHandler<object>.HandleEventAsync),
                            	new[] { eventType });

                        await ((Task)method.Invoke(
                            eventHandlerWrapper.EventHandler, new[] { eventData }));
                    }
                    else
                    {
                        throw new AbpException( /**/ + handlerType.AssemblyQualifiedName);
                    }
                }
            }
            catch (TargetInvocationException ex)
            {
                exceptions.Add(ex.InnerException);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }
    }
}

```

##### 2.5.3 subscribe

* 最终都是调用派生类的`Subscribe(Type eventType, IEventHandlerFactory factory)`方法

```c#
public abstract class EventBusBase : IEventBus
{
    // 1) by action
    public virtual IDisposable Subscribe<TEvent>(
        Func<TEvent, Task> action) where TEvent : class
    {
        // 创建 action_handler, -> 5)subscribe(eventtype, ihandler)        
        return Subscribe(typeof(TEvent), new ActionEventHandler<TEvent>(action));
    }               
        
    // 2) by tevent and thandler
    public virtual IDisposable Subscribe<TEvent, THandler>()
        where TEvent : class
        where THandler : IEventHandler, new()
    {
        // 创建 transient_thandler_factory, -> 5)subscribe(eventtype, ihandlerFactory)        
        return Subscribe(typeof(TEvent), new TransientEventHandlerFactory<THandler>());
    }   
    
    // 3) by eventtype and handler
    public virtual IDisposable Subscribe(
        Type eventType, IEventHandler handler)
    {
        // 创建 singleton_thandler_factory, -> 5)subscribe(eventtype, ihandlerFactory)
        return Subscribe(eventType, new SingleInstanceHandlerFactory(handler));
    }
    
    // 4) by tevent and handlerFactory, -> 5)subscribe(eventtype, ihandlerFactory)
    public virtual IDisposable Subscribe<TEvent>(
        IEventHandlerFactory factory) where TEvent : class
    {
        return Subscribe(typeof(TEvent), factory);
    }
	
    // 5) by event type and handler factory
    // 真正的实现方法，在派生类中实现
    public abstract IDisposable Subscribe(Type eventType, IEventHandlerFactory factory);
}

```

##### 2.5.4 unsubscribe

```c#
public abstract class EventBusBase : IEventBus
{
    // 1) by action
    // 在派生类中实现
    public abstract void Unsubscribe<TEvent>(
        Func<TEvent, Task> action) where TEvent : class;

    // 2) by localEventHandler, -> 5)unsubscribe(eventtype, ihandlerfactory)
    public virtual void Unsubscribe<TEvent>(
        ILocalEventHandler<TEvent> handler) where TEvent : class
    {
        Unsubscribe(typeof(TEvent), handler);
    }

    // 3) by event type and handler,
    // 在派生类中实现
    public abstract void Unsubscribe(Type eventType, IEventHandler handler);

    // 4) by tevent and handler factory, -> 5)unsubscribe(eventtype, ihandlerfactory)
    public virtual void Unsubscribe<TEvent>(
        IEventHandlerFactory factory) where TEvent : class
    {
        Unsubscribe(typeof(TEvent), factory);
    }
	
    // 5) by event type and handler factory
    // 真正的实现方法，在派生类中实现
    public abstract void Unsubscribe(Type eventType, IEventHandlerFactory factory);    
}

```

##### 2.5.5 unsubscribe all

```c#
public abstract class EventBusBase : IEventBus
{  
    public virtual void UnsubscribeAll<TEvent>() where TEvent : class
    {
        UnsubscribeAll(typeof(TEvent));
    }  
    
    // 在派生类中实现
    public abstract void UnsubscribeAll(Type eventType);
}

```

##### 2.5.6 publish

```c#
public abstract class EventBusBase : IEventBus
{    
    public virtual Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class
    {
        return PublishAsync(typeof(TEvent), eventData);
    }

    // 在派生类中实现
    public abstract Task PublishAsync(Type eventType, object eventData);
}

```

#### 2.6 local event bus

##### 2.6.1 接口

```c#
public interface ILocalEventBus : IEventBus
{
    IDisposable Subscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class;    }

```

##### 2.6.2 null local eventBus

```c#
public sealed class NullLocalEventBus : ILocalEventBus
{
    public static NullLocalEventBus Instance { get; } = new NullLocalEventBus();
    
    private NullLocalEventBus()
    {                
    }

    /* subscribe */
    // 1)
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
        return NullDisposable.Instance;
    }
    // 
    public IDisposable Subscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
    {
        return NullDisposable.Instance;
    }
    // 2)
    public IDisposable Subscribe<TEvent, THandler>() where TEvent : class where THandler : IEventHandler, new()
    {
        return NullDisposable.Instance;
    }
    // 3)
    public IDisposable Subscribe(Type eventType, IEventHandler handler)
    {
        return NullDisposable.Instance;
    }
    // 4)
    public IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
        return NullDisposable.Instance;
    }
    // 5)
    public IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
    {
        return NullDisposable.Instance;
    }
    
    /* unsubscribe */
    // 1)
    public void Unsubscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {        
    }
    // 2)
    public void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
    {        
    }
    // 3)
    public void Unsubscribe(Type eventType, IEventHandler handler)
    {        
    }
    // 4)
    public void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {        
    }
    // 5)
    public void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {        
    }
    
    /* unsubscribe all */
    public void UnsubscribeAll<TEvent>() where TEvent : class
    {        
    }
    
    public void UnsubscribeAll(Type eventType)
    {        
    }
    
    /* publish */
    public Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class
    {
        return Task.CompletedTask;
    }    
    public Task PublishAsync(Type eventType, object eventData)
    {
        return Task.CompletedTask;
    }
}

```

##### 2.6.3 local event bus

```c#
[ExposeServices(typeof(ILocalEventBus), typeof(LocalEventBus))]
public class LocalEventBus 
    : EventBusBase, ILocalEventBus, ISingletonDependency
{
    // 注入服务
    public ILogger<LocalEventBus> Logger { get; set; }    
    protected AbpLocalEventBusOptions Options { get; }    
    protected ConcurrentDictionary<Type, List<IEventHandlerFactory>> HandlerFactories { get; }
    public LocalEventBus(
        IOptions<AbpLocalEventBusOptions> options,
        IServiceScopeFactory serviceScopeFactory,
        ICurrentTenant currentTenant) : base(serviceScopeFactory, currentTenant)
    {
        Options = options.Value;
        // logger 使用属性注入
        Logger = NullLogger<LocalEventBus>.Instance;
        // 初始化 event_type 索引的 handler_factories 集合的集合
        HandlerFactories = new ConcurrentDictionary<Type, List<IEventHandlerFactory>>();
        // subscribe（注册 handlers）
        SubscribeHandlers(Options.Handlers);
    }                                   
}

```

###### 2.6.3.1 subscribe handler

```c#
public class LocalEventBus : EventBusBase, ILocalEventBus, ISingletonDependency
{
    private static bool ShouldTriggerEventForHandler(
        Type targetEventType, Type handlerEventType)
    {
        //Should trigger same type
        if (handlerEventType == targetEventType)
        {
            return true;
        }        
        //Should trigger for inherited types
        if (handlerEventType.IsAssignableFrom(targetEventType))
        {
            return true;
        }
        
        return false;
    }
}

```

###### 2.6.3.2 get factories

```c#

public class LocalEventBus : EventBusBase, ILocalEventBus, ISingletonDependency
{
    protected override IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType)
    {
        var handlerFactoryList = new List<EventTypeWithEventHandlerFactories>();
        
        foreach (var handlerFactory in 
                 	HandlerFactories.Where(
                        hf => ShouldTriggerEventForHandler(eventType, hf.Key)))
        {
            handlerFactoryList.Add(new EventTypeWithEventHandlerFactories(
                handlerFactory.Key, handlerFactory.Value));
        }
        
        return handlerFactoryList.ToArray();
    }
    
    private List<IEventHandlerFactory> GetOrCreateHandlerFactories(Type eventType)
    {
        return HandlerFactories.GetOrAdd(
            eventType, 
            (type) => new List<IEventHandlerFactory>());
    }
}

```

###### 2.6.3.3 subscribe

```c#
public class LocalEventBus : EventBusBase, ILocalEventBus, ISingletonDependency
{
    public virtual IDisposable Subscribe<TEvent>(
        ILocalEventHandler<TEvent> handler) where TEvent : class
    {
        return Subscribe(typeof(TEvent), handler);
    }
    
    // override 5) 
    public override IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
    {
        GetOrCreateHandlerFactories(eventType)
            .Locking(factories =>
            	{
                    if (!factory.IsInFactories(factories))
                    {
                        factories.Add(factory);
                    }
                });
        
        return new EventHandlerFactoryUnregistrar(this, eventType, factory);
    }
}

```

###### 2.6.3.4 unsubscribe

```c#
public class LocalEventBus : EventBusBase, ILocalEventBus, ISingletonDependency
{
    // override 1)
    public override void Unsubscribe<TEvent>(Func<TEvent, Task> action)
    {
        Check.NotNull(action, nameof(action));
        
        GetOrCreateHandlerFactories(typeof(TEvent))
            .Locking(factories =>
            	{
                    factories.RemoveAll(factory =>
                    	{
                            var singleInstanceFactory = 
                                factory as SingleInstanceHandlerFactory;
                            if (singleInstanceFactory == null)
                            {
                                return false;
                            }

                            var actionHandler = 
                                singleInstanceFactory.HandlerInstance as 
                                	ActionEventHandler<TEvent>;
                            if (actionHandler == null)
                            {
                                return false;
                            }

                            return actionHandler.Action == action;
                        });
                });
    }
    
    // override 3)
    public override void Unsubscribe(Type eventType, IEventHandler handler)
    {
        GetOrCreateHandlerFactories(eventType)
            .Locking(factories =>
            	{
                    factories.RemoveAll(factory =>
                    	factory is SingleInstanceHandlerFactory &&
                        (factory as SingleInstanceHandlerFactory).HandlerInstance == handler);
                });
    }
    
    // override 5)
    public override void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
        GetOrCreateHandlerFactories(eventType)
            .Locking(factories => factories.Remove(factory));
    }
}

```

###### 2.6.3.5 unsubscribe all

```c#
public class LocalEventBus : EventBusBase, ILocalEventBus, ISingletonDependency
{
    public override void UnsubscribeAll(Type eventType)
    {
        GetOrCreateHandlerFactories(eventType)
            .Locking(factories => factories.Clear());
    }
}
    
```

###### 2.6.3.6 publish

```c#
public class LocalEventBus : EventBusBase, ILocalEventBus, ISingletonDependency
{
    public async override Task PublishAsync(Type eventType, object eventData)
    {
        var exceptions = new List<Exception>();
        
        await TriggerHandlersAsync(eventType, eventData, exceptions);
        
        if (exceptions.Any())
        {
            if (exceptions.Count == 1)
            {
                exceptions[0].ReThrow();
            }
            
            throw new AggregateException("More than one error has occurred while triggering the event: " + eventType, exceptions);
        }
    }
}
    
```

#### 2.7 distributed event bus

##### 2.7.1 接口

```c#
public interface IDistributedEventBus : IEventBus
{    
    IDisposable Subscribe<TEvent>(IDistributedEventHandler<TEvent> handler)
        where TEvent : class;
}

```

##### 2.7.2 null distributed eventBus

```c#
public sealed class NullDistributedEventBus : IDistributedEventBus
{
    public static NullDistributedEventBus Instance { get; } = new NullDistributedEventBus();   
    private NullDistributedEventBus()
    {        
    }

    /* subscirbe */
    // 1)
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
        return NullDisposable.Instance;
    }
    // 
    public IDisposable Subscribe<TEvent>(IDistributedEventHandler<TEvent> handler) where TEvent : class
    {
        return NullDisposable.Instance;
    }
    // 2)
    public IDisposable Subscribe<TEvent, THandler>() where TEvent : class where THandler : IEventHandler, new()
    {
        return NullDisposable.Instance;
    }
    // 3)
    public IDisposable Subscribe(Type eventType, IEventHandler handler)
    {
        return NullDisposable.Instance;
    }
    // 4)
    public IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
        return NullDisposable.Instance;
    }
    // 5)
    public IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
    {
        return NullDisposable.Instance;
    }
    
    /* unsubscribe */
    // 1)
    public void Unsubscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {        
    }
    // 2)
    public void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
    {        
    }
    //3)
    public void Unsubscribe(Type eventType, IEventHandler handler)
    {        
    }
    // 4)
    public void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {        
    }
    // 5)
    public void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {        
    }
    
    /* unsubscribe all */
    public void UnsubscribeAll<TEvent>() where TEvent : class
    {        
    }    
    public void UnsubscribeAll(Type eventType)
    {        
    }
    
    /* publish */
    public Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class
    {
        return Task.CompletedTask;
    }    
    public Task PublishAsync(Type eventType, object eventData)
    {
        return Task.CompletedTask;
    }
}

```

##### 2.7.3 local distributed eventBus

* 包裹`ILocalEventBus`的`IDistributedEventBus`

```c#
[Dependency(TryRegister = true)]
[ExposeServices(typeof(IDistributedEventBus), typeof(LocalDistributedEventBus))]
public class LocalDistributedEventBus 
    : IDistributedEventBus, ISingletonDependency
{
    // 注入服务
    private readonly ILocalEventBus _localEventBus;    
    protected IServiceScopeFactory ServiceScopeFactory { get; }    
    protected AbpDistributedEventBusOptions AbpDistributedEventBusOptions { get; }    
    public LocalDistributedEventBus(
        ILocalEventBus localEventBus,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AbpDistributedEventBusOptions> distributedEventBusOptions)
    {
        _localEventBus = localEventBus;
        ServiceScopeFactory = serviceScopeFactory;
        AbpDistributedEventBusOptions = distributedEventBusOptions.Value;
        
        Subscribe(distributedEventBusOptions.Value.Handlers);
    }
    
    public virtual void Subscribe(ITypeList<IEventHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            var interfaces = handler.GetInterfaces();
            foreach (var @interface in interfaces)
            {
                if (!typeof(IEventHandler).GetTypeInfo().IsAssignableFrom(@interface))
                {
                    continue;
                }
                
                var genericArgs = @interface.GetGenericArguments();
                if (genericArgs.Length == 1)
                {
                    Subscribe(
                        genericArgs[0], 
                        new IocEventHandlerFactory(ServiceScopeFactory, handler));
                }                         
            }
        }

    /* subscribe */    
    //
    public virtual IDisposable Subscribe<TEvent>(
        IDistributedEventHandler<TEvent> handler) where TEvent : class
    {
        return Subscribe(typeof(TEvent), handler);
    }        
    // 1)   
    public IDisposable Subscribe<TEvent>(
        Func<TEvent, Task> action) where TEvent : class
    {
        return _localEventBus.Subscribe(action);
    }
	// 
    public IDisposable Subscribe<TEvent>(
        ILocalEventHandler<TEvent> handler) where TEvent : class
    {
        return _localEventBus.Subscribe(handler);
    }
	// 2)
    public IDisposable Subscribe<TEvent, THandler>() 
        where TEvent : class where THandler : IEventHandler, new()
    {
        return _localEventBus.Subscribe<TEvent, THandler>();
    }
	// 3)
    public IDisposable Subscribe(
        Type eventType, IEventHandler handler)
    {
        return _localEventBus.Subscribe(eventType, handler);
    }
	// 4)
    public IDisposable Subscribe<TEvent>(
        IEventHandlerFactory factory) where TEvent : class
    {
        return _localEventBus.Subscribe<TEvent>(factory);
    }
    // 5)    
    public IDisposable Subscribe(
        Type eventType, IEventHandlerFactory factory)
    {
        return _localEventBus.Subscribe(eventType, factory);
    }

    /* unsubscribe */    
    // 1)
    public void Unsubscribe<TEvent>(
        Func<TEvent, Task> action) where TEvent : class
    {
        _localEventBus.Unsubscribe(action);
    }
    // 
    public void Unsubscribe<TEvent>(
        ILocalEventHandler<TEvent> handler) where TEvent : class
    {
        _localEventBus.Unsubscribe(handler);
    }
    // 3)    
    public void Unsubscribe(
        Type eventType, IEventHandler handler)
    {
        _localEventBus.Unsubscribe(eventType, handler);
    }
    // 4)    
    public void Unsubscribe<TEvent>(
        IEventHandlerFactory factory) where TEvent : class
    {
        _localEventBus.Unsubscribe<TEvent>(factory);
    }
    // 5)    
    public void Unsubscribe(
        Type eventType, IEventHandlerFactory factory)
    {
        _localEventBus.Unsubscribe(eventType, factory);
    }
    
    /* unsubscribe all */
    public void UnsubscribeAll<TEvent>() where TEvent : class
    {
        _localEventBus.UnsubscribeAll<TEvent>();
    }        
    public void UnsubscribeAll(Type eventType)
    {
        _localEventBus.UnsubscribeAll(eventType);
    }
      
    /* publish */
    public Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class
    {
        return _localEventBus.PublishAsync(eventData);
    }        
    public Task PublishAsync(Type eventType, object eventData)
    {
        return _localEventBus.PublishAsync(eventType, eventData);
    }
}
    
```

#### 2.8 注册 local event bus

##### 2.8.1 模块

* 需要使用`Autofac`的拦截器

```c#
[DependsOn(typeof(AbpMultiTenancyModule))]
public class AbpEventBusModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        AddEventHandlers(context.Services);
    }
    
    private static void AddEventHandlers(IServiceCollection services)
    {
        var localHandlers = new List<Type>();
        var distributedHandlers = new List<Type>();
        
        services.OnRegistred(context =>
        	{
                // 添加 local event handler
                if (ReflectionHelper.IsAssignableToGenericType(
                    context.ImplementationType, typeof(ILocalEventHandler<>)))
                {
                    localHandlers.Add(context.ImplementationType);
                }
                // 添加 distributed event handler
                else if (ReflectionHelper.IsAssignableToGenericType(
                    context.ImplementationType, typeof(IDistributedEventHandler<>)))
                {
                    distributedHandlers.Add(context.ImplementationType);
                }                
            });
        
        services.Configure<AbpLocalEventBusOptions>(options =>
        	{
                options.Handlers.AddIfNotContains(localHandlers);
            });		
        services.Configure<AbpDistributedEventBusOptions>(options =>
            {
                options.Handlers.AddIfNotContains(distributedHandlers);
            });        
    }
}

```

##### 2.8.2 options

###### 2.8.2.1 local eventBus options

```c#
public class AbpLocalEventBusOptions
{
    public ITypeList<IEventHandler> Handlers { get; }    
    public AbpLocalEventBusOptions()
    {
        Handlers = new TypeList<IEventHandler>();
    }
}

```

###### 2.8.2.2 distributed eventBus options

```c#
public class AbpDistributedEventBusOptions
{
    public ITypeList<IEventHandler> Handlers { get; }    
    public AbpDistributedEventBusOptions()
    {
        Handlers = new TypeList<IEventHandler>();
    }
}

```

### 3. practice

* 依赖模块 abp event bus module
* 定义 event data，可以使用 几个 特性、接口
* 定义 event handler，注意要实现 ixxxDependency 接口，自动注册
* handler 会在启动时自动注入
* 可以再 subscribe
* 使用`IDistributedEventBus`接口，
  * local event bus 用 localDistributedEventBus 实现
  * distributed event bus用其他实现