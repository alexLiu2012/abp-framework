## about auditing

相关程序集：

* Volo.Abp.Auditing

----

### 1. about

#### 1.1 summary

* abp 框架实现了自动审计功能
  * 自动审计日志
  * 自动处理 entity 审计元素（property）

#### 1.2 how designed

* auditLogInfo 是 auditing log 的模型，包含
  * application name, type ......
  * auditLogAction, 记录 action 信息
  * entityChangeInfo，记录 entity changes 信息

* audit helper 用于创建 auditLogInfo
  * audit log contributor 提供了 audit log 的底层服务
* audit manager 管理 auditLogInfo 的持久化
  * audit store 是存储的具体实现者，
  * 使用 audit serializer 序列化 audit info
* 在 asp.net core mvc audit log 中间件中使用自动注册，调用 interceptor
* 还可以自动审计 entity
  * 实现特定 audit object 接口
  * 在 save async 方法时调用 audit property setter

### 2. details

#### 2.1 audit log

##### 2.1.1 audit log info

* abp 框架定义的 audit log 模型，
* 可以序列化

```c#
[Serializable]
public class AuditLogInfo : IHasExtraProperties
{
    public string ApplicationName { get; set; }
    // user info
    public Guid? UserId { get; set; }    
    public string UserName { get; set; }
    // tenant info
    public Guid? TenantId { get; set; }    
    public string TenantName { get; set; }
    // impersonator inof // not available now
    public Guid? ImpersonatorUserId { get; set; }    
    public Guid? ImpersonatorTenantId { get; set; }
    // execution inof
    public DateTime ExecutionTime { get; set; }    
    public int ExecutionDuration { get; set; }
    // remote client info
    public string ClientId { get; set; }    
    public string CorrelationId { get; set; }    
    public string ClientIpAddress { get; set; }    
    public string ClientName { get; set; }    
    public string BrowserInfo { get; set; }    
    public string HttpMethod { get; set; }    
    public int? HttpStatusCode { get; set; }    
    public string Url { get; set; }
    
    // audit log action
    public List<AuditLogActionInfo> Actions { get; set; }   
    // entity changes
    public List<EntityChangeInfo> EntityChanges { get; }
    
    public List<Exception> Exceptions { get; }    
    public ExtraPropertyDictionary ExtraProperties { get; }        
    public List<string> Comments { get; set; }
    
    public AuditLogInfo()
    {
        Actions = new List<AuditLogActionInfo>();
        EntityChanges = new List<EntityChangeInfo>();
        Exceptions = new List<Exception>();
        ExtraProperties = new ExtraPropertyDictionary();        
        Comments = new List<string>();
    }
    
    public override string ToString()
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"AUDIT LOG: [{HttpStatusCode?.ToString() ?? "---"}: {(HttpMethod ?? "-------").PadRight(7)}] {Url}");
        sb.AppendLine($"- UserName - UserId                 : {UserName} - {UserId}");
        sb.AppendLine($"- ClientIpAddress        : {ClientIpAddress}");
        sb.AppendLine($"- ExecutionDuration      : {ExecutionDuration}");
        
        if (Actions.Any())
        {
            sb.AppendLine("- Actions:");
            foreach (var action in Actions)
            {
                sb.AppendLine($"  - {action.ServiceName}.{action.MethodName} ({action.ExecutionDuration} ms.)");
                sb.AppendLine($"    {action.Parameters}");
            }
        }
        
        if (Exceptions.Any())
        {
            sb.AppendLine("- Exceptions:");
            foreach (var exception in Exceptions)
            {
                sb.AppendLine($"  - {exception.Message}");
                sb.AppendLine($"    {exception}");
            }
        }
        
        if (EntityChanges.Any())
        {
            sb.AppendLine("- Entity Changes:");
            foreach (var entityChange in EntityChanges)
            {
                sb.AppendLine($"  - [{entityChange.ChangeType}] {entityChange.EntityTypeFullName}, Id = {entityChange.EntityId}");
                foreach (var propertyChange in entityChange.PropertyChanges)
                {
                    sb.AppendLine($"    {propertyChange.PropertyName}: {propertyChange.OriginalValue} -> {propertyChange.NewValue}");
                }
            }
        }
        
        return sb.ToString();
    }
}

```

##### 2.1.2 audit log action info

* abp 框架定义的 audit log action 模型，
* 可以序列化

```c#
[Serializable]
public class AuditLogActionInfo : IHasExtraProperties
{
    public string ServiceName { get; set; }    
    public string MethodName { get; set; }    
    public string Parameters { get; set; }    
    public DateTime ExecutionTime { get; set; }    
    public int ExecutionDuration { get; set; }    
    public ExtraPropertyDictionary ExtraProperties { get; }
    
    public AuditLogActionInfo()
    {
        ExtraProperties = new ExtraPropertyDictionary();
    }
}

```

##### 2.1.3 entity change info

* abp 框架定义的 entity change 模型，记录 entity 变化，
* 可以序列化

