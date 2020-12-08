## about object mapping

相关程序集：

* Volo.Abp.ObjectMapping

----

### 1. about

* abp 框架实现了 object mapping
  * 可以指定手动 mapping 方法
  * 集成了 AutoMapper

### 2. details

#### 2.1 object with map

* 实现`IMapX`接口，使 object 可以 map

##### 2.1.1 map to

* 定义 object 可以 map 成 destination_type

```c#
public interface IMapTo<TDestination>
{
    TDestination MapTo();    
    void MapTo(TDestination destination);
}

```

##### 2.1.2 map from

* 定义 object 可以由 source_type map

```c#
public interface IMapFrom<in TSource>
{
    void MapFrom(TSource source);
}

```

#### 2.2 auto mapping

* abp 框架可以实现 auto_mapping

##### 2.2.1 auto mapping provider

* 提供 auto_map 服务的底层架构

###### 2.2.1.1 接口

```c#
public interface IAutoObjectMappingProvider
{
    TDestination Map<TSource, TDestination>(object source);    
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
}
    
```

###### 2.2.1.2 泛型接口

```c#
public interface IAutoObjectMappingProvider<TContext> : IAutoObjectMappingProvider
{    
}

```

##### 2.2.2 not_implement_provider

* 默认的 null 实现
* 自动注册，singleton
* 直接抛出异常

```c#
public sealed class NotImplementedAutoObjectMappingProvider 
    : IAutoObjectMappingProvider, ISingletonDependency
{
    public TDestination Map<TSource, TDestination>(object source)
    {
        throw new NotImplementedException($"Can not map from given object ({source}) to {typeof(TDestination).AssemblyQualifiedName}.");
    }
    
    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        throw new NotImplementedException($"Can no map from {typeof(TSource).AssemblyQualifiedName} to {typeof(TDestination).AssemblyQualifiedName}.");
    }
}

```

##### 2.2.3 AutoMapper  provider

* abp 集成了AutoMapper 以实现`IAutoObjectMappingProvider`接口
* 见 automapper

#### 2.3 mapper

* 暴露给上层架构使用的服务

##### 2.3.1 object mapper

###### 2.3.1.1 接口

```c#
public interface IObjectMapper
{    
    IAutoObjectMappingProvider AutoObjectMappingProvider { get; }    
    // map to new    
    TDestination Map<TSource, TDestination>(TSource source);        
    // map to exist
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
}
        
```

###### 2.3.1.2 TSource 接口

```c#
public interface IObjectMapper<TContext> : IObjectMapper
{    
}

```

###### 2.3.1.3 TSource & TDestination 接口

```c#
public interface IObjectMapper<in TSource, TDestination>
{    
    TDestination Map(TSource source);        
    TDestination Map(TSource source, TDestination destination);
}

```

###### 2.3.1.4 扩展

```c#
public static class ObjectMapperExtensions
{
    private static readonly MethodInfo MapToNewObjectMethod;
    private static readonly MethodInfo MapToExistingObjectMethod;
    
    static ObjectMapperExtensions()
    {
        var methods = typeof(IObjectMapper).GetMethods();
        foreach (var method in methods)
        {
            if (method.Name == nameof(IObjectMapper.Map) && 
                method.IsGenericMethodDefinition)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 1)
                {
                    MapToNewObjectMethod = method;
                }
                else if (parameters.Length == 2)
                {
                    MapToExistingObjectMethod = method;
                }
            }
        }
    }
    
    // map with source_obj 非泛型方法
    public static object Map(
        this IObjectMapper objectMapper, 
        Type sourceType, 
        Type destinationType, 
        object source)
    {
        return MapToNewObjectMethod
            .MakeGenericMethod(sourceType, destinationType)
            .Invoke(objectMapper, new[] { source });
    }
    // map with source_obj, destination_obj 非泛型方法
    public static object Map(
        this IObjectMapper objectMapper, 
        Type sourceType, 
        Type destinationType, 
        object source, 
        object destination)
    {
        return MapToExistingObjectMethod
            .MakeGenericMethod(sourceType, destinationType)
            .Invoke(objectMapper, new[] { source, destination });
    }
}

```

##### 2.3.2 default object mapper

###### 2.3.2.1 实现

