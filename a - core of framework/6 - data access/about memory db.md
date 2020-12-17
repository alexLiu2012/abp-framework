## about memory db

相关程序集：

* Volo.Abp.MemoryDb

----

### 1. about



### 2. details

#### 2.1 注册 memory db

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
    
    protected override IEnumerable<Type> GetEntityTypes(Type dbContextType)
    {
        var memoryDbContext = (MemoryDbContext)Activator
            .CreateInstance(dbContextType);
        return memoryDbContext.GetEntityTypes();
    }
    
    protected override Type GetRepositoryType(
        Type dbContextType, 
        Type entityType)
    {
        return typeof(MemoryDbRepository<,>)
            .MakeGenericType(dbContextType, entityType);
    }
    
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

##### 2.1.3 add memory db

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
        var options = new AbpMemoryDbContextRegistrationOptions(
            typeof(TMemoryDbContext), 
            services);
        optionsBuilder?.Invoke(options);
        
        if (options.DefaultRepositoryDbContextType != typeof(TMemoryDbContext))
        {
            services.TryAddSingleton(
                options.DefaultRepositoryDbContextType, 
                sp => sp.GetRequiredService<TMemoryDbContext>());
        }
        
        foreach (var dbContextType in options.ReplacedDbContextTypes)
        {
            services.Replace(
                ServiceDescriptor.Singleton(
                    dbContextType, 
                    sp => sp.GetRequiredService<TMemoryDbContext>()));
        }
        
        new MemoryDbRepositoryRegistrar(options).AddRepositories();
        
        return services;
    }
}

```

#### 2.2 memory db context

```c#
public abstract class MemoryDbContext : ISingletonDependency
{
    private static readonly Type[] EmptyTypeList = new Type[0];
    
    public virtual IReadOnlyList<Type> GetEntityTypes()
    {
        return EmptyTypeList;
    }
}

```

#### 2.3 memory db repository

##### 2.3.1 接口

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

##### 2.3.2 MemDbRepo(TEntity)

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
```

###### 2.3.2.1 property audit

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

###### 2.3.2.2 entity change event

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

###### 2.3.2.3 trigger domain event

* entity 必须实现 `IGeneratesDomainEvent`激活 domain event 功能

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity> 
{
    protected virtual async Task TriggerDomainEventsAsync(object entity)
    {
        var generatesDomainEventsEntity = entity as IGeneratesDomainEvents;
        if (generatesDomainEventsEntity == null)
        {
            return;
        }
        
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

###### 2.3.2.4 insert

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
        CheckAndSetId(entity);
        SetCreationAuditProperties(entity);
        await TriggerEntityCreateEvents(entity);
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

###### 2.3.2.5 delete

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity> 
{
    protected virtual async Task ApplyAbpConceptsForDeletedEntityAsync(TEntity entity)
    {
        SetDeletionAuditProperties(entity);
        await TriggerEntityDeleteEventsAsync(entity);
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
            
    public async override Task DeleteAsync(
        TEntity entity,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        await ApplyAbpConceptsForDeletedEntityAsync(entity);
        
        if (entity is ISoftDelete softDeleteEntity && !IsHardDeleted(entity))
        {
            softDeleteEntity.IsDeleted = true;
            Collection.Update(entity);
        }
        else
        {
            Collection.Remove(entity);
        }
    }
}

```

###### 2.3.2.6 update

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity> 
{
    public async override Task<TEntity> UpdateAsync(
        TEntity entity,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        SetModificationAuditProperties(entity);
        
        if (entity is ISoftDelete softDeleteEntity && softDeleteEntity.IsDeleted)
        {
            SetDeletionAuditProperties(entity);
            await TriggerEntityDeleteEventsAsync(entity);
        }
        else
        {
            await TriggerEntityUpdateEventsAsync(entity);
        }
        
        await TriggerDomainEventsAsync(entity);
        
        Collection.Update(entity);
        
        return entity;
    }
}

```

###### 2.3.2.7 get or find

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

##### 2.3.3 MemDbRepo(TEntity, TKey)

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

###### 2.3.3.1 insert

```c#
public class MemoryDbRepository<TMemoryDbContext, TEntity, TKey> 
{
    protected virtual void SetIdIfNeeded(TEntity entity)
    {
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

###### 2.3.3.2 delete

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

###### 2.3.3.3 get or find

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

#### 2.4 memory database

##### 2.4.1 memory database

###### 2.4.1.1 接口

```c#
public interface IMemoryDatabase
    {
        IMemoryDatabaseCollection<TEntity> Collection<TEntity>() where TEntity : class, IEntity;

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

###### 2.4.2.1 接口

```c#
public interface IMemoryDatabaseCollection<TEntity> : IEnumerable<TEntity>
{
    void Add(TEntity entity);    
    void Update(TEntity entity);    
    void Remove(TEntity entity);
}

```

###### 2.4.2.2 实现

```c#
public class MemoryDatabaseCollection<TEntity> 
    : IMemoryDatabaseCollection<TEntity>        where TEntity : class, IEntity
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

##### 2.4.5 memory id generator

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

##### 2.4.4 memory database provider

###### 2.4.4.1 接口

```c#
public interface IMemoryDatabaseProvider<TMemoryDbContext>        
    where TMemoryDbContext : MemoryDbContext
{
    TMemoryDbContext DbContext { get; }    
    IMemoryDatabase GetDatabase();
}

```

###### 2.4.4.2 实现

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
        var dbContextKey = $"{typeof(TMemoryDbContext).FullName}_{connectionString}";
        // 获取 database api
        var databaseApi = unitOfWork.GetOrAddDatabaseApi(
            dbContextKey,
            () => new MemoryDbDatabaseApi(
                _memoryDatabaseManager.Get(connectionString)));
        
        return ((MemoryDbDatabaseApi)databaseApi).Database;
    }
}

```

###### 2.4.2.3 memory database manager

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

###### 2.4.2.4 memory database api

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

#### 2.6 使用 memory database

##### 2.6.1 模块

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

##### 2.6.2 注册 memory database

见 2.1

### 3. practice