```c#
[Serializable]
public class EntityChangeInfo : IHasExtraProperties
{
    public DateTime ChangeTime { get; set; }
    
    public EntityChangeType ChangeType { get; set; }
    
    /// <summary>
    /// TenantId of the related entity.
    /// This is not the TenantId of the audit log entry.
    /// There can be multiple tenant data changes in a single audit log entry.
    /// </summary>
    public Guid? EntityTenantId { get; set; }
    
    public string EntityId { get; set; }    
    public string EntityTypeFullName { get; set; }
    
    public List<EntityPropertyChangeInfo> PropertyChanges { get; set; }
    
    public ExtraPropertyDictionary ExtraProperties { get; }
    
    public virtual object EntityEntry { get; set; } //TODO: Try to remove since it breaks serializability
    
    public EntityChangeInfo()
    {
        ExtraProperties = new ExtraPropertyDictionary();
    }
    
    /* 合并 entity change info*/
    public virtual void Merge(EntityChangeInfo changeInfo)
    {
        // 添加或更新 property change info
        foreach (var propertyChange in changeInfo.PropertyChanges)
        {
            var existingChange = PropertyChanges.FirstOrDefault(p =>
            	p.PropertyName == propertyChange.PropertyName);
            // 如果目标中没有 property，添加
            if (existingChange == null)
            {                
                PropertyChanges.Add(propertyChange);
            }
            // 如果有 property，更新
            else
            {
                existingChange.NewValue = propertyChange.NewValue;
            }
        }
        // 添加 extra key
        foreach (var extraProperty in changeInfo.ExtraProperties)
        {
            var key = extraProperty.Key;
            if (ExtraProperties.ContainsKey(key))
            {
                key = InternalUtils.AddCounter(key);
            }
            
            ExtraProperties[key] = extraProperty.Value;
        }
    }
}

```

###### 2.1.3.1 entity change type

* entity 变化类型枚举

```c#
public enum EntityChangeType : byte
{
    Created = 0,    
    Updated = 1,    
    Deleted = 2
}
```

###### 2.1.3.2 entity property change info

* entity property 变化信息，可以序列化

```c#
[Serializable]
public class EntityPropertyChangeInfo
{    
    public const int MaxPropertyNameLength = 96;        
    public const int MaxValueLength = 512;        
    public const int MaxPropertyTypeFullNameLength = 192;    
    public virtual string NewValue { get; set; }    
    public virtual string OriginalValue { get; set; }    
    public virtual string PropertyName { get; set; }    
    public virtual string PropertyTypeFullName { get; set; }
}

```

#### 2.2 log info helper

* 用于创建 audit log info 等

##### 2.2.1 IAuditingHelper

```c#
//TODO: Move ShouldSaveAudit & IsEntityHistoryEnabled and rename to IAuditingFactory
public interface IAuditingHelper
{
    bool ShouldSaveAudit(MethodInfo methodInfo, bool defaultValue = false);    
    bool IsEntityHistoryEnabled(Type entityType, bool defaultValue = false);
    
    // create audit info
    AuditLogInfo CreateAuditLogInfo();    
    // create audit action info with args
    AuditLogActionInfo CreateAuditLogAction(
        AuditLogInfo auditLog,
        Type type,
        MethodInfo method,
        object[] arguments);
    // create audit action info wit arg dict
    AuditLogActionInfo CreateAuditLogAction(
        AuditLogInfo auditLog,
        Type type,
        MethodInfo method,
        IDictionary<string, object> arguments);
}

```

##### 2.2.2 AuditingHelper

* 创建 audit log info 的工具类，
* 自动注册，transient

###### 2.2.2.1 initailize and save audit

```c#
public class AuditingHelper : IAuditingHelper, ITransientDependency
{
    protected ILogger<AuditingHelper> Logger { get; }
    protected IAuditingStore AuditingStore { get; }
    protected ICurrentUser CurrentUser { get; }
    protected ICurrentTenant CurrentTenant { get; }
    rotected ICurrentClient CurrentClient { get; }
    
    protected IClock Clock { get; }
    protected AbpAuditingOptions Options;
    protected IAuditSerializer AuditSerializer;
    protected IServiceProvider ServiceProvider;
    protected ICorrelationIdProvider CorrelationIdProvider { get; }
    
    public AuditingHelper(
        IAuditSerializer auditSerializer,
        IOptions<AbpAuditingOptions> options,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        ICurrentClient currentClient,
        IClock clock,
        IAuditingStore auditingStore,
        ILogger<AuditingHelper> logger,
        IServiceProvider serviceProvider,
        ICorrelationIdProvider correlationIdProvider)
    {
        Options = options.Value;
        AuditSerializer = auditSerializer;
        CurrentUser = currentUser;
        CurrentTenant = currentTenant;
        CurrentClient = currentClient;
        Clock = clock;
        AuditingStore = auditingStore;
        
        Logger = logger;
        ServiceProvider = serviceProvider;
        CorrelationIdProvider = correlationIdProvider;
    }
    
    /* （method）是否存储 audit */
    public virtual bool ShouldSaveAudit(
        MethodInfo methodInfo, 
        bool defaultValue = false)
    {
        // method 为 null，忽略
        if (methodInfo == null)
        {
            return false;
        }
        // method 非 public，忽略
        if (!methodInfo.IsPublic)
        {
            return false;
        }
        // method 标记了 AuditAttribute 特性，True
        if (methodInfo.IsDefined(typeof(AuditedAttribute), true))
        {
            return true;
        }
        // mothed 标记了 DisableAttribute 特性，忽略
        if (methodInfo.IsDefined(typeof(DisableAuditingAttribute), true))
        {
            return false;
        }
        
        // 通过 method 类型判断
        var classType = methodInfo.DeclaringType;
        if (classType != null)
        {
            // 使用 拦截器判断
            var shouldAudit = AuditingInterceptorRegistrar
                .ShouldAuditTypeByDefaultOrNull(classType);
            if (shouldAudit != null)
            {
                return shouldAudit.Value;
            }
        }
        
        return defaultValue;
    }
    
    /* entity 是否开启记录（audit）*/
    public virtual bool IsEntityHistoryEnabled(
        Type entityType, 
        bool defaultValue = false)
    {
        // entity 非 public，忽略
        if (!entityType.IsPublic)
        {
            return false;
        }
        // entity 是 auditing options 中的 ignore_type，忽略
        if (Options.IgnoredTypes.Any(t => t.IsAssignableFrom(entityType)))
        {
            return false;
        }
        // entity 标记了 AuditedAttribute 特性 -> True
        if (entityType.IsDefined(typeof(AuditedAttribute), true))
        {
            return true;
        }
        // entity 中任一、非空、实例 property，
        // 标记了 AuditedAttribute 特性 -> True
        foreach (var propertyInfo in entityType
                 .GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if(propertyInfo.IsDefined(typeof(AuditedAttribute)))
            {
                return true;
            }
        }
        // entity 标记 disable auditing attribute，忽略
        if (entityType.IsDefined(typeof(DisableAuditingAttribute), true))
        {
            return false;
        }
        // entity 是 EntityHistorySelectors 集合中的，True
        if (Options.EntityHistorySelectors.Any(selector =>
        	selector.Predicate(entityType)))
        {
            return true;
        }
        
        return defaultValue;
    }
}

```

