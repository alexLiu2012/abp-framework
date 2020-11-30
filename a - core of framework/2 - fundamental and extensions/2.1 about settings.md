## about abp settings extended

TODO：

tenant provider 中的 get or null，没有null 判断，会抛异常？？

setting management，store to write settings？？

practice



相关程序集：

* Volo.Abp.Settings
* Volo.Abp.Security

----

### 1. about

除了使用 .net core 中的`IConfiguration`，abp框架还扩展了 settings 用于读取、存储用户参数。

settings 支持数据库存储，可用用来存储用户级别的配置数据。

### 2. what and how

#### 2.1 setting definition

* setting_definition 是abp框架定义的 setting 类，setting 的 domain_model

```c#
public class SettingDefinition
{   
    [NotNull]
    public string Name { get; }    
    private ILocalizableString _displayName;    
    public ILocalizableString DisplayName
    {
        get => _displayName;
        set => _displayName = Check.NotNull(value, nameof(value));
    }        
    [CanBeNull]
    public ILocalizableString Description { get; set; }        
    [CanBeNull]
    public string DefaultValue { get; set; }            
    public bool IsVisibleToClients { get; set; }        
    public List<string> Providers { get; } //TODO: Rename to AllowedProviders        
    public bool IsInherited { get; set; }        
    [NotNull]
    public Dictionary<string, object> Properties { get; }
            
    public SettingDefinition(
        string name,
        string defaultValue = null,
        ILocalizableString displayName = null,
        ILocalizableString description = null,
        bool isVisibleToClients = false,
        bool isInherited = true,
        bool isEncrypted = false)
    {
        Name = name;
        DefaultValue = defaultValue;
        IsVisibleToClients = isVisibleToClients;
        // displayName 使用了 localizedString
        DisplayName = displayName ?? new FixedLocalizableString(name);
        Description = description;
        IsInherited = isInherited;
        IsEncrypted = isEncrypted;
        
        Properties = new Dictionary<string, object>();
        Providers = new List<string>();
    }
    // 指定 property  
    public virtual SettingDefinition WithProperty(string key, object value)
    {
        Properties[key] = value;
        return this;
    }
    // 指定 providers    
    public virtual SettingDefinition WithProviders(params string[] providers)
    {
        if (!providers.IsNullOrEmpty())
        {            
            Providers.AddRange(providers);
        }        
        return this;
    }
}

```

##### 2.1.1 setting_definition provider

* settings_definition_provider 用于获取 settings_definition，

* abp框架定义了抽象基类并实现自动注册，
* 用户自定义 settings_definition_provider 继承基类

###### 2.1.1.1 接口和抽象基类

```c#
public interface ISettingDefinitionProvider
{
    void Define(ISettingDefinitionContext context);
}

```

```c#
public abstract class SettingDefinitionProvider 
    : ISettingDefinitionProvider, ITransientDependency
{
    // 将 setting_definition 注入 context
    // context 传递 setting_definition
    public abstract void Define(ISettingDefinitionContext context);
}

```

###### 2.1.1.2 context

```c#
public class SettingDefinitionContext : ISettingDefinitionContext
{
    // 构造 context，就是一个 dictionary
    protected Dictionary<string, SettingDefinition> Settings { get; }    
    public SettingDefinitionContext(Dictionary<string, SettingDefinition> settings)
    {
        Settings = settings;
    }
    
    // 注入 setting_definition
    public virtual void Add(params SettingDefinition[] definitions)
    {
        if (definitions.IsNullOrEmpty())
        {
            return;
        }        
        foreach (var definition in definitions)
        {
            Settings[definition.Name] = definition;
        }
    }
    
    // 获取 setting_definition，找不到返回 null
    public virtual SettingDefinition GetOrNull(string name)
    {
        return Settings.GetOrDefault(name);
    }
    // get all
    public virtual IReadOnlyList<SettingDefinition> GetAll()
    {
        return Settings.Values.ToImmutableList();
    }        
}

```

##### 2.1.2 派生provider（例子）

```c#
internal class EmailSettingProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(
            new SettingDefinition(
                EmailSettingNames.Smtp.Host, 
                "127.0.0.1", 
                L("DisplayName:Abp.Mailing.Smtp.Host"), 
                L("Description:Abp.Mailing.Smtp.Host")),
            
            new SettingDefinition(
                EmailSettingNames.Smtp.Port, 
                "25", 
                L("DisplayName:Abp.Mailing.Smtp.Port"), 
                L("Description:Abp.Mailing.Smtp.Port")));
    }
            
```

