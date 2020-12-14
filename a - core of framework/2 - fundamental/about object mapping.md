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

#### 2.5 abp auto mapper

##### 2.5.1 mapper accessor

* 封装`AutoMapper.IMapper`

###### 2.5.1.1 接口

```c#
public interface IMapperAccessor
{
    IMapper Mapper { get; }
}

```

###### 2.5.1.2 实现

```c#
internal class MapperAccessor : IMapperAccessor
{
    public IMapper Mapper { get; set; }
}

```

##### 2.5.2 auto mapper config context

* 封装`AutoMapper.IMapperConfigurationContext`

###### 2.5.2.1 接口

```c#
public interface IAbpAutoMapperConfigurationContext
{
    IMapperConfigurationExpression MapperConfiguration { get; }    
    IServiceProvider ServiceProvider { get; }
}

```

###### 2.5.2.2 实现

```c#
public class AbpAutoMapperConfigurationContext 
    :IAbpAutoMapperConfigurationContext
{
    public IMapperConfigurationExpression MapperConfiguration { get; }
    public IServiceProvider ServiceProvider { get; }
    
    public AbpAutoMapperConfigurationContext(
        IMapperConfigurationExpression mapperConfigurationExpression, 
        IServiceProvider serviceProvider)
    {
        MapperConfiguration = mapperConfigurationExpression;
        ServiceProvider = serviceProvider;
    }
}

```

##### 2.5.3 automapper as obj mapping provider

* 使用`AutoMapper`作为 object mapping provider

###### 2.5.3.1 automapper mapping provider

```c#
public class AutoMapperAutoObjectMappingProvider 
    : IAutoObjectMappingProvider
{
    // 注入 automapper
    public IMapperAccessor MapperAccessor { get; }    
    public AutoMapperAutoObjectMappingProvider(IMapperAccessor mapperAccessor)
    {
        MapperAccessor = mapperAccessor;
    }
    // map to new 
    public virtual TDestination Map<TSource, TDestination>(
        object source)
    {
        return MapperAccessor.Mapper.Map<TDestination>(source);
    }
    // map to exist
    public virtual TDestination Map<TSource, TDestination>(
        TSource source, TDestination destination)
    {
        return MapperAccessor.Mapper.Map(source, destination);
    }
}

```

###### 2.5.3.2 automapper mapping provider<T>

```c#
public class AutoMapperAutoObjectMappingProvider<TContext> 
    :AutoMapperAutoObjectMappingProvider, 
	 IAutoObjectMappingProvider<TContext>
{
    public AutoMapperAutoObjectMappingProvider(
        IMapperAccessor mapperAccessor) : base(mapperAccessor)
    {
    }
}
    
```

##### 2.5.4 automapper obj extending

```c#
public static class AbpAutoMapperExtensibleDtoExtensions
{
    public static IMappingExpression<TSource, TDestination> 
        MapExtraProperties<TSource, TDestination>(
        	this IMappingExpression<TSource, TDestination> mappingExpression,
        	MappingPropertyDefinitionChecks? definitionChecks = null,
        	string[] ignoredProperties = null)      
        		where TDestination : IHasExtraProperties
                where TSource : IHasExtraProperties
    {
        return mappingExpression
            .ForMember(
            	x => x.ExtraProperties,
            	y => y.MapFrom(
                    (source, destination, extraProps) =>
                    	{
                            var result = extraProps.IsNullOrEmpty()
                                ? new Dictionary<string, object>()
                                : new Dictionary<string, object>(extraProps);
                            
                            ExtensibleObjectMapper
                                .MapExtraPropertiesTo<TSource, TDestination>(
                                source.ExtraProperties,
                                result,
                                definitionChecks,
                                ignoredProperties);
                            
                            return result;
                        }));
	}
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreExtraProperties<TSource, TDestination>(
        	this IMappingExpression<TSource, TDestination> mappingExpression)
        		where TDestination : IHasExtraProperties
                where TSource : IHasExtraProperties
    {
        return mappingExpression.Ignore(x => x.ExtraProperties);
    }
}

```

##### 2.5.5 automapper ignore

* 扩展方法，可以忽略 ddd 定义的审计属性