```c#
public class DefaultObjectMapper : IObjectMapper, ITransientDependency
{
    // 注入服务
    protected IServiceProvider ServiceProvider { get; }
    public IAutoObjectMappingProvider AutoObjectMappingProvider { get; }        
    public DefaultObjectMapper(
        IServiceProvider serviceProvider,
        IAutoObjectMappingProvider autoObjectMappingProvider)
    {
        // auto_obj_mapping_provider 默认是 notImplement
        // 自定义模块中要注册实际的 IAutoObjectMappingProvider，
        // 或者使用 AutoMapper
        AutoObjectMappingProvider = autoObjectMappingProvider;
        ServiceProvider = serviceProvider;
    }
    
    /* map to new */
    public virtual TDestination Map<TSource, TDestination>(TSource source)
    {
        if (source == null)
        {
            return default;
        }
        
        // 如果解析了 IObjectMapper<TSource, TDestination>，
        // 使用 IObjectMapper<TSource, TDestination>.Map(source) 方法
        using (var scope = ServiceProvider.CreateScope())
        {
            var specificMapper = scope.ServiceProvider
                .GetService<IObjectMapper<TSource, TDestination>>();
            if (specificMapper != null)
            {
                return specificMapper.Map(source);
            }
        }
        // 如果 source 实现了 IMapTo，
        // 使用 IMapTo
        if (source is IMapTo<TDestination> mapperSource)
        {
            return mapperSource.MapTo();
        }
        // 如果 destination 实现了 IMapFrom，
        // 使用 IMapFrom
        if (typeof(IMapFrom<TSource>).IsAssignableFrom(typeof(TDestination)))
        {
            try
            {
                //TODO: Check if TDestination has a proper constructor which takes TSource
                //TODO: Check if TDestination has an empty constructor (in this case, use MapFrom)
                
                return (TDestination) Activator.CreateInstance(typeof(TDestination), source);
            }
            catch
            {
                //TODO: Remove catch when TODOs are implemented above
            }
        }
        // 否则，使用 auto_map
        return AutoMap<TSource, TDestination>(source);
    }
    
    /* map to exist */
    public virtual TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        if (source == null)
        {
            return default;
        }
        
        // 如果解析了 IObjectMapper<TSource, TDestination>，
        // 使用 IObjectMapper<TSource, TDestination>.Map(source, destination) 方法
        using (var scope = ServiceProvider.CreateScope())
        {
            var specificMapper = scope.ServiceProvider
                .GetService<IObjectMapper<TSource, TDestination>>();
            if (specificMapper != null)
            {
                return specificMapper.Map(source, destination);
            }
        }
        // 如果 source 实现了 IMapTo，
        // 使用 IMapTo
        if (source is IMapTo<TDestination> mapperSource)
        {
            mapperSource.MapTo(destination);
            return destination;
        }
        // 如果 destination 实现了 IMapFrom，
        // 使用 IMapFrom
        if (destination is IMapFrom<TSource> mapperDestination)
        {
            mapperDestination.MapFrom(source);
            return destination;
        }
        // 否则，使用 auto_map
        return AutoMap(source, destination);
    }
    
    /* auto map to new */
    protected virtual TDestination AutoMap<TSource, TDestination>(object source)
    {
        return AutoObjectMappingProvider.Map<TSource, TDestination>(source);
    }
    /* auto map to exist */
    protected virtual TDestination AutoMap<TSource, TDestination>(TSource source, TDestination destination)
    {
        return AutoObjectMappingProvider.Map<TSource, TDestination>(source, destination);
    }
}

```

###### 2.3.2.2 TSource 实现

```c#
public class DefaultObjectMapper<TContext> : DefaultObjectMapper, IObjectMapper<TContext>
{
    public DefaultObjectMapper(
        IServiceProvider serviceProvider, 
        IAutoObjectMappingProvider<TContext> autoObjectMappingProvider) 
        	: base(serviceProvider, autoObjectMappingProvider)
    {
    }
}

```

#### 2.4 注册 mapper

##### 2.4.1 模块

```c#
public class AbpObjectMappingModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // 如果 mapper 实现了 IObjectMapper<TSource,TDestination> 接口，
        // 注册 mapper 为 IObjectMapper<TSource,TDestination>
        context.Services.OnExposing(onServiceExposingContext =>
        	{
                //Register types for IObjectMapper<TSource, TDestination> if implements
                onServiceExposingContext.ExposedTypes.AddRange(
                    ReflectionHelper.GetImplementedGenericTypes(
                        onServiceExposingContext.ImplementationType,
                        typeof(IObjectMapper<,>)));
            });
    }
    
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 注册 DefaultObjectMapper<TSource>，
        // transient
        context.Services.AddTransient(
            typeof(IObjectMapper<>), typeof(DefaultObjectMapper<>));
    }
}

```

### 3. practice

* 定义 object 实现 IMapTo，IMapFrom
* 或者使用 autoMapper