##### 2.1.3 setting_definition manager

* setting_definition_manager 用于管理 setting_definition_provider
* 自动注册

###### 2.1.3.1 接口

```c#
public interface ISettingDefinitionManager
{
    [NotNull]
    SettingDefinition Get([NotNull] string name);    
    SettingDefinition GetOrNull(string name);
    
    IReadOnlyList<SettingDefinition> GetAll();            
}

```

###### 2.1.3.2 实现

```c#
public class SettingDefinitionManager : ISettingDefinitionManager, ISingletonDependency
{
    protected Lazy<IDictionary<string, SettingDefinition>> SettingDefinitions { get; }    
    
    // 注册服务
    protected AbpSettingOptions Options { get; }    
    protected IServiceProvider ServiceProvider { get; }    
    public SettingDefinitionManager(
        IOptions<AbpSettingOptions> options,
        IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        Options = options.Value;
        // 懒加载 setting_definition 集合
        SettingDefinitions = new Lazy<IDictionary<string, SettingDefinition>>(
            CreateSettingDefinitions, true);
    }
    
    // get setting_definition, 找不到抛出异常
    public virtual SettingDefinition Get(string name)
    {
        Check.NotNull(name, nameof(name));        
        var setting = GetOrNull(name);        
        if (setting == null)
        {
            throw new AbpException("Undefined setting: " + name);
        }        
        return setting;
    }
    // get setting_definition, 找不到返回null
    public virtual SettingDefinition GetOrNull(string name)
    {
        return SettingDefinitions.Value.GetOrDefault(name);
    }        
    // get all setting_definition
    public virtual IReadOnlyList<SettingDefinition> GetAll()
    {
        return SettingDefinitions.Value.Values.ToImmutableList();
    }
            
    protected virtual IDictionary<string, SettingDefinition> CreateSettingDefinitions()
    {
        var settings = new Dictionary<string, SettingDefinition>();
        
        using (var scope = ServiceProvider.CreateScope())
        {
            // 获取 setting_definition_provider
            var providers = Options.DefinitionProviders
                .Select(p => scope.ServiceProvider.GetRequiredService(p) as 
                        SettingDefinitionProvider)
                .ToList();
            
            foreach (var provider in providers)
            {
                // 调用 provider 的 define
                provider.Define(new SettingDefinitionContext(settings));
            }
        }
        
        return settings;
    }
}

```

##### 2.1.4 注册 settings provider

###### 2.1.4.1 模块注册

```c#
[DependsOn(typeof(AbpLocalizationAbstractionsModule),
           typeof(AbpSecurityModule),
           typeof(AbpMultiTenancyModule))]
public class AbpSettingsModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        AutoAddDefinitionProviders(context.Services);
    }
    // 配置 abp_setting_options
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpSettingOptions>(options =>  
        	{
                /* 
                options.ValueProviders.Add<DefaultValueSettingValueProvider>();
                options.ValueProviders.Add<ConfigurationSettingValueProvider>();
                options.ValueProviders.Add<GlobalSettingValueProvider>();
                options.ValueProviders.Add<TenantSettingValueProvider>();
                options.ValueProviders.Add<UserSettingValueProvider>();
                */
            });
    }
    // 添加 setting_definition_provider
    private static void AutoAddDefinitionProviders(IServiceCollection services)
    {
        var definitionProviders = new List<Type>();
        
        // 拦截 ISettingDefinitionProvider 
        services.OnRegistred(context =>
            {
                if (typeof(ISettingDefinitionProvider)
                    .IsAssignableFrom(context.ImplementationType))
                {
                    definitionProviders.Add(context.ImplementationType);
                }
            });
		// 将拦截到的 ISettingDefinitionProvider 集合注入 AbpSettingOptions
        services.Configure<AbpSettingOptions>(options =>
            {
                options.DefinitionProviders.AddIfNotContains(definitionProviders);
            });
        }
    }        
}

```

###### 2.1.4.2 abp setting option

* setting_definition_provider 的类型容器
* setting_value_provider 的类型容器

