## about repository register

相关程序集：

* Volo.Abp.Ddd.Domain

----

### 1. about

* abp 实现了 repository implementation 的注册

#### 1.1 how designed

* 扩展 service collection 的方法，
  * 注册`<TEntity, TRepo<>ForEntity>`或者`<TEntity, TRepo<,>ForEntity`
* commonDbContextRegisterOptions 是自动注册的 options
  * dbContext type
  * default repo implementation type
  * 自动注册 flag
* repository register base 抽象基类实现自动注册
  * 注册 options 中的 custom repos
  * 注册 default repo 
    * IAggregateRoot
    * 包含在 custom repos types 中（调用过 `options.AddRepository()`方法）
    * 标记了RegisterDefaultRepositories 标志位（调用过`options.AddRepositories()`方法）

### 2. details

#### 2.1 repo impl register

* 通过 service collection 注册 repository 的实现
* 注册`<TEntity, TRepo<>ForEntity>`或者`<TEntity, TRepo<,>ForEntity`

```c#
public static class ServiceCollectionRepositoryExtensions
{
    public static IServiceCollection AddDefaultRepository(
        this IServiceCollection services,
        Type entityType, 
        Type repositoryImplementationType)
    {
        /* 如果 repoImpl 是 Repo<>（复合主键），
           注册 repoImpl 并暴露 IRepo<> 接口*/
        //IReadOnlyBasicRepository<TEntity>
        var readOnlyBasicRepositoryInterface = 
            typeof(IReadOnlyBasicRepository<>)
            	.MakeGenericType(entityType);
        
        if (readOnlyBasicRepositoryInterface
            .IsAssignableFrom(repositoryImplementationType))
        {
            services.TryAddTransient(
                readOnlyBasicRepositoryInterface, 
                repositoryImplementationType);
            
            //IReadOnlyRepository<TEntity>
            var readOnlyRepositoryInterface = 
                typeof(IReadOnlyRepository<>)
                	.MakeGenericType(entityType);
            if (readOnlyRepositoryInterface
                .IsAssignableFrom(repositoryImplementationType))
            {
                services.TryAddTransient(
                    readOnlyRepositoryInterface, 
                    repositoryImplementationType);
            }
            
            //IBasicRepository<TEntity>
            var basicRepositoryInterface = 
                typeof(IBasicRepository<>)
                	.MakeGenericType(entityType);
            if (basicRepositoryInterface
                .IsAssignableFrom(repositoryImplementationType))
            {
                services.TryAddTransient(
                    basicRepositoryInterface, 
                    repositoryImplementationType);
                
                //IRepository<TEntity>
                var repositoryInterface = 
                    typeof(IRepository<>)
                    	.MakeGenericType(entityType);
                if (repositoryInterface
                    .IsAssignableFrom(repositoryImplementationType))
                {
                    services.TryAddTransient(
                        repositoryInterface, 
                        repositoryImplementationType);
                }
            }
        }
        
        /* 如果 repoImpl 是 Repo<,>（单主键），
           注册 repoImpl 并暴露 IRepo<,> 接口*/
        var primaryKeyType = EntityHelper.FindPrimaryKeyType(entityType);
        if (primaryKeyType != null)
        {
            //IReadOnlyBasicRepository<TEntity, TKey>
            var readOnlyBasicRepositoryInterfaceWithPk = 
                typeof(IReadOnlyBasicRepository<,>)
                	.MakeGenericType(entityType, primaryKeyType);
            if (readOnlyBasicRepositoryInterfaceWithPk
                .IsAssignableFrom(repositoryImplementationType))
            {                   
                services.TryAddTransient(
                    readOnlyBasicRepositoryInterfaceWithPk, 
                    repositoryImplementationType);
                
                //IReadOnlyRepository<TEntity, TKey>
                var readOnlyRepositoryInterfaceWithPk = 
                    typeof(IReadOnlyRepository<,>)
                    	.MakeGenericType(entityType, primaryKeyType);                   
                if (readOnlyRepositoryInterfaceWithPk
                    .IsAssignableFrom(repositoryImplementationType))
                {
                    services.TryAddTransient(
                        readOnlyRepositoryInterfaceWithPk, 
                        repositoryImplementationType);
                }
                
                //IBasicRepository<TEntity, TKey>
                var basicRepositoryInterfaceWithPk = 
                    typeof(IBasicRepository<,>)
                    	.MakeGenericType(entityType, primaryKeyType);
                if (basicRepositoryInterfaceWithPk
                    .IsAssignableFrom(repositoryImplementationType))
                {
                    services.TryAddTransient(
                        basicRepositoryInterfaceWithPk, 
                        repositoryImplementationType);
                    
                    //IRepository<TEntity, TKey>
                    var repositoryInterfaceWithPk = 
                        typeof(IRepository<,>)
                        	.MakeGenericType(entityType, primaryKeyType);
                    if (repositoryInterfaceWithPk
                        .IsAssignableFrom(repositoryImplementationType))
                    {
                        services.TryAddTransient(
                            repositoryInterfaceWithPk, 
                            repositoryImplementationType);
                    }
                }
            }
        }
        
        return services;
    }
}

```

