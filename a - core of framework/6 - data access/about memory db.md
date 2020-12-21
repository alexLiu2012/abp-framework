## about memory db

相关程序集：

* Volo.Abp.MemoryDb

----

### 1. about

#### 1.1 summary

* abp框架提供了 memory db 功能，用于测试

#### 1.2 how designed

##### 1.2.1 （自动）注册 memory db repo

* memory dbContext register options
  * 继承 common dbContext register options
* memory dbContext register
  * 继承 repo register base 并实现功能
    * get entity types
    * get repo type
* service 中注入和配置 memory dbContext register options

##### 1.2.2 memory repo

* repo 在 memory db 的实现
* 没有通过 memory dbContext？？后续版本修改

##### 1.2.3 memory dbContext

* memory db 的抽象映射
* 暂时没有使用

##### 1.2.4 memory db

* memory provider 获取 current  tenant 中的 database api
* 如果没有 database api，使用 memory manager 创建 database
* memory database 获取 memory collection
* memory collection 中包含 memory serializer，用于序列化

### 2. details

#### 2.1 注册 memory db repo

* 实现 memory db 自动注册

##### 2.1.1 memory dbContext register options

###### 2.1.1.1 memory register options 接口

```c#
public interface IAbpMemoryDbContextRegistrationOptionsBuilder 
    : IAbpCommonDbContextRegistrationOptionsBuilder
{    
}

```

###### 2.1.1.2 memory register options 实现

```c#
public class AbpMemoryDbContextRegistrationOptions 
    : AbpCommonDbContextRegistrationOptions, 
	  IAbpMemoryDbContextRegistrationOptionsBuilder
{
    public AbpMemoryDbContextRegistrationOptions(
        Type originalDbContextType, 
        IServiceCollection services)         
        	: base(originalDbContextType, services)
    {
    }
}

```

##### 2.1.2 memory repository registrar

```c#
public class MemoryDbRepositoryRegistrar 
    : RepositoryRegistrarBase<AbpMemoryDbContextRegistrationOptions>
{
    public MemoryDbRepositoryRegistrar(
        AbpMemoryDbContextRegistrationOptions options)    
        	: base(options)
    {
    }
    
    // 重写 get entity types，
    // 从 memory db context 中 
    protected override IEnumerable<Type> GetEntityTypes(Type dbContextType)
    {
        var memoryDbContext = (MemoryDbContext)Activator
            .CreateInstance(dbContextType);
        return memoryDbContext.GetEntityTypes();
    }
    // 重写 get repo type，
    // 为 entity 创建 memoryDb<TContext,TEntity>
    protected override Type GetRepositoryType(
        Type dbContextType, 
        Type entityType)
    {
        return typeof(MemoryDbRepository<,>)
            .MakeGenericType(dbContextType, entityType);
    }
    // 重写 get repo type，
    // 为 entity 创建 memoryDb<TContext,TEntity,TKey>
    protected override Type GetRepositoryType(
        Type dbContextType, 
        Type entityType, 
        Type primaryKeyType)
    {
        return typeof(MemoryDbRepository<,,>)
            .MakeGenericType(dbContextType, entityType, primaryKeyType);
    }
}

```

##### 2.1.3 add memory dbContext registration options

```c#
public static class AbpMemoryDbServiceCollectionExtensions
{
    public static IServiceCollection 
        AddMemoryDbContext<TMemoryDbContext>(
        	this IServiceCollection services, 
        	Action<IAbpMemoryDbContextRegistrationOptionsBuilder> 
        		optionsBuilder = null)            
        	where TMemoryDbContext : MemoryDbContext
    {
        // 创建 memory dbContext register options 并配置
        var options = new AbpMemoryDbContextRegistrationOptions(
            typeof(TMemoryDbContext), 
            services);
        optionsBuilder?.Invoke(options);
        
        // 注册 TMemoryContext 并暴露为 default repo context type
        if (options.DefaultRepositoryDbContextType != typeof(TMemoryDbContext))
        {
            services.TryAddSingleton(
                options.DefaultRepositoryDbContextType, 
                sp => sp.GetRequiredService<TMemoryDbContext>());
        }
        // 用 TMemoryContext 替代 options.ReplaceContextType 注册并暴露
        foreach (var dbContextType in options.ReplacedDbContextTypes)
        {
            services.Replace(
                ServiceDescriptor.Singleton(
                    dbContextType, 
                    sp => sp.GetRequiredService<TMemoryDbContext>()));
        }
        
        // 创建 memory db registrar 并加载 repos
        new MemoryDbRepositoryRegistrar(options).AddRepositories();
        
        return services;
    }
}

```