###### 2.2.2.2 create audit log info

```c#
public class AuditingHelper : IAuditingHelper, ITransientDependency
{
    public virtual AuditLogInfo CreateAuditLogInfo()
    {
        var auditInfo = new AuditLogInfo
        {
            ApplicationName = Options.ApplicationName,
            TenantId = CurrentTenant.Id,
            TenantName = CurrentTenant.Name,
            UserId = CurrentUser.Id,
            UserName = CurrentUser.UserName,
            ClientId = CurrentClient.Id,
            CorrelationId = CorrelationIdProvider.Get(),
            //TODO: mpersonation system is not available yet!
            //ImpersonatorUserId = AbpSession.ImpersonatorUserId, 
            //ImpersonatorTenantId = AbpSession.ImpersonatorTenantId,
            ExecutionTime = Clock.Now
        };
        // 执行 contributor
        ExecutePreContributors(auditInfo);
        
        return auditInfo;
    }
    
    protected virtual void ExecutePreContributors(AuditLogInfo auditLogInfo)
    {
        using (var scope = ServiceProvider.CreateScope())
        {
            var context = new AuditLogContributionContext(
                scope.ServiceProvider, auditLogInfo);
            
            // 遍历执行 auditing options 中的 contributor
            foreach (var contributor in Options.Contributors)
            {
                try
                {
                    contributor.PreContribute(context);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, LogLevel.Warning);
                }
            }
        }
    }
}

```

###### 2.2.2.3 create audit log action

```c#
// 使用 args[]，
// 实际调用了 args dictionary 参数的方法
public class AuditingHelper : IAuditingHelper, ITransientDependency
{
    /* 使用 args[] 创建 audit log action，
       实际是调用了 args dictionary 参数的创建方法 */
    public virtual AuditLogActionInfo CreateAuditLogAction(
        AuditLogInfo auditLog,
        Type type,
        MethodInfo method,
        object[] arguments)
    {
        return CreateAuditLogAction(
            auditLog, 
            type, 
            method, 
            CreateArgumentsDictionary(method, arguments));
    }
    
    protected virtual Dictionary<string, object> CreateArgumentsDictionary(
        MethodInfo method, 
        object[] arguments)
    {
        var parameters = method.GetParameters();
        var dictionary = new Dictionary<string, object>();
        
        for (var i = 0; i < parameters.Length; i++)
        {
            dictionary[parameters[i].Name] = arguments[i];
        }
        
        return dictionary;
    }
    
    /* 使用 args dictionary 创建 audit log action，
       序列化 args dictionary */
    public class AuditingHelper : IAuditingHelper, ITransientDependency
    {
        public virtual AuditLogActionInfo CreateAuditLogAction(
            AuditLogInfo auditLog,
            Type type,
            MethodInfo method,
            IDictionary<string, object> arguments)
        {
            // 创建 audit log action，
            // 序列化 arguments
            var actionInfo = new AuditLogActionInfo
            {
                ServiceName = type != null
                    ? type.FullName
                    : "",
                MethodName = method.Name,
                Parameters = SerializeConvertArguments(arguments),
                ExecutionTime = Clock.Now
            };
            
            //TODO Execute contributors
            
            return actionInfo;
        }
        
    // 序列化 arguments 的具体方法，
    // 默认或异常，返回 “{}”
    protected virtual string SerializeConvertArguments(
        IDictionary<string, object> arguments)
    {
        try
        {
            // 如果 arguments 为空，返回
            if (arguments.IsNullOrEmpty())
            {
                return "{}";
            }
            
            var dictionary = new Dictionary<string, object>();
            
            // 遍历传入的 arguments
            foreach (var argument in arguments)
            {
                // 如果 argment 是 auditing options 中的 ignore type，
                // 存储 null 入 dictionary
                if (argument.Value != null && 
                    Options.IgnoredTypes.Any(t => 
                    	t.IsInstanceOfType(argument.Value)))
                {
                    dictionary[argument.Key] = null;
                }
                // 否则，存储 argument.value 入 dictionary
                else
                {
                    dictionary[argument.Key] = argument.Value;
                }
            }
            // 序列化 arguments dictionary
            return AuditSerializer.Serialize(dictionary);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, LogLevel.Warning);
            return "{}";
        }
    }
}
    
```