```c#
public class AbpSettingOptions
{
    public ITypeList<ISettingDefinitionProvider> DefinitionProviders { get; }
    //public ITypeList<ISettingValueProvider> ValueProviders { get; }
    
    public AbpSettingOptions()
    {
        DefinitionProviders = new TypeList<ISettingDefinitionProvider>();
        //ValueProviders = new TypeList<ISettingValueProvider>();
    }
}

```

#### 2.2 setting value

* setting_value 是abp框架存储 setting 的 value 的类型
* 它是封装的 NameValue

```c#
[Serializable]    
public class SettingValue : NameValue
{
    public SettingValue()
    {        
    }
    
    public SettingValue(string name, string value)
    {
        Name = name;
        Value = value;
    }
}

```

##### 2.2.1 setting value provider

* 用于实际解析 setting 字符串，自动注册
* 框架定义了接口和抽象基类
* 预定义了不同的provider

###### 2.2.1.1 接口

```c#
public interface ISettingValueProvider
{
    string Name { get; }    
    Task<string> GetOrNullAsync([NotNull] SettingDefinition setting);
}

```

###### 2.2.1.2 抽象基类

```c#
public abstract class SettingValueProvider : ISettingValueProvider, ITransientDependency
{
    public abstract string Name { get; }
    // 注入服务
    protected ISettingStore SettingStore { get; }    
    protected SettingValueProvider(ISettingStore settingStore)
    {
        SettingStore = settingStore;
    }
    
    public abstract Task<string> GetOrNullAsync(SettingDefinition setting);
}

```

##### 2.2.2 不同的 set_val_provider

###### 2.2.2.1 default setting value provider (D)

获取 setting 的默认值（在 setting_definition 中定义）

```c#
public class DefaultValueSettingValueProvider : SettingValueProvider
{
    // provider name = D (for short)
    public const string ProviderName = "D";    
    public override string Name => ProviderName;
    
    public DefaultValueSettingValueProvider(ISettingStore settingStore) : base(settingStore)
    {        
    }    
    public override Task<string> GetOrNullAsync(SettingDefinition setting)
    {
        return Task.FromResult(setting.DefaultValue);
    }
}

```

###### 2.2.2.2 configuration setting value provider (C)

从 .net core configuration 中读取 setting

```c#
public class ConfigurationSettingValueProvider : ISettingValueProvider, ITransientDependency
{
    public const string ConfigurationNamePrefix = "Settings:";
    // provider name = C (for short)
    public const string ProviderName = "C";    
    public string Name => ProviderName;
    // 注入 IConfiguration
    protected IConfiguration Configuration { get; }    
    public ConfigurationSettingValueProvider(IConfiguration configuration)
    {
        Configuration = configuration;
    }
    
    public virtual Task<string> GetOrNullAsync(SettingDefinition setting)
    {
        return Task.FromResult(Configuration[ConfigurationNamePrefix + setting.Name]);
    }
}

```

###### 2.2.2.3 global setting value provider (G)

从 setting store 中获取 setting

```c#
public class GlobalSettingValueProvider : SettingValueProvider
{
    // provider name = G (for short)
    public const string ProviderName = "G";    
    public override string Name => ProviderName;
    
    public GlobalSettingValueProvider(ISettingStore settingStore) : base(settingStore)
    {
    }
    
    public override Task<string> GetOrNullAsync(SettingDefinition setting)
    {
        return SettingStore.GetOrNullAsync(setting.Name, Name, null);
    }
}

```

###### 2.2.2.4 tenant setting value provider (T)

从 setting store 中获取当前 tenant 的 setting

```c#
public class TenantSettingValueProvider : SettingValueProvider
{
    // provider name = T (for short)
    public const string ProviderName = "T";    
    public override string Name => ProviderName;
    // 注入 ICurrentTenant
    protected ICurrentTenant CurrentTenant { get; }    
    public TenantSettingValueProvider(ISettingStore settingStore, ICurrentTenant currentTenant)
        : base(settingStore)
    {
        CurrentTenant = currentTenant;
    }
    
    public override async Task<string> GetOrNullAsync(SettingDefinition setting)
    {
        // ??? 会抛异常？？？
        return await SettingStore.GetOrNullAsync(
            setting.Name, Name, CurrentTenant.Id?.ToString());
    }
}

```

###### 2.2.2.5 user setting value provider (U)

