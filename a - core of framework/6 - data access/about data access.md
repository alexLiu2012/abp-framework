## about data access

相关程序集：

* Volo.Abp.DataAccess

### 1. about

 #### 1.1 summary

* abp框架定义的 data access 基础功能

#### 1.2 how designed

##### 1.2.1 解析 conn string

* `IConnectionStringResolver` 是上层架构使用的服务接口
  * `IConnectionStringResolver.Resolve(name)`获取 conn string

* `AbpDbConnectionOptions`存储 conn string
  * 从 ms configuration 中获取 “ConnectionStrings" section

##### 1.2.2 data filter

* `IDataFilter`是上层架构使用的服务接口，
  * enable/disable TDataFilter
  * 应该叫 data filter manager
* `IDataFilter<T>`真正 enable/disable T filter
  * ISoftDelete、IMultiTenant 是预定义 TDataFilter
  * 可以自定义，eg IISAvailable，使用 IDataFilter<IIsAvailable> 开启、停止
* `AbpDataFilterOptions`存储要使用的 TDataFilter 以及默认状态（enable/disable）

##### 1.2.3 种子数据

* `IDataSeeder`是上层架构使用的服务接口，
  * 内部使用`IDataSeedContributor`创建种子数据
* `AbpDataSeedOptions`存储`IDataSeedContributor`集合
* 自定义 contributor 实现`IDataSeedContributor`，实现种子数据创建

### 2. details

#### 2.1 connection string resolve

* abp框架将 connection string 解析抽离成独立服务

##### 2.1.1 conn string resolver

###### 2.1.1.1 接口

```c#
public interface IConnectionStringResolver
{
    [NotNull]
    string Resolve(string connectionStringName = null);
}

```

###### 2.1.1.2 扩展

```c#
public static class ConnectionStringResolverExtensions
{
    public static string Resolve<T>(this IConnectionStringResolver resolver)
    {
        return resolver.Resolve(
            ConnectionStringNameAttribute.GetConnStringName<T>());
    }
}

```

###### 2.1.1.3 实现

* 自动注册，Transient

```c#
public class DefaultConnectionStringResolver 
    : IConnectionStringResolver, 
	  ITransientDependency
{
    // 注入 abp dbConnction options
    protected AbpDbConnectionOptions Options { get; }   
    public DefaultConnectionStringResolver(
        IOptionsSnapshot<AbpDbConnectionOptions> options)
    {
        Options = options.Value;
    }
    
    // 从 abpConnectionOptions 中获取 conn string，
    // 输入的 conn string name 为 key，
    // 没有查到返回 default
    public virtual string Resolve(string connectionStringName = null)
    {
        //Get module specific value if provided
        if (!connectionStringName.IsNullOrEmpty())
        {
            var moduleConnString = Options.ConnectionStrings
                .GetOrDefault(connectionStringName);
            
            if (!moduleConnString.IsNullOrEmpty())
            {
                return moduleConnString;
            }
        }
        
        //Get default value
        return Options.ConnectionStrings.Default;
    }
}

```

##### 2.1.2 conn string name attribute

* 标记 conn string name 特性

```c#
public class ConnectionStringNameAttribute : Attribute
{
    [NotNull]
    public string Name { get; }    
    public ConnectionStringNameAttribute([NotNull] string name)
    {
        Check.NotNull(name, nameof(name));        
        Name = name;
    }
    
    // 获取类型 T 标记的 conn string name
    public static string GetConnStringName<T>()
    {
        return GetConnStringName(typeof(T));
    }    
    public static string GetConnStringName(Type type)
    {
        var nameAttribute = type.GetTypeInfo()
            .GetCustomAttribute<ConnectionStringNameAttribute>();
        
        if (nameAttribute == null)
        {
            return type.FullName;
        }
        
        return nameAttribute.Name;        
    }
}

```

##### 2.1.3 dbConnection options

* 用于存储 connection string 的容器，
* 封装 connection strings，真正的容器

```c#
public class AbpDbConnectionOptions
{
    public ConnectionStrings ConnectionStrings { get; set; }    
    public AbpDbConnectionOptions()
    {
        ConnectionStrings = new ConnectionStrings();
    }
}

```