##### 2.2.3 argument serializer

* auditing log action arguments 的序列化器

###### 2.2.3.1 IAuditSerializer

```c#
public interface IAuditSerializer
{
    string Serialize(object obj);
}

```

###### 2.2.3.2 JosnNetAuditSerializer

* 使用 json 作为序列化器

```c#
public class JsonNetAuditSerializer : IAuditSerializer, ITransientDependency
{
    protected AbpAuditingOptions Options;    
    public JsonNetAuditSerializer(IOptions<AbpAuditingOptions> options)
    {
        Options = options.Value;
    }
    
    public string Serialize(object obj)
    {
        return JsonConvert.SerializeObject(
            obj, 
            GetSharedJsonSerializerSettings());
    }
    
    private static readonly object SyncObj = new object();
    private static JsonSerializerSettings _sharedJsonSerializerSettings;
    
    // 获取 json serialize settings，
    // 如果没有，创建且 _share...Settings = new...Settings
    private JsonSerializerSettings GetSharedJsonSerializerSettings()
    {
        if (_sharedJsonSerializerSettings == null)
        {
            lock (SyncObj)
            {
                if (_sharedJsonSerializerSettings == null)
                {
                    _sharedJsonSerializerSettings = new JsonSerializerSettings
                    {
                        ContractResolver = new AuditingContractResolver(
                            Options.IgnoredTypes)
                    };
                }
            }
        }
        
        return _sharedJsonSerializerSettings;
    }
}

```

###### 2.2.3.3 AuditingContractResolver

* json settings resolver

```c#
public class AuditingContractResolver : CamelCasePropertyNamesContractResolver
{
    private readonly List<Type> _ignoredTypes;    
    public AuditingContractResolver(List<Type> ignoredTypes)
    {
        _ignoredTypes = ignoredTypes;
    }
    // 创建 JsonProperty
    protected override JsonProperty CreateProperty(
        MemberInfo member, 
        MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);
        
        // 如果是 ignore type 包含的 type，忽略
        if (_ignoredTypes.Any(ignoredType => 
        	ignoredType.GetTypeInfo()
            	.IsAssignableFrom(property.PropertyType)))
        {
            property.ShouldSerialize = instance => false;
            return property;
        }
        // 如果 member declarType 标记了 disable auditing 特性，
        // 或者 JsonIgnore texing，忽略
        if (member.DeclaringType != null && 
            (member.DeclaringType
            	.IsDefined(typeof(DisableAuditingAttribute)) ||
             member.DeclaringType
             	.IsDefined(typeof(JsonIgnoreAttribute))))
        {
            property.ShouldSerialize = instance => false;
            return property;
        }
        // 如果 member 标记了 disable auditing 特性，  
        // 或者 JsonIgnore texing，忽略
        if (member.IsDefined(typeof(DisableAuditingAttribute)) || 
            member.IsDefined(typeof(JsonIgnoreAttribute)))
        {
            property.ShouldSerialize = instance => false;
        }        
        return property;
    }
}

```

##### 2.2.3 auditing options

```c#
public class AbpAuditingOptions
{
    //TODO: Consider to add an option to disable auditing for application service methods?    
    
    public bool HideErrors { get; set; }        
    public bool IsEnabled { get; set; }        
    public string ApplicationName { get; set; }        
    public bool IsEnabledForAnonymousUsers { get; set; }        
    public bool AlwaysLogOnException { get; set; }
    
    public List<AuditLogContributor> Contributors { get; }    
    public List<Type> IgnoredTypes { get; }    
    public IEntityHistorySelectorList EntityHistorySelectors { get; }
    
    //TODO: Move this to asp.net core layer or convert it to a more dynamic strategy?    
    public bool IsEnabledForGetRequests { get; set; }
    
    public AbpAuditingOptions()
    {
        IsEnabled = true;
        IsEnabledForAnonymousUsers = true;
        HideErrors = true;
        AlwaysLogOnException = true;
        
        Contributors = new List<AuditLogContributor>();
        
        IgnoredTypes = new List<Type>
        {
            typeof(Stream),
            typeof(Expression)
        };
        
        EntityHistorySelectors = new EntityHistorySelectorList();
    }
}

```

###### 2.2.3.1 audit log contributor

* 生成 audit log 的底层服务

```c#
public abstract class AuditLogContributor
{
    public virtual void PreContribute(AuditLogContributionContext context)
    {        
    }
    
    public virtual void PostContribute(AuditLogContributionContext context)
    {        
    }
}

```

###### 2.2.3.2 audit log contributor context

* contributor context，包含需要生成（修改）的 audit info