#### 2.2 auto repo(impl) register

##### 2.2.1 registration options builder

```c#
public interface IAbpCommonDbContextRegistrationOptionsBuilder
{
    IServiceCollection Services { get; }
        
    // 注册 default repos 
    IAbpCommonDbContextRegistrationOptionsBuilder 
        AddDefaultRepositories(
        	bool includeAllEntities = false);
        
    // 为 TDbContext 注册 default repos 
    IAbpCommonDbContextRegistrationOptionsBuilder 
        AddDefaultRepositories<TDefaultRepositoryDbContext>(
        	bool includeAllEntities = false);        
    IAbpCommonDbContextRegistrationOptionsBuilder 
        AddDefaultRepositories(
        	Type defaultRepositoryDbContextType, 
        	bool includeAllEntities = false);
        
    // 为 TEntity 注册 TRepo（注册 custom repo）
    IAbpCommonDbContextRegistrationOptionsBuilder 
        AddRepository<TEntity, TRepository>();
        
    // 将传入的 repoImpl 设置为 default repo
    IAbpCommonDbContextRegistrationOptionsBuilder 
        SetDefaultRepositoryClasses(
        	[NotNull] Type repositoryImplementationType, 
        	[NotNull] Type repositoryImplementationTypeWithoutKey);
        
    // 用 default repo 替代 other dbContext
    IAbpCommonDbContextRegistrationOptionsBuilder 
        ReplaceDbContext<TOtherDbContext>();
    IAbpCommonDbContextRegistrationOptionsBuilder 
        ReplaceDbContext(Type otherDbContextType);
}

```

##### 2.2.2 common dbContext regist options

```c#
public abstract class AbpCommonDbContextRegistrationOptions 
    : IAbpCommonDbContextRegistrationOptionsBuilder
{
    public IServiceCollection Services { get; }
    
    // dbContext type
    public Type OriginalDbContextType { get; }          
    public List<Type> ReplacedDbContextTypes { get; }
    
    // repository type (service type),    
    public Type DefaultRepositoryDbContextType { get; protected set; }    
    // repository implementation type
    public Type DefaultRepositoryImplementationType { get; private set; }    
    public Type DefaultRepositoryImplementationTypeWithoutKey { get; private set; }
    
    public bool RegisterDefaultRepositories { get; private set; }    
    public bool IncludeAllEntitiesForDefaultRepositories { get; private set; }
    
    // custom repo(type) 容器
    public Dictionary<Type, Type> CustomRepositories { get; }
    
    // 是否指定了 default repo type，
    // def repo impl 和 def repo impl wo key 都不为 null，True
    public bool SpecifiedDefaultRepositoryTypes => 
        DefaultRepositoryImplementationType != null &&
        DefaultRepositoryImplementationTypeWithoutKey != null;
    
    protected AbpCommonDbContextRegistrationOptions(
        Type originalDbContextType, 
        IServiceCollection services)
    {
        Services = services;
        OriginalDbContextType = originalDbContextType;        
        DefaultRepositoryDbContextType = originalDbContextType;
        
        CustomRepositories = new Dictionary<Type, Type>();
        ReplacedDbContextTypes = new List<Type>();
    }
    
    
    
    
    
    
    
    
    
    
    
    
}

```