#### 2.2 memory db repository

* memory db repo 的实现

##### 2.2.1 接口

```c#
public interface IMemoryDbRepository<TEntity> 
    : IRepository<TEntity>    
        where TEntity : class, IEntity
{
    IMemoryDatabase Database { get; }    
    IMemoryDatabaseCollection<TEntity> Collection { get; }
}

public interface IMemoryDbRepository<TEntity, TKey> 
    : IMemoryDbRepository<TEntity>, 
	  IRepository<TEntity, TKey>    
         where TEntity : class, IEntity<TKey>
{    
}

```

##### 2.2.2 MemDbRepo(TEntity)

* repo 在 memory db 的实现，
* 后续版本会包裹 dbContext

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity> 
    : RepositoryBase<TEntity>, 
	  IMemoryDbRepository<TEntity>        
          where TMemoryDbContext : MemoryDbContext       
          where TEntity : class, IEntity
{
    //TODO: Add dbcontext just like mongodb implementation!    
    
    /* memory db 底层实现服务 */
    public virtual IMemoryDatabaseCollection<TEntity> Collection => 
        Database.Collection<TEntity>();    
    public virtual IMemoryDatabase Database => DatabaseProvider.GetDatabase();   
    // 注入 memory database provider
    protected IMemoryDatabaseProvider<TMemoryDbContext> DatabaseProvider { get; }
    
    /* 注入 event bus */
    public ILocalEventBus LocalEventBus { get; set; }    
    public IDistributedEventBus DistributedEventBus { get; set; }   
    /* 注入 entity change helper */
    public IEntityChangeEventHelper EntityChangeEventHelper { get; set; }    
    /* 注入 audit property helper */
    public IAuditPropertySetter AuditPropertySetter { get; set; }
    /* 注入 guid generator */
    public IGuidGenerator GuidGenerator { get; set; }
    
    public MemoryDbRepository(IMemoryDatabaseProvider<TMemoryDbContext> databaseProvider)
    {
        DatabaseProvider = databaseProvider;
        // eventBus、entity change helper 属性注入
        LocalEventBus = NullLocalEventBus.Instance;
        DistributedEventBus = NullDistributedEventBus.Instance;
        EntityChangeEventHelper = NullEntityChangeEventHelper.Instance;
        
        // 没有 guid generator？？属性注入
    }
    
    protected override IQueryable<TEntity> GetQueryable()
    {
        return ApplyDataFilters(Collection.AsQueryable());
    }            
}                                   
```

###### 2.2.2.1 property audit

* 设置 audit property

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity> 
{
    protected virtual void SetCreationAuditProperties(TEntity entity)
    {
        AuditPropertySetter.SetCreationProperties(entity);
    }
    
    protected virtual void SetModificationAuditProperties(TEntity entity)
    {
        AuditPropertySetter.SetModificationProperties(entity);
    }
    
    protected virtual void SetDeletionAuditProperties(TEntity entity)
    {
        AuditPropertySetter.SetDeletionProperties(entity);
    }
}

```

###### 2.2.2.2 entity change event

* 触发 entity change event，实现自动审计事件

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity> 
{
    protected virtual async Task TriggerEntityCreateEvents(TEntity entity)
    {
        await EntityChangeEventHelper
            .TriggerEntityCreatedEventOnUowCompletedAsync(entity);
        await EntityChangeEventHelper
            .TriggerEntityCreatingEventAsync(entity);
    }
    
    protected virtual async Task TriggerEntityUpdateEventsAsync(TEntity entity)
    {
        await EntityChangeEventHelper
            .TriggerEntityUpdatedEventOnUowCompletedAsync(entity);
        await EntityChangeEventHelper
            .TriggerEntityUpdatingEventAsync(entity);
    }
    
    protected virtual async Task TriggerEntityDeleteEventsAsync(TEntity entity)
    {
        await EntityChangeEventHelper
            .TriggerEntityDeletedEventOnUowCompletedAsync(entity);
        await EntityChangeEventHelper
            .TriggerEntityDeletingEventAsync(entity);
    }
}