```c#
public class AuditLogContributionContext : IServiceProviderAccessor
{
    public IServiceProvider ServiceProvider { get; }    
    public AuditLogInfo AuditInfo { get; }
    
    public AuditLogContributionContext(
        IServiceProvider serviceProvider, 
        AuditLogInfo auditInfo)
    {
        ServiceProvider = serviceProvider;
        AuditInfo = auditInfo;
    }
}

```

###### 2.2.3.3 entity history selector list

* 需要记录 change info 的 entity 类型集合

```c#
internal class EntityHistorySelectorList 
    : List<NamedTypeSelector>, IEntityHistorySelectorList
{
    public bool RemoveByName(string name)
    {
        return RemoveAll(s => s.Name == name) > 0;
    }
}

```

#### 2.3 audit manager

* 用来管理 audit log scope 和序列化

##### 2.3.1 IAuditingManager

```c#
public interface IAuditingManager
{
    [CanBeNull]
    IAuditLogScope Current { get; }    
    IAuditLogSaveHandle BeginScope();
}

```

##### 2.3.2 auditing manager

###### 2.3.2.1 initialize

```c#
public class AuditingManager 
    : IAuditingManager, ITransientDependency
{
    private const string AmbientContextKey = "Volo.Abp.Auditing.IAuditLogScope";
        
    protected IServiceProvider ServiceProvider { get; }
    protected AbpAuditingOptions Options { get; }
    protected ILogger<AuditingManager> Logger { get; set; }
    // 注入服务   
    private readonly IAmbientScopeProvider<IAuditLogScope> _ambientScopeProvider;
    private readonly IAuditingHelper _auditingHelper;
    private readonly IAuditingStore _auditingStore;
    
    public AuditingManager(
        IAmbientScopeProvider<IAuditLogScope> ambientScopeProvider,
        IAuditingHelper auditingHelper,
        IAuditingStore auditingStore,
        IServiceProvider serviceProvider,
        IOptions<AbpAuditingOptions> options)
    {
        ServiceProvider = serviceProvider;
        Options = options.Value;
        // logger 属性注入
        Logger = NullLogger<AuditingManager>.Instance;
        
        _ambientScopeProvider = ambientScopeProvider;
        _auditingHelper = auditingHelper;
        _auditingStore = auditingStore;
    }                       
}

```

###### 2.3.2.2 get current audit log scope

* 获取当前 audit scope

```c#
public class AuditingManager 
    : IAuditingManager, ITransientDependency
{
    public IAuditLogScope Current => 
        _ambientScopeProvider.GetValue(AmbientContextKey);    
}

```

###### 2.3.2.3 begin scope

* 获取 audit log save handle

```c#
public class AuditingManager 
    : IAuditingManager, ITransientDependency
{
    public IAuditLogSaveHandle BeginScope()
    {
        var ambientScope = _ambientScopeProvider.BeginScope(
            AmbientContextKey,
            new AuditLogScope(_auditingHelper.CreateAuditLogInfo()));
        
        Debug.Assert(Current != null, "Current != null");
        
        return new DisposableSaveHandle(
            this, 
            ambientScope, 
            Current.Log, 
            Stopwatch.StartNew());
    }
}

```

###### 2.3.2.4 save

```c#
public class AuditingManager 
    : IAuditingManager, ITransientDependency
{
    protected virtual async Task SaveAsync(DisposableSaveHandle saveHandle)
    {
        BeforeSave(saveHandle);        
        await _auditingStore.SaveAsync(saveHandle.AuditLog);
    }
        
    protected virtual void BeforeSave(DisposableSaveHandle saveHandle)
    {
        saveHandle.StopWatch.Stop();
        saveHandle.AuditLog.ExecutionDuration = Convert.ToInt32(
            saveHandle.StopWatch.Elapsed.TotalMilliseconds);
        // 执行 post contributor
        ExecutePostContributors(saveHandle.AuditLog);
        // 合并 entity changes
        MergeEntityChanges(saveHandle.AuditLog);
    }    
    
    /* 执行 post contribute*/
    protected virtual void ExecutePostContributors(AuditLogInfo auditLogInfo)
    {
        using (var scope = ServiceProvider.CreateScope())
        {
            var context = new AuditLogContributionContext(
                scope.ServiceProvider, auditLogInfo);
            // 遍历 audit options 中的 contributors
            foreach (var contributor in Options.Contributors)
            {
                try
                {
                    // 执行 contributor.postContribute
                    contributor.PostContribute(context);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, LogLevel.Warning);
                }
            }
        }
    }
    /* 合并 entity changes */    
    protected virtual void MergeEntityChanges(AuditLogInfo auditLog)
    {
        // 将 auditLog.EntityChanges 按 entityTypeFullName, entityId 分组
        var changeGroups = auditLog.EntityChanges
            .Where(e => e.ChangeType == EntityChangeType.Updated)
            .GroupBy(e => new { e.EntityTypeFullName, e.EntityId })
            .ToList();
        
        // 遍历每个分组
        foreach (var changeGroup in changeGroups)
        {
            // 如果分组内 entityChanges 仅有1个或没有，忽略
            if (changeGroup.Count() <= 1)
            {
                continue;
            }
            // 否则，合并 entity changes
            var firstEntityChange = changeGroup.First();            
            foreach (var entityChangeInfo in changeGroup)
            {
                if (entityChangeInfo == firstEntityChange)
                {
                    continue;
                }
                
                firstEntityChange.Merge(entityChangeInfo);                
                auditLog.EntitChanges.Remove(entityChangeInfo);
            }
        }
    }
}

```