###### 2.2.2.1 replace dbContext type

* 将派生 dbContext 添加到`ReplaceDbContextTypes`中

```c#
public abstract class AbpCommonDbContextRegistrationOptions 
    : IAbpCommonDbContextRegistrationOptionsBuilder
{
    // replace<TDbContext>, ->
    public IAbpCommonDbContextRegistrationOptionsBuilder 
        ReplaceDbContext<TOtherDbContext>()
    {
        return ReplaceDbContext(typeof(TOtherDbContext));
    }    
    // replace(TDbContext)    
    public IAbpCommonDbContextRegistrationOptionsBuilder 
        ReplaceDbContext(Type otherDbContextType)
    {
        // 如果 TDbContext 不是 OriginalDbContextType 的派生类，
        // 抛出异常
        if (!otherDbContextType
            	.IsAssignableFrom(OriginalDbContextType))
        {
            throw new AbpException($"{OriginalDbContextType.AssemblyQualifiedName} should inherit/implement {otherDbContextType.AssemblyQualifiedName}!");
        }
        // 向 replaceTypes 中添加 dbContextType
        ReplacedDbContextTypes.Add(otherDbContextType);
        
        return this;
    }
}

```

###### 2.2.2.1 set default repo type

* 将传入的 repo impl type 和 repo impl type wo key 设置为 default repo type

```c#
public abstract class AbpCommonDbContextRegistrationOptions 
    : IAbpCommonDbContextRegistrationOptionsBuilder
{
    public IAbpCommonDbContextRegistrationOptionsBuilder 
        SetDefaultRepositoryClasses(
        	Type repositoryImplementationType,
        	Type repositoryImplementationTypeWithoutKey)
    {
        Check.NotNull(
            repositoryImplementationType, 
            nameof(repositoryImplementationType));
        Check.NotNull(
            repositoryImplementationTypeWithoutKey, 
            ameof(repositoryImplementationTypeWithoutKey));
        
        DefaultRepositoryImplementationType = 
            repositoryImplementationType;
        DefaultRepositoryImplementationTypeWithoutKey = 
            repositoryImplementationTypeWithoutKey;
        
        return this;
    }        
}

```

###### 2.2.2.3 add default repositories (flag)

* 添加（设置）default repo register 标志为 True

```c#
public abstract class AbpCommonDbContextRegistrationOptions 
    : IAbpCommonDbContextRegistrationOptionsBuilder
{
    // 为 TDbContext 创建 default repos（标记）， -> 
    public IAbpCommonDbContextRegistrationOptionsBuilder 
        AddDefaultRepositories
        	<TDefaultRepositoryDbContext>(bool includeAllEntities = false)
    {
        return AddDefaultRepositories(
            typeof(TDefaultRepositoryDbContext), 
            includeAllEntities);
    }   
        
    // 为 TDbContext 创建 default repos（标记）  
    public IAbpCommonDbContextRegistrationOptionsBuilder 
        AddDefaultRepositories(
        	Type defaultRepositoryDbContextType, 
        	bool includeAllEntities = false)
    {
        if (!defaultRepositoryDbContextType
            	.IsAssignableFrom(OriginalDbContextType))
        {
            throw new AbpException($"{OriginalDbContextType.AssemblyQualifiedName} should inherit/implement {defaultRepositoryDbContextType.AssemblyQualifiedName}!");
        }
        // 将传入的 repo type 设置为 default repo type
        DefaultRepositoryDbContextType = defaultRepositoryDbContextType;
        // 添加（设置）register default repo 标志，->
        return AddDefaultRepositories(includeAllEntities);
    }    
        
    // 设置 default repo register 标志            
    public IAbpCommonDbContextRegistrationOptionsBuilder 
        AddDefaultRepositories(bool includeAllEntities = false)
    {
        RegisterDefaultRepositories = true;
        IncludeAllEntitiesForDefaultRepositories = includeAllEntities;
        
        return this;
    }
}    
    
```