##### 2.1.4 connection strings

* 真正的 conn string 容器

```c#
[Serializable]
public class ConnectionStrings : Dictionary<string, string>
{
    public const string DefaultConnectionStringName = "Default";
    
    public string Default
    {
        get => this.GetOrDefault(DefaultConnectionStringName);
        set => this[DefaultConnectionStringName] = value;
    }
}

```

#### 2.2 data filter

##### 2.2.1 data filter (manager)

###### 2.2.1.1 接口

```c#
public interface IDataFilter
{
    IDisposable Enable<TFilter>() where TFilter : class;    
    IDisposable Disable<TFilter>() where TFilter : class;
    
    bool IsEnabled<TFilter>() where TFilter : class;
}
```

###### 2.2.1.2 实现

```c#
//TODO: Create a Volo.Abp.Data.Filtering namespace?
public class DataFilter : IDataFilter, ISingletonDependency
{
    private readonly ConcurrentDictionary<Type, object> _filters;    
    private readonly IServiceProvider _serviceProvider;    
    public DataFilter(IServiceProvider serviceProvider)
    {
        // 注入 service provider
        _serviceProvider = serviceProvider;
        // 创建 dataFilter 集合
        _filters = new ConcurrentDictionary<Type, object>();
    }
    
    public IDisposable Enable<TFilter>()        
        where TFilter : class        
    {
        return GetFilter<TFilter>().Enable();
    }
    
    public IDisposable Disable<TFilter>()            
        where TFilter : class
    {
        return GetFilter<TFilter>().Disable();
    }
    
    public bool IsEnabled<TFilter>()            
        where TFilter : class
    {
        return GetFilter<TFilter>().IsEnabled;
    }
    
    private IDataFilter<TFilter> GetFilter<TFilter>()            
        where TFilter : class
    {
        // 从 dataFilter 容器中获取 TFilter，
        // 如果没有，创建 TFilter 并注入容器
        return _filters.GetOrAdd(
            typeof(TFilter),
            () => _serviceProvider.GetRequiredService<IDataFilter<TFilter>>()) 
        as IDataFilter<TFilter>;
    }
}

```

##### 2.2.1 data filter <T>

###### 2.2.1.1 接口

```c#
public interface IDataFilter<TFilter>        
    where TFilter : class
{
    IDisposable Enable();    
    IDisposable Disable();
    
    bool IsEnabled { get; }
}

```

###### 2.2.1.2 实现

```c#
public class DataFilter<TFilter> 
    : IDataFilter<TFilter>        
        where TFilter : class
{      
    private readonly AbpDataFilterOptions _options;    
    private readonly AsyncLocal<DataFilterState> _filter;    
    public DataFilter(IOptions<AbpDataFilterOptions> options)
    {
        // 注入 data filter options
        _options = options.Value;
        // 创建 asyncLocal<data filter state>
        _filter = new AsyncLocal<DataFilterState>();
    }
    
     public bool IsEnabled
    {
        get
        {
            EnsureInitialized();
            return _filter.Value.IsEnabled;
        }
    }
    
    // enable data filter
    public IDisposable Enable()
    {
        if (IsEnabled)
        {
            return NullDisposable.Instance;
        }
        
        _filter.Value.IsEnabled = true;        
        return new DisposeAction(() => Disable());
    }
            
    // disable data filter
    public IDisposable Disable()
    {
        if (!IsEnabled)
        {
            return NullDisposable.Instance;
        }
        
        _filter.Value.IsEnabled = false;        
        return new DisposeAction(() => Enable());
    }
            
    // ensure dataFilterOptions.DefaultState 不为 null
    private void EnsureInitialized()
    {
        if (_filter.Value != null)
        {
            return;
        }
        // 如果为 null，创建 dataFilterState 为 true
        _filter.Value = _options.DefaultStates.GetOrDefault(
            	typeof(TFilter))
            		?.Clone() ?? new DataFilterState(true);
    }
}

```

##### 2.2.3 data filter options

* filter state 的容器

```c#
public class AbpDataFilterOptions
{
    public Dictionary<Type, DataFilterState> DefaultStates { get; }    
    public AbpDataFilterOptions()
    {
        DefaultStates = new Dictionary<Type, DataFilterState>();
    }
}

```