##### 2.3.3 audit log save handle

* 真正执行 audit log 存储的底层服务

###### 2.3.3.1 IAuditLogSaveHandle

```c#
public interface IAuditLogSaveHandle : IDisposable
{
    Task SaveAsync();
}
```

###### 2.3.3.2 DisposableSaveHandle

* 使用 `IAuditingManager.SaveAsync() `方法

```c#
protected class DisposableSaveHandle : IAuditLogSaveHandle
{
    public AuditLogInfo AuditLog { get; }
    public Stopwatch StopWatch { get; }
    
    private readonly AuditingManager _auditingManager;
    private readonly IDisposable _scope;
    
    public DisposableSaveHandle(
        AuditingManager auditingManager,
        IDisposable scope,
        AuditLogInfo auditLog,
        Stopwatch stopWatch)
    {
        _auditingManager = auditingManager;
        _scope = scope;
        AuditLog = auditLog;
        StopWatch = stopWatch;
    }
    
    public async Task SaveAsync()
    {
        await _auditingManager.SaveAsync(this);
    }
    
    public void Dispose()
    {
        _scope.Dispose();
    }
}

```

##### 2.3.4 audit store

* audit log 存储层
* 默认是`ILogger`，在 asp.net core 中有中间件

###### 2.3.4.1 IAuditStore

```c#
public interface IAuditingStore
{
    Task SaveAsync(AuditLogInfo auditInfo);
}

```

###### 2.3.4.2 SimpleLogAuditingStore

```c#
[Dependency(TryRegister = true)]
public class SimpleLogAuditingStore : IAuditingStore, ISingletonDependency
{
    public ILogger<SimpleLogAuditingStore> Logger { get; set; }    
    public SimpleLogAuditingStore()
    {
        // 属性注入 ILogger
        Logger = NullLogger<SimpleLogAuditingStore>.Instance;
    }
    
    public Task SaveAsync(AuditLogInfo auditInfo)
    {
        Logger.LogInformation(auditInfo.ToString());
        return Task.FromResult(0);
    }
}

```

#### 2.4 auto audit log

* abp 框架实现了自动审计日志（auditing log）

##### 2.4.1 模块

```c#
[DependsOn(typeof(AbpDataModule),
           typeof(AbpJsonModule),
           typeof(AbpTimingModule),
           typeof(AbpSecurityModule),
           typeof(AbpThreadingModule),
           typeof(AbpMultiTenancyModule))]
public class AbpAuditingModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // 注册 auditing interceptor
        context.Services.OnRegistred(AuditingInterceptorRegistrar.RegisterIfNeeded);
    }
}

```

##### 2.4.2 audit interceptor

###### 2.4.2.1 AuditingInterceptor

```c#
public class AuditingInterceptor : AbpInterceptor, ITransientDependency
{
    // 注入 audit_helper, audit_manager
    private readonly IAuditingHelper _auditingHelper;
    private readonly IAuditingManager _auditingManager;    
    public AuditingInterceptor(
        IAuditingHelper auditingHelper, 
        IAuditingManager auditingManager)
    {
        _auditingHelper = auditingHelper;
        _auditingManager = auditingManager;
    }
    
    public async override Task InterceptAsync(IAbpMethodInvocation invocation)
    {
        // 判断 method 是否需要拦截，同时创建 auditLogInfo 和 auditLogAction，
        // 如果 method 不需要拦截（auditing），直接执行 method
        if (!ShouldIntercept(invocation, out var auditLog, out var auditLogAction))
        {            
            await invocation.ProceedAsync();
            return;
        }
        // 开始计时
        var stopwatch = Stopwatch.StartNew();
        // 运行方法
        try
        {
            await invocation.ProceedAsync();
        }
        catch (Exception ex)
        {
            auditLog.Exceptions.Add(ex);
            throw;
        }
        finally
        {
            // 停止计时，记录 method 运行时间
            stopwatch.Stop();            
            auditLogAction.ExecutionDuration = Convert.ToInt32(
                stopwatch.Elapsed.TotalMilliseconds);
            // 向 auditLogInfo 中注入 auditLogAction
            auditLog.Actions.Add(auditLogAction);
        }
    }
    
    protected virtual bool ShouldIntercept(
        IAbpMethodInvocation invocation,
        out AuditLogInfo auditLog,
        out AuditLogActionInfo auditLogAction)
    {
        // auditLog & auditLogAction 为 null
        auditLog = null;
        auditLogAction = null;
        
        // 如果是 abp cross concern for auditing，忽略
        if (AbpCrossCuttingConcerns.IsApplied(
            invocation.TargetObject, 
            AbpCrossCuttingConcerns.Auditing))
        {
            return false;
        }
        // 如果 current audit log scope 为 null，忽略
        var auditLogScope = _auditingManager.Current;
        if (auditLogScope == null)
        {
            return false;
        }
        // 如果 auditHelper.ShouldSaveAudit == false，忽略
        if (!_auditingHelper.ShouldSaveAudit(invocation.Method))
        {
            return false;
        }
        
        // override auditLog & auditLogAction
        auditLog = auditLogScope.Log;
        auditLogAction = _auditingHelper.CreateAuditLogAction(
            auditLog,
            invocation.TargetObject.GetType(),
            invocation.Method,
            invocation.Arguments);
        
        return true;
    }
}

```