从 setting store 中获取当前 user 的 setting

```c#
public class UserSettingValueProvider : SettingValueProvider
{    
    // provider name = U (for short)
    public const string ProviderName = "U";    
    public override string Name => ProviderName;
    // 注入 ICurrentUser
    protected ICurrentUser CurrentUser { get; }    
    public UserSettingValueProvider(ISettingStore settingStore, ICurrentUser currentUser)
        : base(settingStore)
    {
        CurrentUser = currentUser;
    }

    public override async Task<string> GetOrNullAsync(SettingDefinition setting)
    {
        if (CurrentUser.Id == null)
        {
            return null;
        }
        
        return await SettingStore.GetOrNullAsync(
            setting.Name, Name, CurrentUser.Id.ToString());
    }
}

```

##### 2.2.3 setting store

* setting store 实现了 setting 仓储，是真正提供 setting (value) 的地方
* abp框架默认实现了 null_setting_store，自动注册
* setting management 模块中定义了其他 store

###### 2.2.3.1 接口

```c#
public interface ISettingStore
{
    Task<string> GetOrNullAsync(
        [NotNull] string name,
        [CanBeNull] string providerName,
        [CanBeNull] string providerKey
    );
}

```

###### 2.2.3.2 默认实现

```c#
[Dependency(TryRegister = true)]
public class NullSettingStore : ISettingStore, ISingletonDependency
{
    public ILogger<NullSettingStore> Logger { get; set; }    
    public NullSettingStore()
    {
        Logger = NullLogger<NullSettingStore>.Instance;
    }
    
    public Task<string> GetOrNullAsync(string name, string providerName, string providerKey)
    {
        return Task.FromResult((string) null);
    }
}

```

###### 2.2.3.3 其他store

见后

##### 2.2.4 setting value provider manager

* 负责管理 setting_value_provider
* 自动注册

###### 2.2.4.1 接口

```c#
public interface ISettingValueProviderManager
{
    List<ISettingValueProvider> Providers { get; }    
}

```

###### 2.2.4.2 实现

```c#
public class SettingValueProviderManager : ISettingValueProviderManager, ISingletonDependency
{
    private readonly Lazy<List<ISettingValueProvider>> _lazyProviders;
    public List<ISettingValueProvider> Providers => _lazyProviders.Value;    
    protected AbpSettingOptions Options { get; }   
    
    public SettingValueProviderManager(
        IServiceProvider serviceProvider,IOptions<AbpSettingOptions> options)
    {
        // 注入 abp setting options（setting_value_provider的容器）
        Options = options.Value;
        // 懒加载 setting_value_provider 集合
        _lazyProviders = new Lazy<List<ISettingValueProvider>>(
            () => Options.ValueProviders.Select(type =>
            	serviceProvider.GetRequiredService(type) 
                	as ISettingValueProvider).ToList(),
            true);
        }
    }
}

```

##### 2.2.5 注册 setting value provider

###### 2.2.5.1 模块注册

```c#
[DependsOn(typeof(AbpLocalizationAbstractionsModule),
           typeof(AbpSecurityModule),
           typeof(AbpMultiTenancyModule))]
public class AbpSettingsModule : AbpModule
{    
    // 配置 abp_setting_options
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpSettingOptions>(options =>  
        	{            
                // 注册了全部预定义 setting_value_provider
                options.ValueProviders.Add<DefaultValueSettingValueProvider>();
                options.ValueProviders.Add<ConfigurationSettingValueProvider>();
                options.ValueProviders.Add<GlobalSettingValueProvider>();
                options.ValueProviders.Add<TenantSettingValueProvider>();
                options.ValueProviders.Add<UserSettingValueProvider>();                
            });
    }
}

```

###### 2.2.5.2 abp setting option

* setting_definition_provider 的类型容器
* setting_value_provider 的类型容器

```c#
public class AbpSettingOptions
{
    //public ITypeList<ISettingDefinitionProvider> DefinitionProviders { get; }
    public ITypeList<ISettingValueProvider> ValueProviders { get; }
    
    public AbpSettingOptions()
    {
        //DefinitionProviders = new TypeList<ISettingDefinitionProvider>();
        ValueProviders = new TypeList<ISettingValueProvider>();
    }
}

```

#### 2.3 setting provider