```c#
public static class AutoMapperExpressionExtensions
{
    public static IMappingExpression<TDestination, TMember> 
        Ignore<TDestination, TMember, TResult>(
        	this IMappingExpression<TDestination, TMember> mappingExpression,
        	Expression<Func<TMember, TResult>> destinationMember)
    {
        return mappingExpression.ForMember(destinationMember, opts => opts.Ignore());
    }
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreHasCreationTimeProperties<TSource, TDestination>(
        	this IMappingExpression<TSource, TDestination> mappingExpression)
        		where TDestination : IHasCreationTime
    {
        return mappingExpression.Ignore(x => x.CreationTime);
    }
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreMayHaveCreatorProperties<TSource, TDestination>(
        	this IMappingExpression<TSource, TDestination> mappingExpression)
        		where TDestination : IMayHaveCreator
    {
        return mappingExpression.Ignore(x => x.CreatorId);
    }
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreCreationAuditedObjectProperties<TSource, TDestination>(
        	this IMappingExpression<TSource, TDestination> mappingExpression)
        		where TDestination : ICreationAuditedObject
    {
        return mappingExpression
            .IgnoreHasCreationTimeProperties()
            .IgnoreMayHaveCreatorProperties();
    }
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreHasModificationTimeProperties<TSource, TDestination>(
            this IMappingExpression<TSource, TDestination> mappingExpression)        
        		where TDestination : IHasModificationTime
    {
        return mappingExpression.Ignore(x => x.LastModificationTime);
    }
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreModificationAuditedObjectProperties<TSource, TDestination>(
            this IMappingExpression<TSource, TDestination> mappingExpression)
        		where TDestination : IModificationAuditedObject
    {
        return mappingExpression
            .IgnoreHasModificationTimeProperties()
            .Ignore(x => x.LastModifierId);
    }
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreAuditedObjectProperties<TSource, TDestination>(
        	this IMappingExpression<TSource, TDestination> mappingExpression)
        		where TDestination : IAuditedObject
    {
        return mappingExpression
            .IgnoreCreationAuditedObjectProperties()
            .IgnoreModificationAuditedObjectProperties();
    }
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreSoftDeleteProperties<TSource, TDestination>(
        	this IMappingExpression<TSource, TDestination> mappingExpression)    
        		where TDestination : ISoftDelete
    {
        return mappingExpression.Ignore(x => x.IsDeleted);
    }
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreHasDeletionTimeProperties<TSource, TDestination>(
        	this IMappingExpression<TSource, TDestination> mappingExpression)
        		where TDestination : IHasDeletionTime
    {
        return mappingExpression
            .IgnoreSoftDeleteProperties()
            .Ignore(x => x.DeletionTime);
    }
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreDeletionAuditedObjectProperties<TSource, TDestination>(
        	this IMappingExpression<TSource, TDestination> mappingExpression)
        		where TDestination : IDeletionAuditedObject
    {
        return mappingExpression
            .IgnoreHasDeletionTimeProperties()
            .Ignore(x => x.DeleterId);
    }
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreFullAuditedObjectProperties<TSource, TDestination>(
        	this IMappingExpression<TSource, TDestination> mappingExpression)
        		where TDestination : IFullAuditedObject
    {
        return mappingExpression
            .IgnoreAuditedObjectProperties()
            .IgnoreDeletionAuditedObjectProperties();
    }
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreMayHaveCreatorProperties<TSource, TDestination, TUser>(
            this IMappingExpression<TSource, TDestination> mappingExpression)
        		where TDestination : IMayHaveCreator<TUser>
    {
        return mappingExpression
            .Ignore(x => x.Creator);
    }
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreCreationAuditedObjectProperties<TSource, TDestination, TUser>(
            this IMappingExpression<TSource, TDestination> mappingExpression)
        		where TDestination : ICreationAuditedObject<TUser>
    {
        return mappingExpression
            .IgnoreCreationAuditedObjectProperties()
            .IgnoreMayHaveCreatorProperties<TSource, TDestination, TUser>();
    }
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreModificationAuditedObjectProperties<TSource, TDestination, TUser>(
        	this IMappingExpression<TSource, TDestination> mappingExpression)
        		where TDestination : IModificationAuditedObject<TUser>
    {
        return mappingExpression
            .IgnoreModificationAuditedObjectProperties()
            .Ignore(x => x.LastModifier);
    }
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreAuditedObjectProperties<TSource, TDestination, TUser>(
        	this IMappingExpression<TSource, TDestination> mappingExpression)
        		where TDestination : IAuditedObject<TUser>
    {
        return mappingExpression
            .IgnoreCreationAuditedObjectProperties<TSource, TDestination, TUser>()
            .IgnoreModificationAuditedObjectProperties<TSource, TDestination, TUser>();
    }
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreDeletionAuditedObjectProperties<TSource, TDestination, TUser>(
        	this IMappingExpression<TSource, TDestination> mappingExpression)
        		where TDestination : IDeletionAuditedObject<TUser>
    {
        return mappingExpression
            .IgnoreDeletionAuditedObjectProperties()
            .Ignore(x => x.Deleter);
    }
    
    
    public static IMappingExpression<TSource, TDestination> 
        IgnoreFullAuditedObjectProperties<TSource, TDestination, TUser>(
        	this IMappingExpression<TSource, TDestination> mappingExpression)
        		where TDestination : IFullAuditedObject<TUser>
    {
        return mappingExpression
            .IgnoreAuditedObjectProperties<TSource, TDestination, TUser>()
            .IgnoreDeletionAuditedObjectProperties<TSource, TDestination, TUser>();
    }
}

```

#### 2.6 注册 automapper map provider

##### 2.5.1 模块