###### 2.4.2.2 AuditingInterceptorRegistrar

```c#
public static class AuditingInterceptorRegistrar
{
    public static void RegisterIfNeeded(IOnServiceRegistredContext context)
    {
        if (ShouldIntercept(context.ImplementationType))
        {
            context.Interceptors.TryAdd<AuditingInterceptor>();
        }
    }
    
    private static bool ShouldIntercept(Type type)
    {
        // 如果是 dynamic proxy ignore type，忽略
        if (DynamicProxyIgnoreTypes.Contains(type))
        {
            return false;
        }
        // 如果 auditTypeDefaultOrNull == true，True
        if (ShouldAuditTypeByDefaultOrNull(type) == true)
        {
            return true;
        }
        // 如果 type 的 method 标记了 AuditAttribute 特性，True
        if (type.GetMethods().Any(m => m.IsDefined(typeof(AuditedAttribute), true)))
        {
            return true;
        }
        
        return false;
    }
    
    //TODO: Move to a better place
    public static bool? ShouldAuditTypeByDefaultOrNull(Type type)
    {
        //TODO: In an inheritance chain, it would be better to check the attributes on the top class first.
        // 如果标记了 AuditedAttribute，True
        if (type.IsDefined(typeof(AuditedAttribute), true))
        {
            return true;
        }
        // 如果标记了 disableAuditingAttribute，忽略
        if (type.IsDefined(typeof(DisableAuditingAttribute), true))
        {
            return false;
        }
        // 如果实现了 IAuditingEnable，True
        if (typeof(IAuditingEnabled).IsAssignableFrom(type))
        {
            return true;
        }
        
        return null;
    }
}

```

##### 2.4.3 使用 interceptor

* asp.net core mvc 中使用了 auto auditing log 中间件

#### 2.5 auto entity auditing

##### 2.5.1 audited property

```c#
// has creation time
public interface IHasCreationTime
{        
    DateTime CreationTime { get; }
}

// has modification time
public interface IHasModificationTime
{        
    DateTime? LastModificationTime { get; set; }
}

// has deletion time
public interface IHasDeletionTime : ISoftDelete
{        
    DateTime? DeletionTime { get; set; }
}

// may have creator
public interface IMayHaveCreator<TCreator>
{        
    [CanBeNull]
    TCreator Creator { get; }
}
public interface IMayHaveCreator
{        
    Guid? CreatorId { get; }
}

// must have creator
public interface IMustHaveCreator
{        
    Guid CreatorId { get; }
}
public interface IMustHaveCreator<TCreator> 
    : IMustHaveCreator
{        
    [NotNull]
    TCreator Creator { get; }
}

```

##### 2.5.2 audited object

###### 2.5.2.1 ICreationAuditedObject  

```c#
public interface ICreationAuditedObject 
    : IHasCreationTime, IMayHaveCreator
{    
}
    
public interface ICreationAuditedObject<TCreator> 
    : ICreationAuditedObject, IMayHaveCreator<TCreator>
{    
}

```

###### 2.5.2.2 IModificationAuditedObject

```c#
public interface IModificationAuditedObject 
    : IHasModificationTime
{   
    Guid? LastModifierId { get; set; }
}


public interface IModificationAuditedObject<TUser> 
    : IModificationAuditedObject
{    
    TUser LastModifier { get; set; }
}

```

###### 2.5.2.3 IAuditedObject

```c#
public interface IAuditedObject 
    : ICreationAuditedObject, IModificationAuditedObject
{    
}
    
public interface IAuditedObject<TUser> 
    : IAuditedObject, ICreationAuditedObject<TUser>, 
	  IModificationAuditedObject<TUser>
{    
}

```

###### 2.5.2.4 IDeletionAuditedObject

```c#
public interface IDeletionAuditedObject 
    : IHasDeletionTime
{    
    Guid? DeleterId { get; set; }
}


public interface IDeletionAuditedObject<TUser> 
    : IDeletionAuditedObject
{    
    TUser Deleter { get; set; }
}

```

###### 2.5.2.5 IFullAuditedObject

```c#
public interface IFullAuditedObject 
    : IAuditedObject, IDeletionAuditedObject
{    
}
    
public interface IFullAuditedObject<TUser> 
    : IAuditedObject<TUser>, IFullAuditedObject, 
	  IDeletionAuditedObject<TUser>
{    
}

```

##### 2.5.3 audit property setter

###### 2.5.3.1 IAuditPropertySetter

```c#
public interface IAuditPropertySetter
{
    void SetCreationProperties(object targetObject);    
    void SetModificationProperties(object targetObject);
    void SetDeletionProperties(object targetObject);
}

```

###### 2.5.3.2 AuditPropertySetter