```

###### 2.2.2.3 trigger domain event

* domain event

* entity 必须实现 `IGeneratesDomainEvent`激活 domain event 功能

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity> 
{
    protected virtual async Task TriggerDomainEventsAsync(object entity)
    {
        var generatesDomainEventsEntity = entity as IGeneratesDomainEvents;
        
        // 如果没有实现 IGeneratesDomainEvent，忽略
        if (generatesDomainEventsEntity == null)
        {
            return;
        }
        
        // 发布 local event
        var localEvents = generatesDomainEventsEntity
            .GetLocalEvents()?.ToArray();
        if (localEvents != null && localEvents.Any())
        {
            foreach (var localEvent in localEvents)
            {
                await LocalEventBus
                    .PublishAsync(
                    	localEvent.GetType(), 
                    	localEvent);
            }
            
            generatesDomainEventsEntity.ClearLocalEvents();
        }
        // 发布 distributed event
        var distributedEvents = generatesDomainEventsEntity
            .GetDistributedEvents()?.ToArray();
        if (distributedEvents != null && distributedEvents.Any())
        {
            foreach (var distributedEvent in distributedEvents)
            {
                await DistributedEventBus
                    .PublishAsync(
                    	distributedEvent.GetType(), 
                    	distributedEvent);
            }
            
            generatesDomainEventsEntity.ClearDistributedEvents();
        }
    }
}
```

###### 2.2.2.4 insert

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity> 
{
    protected virtual void CheckAndSetId(TEntity entity)
    {
        if (entity is IEntity<Guid> entityWithGuidId)
        {
            TrySetGuidId(entityWithGuidId);
        }
    }
    
    protected virtual void TrySetGuidId(IEntity<Guid> entity)
    {
        // 如果 id 不为 null，即设置过 id，忽略
        if (entity.Id != default)
        {
            return;
        }
        
        EntityHelper.TrySetId(
            entity,
            () => GuidGenerator.Create(),
            true);
    }
    
    protected virtual async Task ApplyAbpConceptsForAddedEntityAsync(TEntity entity)
    {
        // set id
        CheckAndSetId(entity);
        // 设置 audit property
        SetCreationAuditProperties(entity);
        // 发布 entity create event（create）
        await TriggerEntityCreateEvents(entity);
        // 发布 domain event
        await TriggerDomainEventsAsync(entity);
    }
                
    public async override Task<TEntity> InsertAsync(
        TEntity entity,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        await ApplyAbpConceptsForAddedEntityAsync(entity);
        
        Collection.Add(entity);        
        return entity;
    }
}

```

###### 2.2.2.5 delete

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity> 
{
    protected virtual async Task ApplyAbpConceptsForDeletedEntityAsync(TEntity entity)
    {
        // 设置 audit property
        SetDeletionAuditProperties(entity);
        // 发布 entity change event（delete）
        await TriggerEntityDeleteEventsAsync(entity);
        // 发布 domain event
        await TriggerDomainEventsAsync(entity);
    }
    
    public async override Task DeleteAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        var entities = GetQueryable().Where(predicate).ToList();
        foreach (var entity in entities)
        {
            await DeleteAsync(entity, autoSave, cancellationToken);
        }
    }
        
    protected virtual bool IsHardDeleted(TEntity entity)
    {
        // 如果 uow.current.items 中不包含 hard deleted entities 集合，
        // 不支持记录 hard deleted entity，False
        if (!(UnitOfWorkManager?.Current?.Items
              .GetOrDefault(UnitOfWorkItemNames.HardDeletedEntities) is 
                  HashSet<IEntity> hardDeletedEntities))
        {
            return false;
        }
        // hardDeletedEntities 是否包含 entity
        return hardDeletedEntities.Contains(entity);
    }                                                                           
    
    public async override Task DeleteAsync(
        TEntity entity,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        await ApplyAbpConceptsForDeletedEntityAsync(entity);
        // 如果支持 soft delete（实现 ISoftDelete 接口），
        // 标记 soft deleted
        if (entity is ISoftDelete softDeleteEntity && 
            !IsHardDeleted(entity))
        {
            softDeleteEntity.IsDeleted = true;
            Collection.Update(entity);
        }
        // 不支持 soft delete，直接删除
        else
        {
            Collection.Remove(entity);
        }
    }
}

```