```c#
[DependsOn(typeof(AbpObjectMappingModule),
           typeof(AbpObjectExtendingModule),
           typeof(AbpAuditingModule))]
public class AbpAutoMapperModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 使用 automapper 作为 IAutoObjectMappingProvider
        context.Services.AddAutoMapperObjectMapper();
        
        // 注册 IMapperAccessor，
        // 创建了 IMapper
        var mapperAccessor = new MapperAccessor();
        context.Services.AddSingleton<IMapperAccessor>(_ => mapperAccessor);
        context.Services.AddSingleton<MapperAccessor>(_ => mapperAccessor);
    }
    
    public override void OnPreApplicationInitialization(
        ApplicationInitializationContext context)
    {
        CreateMappings(context.ServiceProvider);
    }
    
    private void CreateMappings(IServiceProvider serviceProvider)
    {
        using (var scope = serviceProvider.CreateScope())
        {
            // 获取 abp_automapper_options, 
            // 包含 configuration 的容器
            var options = scope.ServiceProvider
                .GetRequiredService<IOptions<AbpAutoMapperOptions>>().Value;                        
            // 创建 mapperConfiguration 并配置 
            var mapperConfiguration = new MapperConfiguration(
                mapperConfigurationExpression =>
                {
                    ConfigureAll(new AbpAutoMapperConfigurationContext(
                        mapperConfigurationExpression, 
                        scope.ServiceProvider));
                });
            
            // 验证 mapperConfiguration 中的 configuration
            ValidateAll(mapperConfiguration);
            
            // 从 mapperConfiguration 创建 IMapper，
            // 将 IMapper 赋值给 mapperAccessor
            scope.ServiceProvider
                .GetRequiredService<MapperAccessor>().Mapper = 
                	mapperConfiguration.CreateMapper();
            
            /* configure 的具体方法*/
            void ConfigureAll(IAbpAutoMapperConfigurationContext ctx)
            {
                foreach (var configurator in options.Configurators)
                {
                    configurator(ctx);
                }
            }
            /* validate 的具体方法*/ 
            void ValidateAll(IConfigurationProvider config)
            {
                foreach (var profileType in options.ValidatingProfiles)
                {
                        config.AssertConfigurationIsValid(
                            ((Profile)Activator.CreateInstance(profileType))
                            	.ProfileName);
                }
            }                        
        }
    }
}

```

##### 2.5.2 添加 automapper map provider 

```c#
public static class AbpAutoMapperServiceCollectionExtensions
{
    // 使用 AutoMapperAutoObjectMappingProvider
    public static IServiceCollection AddAutoMapperObjectMapper(
        this IServiceCollection services)
    {
        return services.Replace(
            ServiceDescriptor.Transient<IAutoObjectMappingProvider, 
            AutoMapperAutoObjectMappingProvider>());
    }
    // 使用 AutoMapperAutoObjectMappingProvider<T>
    public static IServiceCollection AddAutoMapperObjectMapper<TContext>(
        this IServiceCollection services)
    {
        return services.Replace(
            ServiceDescriptor.Transient<IAutoObjectMappingProvider<TContext>, 
            AutoMapperAutoObjectMappingProvider<TContext>>());
    }
}

```

##### 2.5.3 abp automapper options

```c#
public class AbpAutoMapperOptions
{
    public List<Action<IAbpAutoMapperConfigurationContext>> Configurators { get; }    
    public ITypeList<Profile> ValidatingProfiles { get; set; }    
    public AbpAutoMapperOptions()
    {
        Configurators = new List<Action<IAbpAutoMapperConfigurationContext>>();
        ValidatingProfiles = new TypeList<Profile>();
    }
    
    /* 添加 automapper profile */
    // 添加 assembly 下的所有 profile 到 configurations
    public void AddMaps<TModule>(bool validate = false)
    {
        var assembly = typeof(TModule).Assembly;
        
        Configurators.Add(context =>
        	{
                context.MapperConfiguration.AddMaps(assembly);
            });
        
        if (validate)
        {
            var profileTypes = assembly.DefinedTypes.Where(
                type => typeof(Profile).IsAssignableFrom(type) && 
                		!type.IsAbstract && 
                		!type.IsGenericType);
            
            foreach (var profileType in profileTypes)
            {
                ValidatingProfiles.Add(profileType);
            }
        }
    }
    // 添加一个 profile 到 configurations   
    public void AddProfile<TProfile>(bool validate = false)
        where TProfile : Profile, new()
    {
        Configurators.Add(context =>
    	    {
                context.MapperConfiguration.AddProfile<TProfile>);
            });
        
        if (validate)
        {
            ValidateProfile(typeof(TProfile));
        }
    }
    
    /* 验证 automapper profile*/
    public void ValidateProfile<TProfile>(bool validate = true)
        where TProfile : Profile
    {
        ValidateProfile(typeof(TProfile), validate);
    }        
    public void ValidateProfile(Type profileType, bool validate = true)
    {
        if (validate)
        {
            ValidatingProfiles.AddIfNotContains(profileType);
        }
        else
        {
            ValidatingProfiles.Remove(profileType);
        }
    }
}
 
```

### 3. practice

* 定义 object 实现 IMapTo，IMapFrom
* 或者使用 autoMapper

* 定义 automapper profile，添加整个 assembly