* setting provider 是提供给上层服务（如controller）使用的接口
* 自动注册

##### 2.3.1 接口

```c#
public interface ISettingProvider
{
    Task<string> GetOrNullAsync([NotNull]string name);    
    Task<List<SettingValue>> GetAllAsync();
}

```

##### 2.3.2 实现

```c#
public class SettingProvider : ISettingProvider, ITransientDependency
{
    // 注入服务 
    protected ISettingDefinitionManager SettingDefinitionManager { get; }
    protected ISettingEncryptionService SettingEncryptionService { get; }
    protected ISettingValueProviderManager SettingValueProviderManager { get; }    
    public SettingProvider(
        ISettingDefinitionManager settingDefinitionManager,
        ISettingEncryptionService settingEncryptionService,
        ISettingValueProviderManager settingValueProviderManager)
    {
        SettingDefinitionManager = settingDefinitionManager;
        SettingEncryptionService = settingEncryptionService;
        SettingValueProviderManager = settingValueProviderManager;
    }
    // 获取 setting value
    public virtual async Task<string> GetOrNullAsync(string name)
    {
        // 获取 setting_definition
        var setting = SettingDefinitionManager.Get(name);
        // 获取 setting_value_provider
        var providers = Enumerable.Reverse(SettingValueProviderManager.Providers);
        
        if (setting.Providers.Any())
        {
            // 过滤 setting_definition 中使用的 setting_value_provider
            providers = providers.Where(p => setting.Providers.Contains(p.Name));
        }
        
        //TODO: How to implement setting.IsInherited?
        
        // 调用 setting value provider 的方法，
        // 从 setting definition Provider 获取 setting (string)
        var value = await GetOrNullValueFromProvidersAsync(providers, setting);
        // 解密 setting value
        if (value != null && setting.IsEncrypted)
        {
            value = SettingEncryptionService.Decrypt(setting, value);
        }
        
        return value;
    }
    // 获取所有 setting value
    public virtual async Task<List<SettingValue>> GetAllAsync()
    {
        var settingValues = new Dictionary<string, SettingValue>();
        var settingDefinitions = SettingDefinitionManager.GetAll();
        // 遍历 setting value provider
        foreach (var provider in SettingValueProviderManager.Providers)
        {
            // 遍历 setting definition 
            foreach (var setting in settingDefinitions)
            {
                // 调用 setting value provider 的方法，
                // 从 setting definition Provider 获取 setting (string)
                var value = await provider.GetOrNullAsync(setting);
                if (value != null)
                {
                    // 解密 setting value
                    if (setting.IsEncrypted)
                    {
                        value = SettingEncryptionService.Decrypt(setting, value);
                    }
                    
                    settingValues[setting.Name] = new SettingValue(setting.Name, value);
                }
            }
        }
        
        return settingValues.Values.ToList();
    }
    
    
    protected virtual async Task<string> GetOrNullValueFromProvidersAsync(
        IEnumerable<ISettingValueProvider> providers,
        SettingDefinition setting)
    {
        // 返回第一个匹配的 setting
        foreach (var provider in providers)
        {
            var value = await provider.GetOrNullAsync(setting);
            if (value != null)
            {
                return value;
            }
        }        
        return null;
    }
}

```

##### 2.3.3 setting encryption

* setting encryption service 实现了 setting 字符串的加密、解密
* 通过`IStringEncryptionService`完成
* 自动注册

```c#
public class SettingEncryptionService : ISettingEncryptionService, ITransientDependency
{
    // 注入 IStringEncryptionService
    protected IStringEncryptionService StringEncryptionService { get; }    
    public SettingEncryptionService(IStringEncryptionService stringEncryptionService)
    {
        StringEncryptionService = stringEncryptionService;
    }
    // 加密
    public virtual string Encrypt(SettingDefinition settingDefinition, string plainValue)
    {
        if (plainValue.IsNullOrEmpty())
        {
            return plainValue;
        }        
        return StringEncryptionService.Encrypt(plainValue);
    }
    // 解密
    public virtual string Decrypt(SettingDefinition settingDefinition, string encryptedValue)       {
        if (encryptedValue.IsNullOrEmpty())
        {
            return encryptedValue;
        }        
        return StringEncryptionService.Decrypt(encryptedValue);
    }
}

```

### 3. setting management

TODO

### 4. practice

TODO