###### 2.2.2.6 update

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity> 
{
    public async override Task<TEntity> UpdateAsync(
        TEntity entity,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        // 设置 audit property
        SetModificationAuditProperties(entity);        
        // 如果被标记为 soft deleted
        if (entity is ISoftDelete softDeleteEntity && 
            softDeleteEntity.IsDeleted)
        {
            // 设置 audit property
            SetDeletionAuditProperties(entity);
            // 发布 entity change event（delete）
            await TriggerEntityDeleteEventsAsync(entity);
        }
        else
        {
            // 否则发布 entity change event（update）
            await TriggerEntityUpdateEventsAsync(entity);
        }        
        // 发布 domain event
        await TriggerDomainEventsAsync(entity);
        
        // 更新
        Collection.Update(entity);        
        return entity;
    }
}

```

###### 2.2.2.7 get or find

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity> 
{        
    public override Task<List<TEntity>> GetListAsync(
        bool includeDetails = false, 
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetQueryable().ToList());
    }
    
    public override Task<long> GetCountAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetQueryable().LongCount());
    }
    
    public override Task<List<TEntity>> GetPagedListAsync(
        int skipCount,
        int maxResultCount,
        string sorting,
        bool includeDetails = false,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetQueryable()
                               .OrderBy(sorting)
                .PageBy(skipCount, maxResultCount)
                .ToList());
        }
    }

	public override Task<TEntity> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            GetQueryable().Where(predicate).SingleOrDefault());
    }
}

```

##### 2.2.3 MemDbRepo(TEntity, TKey)

* memory repos with TKey
* 使用 database 的 generate id 方法

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity, TKey> 
    : MemoryDbRepository<TMemoryDbContext, TEntity>, 
	  IMemoryDbRepository<TEntity, TKey>        
          where TMemoryDbContext : MemoryDbContext       
          where TEntity : class, IEntity<TKey>
{
    public MemoryDbRepository(
        IMemoryDatabaseProvider<TMemoryDbContext> databaseProvider)            
        	: base(databaseProvider)
    {
    }                                        
}

```

###### 2.2.3.1 insert

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity, TKey> 
{
    protected virtual void SetIdIfNeeded(TEntity entity)
    {
        // 如果 TKey 是 int、log、guid
        if (typeof(TKey) == typeof(int) ||
            typeof(TKey) == typeof(long) ||
            typeof(TKey) == typeof(Guid))
        {
            if (EntityHelper.HasDefaultId(entity))
            {
                EntityHelper.TrySetId(
                    entity, 
                    () => Database.GenerateNextId<TEntity, TKey>());
            }
        }
    }

	public override Task<TEntity> InsertAsync(
        TEntity entity, 
        bool autoSave = false, 
        CancellationToken cancellationToken = default)
    {
        SetIdIfNeeded(entity);        
        return base.InsertAsync(entity, autoSave, cancellationToken);
    }
}

```

###### 2.2.3.2 delete

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity, TKey> 
{
    public virtual async Task DeleteAsync(
        TKey id, 
        bool autoSave = false, 
        CancellationToken cancellationToken = default)
    {
        await DeleteAsync(x => x.Id.Equals(id), autoSave, cancellationToken);
    }
}

```

###### 2.2.3.3 get or find

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity, TKey> 
{
    public virtual Task<TEntity> FindAsync(
        TKey id, 
        bool includeDetails = true, 
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetQueryable().FirstOrDefault(e => e.Id.Equals(id)));
    }

	public virtual async Task<TEntity> GetAsync(
        TKey id, 
        bool includeDetails = true, 
        CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(id, includeDetails, cancellationToken);
        
        if (entity == null)
        {
            throw new EntityNotFoundException(typeof(TEntity), id);
        }
        
        return entity;
    }
}

```

#### 2.3 memory dbContext

* memory db 的抽象映射
* 隔离 repo 和 db
* 没有实现？ TODO

```c#
public abstract class MemoryDbContext : ISingletonDependency
{
    private static readonly Type[] EmptyTypeList = new Type[0];
    
    // 在派生类中需要重写
    public virtual IReadOnlyList<Type> GetEntityTypes()
    {
        return EmptyTypeList;
    }
}

```

#### 2.4 memory database

##### 2.4.1 memory database

* 数据库，存储数据表（memory database collection）的容器

###### 2.4.1.1 接口

```c#
public interface IMemoryDatabase
{
    IMemoryDatabaseCollection<TEntity> Collection<TEntity>() 
        where TEntity : class, IEntity;
    
    TKey GenerateNextId<TEntity, TKey>();
}

```

###### 2.4.1.2 实现