###### 2.2.2.4 add (custom) repository

```c#
public abstract class AbpCommonDbContextRegistrationOptions 
    : IAbpCommonDbContextRegistrationOptionsBuilder
{
    // 添加 <TEntity, TRepo>，-> add custom repo
    public IAbpCommonDbContextRegistrationOptionsBuilder 
        AddRepository<TEntity, TRepository>()
    {
        AddCustomRepository(
            typeof(TEntity), 
            typeof(TRepository));
        
        return this;
    }
        
    // 添加 custom repo <TEntity, TRepo>
    private void AddCustomRepository(Type entityType, Type repositoryType)
    {
        // 如果 TEntity 没有实现 IEntity 接口，抛出异常
        if (!typeof(IEntity)
            	.IsAssignableFrom(entityType))
        {
            throw new AbpException($"Given entityType is not an entity: {entityType.AssemblyQualifiedName}. It must implement {typeof(IEntity<>).AssemblyQualifiedName}.");
        }
        // 如果 TRepo 没有实现 IRepository 接口，抛出异常
        if (!typeof(IRepository)
            .IsAssignableFrom(repositoryType))
        {
            throw new AbpException($"Given repositoryType is not a repository: {entityType.AssemblyQualifiedName}. It must implement {typeof(IBasicRepository<>).AssemblyQualifiedName}.");
        }
        // 向 custom repos（dictionary）中添加 <TEntity,TRepo>
        CustomRepositories[entityType] = repositoryType;
    }
}

```

###### 2.2.2.5 should register repo for entity

* 标记 RegisterDefaultRepositories 为True（通过AddDefaultRepositories方法）
* entity type 实现了`IAggregateRoot`接口
* 且 entity type 包含在`CustomRepositories`中

```c#
public abstract class AbpCommonDbContextRegistrationOptions 
    : IAbpCommonDbContextRegistrationOptionsBuilder
{
    public bool 
        ShouldRegisterDefaultRepositoryFor(Type entityType)
    {
        // 如果 标记 register default repos 为 false，False
        if (!RegisterDefaultRepositories)
        {
            return false;
        }
        // 如果 custom repos 中不包含 entity type，False
        if (CustomRepositories.ContainsKey(entityType))
        {
            return false;
        }
        // 如果没有标记 include all entities，
        // 或者 entity type 没有实现 IAggregateRoot 接口，False
        if (!IncludeAllEntitiesForDefaultRepositories &&
            !typeof(IAggregateRoot).IsAssignableFrom(entityType))
        {
            return false;
        }
        
        return true;
    }
}

```

##### 2.2.3 repo registrar base

* 实现 repos 自动注册的抽象基类

```c#
public abstract class RepositoryRegistrarBase<TOptions>        
    where TOptions: AbpCommonDbContextRegistrationOptions
{
    // 注入 (common) dbContext register options
    public TOptions Options { get; }    
    protected RepositoryRegistrarBase(TOptions options)
    {
        Options = options;
    }
    
    
    
    
    
    
    
    
}

```

###### 2.2.3.1 add repositories