```c#
public class AuditPropertySetter : IAuditPropertySetter, ITransientDependency
{
    protected ICurrentUser CurrentUser { get; }
    protected ICurrentTenant CurrentTenant { get; }
    protected IClock Clock { get; }
    
    public AuditPropertySetter(
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        CurrentUser = currentUser;
        CurrentTenant = currentTenant;
        Clock = clock;
    }
    // creation
    public void SetCreationProperties(object targetObject)
    {
        SetCreationTime(targetObject);
        SetCreatorId(targetObject);
    }
    // modification
    public void SetModificationProperties(object targetObject)
    {
        SetLastModificationTime(targetObject);
        SetLastModifierId(targetObject);
    }
    // deletion
    public void SetDeletionProperties(object targetObject)
    {
        SetDeletionTime(targetObject);
        SetDeleterId(targetObject);
    }
    
    /* set creation properties */
    // creation time
    private void SetCreationTime(object targetObject)
    {
        if (!(targetObject is IHasCreationTime objectWithCreationTime))
        {
            return;
        }        
        if (objectWithCreationTime.CreationTime == default)
        {
            ObjectHelper.TrySetProperty(
                objectWithCreationTime, 
                x => x.CreationTime, 
                () => Clock.Now);
        }
    }
    // creation id and creator
    private void SetCreatorId(object targetObject)
    {
        // current user == null, 忽略
        if (!CurrentUser.Id.HasValue)
        {
            return;
        }
        // 实现了 IMultiTenant 接口
        // current tenant ！= current user.Tenant，忽略
        if (targetObject is IMultiTenant multiTenantEntity)
        {
            if (multiTenantEntity.TenantId != CurrentUser.TenantId)
            {
                return;
            }
        }
        
        /* TODO: The code below is from old ABP, not implemented yet
        if (tenantId.HasValue && MultiTenancyHelper.IsHostEntity(entity))
        {
        	//Tenant user created a host entity
        	return;
        }
        */
        
		// 如果实现了 IMayHaveCreatorObject 接口
        if (targetObject is IMayHaveCreator mayHaveCreatorObject)
        {
            if (mayHaveCreatorObject.CreatorId.HasValue && 
                mayHaveCreatorObject.CreatorId.Value != default)
            {
                return;
            }
            
            ObjectHelper.TrySetProperty(
                mayHaveCreatorObject, 
                x => x.CreatorId, 
                () => CurrentUser.Id);
        }
        // 如果实现了 IMustHaveCreatorObject 接口
        else if (targetObject is IMustHaveCreator mustHaveCreatorObject)
        {
            if (mustHaveCreatorObject.CreatorId != default)
            {
                return;
            }
            
            ObjectHelper.TrySetProperty(
                mustHaveCreatorObject, 
                x => x.CreatorId, 
                () => CurrentUser.Id.Value);
        }
    }
    
    /* set modification properties，
       property value 可以为 null，所以直接赋值 */
    // modify time
    private void SetLastModificationTime(object targetObject)
    {
        if (targetObject is IHasModificationTime objectWithModificationTime)
        {
            objectWithModificationTime.LastModificationTime = Clock.Now;
        }
    }
    // modify id
    private void SetLastModifierId(object targetObject)
    {
        // 没有实现 IModificationAuditedObject 接口，忽略
        if (!(targetObject is IModificationAuditedObject modificationAuditedObject))
        {
            return;
        }
        // current user == null, modify id = null
        if (!CurrentUser.Id.HasValue)
        {
            modificationAuditedObject.LastModifierId = null;
            return;
        }
        // 实现了 IMultiTenant 接口，
        // current tenant ！= current user.Tenant，modify id = null
        if (modificationAuditedObject is IMultiTenant multiTenantEntity)
        {
            if (multiTenantEntity.TenantId != CurrentUser.TenantId)
            {
                modificationAuditedObject.LastModifierId = null;
                return;
            }
        }
        
        /* TODO: The code below is from old ABP, not implemented yet
        if (tenantId.HasValue && MultiTenancyHelper.IsHostEntity(entity))
        {
            //Tenant user modified a host entity
            modificationAuditedObject.LastModifierId = null;
            return;
        }
        */
        
        modificationAuditedObject.LastModifierId = CurrentUser.Id;
    }
    
    /* set deleting properties,
        property value 可以为 null，所以直接赋值 */
    // deleted time
    private void SetDeletionTime(object targetObject)
    {
        if (targetObject is IHasDeletionTime objectWithDeletionTime)
        {
            if (objectWithDeletionTime.DeletionTime == null)
            {
                objectWithDeletionTime.DeletionTime = Clock.Now;
            }
        }
    }
    // deleted id
    private void SetDeleterId(object targetObject)
    {
        // 如果没有实现IDeletionAuditedObject 接口，忽略
        if (!(targetObject is IDeletionAuditedObject deletionAuditedObject))
        {
            return;
        }
        // 已经标记 deleted id，deleted id = null
        if (deletionAuditedObject.DeleterId != null)
        {
            return;
        }
        // current user == null, deleted id = null
        if (!CurrentUser.Id.HasValue)
        {
            deletionAuditedObject.DeleterId = null;
            return;
        }
        // 实现了 IMultiTenant 接口，
        // current tenant ！= current user.Tenant，deleted id = null
        if (deletionAuditedObject is IMultiTenant multiTenantEntity)
        {
            if (multiTenantEntity.TenantId != CurrentUser.TenantId)
            {
                deletionAuditedObject.DeleterId = null;
                return;
            }
        }
        
        deletionAuditedObject.DeleterId = CurrentUser.Id;
    }
}

```

##### 2.5.4 使用 auto entity auditing

* repository 执行 saveAsync 方法是调用 auto entity auditing

### 3. practice

* abp module中有 auditing log，实现了 IAuditStore 的 ef core， mongodb 版本