```c#
public class MemoryDatabase : IMemoryDatabase, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;
    // collection 容器
    private readonly ConcurrentDictionary
        <Type, object> _sets;    
    // id generators 容器
    private readonly ConcurrentDictionary
        <Type, InMemoryIdGenerator> _entityIdGenerators; 
    
    // 注入 service provider，实例化容器
    public MemoryDatabase(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        
        _sets = new ConcurrentDictionary<Type, object>();
        _entityIdGenerators = new ConcurrentDictionary<Type, InMemoryIdGenerator>();
    }
    
    public IMemoryDatabaseCollection<TEntity> Collection<TEntity>()            
        where TEntity : class, IEntity
    {
        return _sets.GetOrAdd(
            typeof(TEntity),                    
            _ => _serviceProvider.GetRequiredService
            	<IMemoryDatabaseCollection<TEntity>>()) as
        IMemoryDatabaseCollection<TEntity>;
    }
    
    public TKey GenerateNextId<TEntity, TKey>()
    {
        return _entityIdGenerators.GetOrAdd(
            typeof(TEntity), 
            () => new InMemoryIdGenerator())
        .GenerateNext<TKey>();
    }
}

```

##### 2.4.2  memoryCollection

* 数据表，存储 entity 的容器

###### 2.4.2.1 接口

```c#
public interface IMemoryDatabaseCollection<TEntity> 
    : IEnumerable<TEntity>
{
    void Add(TEntity entity);    
    void Update(TEntity entity);    
    void Remove(TEntity entity);
}

```

###### 2.4.2.2 实现

```c#
public class MemoryDatabaseCollection<TEntity> 
    : IMemoryDatabaseCollection<TEntity> where TEntity : class, IEntity
{
    // 容器
    private readonly Dictionary<string, byte[]> _dictionary = 
        new Dictionary<string, byte[]>();
    
    // 注入序列化器
    private readonly IMemoryDbSerializer _memoryDbSerializer;    
    public MemoryDatabaseCollection(IMemoryDbSerializer memoryDbSerializer)
    {
        _memoryDbSerializer = memoryDbSerializer;
    }
    
    public IEnumerator<TEntity> GetEnumerator()
    {
        foreach (var entity in _dictionary.Values)
        {
            yield return _memoryDbSerializer.Deserialize(
                entity, typeof(TEntity)).As<TEntity>();
        }
    }    
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    
    public void Add(TEntity entity)
    {
        _dictionary.Add(
            GetEntityKey(entity), 
            _memoryDbSerializer.Serialize(entity));
    }
    
    public void Update(TEntity entity)
    {
        if (_dictionary.ContainsKey(GetEntityKey(entity)))
        {
            _dictionary[GetEntityKey(entity)] = 
                _memoryDbSerializer.Serialize(entity);
        }
    }
    
    public void Remove(TEntity entity)
    {
        _dictionary.Remove(GetEntityKey(entity));
    }
    
    private string GetEntityKey(TEntity entity)
    {
        return entity.GetKeys().JoinAsString(",");
    }
}

```

##### 2.4.3 memory serializer

###### 2.4.3.1 接口

```c#
public interface IMemoryDbSerializer
{
    byte[] Serialize(object obj);    
    object Deserialize(byte[] value, Type type);
}

```

###### 2.4.3.2 实现

```c#
public class Utf8JsonMemoryDbSerializer : IMemoryDbSerializer, ITransientDependency
{
    protected Utf8JsonMemoryDbSerializerOptions Options { get; }    
    public Utf8JsonMemoryDbSerializer(IOptions<Utf8JsonMemoryDbSerializerOptions> options)
    {
        Options = options.Value;
    }
    
    byte[] IMemoryDbSerializer.Serialize(object obj)
    {
        return JsonSerializer.SerializeToUtf8Bytes(obj, Options.JsonSerializerOptions);
    }
    
    public object Deserialize(byte[] value, Type type)
    {
        return JsonSerializer.Deserialize(value, type, Options.JsonSerializerOptions);
    }
}

```

###### 2.4.3.3 memory serializer options

```c#
public class Utf8JsonMemoryDbSerializerOptions
{
    public JsonSerializerOptions JsonSerializerOptions { get; }    
    public Utf8JsonMemoryDbSerializerOptions()
    {
        JsonSerializerOptions = new JsonSerializerOptions();
    }
}

```

##### 2.4.4 memory id generator