##### 2.2.4 data filter state

```c#
public class DataFilterState
{
    public bool IsEnabled { get; set; }    
    public DataFilterState(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }
    
    public DataFilterState Clone()
    {
        return new DataFilterState(IsEnabled);
    }
}

```

#### 2.3 data seed

##### 2.3.1 data seeder

###### 2.3.1.1 接口

```c#
public interface IDataSeeder
{
    Task SeedAsync(DataSeedContext context);
}

```

###### 2.3.1.2 实现

```c#
//TODO: Create a Volo.Abp.Data.Seeding namespace?
public class DataSeeder 
    : IDataSeeder, 
	  ITransientDependency
{
    // 注入服务、options
    protected IServiceScopeFactory ServiceScopeFactory { get; }
    protected AbpDataSeedOptions Options { get; }
    public DataSeeder(
        IOptions<AbpDataSeedOptions> options,
        IServiceScopeFactory serviceScopeFactory)
    {
        ServiceScopeFactory = serviceScopeFactory;
        Options = options.Value;
    }
    
    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            // 遍历 options 中的所有 contributor
            foreach (var contributorType in Options.Contributors)
            {
                var contributor = (IDataSeedContributor) scope
                    .ServiceProvider                        
                    .GetRequiredService(contributorType);
                
                // 执行 contributor 
                await contributor.SeedAsync(context);
            }
        }
    }
}

```

##### 2.3.2 data seed options

* data seed contributor 容器，
* 封装了 data seed contributor list，真正的 contributor 容器

```c#
public class AbpDataSeedOptions
{
    public DataSeedContributorList Contributors { get; }    
    public AbpDataSeedOptions()
    {
        // 创建 data seed contributor list
        Contributors = new DataSeedContributorList();
    }
}

```

##### 2.3.3 data seed contributor

###### 2.3.3.1 data seed contributor list

* 真正的 data seed contributor 容器

```c#
public class DataSeedContributorList 
    : TypeList<IDataSeedContributor>
{    
}

```

###### 2.3.3.2 data seed contributor 接口

* 定义 contributor 实现此接口

```c#
public interface IDataSeedContributor
{
    Task SeedAsync(DataSeedContext context);
}

```

###### 2.3.3.3 data seed context

```c#
public class DataSeedContext
{
    public Guid? TenantId { get; set; }        
    [NotNull]
    public Dictionary<string, object> Properties { get; }    
    public DataSeedContext(Guid? tenantId = null)
    {
        // 注入 tenant id
        TenantId = tenantId;
        // 创建 property 容器，
        // 用于传递数据
        Properties = new Dictionary<string, object>();
    }
    
    // 索引器
    [CanBeNull]
    public object this[string name]
    {
        get => Properties.GetOrDefault(name);
        set => Properties[name] = value;
    }
    
    // 传入数据
    public virtual DataSeedContext WithProperty(string key, object value)
    {
        Properties[key] = value;
        return this;
    }
}

```

#### 2.4 data access  module

```c#
[DependsOn(typeof(AbpObjectExtendingModule),
           typeof(AbpUnitOfWorkModule))]
public class AbpDataModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // （从IHost）获取 ms configuration，
        // 配置 dbConnection options
        var configuration = context.Services.GetConfiguration();        
        Configure<AbpDbConnectionOptions>(configuration);
        
        // 注入泛型 dataFilter<T>
        context.Services.AddSingleton(
            typeof(IDataFilter<>), 
            typeof(DataFilter<>));
    }
    
    // 注入所有 data seed contributor，
    // 即实现了 IDataSeedContributor 接口
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        AutoAddDataSeedContributors(context.Services);
    }
            
    private static void AutoAddDataSeedContributors(IServiceCollection services)
    {
        var contributors = new List<Type>();
        
        services.OnRegistred(context =>
        	{
                if (typeof(IDataSeedContributor)
                    .IsAssignableFrom(context.ImplementationType))
                {
                    contributors.Add(context.ImplementationType);
                }
            });
        
        services.Configure<AbpDataSeedOptions>(options =>
            {
                options.Contributors.AddIfNotContains(contributors);
            });
    }
}

```

### 3. practice