```c#
public abstract class RepositoryRegistrarBase<TOptions>        
    where TOptions: AbpCommonDbContextRegistrationOptions
{    
    public virtual void AddRepositories()
    {
        // 遍历注册 options 中的 custom repos
        foreach (var customRepository in Options.CustomRepositories)
        {
            Options.Services
                .AddDefaultRepository(
                	customRepository.Key, 
                	customRepository.Value);
        }
        // 如果 options 标记了 register default repos，
        // 注册 default repos
        if (Options.RegisterDefaultRepositories)
        {            
            RegisterDefaultRepositories();
        }
    }
}

```

###### 2.2.3.2 register default repositories

```c#
public abstract class RepositoryRegistrarBase<TOptions>        
    where TOptions: AbpCommonDbContextRegistrationOptions
{    
    protected virtual void RegisterDefaultRepositories()
    {
        // 遍历 dbContext 中的所有 entity
        foreach (var entityType in GetEntityTypes(
            Options.OriginalDbContextType))
        {
            // 如果判断 entity 不需要注册 repo，忽略
            if (!ShouldRegisterDefaultRepositoryFor(entityType))
            {
                continue;
            }
            // 创建 entity 的 repo
            RegisterDefaultRepository(entityType);
        }
    }
    // get entity types，在派生类中实现    
    protected abstract IEnumerable<Type> GetEntityTypes(
        Type dbContextType);
    
    // 注册 <TEntity,TRepoImpl>
    protected virtual void RegisterDefaultRepository(Type entityType)
    {
        Options.Services.AddDefaultRepository(
            entityType,
            GetDefaultRepositoryImplementationType(entityType));
    }
}

```

###### 2.2.3.3 get default repo impl type

```c#
public abstract class RepositoryRegistrarBase<TOptions>        
    where TOptions: AbpCommonDbContextRegistrationOptions
{
    protected virtual Type 
        GetDefaultRepositoryImplementationType(Type entityType)
    {
        // 获取 primary key type
        var primaryKeyType = EntityHelper.FindPrimaryKeyType(entityType);
        
        // 复合主键
        if (primaryKeyType == null)
        {
            // 如果 options 中标记了 specified default repo type
            return Options.SpecifiedDefaultRepositoryTypes
                // 返回 option.default repo impl type
                ? Options.DefaultRepositoryImplementationTypeWithoutKey
                	.MakeGenericType(entityType)
                // 否则，返回 get repo type（派生类中实现），
                // -> get repo type
                : GetRepositoryType(
                    Options.DefaultRepositoryDbContextType, 
                    entityType);
        }
        
        // 单主键
        // 如果 options 中标记了 specified default repo type
        return Options.SpecifiedDefaultRepositoryTypes
            	// 返回 option.default repo impl type
                ? Options.DefaultRepositoryImplementationType
            		.MakeGenericType(entityType, primaryKeyType)
            	// 否则，返回 get repo type（派生类中实现），
            	// -> get repo type (key)
                : GetRepositoryType(
                    Options.DefaultRepositoryDbContextType, 
                    entityType, 
                    primaryKeyType);
    }
    
    // 获取复合主键 repo，在派生类中实现
    protected abstract Type GetRepositoryType(
        Type dbContextType, 
        Type entityType);
        
    // 获取单主键 repo，在派生类中实现
    protected abstract Type GetRepositoryType(
        Type dbContextType, 
        Type entityType,
        Type primaryKeyType);
}

```

###### 2.2.3.4 should register default repo for entity

* 同 common dbContext register options

```c#
public abstract class RepositoryRegistrarBase<TOptions>        
    where TOptions: AbpCommonDbContextRegistrationOptions
{
    protected virtual bool 
        ShouldRegisterDefaultRepositoryFor(Type entityType)
    {
        if (!Options.RegisterDefaultRepositories)
        {
            return false;
        }
        
        if (Options.CustomRepositories.ContainsKey(entityType))
        {
            return false;
        }
        
        if (!Options.IncludeAllEntitiesForDefaultRepositories && 
            !typeof(IAggregateRoot).IsAssignableFrom(entityType))
        {
            return false;
        }
        
        return true;
    }    
}

```

### 3. practice