```c#
internal class InMemoryIdGenerator
{
    private int _lastInt;
    private long _lastLong;
    
    public TKey GenerateNext<TKey>()
    {
        if (typeof(TKey) == typeof(Guid))
        {
            return (TKey)(object)Guid.NewGuid();
        }
        
        if (typeof(TKey) == typeof(int))
        {
            return (TKey)(object)Interlocked.Increment(ref _lastInt);
        }
        
        if (typeof(TKey) == typeof(long))
        {
            return (TKey)(object)Interlocked.Increment(ref _lastLong);
        }
        
        throw new AbpException("Not supported PrimaryKey type: " + typeof(TKey).FullName);
    }
}

```

##### 2.4.5 memory database provider

###### 2.4.5.1 接口

```c#
public interface IMemoryDatabaseProvider<TMemoryDbContext>        
    where TMemoryDbContext : MemoryDbContext
{
    TMemoryDbContext DbContext { get; }    
    IMemoryDatabase GetDatabase();
}

```

###### 2.4.5.2 实现

```c#
public class UnitOfWorkMemoryDatabaseProvider<TMemoryDbContext> 
    : IMemoryDatabaseProvider<TMemoryDbContext>        
        where TMemoryDbContext : MemoryDbContext
{
    public TMemoryDbContext DbContext { get; }
    
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IConnectionStringResolver _connectionStringResolver;
    private readonly MemoryDatabaseManager _memoryDatabaseManager;
    
    // 注入服务
    public UnitOfWorkMemoryDatabaseProvider(
        IUnitOfWorkManager unitOfWorkManager,
        IConnectionStringResolver connectionStringResolver,
        TMemoryDbContext dbContext, 
        MemoryDatabaseManager memoryDatabaseManager)
    {
        _unitOfWorkManager = unitOfWorkManager;
        _connectionStringResolver = connectionStringResolver;
        DbContext = dbContext;
        _memoryDatabaseManager = memoryDatabaseManager;
    }
    
    public IMemoryDatabase GetDatabase()
    {
        // 如果 current tenant 为 null，抛出异常
        var unitOfWork = _unitOfWorkManager.Current;
        if (unitOfWork == null)
        {
            throw new AbpException($"A {nameof(IMemoryDatabase)} instance can only be created inside a unit of work!");
        }
        
        // 获取 connection string
        var connectionString = _connectionStringResolver.Resolve<TMemoryDbContext>();
        // 用 conn string 作为 memory db 名字一部分
        var dbContextKey = $"{typeof(TMemoryDbContext).FullName}_{connectionString}";
        
        // 获取 database api（没有则添加）        
        var databaseApi = unitOfWork.GetOrAddDatabaseApi(
            dbContextKey,
            () => new MemoryDbDatabaseApi(
                _memoryDatabaseManager.Get(connectionString)));
        
        // 返回 database api 封装的 database
        return ((MemoryDbDatabaseApi)databaseApi).Database;
    }
}

```

###### 2.4.5.3 memory database manager

* 解析 memory db 实例

```c#
public class MemoryDatabaseManager : ISingletonDependency
{
    private readonly ConcurrentDictionary<string, IMemoryDatabase> _databases =
        new ConcurrentDictionary<string, IMemoryDatabase>();
    
    private readonly IServiceProvider _serviceProvider;    
    public MemoryDatabaseManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public IMemoryDatabase Get(string databaseName)
    {
        return _databases.GetOrAdd(
            databaseName, 
            _ => _serviceProvider.GetRequiredService<IMemoryDatabase>());
        }
    }
```

###### 2.4.5.4 memory database api

* database 的封装

```c#
public class MemoryDbDatabaseApi: IDatabaseApi
{
    public IMemoryDatabase Database { get; }
    
    public MemoryDbDatabaseApi(IMemoryDatabase database)
    {
        Database = database;
    }
}

```

##### 2.4.6 注册 memory db

* 在模块中注册 memory db

```c#
[DependsOn(typeof(AbpDddDomainModule))]
public class AbpMemoryDbModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 注册 memory database provider 泛型
        context.Services.TryAddTransient(
            typeof(IMemoryDatabaseProvider<>), 
            typeof(UnitOfWorkMemoryDatabaseProvider<>));
        // 注册 memory database collection 泛型
        context.Services.TryAddTransient(
            typeof(IMemoryDatabaseCollection<>), 
            typeof(MemoryDatabaseCollection<>));
    }
}
```

### 3. practice

