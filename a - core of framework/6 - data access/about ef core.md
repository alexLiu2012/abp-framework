## about ef core

相关程序集：

* Volo.Abp.EntityFrameworkCore

----

### 1. about

#### 1.1 summary

* abp 框架集成了 ef core

#### 1.2 how designed

##### 1.2.1 （自动）注册 ef core repo

* ef core dbContext register options
  * 继承 common dbContext register options
* ef core repo registrar
  * 继承 repo register base
    * 实现 get entities
    * 实现 get repo type
* 扩展 services 的`AddAbpDbContext()`方法
  * 注册并配置 abp dbContext options
  * 注册 TDbContext
  * 注册 repos（custom 和 default）

##### 1.2.2 abp dbContext

* orm中间层，实现 crud 操作的地方
* 通过 services 扩展方法`services.AddAbpDbContext<T>()`注册
  * 注册并配置 abp dbContext register options
  * 注册 TDbContext
  * 注册repos

##### 1.2.3 abp dbContext options

##### 1.2.4 database api



### 2. details

#### 2.1 注册 ef core repo

* ef core db repo 自动注册

##### 2.1.1 ef core dbContext register options

###### 2.1.1.1 ef core context options 接口

```c#
public interface IAbpDbContextRegistrationOptionsBuilder 
    : IAbpCommonDbContextRegistrationOptionsBuilder
{
    void Entity<TEntity>(
        [NotNull] Action<AbpEntityOptions<TEntity>> optionsAction)
        	where TEntity : IEntity;
}
```

###### 2.1.1.2 ef core context options 实现

```c#
public class AbpDbContextRegistrationOptions 
    : AbpCommonDbContextRegistrationOptions, 
	  IAbpDbContextRegistrationOptionsBuilder
{
    // 以 entityType 索引的 abpEntityOptions，
    // 即以 type 分类索引的 entity 容器
    public Dictionary<Type, object> AbpEntityOptions { get; }
    
    public AbpDbContextRegistrationOptions(
        Type originalDbContextType, 
        IServiceCollection services)            
        	: base(originalDbContextType, services)
    {
        // 初始化时创建 abp entity options
        AbpEntityOptions = new Dictionary<Type, object>();
    }
    
    // 配置 entity options
    public void Entity<TEntity>(
        Action<AbpEntityOptions<TEntity>> optionsAction) 
        	where TEntity : IEntity
    {
        Services.Configure<AbpEntityOptions>(options =>
        	{
                options.Entity(optionsAction);
            });
    }
}

```

##### 2.1.2 abp entity options

* 存储 type 索引的 entity 的容器

###### 2.1.2.1 EntityOptions

```c#
public class AbpEntityOptions
{
    private readonly IDictionary<Type, object> _options;    
    public AbpEntityOptions()
    {
        _options = new Dictionary<Type, object>();
    }
    
    public AbpEntityOptions<TEntity> GetOrNull<TEntity>()        
        where TEntity : IEntity
    {
        return _options.GetOrDefault(
            typeof(TEntity)) as AbpEntityOptions<TEntity>;
    }
    
    public void Entity<TEntity>(
        [NotNull] Action<AbpEntityOptions<TEntity>> optionsAction)
        	where TEntity : IEntity
    {
        Check.NotNull(
            optionsAction, 
            nameof(optionsAction));
        
        optionsAction(_options
            .GetOrAdd(
                typeof(TEntity),
                () => new AbpEntityOptions<TEntity>()) 
            as AbpEntityOptions<TEntity>);
    }
}

```

###### 2.1.2.2 EntityOptions(TEntity)

```c#
public class AbpEntityOptions<TEntity>        
    where TEntity : IEntity
{
    public static AbpEntityOptions<TEntity> Empty { get; } = 
        new AbpEntityOptions<TEntity>();
    
    public Func<IQueryable<TEntity>, IQueryable<TEntity>> 
        DefaultWithDetailsFunc { get; set; }
}
   
```

##### 2.1.3 ef core repos registrar

* 实现 get entities，
* 实现 get repo type

```c#
public class EfCoreRepositoryRegistrar 
    : RepositoryRegistrarBase<AbpDbContextRegistrationOptions>
{
    public EfCoreRepositoryRegistrar(
        AbpDbContextRegistrationOptions options)            
        	: base(options)
    {        
    }
    
    protected override IEnumerable<Type> 
        GetEntityTypes(Type dbContextType)
    {
        return DbContextHelper.GetEntityTypes(dbContextType);
    }
    
    protected override Type GetRepositoryType(
        Type dbContextType, 
        Type entityType)
    {
        return typeof(EfCoreRepository<,>)
            .MakeGenericType(dbContextType, entityType);
    }
    
    protected override Type GetRepositoryType(
        Type dbContextType, 
        Type entityType, 
        Type primaryKeyType)
    {
        return typeof(EfCoreRepository<,,>)
            .MakeGenericType(dbContextType, entityType, primaryKeyType);
    }
}

```

##### 2.1.4 dbContext helper

* 通过反射获取 dbContext 中符合条件的 entity，
* 即 IEntity 接口的，DbSet<T> 定义的 

```c#
internal static class DbContextHelper
{
    public static IEnumerable<Type> GetEntityTypes(Type dbContextType)
    {
        return
            from property in 
            	dbContextType.GetTypeInfo().GetProperties(
            		BindingFlags.Public | 
            		BindingFlags.Instance)
            where
            	// 定义的 DbSet<T> 项（property）
            	ReflectionHelper.IsAssignableToGenericType(
            		property.PropertyType, typeof(DbSet<>)) &&
            	// 实现了 IEntity 接口
            	typeof(IEntity).IsAssignableFrom(
            		property.PropertyType.GenericTypeArguments[0])
            select property.PropertyType.GenericTypeArguments[0];
    }
}

```

##### 2.1.5 add abp (ef core) dbContext

* 扩展 services 添加 abp dbContext 方法

```c#
public static class AbpEfCoreServiceCollectionExtensions
{
    public static IServiceCollection AddAbpDbContext<TDbContext>(        
        this IServiceCollection services,
        Action<IAbpDbContextRegistrationOptionsBuilder> optionsBuilder = null)        	
        	where TDbContext : AbpDbContext<TDbContext>
    {
        services.AddMemoryCache();
        
        // 添加和配置 abp(ef core) dbContext register options
        var options = new AbpDbContextRegistrationOptions(
            typeof(TDbContext), 
            services);
        optionsBuilder?.Invoke(options);
        
        // 创建并注册 TDbContext options，
        // 即自定义的 dbContext，包含具体 entity
        services.TryAddTransient(
            DbContextOptionsFactory.Create<TDbContext>);
        
        // 用 TDbContext 替换 abp dbContext register options 中的 default dbContext
        foreach (var dbContextType in options.ReplacedDbContextTypes)
        {
            services.Replace(
                ServiceDescriptor.Transient(
                    dbContextType, 
                    typeof(TDbContext)));
        }
        
        // 用 abp dbContext register options 创建 ef core registrar，
        // 注册 repository 
        new EfCoreRepositoryRegistrar(options).AddRepositories();
        
        return services;
    }
}

```

##### 2.1.6  dbContext options factory

* 创建 dbContextOptions 的静态工厂方法，
  * `a` (dbContext options) creation context ->
  * 用`a`创建 `b`(dbContext options) configuration context ->
  * 解析`c`dbContext options
  * 用`b`配置（执行actions）`c`

```c#
public static class DbContextOptionsFactory
{
    public static DbContextOptions<TDbContext> Create<TDbContext>(
        IServiceProvider serviceProvider)            
        	where TDbContext : AbpDbContext<TDbContext>
    {
        // 获取 dbContext (options) creation context
        var creationContext = GetCreationContext<TDbContext>(serviceProvider);
        // 创建 dbContext (options) configuration context
        var context = new AbpDbContextConfigurationContext<TDbContext>(
            creationContext.ConnectionString,
            serviceProvider,
            creationContext.ConnectionStringName,
            creationContext.ExistingConnection            );
        
        // 获取 dbContext options
        var options = GetDbContextOptions<TDbContext>(serviceProvider);
        
        // pre-configure “dbContext options”
        PreConfigure(options, context);
        // configure "dbContext options"
        Configure(options, context);
        
        return context.DbContextOptions.Options;
    }  
    
    // 解析 abp dbContext options
    private static AbpDbContextOptions GetDbContextOptions<TDbContext>(
        IServiceProvider serviceProvider)        
        	where TDbContext : AbpDbContext<TDbContext>
    {
        return serviceProvider
            .GetRequiredService<IOptions<AbpDbContextOptions>>()
            	.Value;
    }
}

```

###### 2.1.6.1 get creation context

```c#
public static class DbContextOptionsFactory
{
    private static DbContextCreationContext GetCreationContext<TDbContext>(
        IServiceProvider serviceProvider)            	
        	where TDbContext : AbpDbContext<TDbContext>
    {
        var context = DbContextCreationContext.Current;
        if (context != null)
        {
            return context;
        }
        
        var connectionStringName = ConnectionStringNameAttribute
            .GetConnStringName<TDbContext>();
        var connectionString = serviceProvider
            .GetRequiredService<IConnectionStringResolver>()
            	.Resolve(connectionStringName);
        
        return new DbContextCreationContext(
            connectionStringName,
            connectionString);        
    }
}

```

###### 2.1.6.2 create configuration options

见2.5

###### 2.1.6.3 pre configure

```c#
public static class DbContextOptionsFactory
{
    private static void PreConfigure<TDbContext>(
        AbpDbContextOptions options,
        AbpDbContextConfigurationContext<TDbContext> context)        
        	where TDbContext : AbpDbContext<TDbContext>
    {
        foreach (var defaultPreConfigureAction in 
                 options.DefaultPreConfigureActions)
        {
            defaultPreConfigureAction.Invoke(context);
        }
        
        var preConfigureActions = options.PreConfigureActions
            .GetOrDefault(typeof(TDbContext));
        if (!preConfigureActions.IsNullOrEmpty())
        {
            foreach (var preConfigureAction in preConfigureActions)
            {
                ((Action<AbpDbContextConfigurationContext<TDbContext>>)
                 preConfigureAction)
                	.Invoke(context);
            }
        }
    }
}

```

###### 2.1.6.4 configure

```c#
public static class DbContextOptionsFactory
{
    private static void Configure<TDbContext>(
        AbpDbContextOptions options,
        AbpDbContextConfigurationContext<TDbContext> context)            
        	where TDbContext : AbpDbContext<TDbContext>
    {
        var configureAction = options.ConfigureActions
            .GetOrDefault(typeof(TDbContext));
        if (configureAction != null)
        {
            ((Action<AbpDbContextConfigurationContext<TDbContext>>)
             configureAction)
            	.Invoke(context);
        }
        else if (options.DefaultConfigureAction != null)
        {
            options.DefaultConfigureAction.Invoke(context);
        }
        else
        {
            throw new AbpException(
                $"No configuration found for {typeof(DbContext).AssemblyQualifiedName}! Use services.Configure<AbpDbContextOptions>(...) to configure it.");
        }
    }
}

```

#### 2.2 ef core repo

* repository 在 ef core 下的实现
* 内部通过 dbContext 和 dbSet 完成 crud，解耦了 db

##### 2.2.1 IEfCoreRepo 接口

```c#
// ef core repo (entity)
public interface IEfCoreRepository<TEntity> 
    : IRepository<TEntity>        
        where TEntity : class, IEntity
{
    DbContext DbContext { get; }    
    DbSet<TEntity> DbSet { get; }
}

// ef core repo (entity, key)
public interface IEfCoreRepository<TEntity, TKey> 
    : IEfCoreRepository<TEntity>, IRepository<TEntity, TKey>        
        where TEntity : class, IEntity<TKey>
{    
}

```

##### 2.2.2 EfCoreRepo(TEntity)

```c#
public class EfCoreRepository<TDbContext, TEntity> 
    : RepositoryBase<TEntity>, 
	  IEfCoreRepository<TEntity>, 
	  IAsyncEnumerable<TEntity>        
          where TDbContext : IEfCoreDbContext        
          where TEntity : class, IEntity
{
    // 转换为 ms ef core “dbContext" 和 ”dbSet”
    DbContext IEfCoreRepository<TEntity>.DbContext => 
        DbContext.As<DbContext>();              
    public virtual DbSet<TEntity> DbSet => 
        DbContext.Set<TEntity>();        
    
    // 从 dbContextProvider 解析 TDbContext
    protected virtual TDbContext DbContext => 
        _dbContextProvider.GetDbContext();
    // 从 entityOptionsLazy 中懒加载 entity options
    protected virtual AbpEntityOptions<TEntity> AbpEntityOptions => 
        _entityOptionsLazy.Value;
    
    private readonly IDbContextProvider<TDbContext> _dbContextProvider;
    private readonly Lazy<AbpEntityOptions<TEntity>> _entityOptionsLazy;    
    public virtual IGuidGenerator GuidGenerator { get; set; }
    
    public EfCoreRepository(IDbContextProvider<TDbContext> dbContextProvider)
    {                
        // 注入 (abp ef core) dbContext provider
        _dbContextProvider = dbContextProvider;                
        // 懒加载 IOptions<abp entity options>.Value，
        // 如果没有，创建
        _entityOptionsLazy = new Lazy<AbpEntityOptions<TEntity>>(
            () => ServiceProvider
            	.GetRequiredService<IOptions<AbpEntityOptions>>()
            		.Value.GetOrNull<TEntity>() 
            			?? AbpEntityOptions<TEntity>.Empty);
        
        // 生成 guid generator
        GuidGenerator = SimpleGuidGenerator.Instance;
    }
    
    // ef core 支持 IQuryable                            
    protected override IQueryable<TEntity> GetQueryable()
    {
        return DbSet.AsQueryable();
    }
    
    public IAsyncEnumerator<TEntity> GetAsyncEnumerator(
        CancellationToken cancellationToken = default)
    {
        return DbSet.AsAsyncEnumerable().GetAsyncEnumerator(cancellationToken);
    }
            
    public virtual async Task EnsureCollectionLoadedAsync<TProperty>(
        TEntity entity,
        Expression<Func<TEntity, IEnumerable<TProperty>>> propertyExpression,
        CancellationToken cancellationToken = default)        
        	where TProperty : class
    {
        await DbContext.Entry(entity)
            .Collection(propertyExpression)
            .LoadAsync(GetCancellationToken(cancellationToken));
    }
    
    public virtual async Task EnsurePropertyLoadedAsync<TProperty>(
        TEntity entity,
        Expression<Func<TEntity, TProperty>> propertyExpression,
        CancellationToken cancellationToken = default)        
        	where TProperty : class
    {
        await DbContext.Entry(entity)
            .Reference(propertyExpression)
            .LoadAsync(GetCancellationToken(cancellationToken));
    }                            
}

```

###### 2.2.2.1 insert

```c#
public class EfCoreRepository<TDbContext, TEntity> 
{
    // 如果是 IEntity<Guid>，自动设置 id
    // 否则需要指定 id
    protected virtual void CheckAndSetId(TEntity entity)
    {
        if (entity is IEntity<Guid> entityWithGuidId)
        {
            TrySetGuidId(entityWithGuidId);
        }
    }    
    protected virtual void TrySetGuidId(IEntity<Guid> entity)
    {
        // 如果设置过 id（entity.id）不为 null，忽略
        if (entity.Id != default)
        {
            return;
        }
        
        EntityHelper.TrySetId(
            entity,
            () => GuidGenerator.Create(),
            true);
    }
    
    public async override Task<TEntity> InsertAsync(
        TEntity entity, 
        bool autoSave = false, 
        CancellationToken cancellationToken = default)
    {
        // 设置 entity.id（懒存储）
        CheckAndSetId(entity);
        // 向 DbSet 中添加
        var savedEntity = DbSet.Add(entity).Entity;
        
        // 如果使用 autoSave，保存到数据库
        if (autoSave)
        {
            await DbContext.SaveChangesAsync(
                GetCancellationToken(cancellationToken));
        }
        
        return savedEntity;
    }
}

```

###### 2.2.2.2 delete

```c#
public class EfCoreRepository<TDbContext, TEntity> 
{
    // 删除 entity
    public async override Task DeleteAsync(
        TEntity entity, 
        bool autoSave = false, 
        CancellationToken cancellationToken = default)
    {
        // 从 DbSet 删除 entity(object)，
        // 如果没有 entity，会报错？抛异常？ 
        DbSet.Remove(entity);
        
        // 如果使用 autoSave，保存到数据库
        if (autoSave)
        {
            await DbContext.SaveChangesAsync(
                GetCancellationToken(cancellationToken));
        }
    }
    
    // 条件删除
    public async override Task DeleteAsync(
        Expression<Func<TEntity, bool>> predicate, 
        bool autoSave = false, 
        CancellationToken cancellationToken = default)
    {
        // 获取符合条件的 entities，
        // ef core 支持 IQueryable
        var entities = await GetQueryable()
            .Where(predicate)
            .ToListAsync(GetCancellationToken(cancellationToken));
        
        // 从 DbSet 删除 entity(object)
        // 如果没有 entity，会报错？抛异常？ 
        foreach (var entity in entities)
        {
            DbSet.Remove(entity);
        }
        
        // 如果使用 autoSave，保存到数据库
        if (autoSave)
        {
            await DbContext.SaveChangesAsync(
                GetCancellationToken(cancellationToken));
        }
    }
}
```

###### 2.2.2.3 update

```c#
public class EfCoreRepository<TDbContext, TEntity> 
{
    public async override Task<TEntity> UpdateAsync(
        TEntity entity, 
        bool autoSave = false, 
        CancellationToken cancellationToken = default)
    {
        // 向 dbContext 附加 entity 并更新
        DbContext.Attach(entity);        
        var updatedEntity = DbContext.Update(entity).Entity;
        
        // 如果使用 autoSave，保存到数据库
        if (autoSave)
        {
            await DbContext.SaveChangesAsync(
                GetCancellationToken(cancellationToken));
        }
        
        return updatedEntity;
    }
}

```

###### 2.2.2.4 with details

```c#
public class EfCoreRepository<TDbContext, TEntity> 
{
    public override IQueryable<TEntity> WithDetails()
    {
        // 如果 entity options 中没有定义了 details func
        if (AbpEntityOptions.DefaultWithDetailsFunc == null)
        {
            // 使用 ms ef core 的 with details 方法
            return base.WithDetails();
        }
        // 使用 entity optinos 中定义的 details func
        return AbpEntityOptions.DefaultWithDetailsFunc(GetQueryable());
    }
    
    public override IQueryable<TEntity> WithDetails(
        params Expression<Func<TEntity, object>>[] propertySelectors)
    {
        var query = GetQueryable();
        
        // 遍历传入的 entity func 过滤 entity
        if (!propertySelectors.IsNullOrEmpty())
        {
            foreach (var propertySelector in propertySelectors)
            {
                query = query.Include(propertySelector);
            }
        }
        
        return query;
    }
}

```

###### 2.2.2.5 find

```c#
public class EfCoreRepository<TDbContext, TEntity> 
{
    // find, single or default, 不抛出异常（但可以为 null）
    public async override Task<TEntity> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        return includeDetails
            // 加载 entity with details，再过滤
            ? await WithDetails()
            	.Where(predicate)
            	.SingleOrDefaultAsync(
            		GetCancellationToken(cancellationToken))
            // 加载 entity，过滤
            : await DbSet
                .Where(predicate)
                .SingleOrDefaultAsync(
                	GetCancellationToken(cancellationToken));
    }
    
    public async override Task<List<TEntity>> GetListAsync(
        bool includeDetails = false, 
        CancellationToken cancellationToken = default)
    {
        return includeDetails
            // 加载 entity with details
            ? await WithDetails()
            	.ToListAsync(
            		GetCancellationToken(cancellationToken))
            // 加载 entity
            : await DbSet
                .ToListAsync(
                	GetCancellationToken(cancellationToken));
    }       
}

```

###### 2.2.2.6 get list

```c#
public class EfCoreRepository<TDbContext, TEntity> 
{    
    public async override Task<List<TEntity>> GetListAsync(
        bool includeDetails = false, 
        CancellationToken cancellationToken = default)
    {
        return includeDetails
            // 如果 include details
            ? await WithDetails()
            	.ToListAsync(
            		GetCancellationToken(cancellationToken))
            // not include details
            : await DbSet
                .ToListAsync(
                	GetCancellationToken(cancellationToken));
    }
    
    public async override Task<long> GetCountAsync(
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .LongCountAsync(
            	GetCancellationToken(cancellationToken));
    }
    
    public async override Task<List<TEntity>> GetPagedListAsync(
        int skipCount,
        int maxResultCount,
        string sorting,
        bool includeDetails = false,
        CancellationToken cancellationToken = default)
    {
        var queryable = includeDetails ? WithDetails() : DbSet;
        
        return await queryable
            .OrderBy(sorting)
            .PageBy(
            	skipCount, 
            	maxResultCount)
            .ToListAsync(
            	GetCancellationToken(cancellationToken));
    }
}

```

##### 2.2.3 EFCoreRepo(TEntity, TKey)

```c#
public class EfCoreRepository<TDbContext, TEntity, TKey> 
    : EfCoreRepository<TDbContext, TEntity>,        
	  IEfCoreRepository<TEntity, TKey>,        
	  ISupportsExplicitLoading<TEntity, TKey>        
          where TDbContext : IEfCoreDbContext        
          where TEntity : class, IEntity<TKey>
{
    public EfCoreRepository(
        IDbContextProvider<TDbContext> dbContextProvider)            
        	: base(dbContextProvider)
    {        
    }                        
}

```

###### 2.2.3.1 delete

```c#
public class EfCoreRepository<TDbContext, TEntity, TKey> 
{
    // 删除特定 id 的 entity，
    // 如果没有 entity，不会抛出异常或错误
    public virtual async Task DeleteAsync(
        TKey id, 
        bool autoSave = false, 
        CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(
            id, 
            cancellationToken: cancellationToken);
        
        if (entity == null)
        {
            return;
        }
        
        await DeleteAsync(entity, autoSave, cancellationToken);
    }
}

```

###### 2.2.3.2 find

```c#
public class EfCoreRepository<TDbContext, TEntity, TKey> 
{
    // 用 id 查找，
    // 找不到抛出异常？？
    public virtual async Task<TEntity> FindAsync(
        TKey id, 
        bool includeDetails = true, 
        CancellationToken cancellationToken = default)
    {
        return includeDetails
            ? await WithDetails()
            	.FirstOrDefaultAsync(
            		e => e.Id.Equals(id), 
            		GetCancellationToken(cancellationToken))
            : await DbSet.FindAsync(
                new object[] {id}, 
                GetCancellationToken(cancellationToken));
    }
}

```

###### 2.2.3.3 get

* 找不到抛出异常

```c#
public class EfCoreRepository<TDbContext, TEntity, TKey> 
{
    // 用 id 查找，
    // 找不到抛出异常
    public virtual async Task<TEntity> GetAsync(
        TKey id, 
        bool includeDetails = true, 
        CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(
            id, 
            includeDetails, 
            GetCancellationToken(cancellationToken));
        
        if (entity == null)
        {
            throw new EntityNotFoundException(typeof(TEntity), id);
        }
        
        return entity;
    }
}

```

#### 2.3 abp db context 接口

* abp 框架统一封装的 dbContext 接口

##### 2.3.1 IEfCoreDbContext 

* 反向定义的 EfCore.DbContext 的接口

```c#
public interface IEfCoreDbContext 
    : IDisposable, 
	  IInfrastructure<IServiceProvider>, 
	  IDbContextDependencies, 
	  IDbSetCache, 
	  IDbContextPoolable
{
    DbSet<T> Set<T>() where T: class;    
    DatabaseFacade Database { get; }    
	ChangeTracker ChangeTracker { get; }
          
    /* get entity entry */
    EntityEntry Entry(
        [NotNull] object entity);
          
    EntityEntry<TEntity> Entry<TEntity>(
        [NotNull] TEntity entity) where TEntity : class;       
}

```

###### 2.3.1.1 add and add range

```c#
public interface IEfCoreDbContext 
{
    /* add */          
    EntityEntry Add(
        [NotNull] object entity);    
          
    EntityEntry<TEntity> Add<TEntity>(
        [NotNull] TEntity entity) where TEntity : class;    
          
    ValueTask<EntityEntry> AddAsync(
        [NotNull] object entity, 
        CancellationToken cancellationToken = default);    
          
    ValueTask<EntityEntry<TEntity>> AddAsync<TEntity>(
        [NotNull] TEntity entity, 
        CancellationToken cancellationToken = default) where TEntity : class;
    
    /* add range */
    void AddRange(
        [NotNull] params object[] entities);  
          
    void AddRange(
        [NotNull] IEnumerable<object> entities);     
          
    Task AddRangeAsync(
        [NotNull] params object[] entities);    
          
    Task AddRangeAsync(
        [NotNull] IEnumerable<object> entities, 
        CancellationToken cancellationToken = default);
}

```

###### 2.3.1.2 attach and attach range

```c#
public interface IEfCoreDbContext 
{
    /* attach */
    EntityEntry Attach(
        [NotNull] object entity);
          
    EntityEntry<TEntity> Attach<TEntity>(
        [NotNull] TEntity entity) where TEntity : class;
    
    /* attach range */
    void AttachRange(
        [NotNull] params object[] entities);
          
    void AttachRange(
        [NotNull] IEnumerable<object> entities);     
}

```

###### 2.3.1.3  remove and remove range

```c#
public interface IEfCoreDbContext 
{
    /* remove */
    EntityEntry Remove(
        [NotNull] object entity);
          
    EntityEntry<TEntity> Remove<TEntity>(
        [NotNull] TEntity entity) where TEntity : class;    
    
    /* remove range */
    void RemoveRange(
        [NotNull] params object[] entities);
          
    void RemoveRange(
        [NotNull] IEnumerable<object> entities);
}

```

###### 2.3.1.4 update and update range

```c#
public interface IEfCoreDbContext 
{
    /* update */
    EntityEntry Update(
        [NotNull] object entity);
          
    EntityEntry<TEntity> Update<TEntity>(
        [NotNull] TEntity entity) where TEntity : class;
        
    /* update range */
    void UpdateRange(
        [NotNull] params object[] entities);    
          
    void UpdateRange(
        [NotNull] IEnumerable<object> entities);
}

```

###### 2.3.1.5 find

```c#
public interface IEfCoreDbContext 
{
    /* find */
    object Find(
        [NotNull] Type entityType, 
        [NotNull] params object[] keyValues);    
          
    TEntity Find<TEntity>(
        [NotNull] params object[] keyValues) where TEntity : class;
    
    ValueTask<object> FindAsync(
        NotNull] Type entityType, 
        [NotNull] params object[] keyValues);      
                    
    ValueTask<TEntity> FindAsync<TEntity>(
        [NotNull] params object[] keyValues) where TEntity : class;    
                  
    ValueTask<object> FindAsync(
        [NotNull] Type entityType, 
        [NotNull] object[] keyValues, 
        CancellationToken cancellationToken);    
          
    ValueTask<TEntity> FindAsync<TEntity>(
        [NotNull] object[] keyValues, 
        CancellationToken cancellationToken) where TEntity : class;
}

```

###### 2.3.1.6 save changes

```c#
public interface IEfCoreDbContext 
{
    /* save changes */
    int SaveChanges();   
          
    int SaveChanges(
        bool acceptAllChangesOnSuccess);
    
    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
          
    Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, 
        CancellationToken cancellationToken = default);
                
    // call the DbContext,
    // <see cref="SaveChangesAsync(bool, CancellationToken)"/> method,
    // directly of EF Core, 
    // which doesn't apply concepts of abp.         
    Task<int> SaveChangesOnDbContextAsync(
        bool acceptAllChangesOnSuccess, 
        CancellationToken cancellationToken = default);          
}

```

##### 2.3.2 IAbpEfCoreDbContext

* abp 框架扩展的 (ef core) dbContext 接口

```c#
public interface IAbpEfCoreDbContext : IEfCoreDbContext
{
    void Initialize(
        AbpEfCoreDbContextInitializationContext initializationContext);
}

```

###### 2.3.2.1 ef core dbContext initialize context

```c#
public class AbpEfCoreDbContextInitializationContext
{
    public IUnitOfWork UnitOfWork { get; }    
    public AbpEfCoreDbContextInitializationContext(IUnitOfWork unitOfWork)
    {
        UnitOfWork = unitOfWork;
    }
}

```

#### 2.4 abp db context 实现

* abp 框架定义的 abp ef core DbContext 实现
* 扩展 ef core DbContext 功能
* IEfCoreDbContext 的超集

```c#
public abstract class AbpDbContext<TDbContext> 
    : DbContext, 
	  IAbpEfCoreDbContext, 
	  ITransientDependency        
          where TDbContext : DbContext
{
    // tenant info
    public ICurrentTenant CurrentTenant { get; set; }
    protected virtual Guid? CurrentTenantId => 
        CurrentTenant?.Id;    
    
    // datafilter，
    // 是否支持 softDelete、multiTenant
    public IDataFilter DataFilter { get; set; }          
    protected virtual bool IsMultiTenantFilterEnabled => 
        DataFilter?.IsEnabled<IMultiTenant>() ?? false;    
    protected virtual bool IsSoftDeleteFilterEnabled => 
        DataFilter?.IsEnabled<ISoftDelete>() ?? false;
    
    // entity changes
    public IEntityChangeEventHelper EntityChangeEventHelper { get; set; }
    public IEntityHistoryHelper EntityHistoryHelper { get; set; }
    // entity audit
    public IAuditingManager AuditingManager { get; set; }          
    public IAuditPropertySetter AuditPropertySetter { get; set; }
    
    public IUnitOfWorkManager UnitOfWorkManager { get; set; }
    
    public IGuidGenerator GuidGenerator { get; set; }            
    public IClock Clock { get; set; }    
    public ILogger<AbpDbContext<TDbContext>> Logger { get; set; }
    
    private static readonly MethodInfo 
        ConfigureBasePropertiesMethodInfo
        	= typeof(AbpDbContext<TDbContext>)
        		.GetMethod(
                    nameof(ConfigureBaseProperties),
                    BindingFlags.Instance | BindingFlags.NonPublic);
    
    private static readonly MethodInfo 
        ConfigureValueConverterMethodInfo
            = typeof(AbpDbContext<TDbContext>)
                .GetMethod(
                    nameof(ConfigureValueConverter),
                    BindingFlags.Instance | BindingFlags.NonPublic);
    
    private static readonly MethodInfo 
        ConfigureValueGeneratedMethodInfo
            = typeof(AbpDbContext<TDbContext>)
                .GetMethod(
                    nameof(ConfigureValueGenerated),
                    BindingFlags.Instance | BindingFlags.NonPublic);
    
    protected AbpDbContext(
        DbContextOptions<TDbContext> options) : base(options)
    {        
        GuidGenerator = SimpleGuidGenerator.Instance;
        
        // 属性注入
        EntityChangeEventHelper = NullEntityChangeEventHelper.Instance;
        EntityHistoryHelper = NullEntityHistoryHelper.Instance;
        Logger = NullLogger<AbpDbContext<TDbContext>>.Instance;
    }
                                                               
            
    protected virtual bool TryCancelDeletionForSoftDelete(EntityEntry entry)
    {
        if (!(entry.Entity is ISoftDelete))
        {
            return false;
        }
        
        if (IsHardDeleted(entry))
        {
            return false;
        }
        
        entry.Reload();
        entry.State = EntityState.Modified;
        entry.Entity.As<ISoftDelete>().IsDeleted = true;
        return true;
    }
               
    protected virtual bool 
        ShouldFilterEntity<TEntity>(
        	IMutableEntityType entityType) where TEntity : class
    {
        if (typeof(IMultiTenant).IsAssignableFrom(typeof(TEntity)))
        {
            return true;
        }
        
        if (typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
        {
            return true;
        }
        
        return false;
    }
    
    protected virtual Expression<Func<TEntity, bool>> 
        CreateFilterExpression<TEntity>() where TEntity : class
    {
        Expression<Func<TEntity, bool>> expression = null;
        
        if (typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
        {
            expression = e => 
                !IsSoftDeleteFilterEnabled || 
                !EF.Property<bool>(e, "IsDeleted");
        }
        
        if (typeof(IMultiTenant).IsAssignableFrom(typeof(TEntity)))
        {
            Expression<Func<TEntity, bool>> multiTenantFilter = e =>
                !IsMultiTenantFilterEnabled || 
                EF.Property<Guid>(e, "TenantId") == CurrentTenantId;
            
            expression = expression == null 
                ? multiTenantFilter 
                : CombineExpressions(
                    expression, 
                    multiTenantFilter);
        }
        
        return expression;
    }
    
    protected virtual Expression<Func<T, bool>> 
        CombineExpressions<T>(
        	Expression<Func<T, bool>> expression1, 
        	Expression<Func<T, bool>> expression2)
    {
        var parameter = Expression.Parameter(typeof(T));
        
        var leftVisitor = new ReplaceExpressionVisitor(
            expression1.Parameters[0], 
            parameter);
        var left = leftVisitor.Visit(expression1.Body);
        
        var rightVisitor = new ReplaceExpressionVisitor(
            expression2.Parameters[0], 
            parameter);
        var right = rightVisitor.Visit(expression2.Body);
        
        return Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(left, right), 
            parameter);
    }
    
    class ReplaceExpressionVisitor : ExpressionVisitor
    {
        private readonly Expression _oldValue;
        private readonly Expression _newValue;
        
        public ReplaceExpressionVisitor(Expression oldValue, Expression newValue)
        {
            _oldValue = oldValue;
            _newValue = newValue;
        }
        
        public override Expression Visit(Expression node)
        {
            if (node == _oldValue)
            {
                return _newValue;
            }
            
            return base.Visit(node);
        }
    }
}

```

##### 2.4.1 initialize

* 在创建 abp dbContext 时调用

```c#
public abstract class AbpDbContext<TDbContext> 
{
    public virtual void Initialize(
        AbpEfCoreDbContextInitializationContext initializationContext)
    {
        // 设置 command timeout，
        // 如果 initia  contex 中有值，
        // database 中没有设置过，
        // database 是 relational ？？？
        if (initializationContext
            	.UnitOfWork.Options.Timeout.HasValue &&
            Database.IsRelational() &&
            !Database.GetCommandTimeout().HasValue)
        {
            Database.SetCommandTimeout(
                TimeSpan.FromMilliseconds(
                    initializationContext
                    	.UnitOfWork.Options.Timeout.Value));
        }
        // tracker cascade delete
        ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
        // 订阅 ms ef core change tracker
        ChangeTracker.Tracked += ChangeTracker_Tracked;
    }
}

```

###### 2.4.1.1 change tracked

```c#
public abstract class AbpDbContext<TDbContext> 
{
    // abp tracker，
    // 跟踪 extra properties 变化
    protected virtual void ChangeTracker_Tracked(
        object sender, 
        EntityTrackedEventArgs e)
    {
        FillExtraPropertiesForTrackedEntities(e);
    }
    
    // 跟踪 extra properties 具体实现
    protected virtual void FillExtraPropertiesForTrackedEntities(
        EntityTrackedEventArgs e)
    {
        // 如果 (extra)entity type 为 null，忽略
        var entityType = e.Entry.Metadata.ClrType;
        if (entityType == null)
        {
            return;
        }
        // 如果 entity 不支持 extra properties，忽略
        // 即，entity 没有实现 IHasExtraProperties 接口
        if (!(e.Entry.Entity is IHasExtraProperties entity))
        {
            return;
        }
        // ???
        if (!e.FromQuery)
        {
            return;
        }
        // 如果没有 objectExtensionManager，忽略
        // 即，没有依赖 object extending 模块
        var objectExtension = ObjectExtensionManager.Instance
            .GetOrNull(entityType);
        if (objectExtension == null)
        {
            return;
        }
        
        // 遍历 extra property
        foreach (var property in objectExtension.GetProperties())
        {
            // 如果 property 不 mapped to field for ef core，忽略
            if (!property.IsMappedToFieldForEfCore())
            {
                continue;
            }
            
            /* Checking "currentValue != null" has a good advantage:
            * Assume that you we already using a named extra property,
            * then decided to create a field (entity extension) for it.
            * In this way, it prevents to delete old value in the JSON and
            * updates the field on the next save!
            */
            // 向 entity 更新 extra property
            var currentValue = e.Entry.CurrentValues[property.Name];
            if (currentValue != null)
            {
                entity.ExtraProperties[property.Name] = currentValue;
            }
        }
    }
}

```

##### 2.4.2 on model creating

* 重写 ms ef core dbContext 对应方法

```c#
public abstract class AbpDbContext<TDbContext> 
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 调用 ef core model creating
        base.OnModelCreating(modelBuilder);
        
        /* abp 增加的配置 */
        
        // 设置 database provider
        TrySetDatabaseProvider(modelBuilder);        
        // override configure
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            ConfigureBasePropertiesMethodInfo
                .MakeGenericMethod(entityType.ClrType)
                	.Invoke(
                		this, 
                		new object[] { modelBuilder, entityType });
            
            ConfigureValueConverterMethodInfo
                .MakeGenericMethod(entityType.ClrType)
                	.Invoke(
                		this, 
                		new object[] { modelBuilder, entityType });
            
            ConfigureValueGeneratedMethodInfo
                .MakeGenericMethod(entityType.ClrType)
                	.Invoke(
                		this, 
                		new object[] { modelBuilder, entityType });
        }
    }       
}

```

###### 2.4.2.1 set database provider

* 获取 database provider （枚举 name）

```c#
public abstract class AbpDbContext<TDbContext> 
{
    protected virtual void TrySetDatabaseProvider(
        ModelBuilder modelBuilder)
    {
        // 获取 db provider(name)
        var provider = GetDatabaseProviderOrNull(modelBuilder);
        
        // 如果 db provider(name) 不为 null，
        // 使用 ms ef core. modelBuilder 中的 setDbProvider 方法
        if (provider != null)
        {
            modelBuilder.SetDatabaseProvider(provider.Value);
        }
    }
        
    protected virtual EfCoreDatabaseProvider? 
        GetDatabaseProviderOrNull(ModelBuilder modelBuilder)
    {        
        switch (Database.ProviderName)	
        {
            case "Microsoft.EntityFrameworkCore.SqlServer":
                return EfCoreDatabaseProvider.SqlServer;
            case "Npgsql.EntityFrameworkCore.PostgreSQL":
                return EfCoreDatabaseProvider.PostgreSql;
            case "Pomelo.EntityFrameworkCore.MySql":
                return EfCoreDatabaseProvider.MySql;
            case "Oracle.EntityFrameworkCore":
            case "Devart.Data.Oracle.Entity.EFCore":
                return EfCoreDatabaseProvider.Oracle;
            case "Microsoft.EntityFrameworkCore.Sqlite":
                return EfCoreDatabaseProvider.Sqlite;
            case "Microsoft.EntityFrameworkCore.InMemory":
                return EfCoreDatabaseProvider.InMemory;
            case "FirebirdSql.EntityFrameworkCore.Firebird":
                return EfCoreDatabaseProvider.Firebird;
            case "Microsoft.EntityFrameworkCore.Cosmos":
                return EfCoreDatabaseProvider.Cosmos;
            default:
                return null;
        }
    }
}

```

###### 2.4.2.2 model builder set db provider 扩展

```c#
public static class AbpModelBuilderExtensions
{
    private const string ModelDatabaseProviderAnnotationKey = "_Abp_DatabaseProvider";
    
    public static void SetDatabaseProvider(
        this ModelBuilder modelBuilder,
        EfCoreDatabaseProvider databaseProvider)
    {
        modelBuilder.Model.SetAnnotation(
            ModelDatabaseProviderAnnotationKey, 
            databaseProvider);
    }
    
    public static void ClearDatabaseProvider(
        this ModelBuilder modelBuilder)
    {
        modelBuilder.Model.RemoveAnnotation(
            ModelDatabaseProviderAnnotationKey);
    }
    
    public static EfCoreDatabaseProvider? GetDatabaseProvider(
        this ModelBuilder modelBuilder    )
    {
        return (EfCoreDatabaseProvider?) modelBuilder.Model[
            ModelDatabaseProviderAnnotationKey];
    }
    
    /* using xxxdb
    public static void UseXxxDb(this ModelBuilder modelBuilder)
    {
        modelBuilder.SetDatabaseProvider(
            EfCoreDatabaseProvider.XxxDb);
    }
    */
    
    /* is using xxxdb
    public static bool IsUsingXxx(this ModelBuilder modelBuilder)
    {
        return modelBuilder.GetDatabaseProvider() == 
            EfCoreDatabaseProvider.XxxDb;
    }
    */
}

```



###### 2.4.2.2 configure base properties

```c#
public abstract class AbpDbContext<TDbContext> 
{
    protected virtual void ConfigureBaseProperties<TEntity>(
        ModelBuilder modelBuilder, 
        IMutableEntityType mutableEntityType)            
        	where TEntity : class
    {
        if (mutableEntityType.IsOwned())
        {
            return;
        }
        
        if (!typeof(IEntity).IsAssignableFrom(typeof(TEntity)))
        {
            return;
        }
        
        modelBuilder.Entity<TEntity>().ConfigureByConvention();
        
        ConfigureGlobalFilters<TEntity>(
            modelBuilder, 
            mutableEntityType);
    }
}
    
```

###### 2.4.2.3 confiure global filters

```c#
public abstract class AbpDbContext<TDbContext> 
{
    protected virtual void ConfigureGlobalFilters<TEntity>(
        ModelBuilder modelBuilder, 
        IMutableEntityType mutableEntityType)            
        	where TEntity : class
    {
        if (mutableEntityType.BaseType == null && 
            ShouldFilterEntity<TEntity>(mutableEntityType))
        {
            var filterExpression = CreateFilterExpression<TEntity>();
            if (filterExpression != null)
            {
                modelBuilder.Entity<TEntity>()
                    .HasQueryFilter(filterExpression);
            }
        }
    }
}

```

###### 2.4.2.3 configure value converter

```c#
public abstract class AbpDbContext<TDbContext> 
{
    protected virtual void ConfigureValueConverter<TEntity>(
        ModelBuilder modelBuilder, 
        IMutableEntityType mutableEntityType)        
        	where TEntity : class
    {
        if (mutableEntityType.BaseType == null &&
            !typeof(TEntity).IsDefined(
                typeof(DisableDateTimeNormalizationAttribute), true) &&
            !typeof(TEntity).IsDefined(
                typeof(OwnedAttribute), true) &&
            !mutableEntityType.IsOwned())
        {
            if (Clock == null || !Clock.SupportsMultipleTimezone)
            {
                return;
            }
            
            var dateTimeValueConverter = 
                new AbpDateTimeValueConverter(Clock);
            
            var dateTimePropertyInfos = typeof(TEntity).GetProperties()
                .Where(property =>
                	(property.PropertyType == typeof(DateTime) ||
                    property.PropertyType == typeof(DateTime?)) &&
                    property.CanWrite &&
                    !property.IsDefined(
                        typeof(DisableDateTimeNormalizationAttribute), 
                        true))
                .ToList();
            
            dateTimePropertyInfos.ForEach(property =>
            	{
                    modelBuilder.Entity<TEntity>()
                        .Property(property.Name)
                        .HasConversion(dateTimeValueConverter);
                });
        }
    }
}

```

###### 2.4.2.4 configure value generated

```c#
public abstract class AbpDbContext<TDbContext> 
{
    protected virtual void ConfigureValueGenerated<TEntity>(
        ModelBuilder modelBuilder, 
        IMutableEntityType mutableEntityType)            
        	where TEntity : class
    {
        if (!typeof(IEntity<Guid>).IsAssignableFrom(typeof(TEntity)))
        {
            return;
        }
        
        var idPropertyBuilder = 
            modelBuilder.Entity<TEntity>().Property(x => 
            	((IEntity<Guid>)x).Id);
        if (idPropertyBuilder.Metadata.PropertyInfo
            	.IsDefined(
                    typeof(DatabaseGeneratedAttribute), 
                    true))
        {
            return;
        }
        
        idPropertyBuilder.ValueGeneratedNever();
    }
}

```

##### 2.4.3 save changes

* 重写 ms ef core dbContext 对应方法

```c#
public abstract class AbpDbContext<TDbContext> 
{
    public async override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auditLog = AuditingManager?.Current?.Log;
            
            // 创建 entity change list
            List<EntityChangeInfo> entityChangeList = null;
            if (auditLog != null)
            {
                entityChangeList = EntityHistoryHelper.
                    CreateChangeList(ChangeTracker.Entries().ToList());
            }
            
            // 执行 abp concepts（audit property、domain event等）
            var changeReport = ApplyAbpConcepts();
            
            // 调用 ms ef core 的 save changes 方法
            var result = await base.SaveChangesAsync(
                acceptAllChangesOnSuccess, 
                cancellationToken);
            
            // 发布 entity change event
            await EntityChangeEventHelper
                .TriggerEventsAsync(changeReport);
            
            // 记录 entity change 到自动审计日志
            if (auditLog != null)
            {
                // 更新 entity change list
                EntityHistoryHelper
                    .UpdateChangeList(entityChangeList);
                // 向 audit log 添加 entity change list
                auditLog.EntityChanges
                    .AddRange(entityChangeList);
                // 打印日志
                Logger.LogDebug(
                    $"Added {entityChangeList.Count} entity changes 
                    to the current audit log");
            }
            
            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new AbpDbConcurrencyException(ex.Message, ex);
        }
        finally
        {
            // 设置 auto tracker detected 为 True
            ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }
}
    
```

###### 2.4.3.1 apply abp concepts

```c#
public abstract class AbpDbContext<TDbContext> 
{
    protected virtual EntityChangeReport ApplyAbpConcepts()
    {
        var changeReport = new EntityChangeReport();
        
        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            ApplyAbpConcepts(entry, changeReport);
        }
        
        return changeReport;
    }
    
    protected virtual void ApplyAbpConcepts(
        EntityEntry entry, 
        EntityChangeReport changeReport)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                ApplyAbpConceptsForAddedEntity(entry, changeReport);
                break;
            case EntityState.Modified:
                ApplyAbpConceptsForModifiedEntity(entry, changeReport);
                break;
            case EntityState.Deleted:
                ApplyAbpConceptsForDeletedEntity(entry, changeReport);
                break;
        }
        
        HandleExtraPropertiesOnSave(entry);
        
        AddDomainEvents(changeReport, entry.Entity);
    }                        
}

```

###### 2.4.3.2 insert concept

```c#
public abstract class AbpDbContext<TDbContext> 
{
    protected virtual void CheckAndSetId(EntityEntry entry)
    {
        if (entry.Entity is IEntity<Guid> entityWithGuidId)
        {
            TrySetGuidId(entry, entityWithGuidId);
        }
    }
    
    protected virtual void TrySetGuidId(
        EntityEntry entry, 
        IEntity<Guid> entity)
    {
        if (entity.Id != default)
        {
            return;
        }
        
        var idProperty = entry.Property("Id").Metadata.PropertyInfo;
        
        //Check for DatabaseGeneratedAttribute
        var dbGeneratedAttr = ReflectionHelper
            .GetSingleAttributeOrDefault
            <DatabaseGeneratedAttribute>(idProperty);
        
        if (dbGeneratedAttr != null && 
            dbGeneratedAttr.DatabaseGeneratedOption != 
            atabaseGeneratedOption.None)
        {
            return;
        }
        
        EntityHelper.TrySetId(
            entity,
            () => GuidGenerator.Create(),
            true);
    }
    
    protected virtual void ApplyAbpConceptsForAddedEntity(
        EntityEntry entry, 
        EntityChangeReport changeReport)
    {
        CheckAndSetId(entry);
        SetConcurrencyStampIfNull(entry);
        SetCreationAuditProperties(entry);
        changeReport.ChangedEntities.Add(
            new EntityChangeEntry(
                entry.Entity, 
                EntityChangeType.Created));
    }
}

```

###### 2.4.3.3 delete concept

```c#
public abstract class AbpDbContext<TDbContext> 
{
    protected virtual void ApplyAbpConceptsForDeletedEntity(
        EntityEntry entry, 
        EntityChangeReport changeReport)
    {
        if (TryCancelDeletionForSoftDelete(entry))
        {
            UpdateConcurrencyStamp(entry);
            SetDeletionAuditProperties(entry);
        }
        
        changeReport.ChangedEntities.Add(
            new EntityChangeEntry(
                entry.Entity, 
                EntityChangeType.Deleted));
    }
}

```

###### 2.4.3.4 update concept

```c#
public abstract class AbpDbContext<TDbContext> 
{
    protected virtual void ApplyAbpConceptsForModifiedEntity(
        EntityEntry entry, 
        EntityChangeReport changeReport)
    {
        UpdateConcurrencyStamp(entry);
        SetModificationAuditProperties(entry);
        
        if (entry.Entity is ISoftDelete && 
            entry.Entity.As<ISoftDelete>().IsDeleted)
        {
            SetDeletionAuditProperties(entry);
            changeReport.ChangedEntities.Add(
                new EntityChangeEntry(
                    entry.Entity, 
                    EntityChangeType.Deleted));
        }
        else
        {
            changeReport.ChangedEntities.Add(
                new EntityChangeEntry(
                    entry.Entity, 
                    EntityChangeType.Updated));
        }
    }
}

```

###### 2.4.3.5 handle extra

```c#
public abstract class AbpDbContext<TDbContext> 
{
    protected virtual void HandleExtraPropertiesOnSave(
        EntityEntry entry)
    {
        if (entry.State.IsIn(
            EntityState.Deleted, 
            EntityState.Unchanged))
        {
            return;
        }
        
        var entityType = entry.Metadata.ClrType;
        if (entityType == null)
        {
            return;
        }
        
        if (!(entry.Entity is IHasExtraProperties entity))
        {
            return;
        }
        
        var objectExtension = ObjectExtensionManager.Instance
            .GetOrNull(entityType);
        if (objectExtension == null)
        {
            return;
        }
        
        var efMappedProperties = ObjectExtensionManager.Instance
            .GetProperties(entityType)
            	.Where(p => p.IsMappedToFieldForEfCore());
        
        foreach (var property in efMappedProperties)
        {
            if (!entity.HasProperty(property.Name))
            {
                continue;
            }
            
            var entryProperty = entry.Property(property.Name);
            var entityProperty = entity.GetProperty(property.Name);
            if (entityProperty == null)
            {
                entryProperty.CurrentValue = null;
                continue;
            }
            
            if (entryProperty.Metadata.ClrType == 
                entityProperty.GetType())
            {
                entryProperty.CurrentValue = entityProperty;
            }
            else
            {
                if (TypeHelper
                    	.IsPrimitiveExtended(
                            entryProperty.Metadata.ClrType, 
                            includeEnums: true))
                {
                    var conversionType = entryProperty.Metadata.ClrType;
                    if (TypeHelper.IsNullable(conversionType))
                    {
                        conversionType = conversionType
                            .GetFirstGenericArgumentIfNullable();
                    }
                    
                    if (conversionType == typeof(Guid))
                    {
                        entryProperty.CurrentValue = 
                            TypeDescriptor.GetConverter(conversionType)
                            	.ConvertFromInvariantString(
                            		entityProperty.ToString());
                    }
                    else
                    {
                        entryProperty.CurrentValue = 
                            Convert.ChangeType(
                            	entityProperty, 
                            	conversionType, 
                            	CultureInfo.InvariantCulture);
                    }
                }
            }
        }
    }
}
        
```

###### 2.4.3.6 add domain events

```c#
public abstract class AbpDbContext<TDbContext> 
{
    protected virtual void AddDomainEvents(
        EntityChangeReport changeReport, 
        object entityAsObj)
    {
        var generatesDomainEventsEntity = 
            entityAsObj as IGeneratesDomainEvents;
        if (generatesDomainEventsEntity == null)
        {
            return;
        }
        
        var localEvents = generatesDomainEventsEntity
            .GetLocalEvents()?.ToArray();
        if (localEvents != null && localEvents.Any())
        {
            changeReport.DomainEvents
                .AddRange(localEvents.Select(eventData => 
                	new DomainEventEntry(
                        entityAsObj, 
                        eventData)));
            
            generatesDomainEventsEntity.ClearLocalEvents();
        }
        
        var distributedEvents = generatesDomainEventsEntity
            .GetDistributedEvents()?.ToArray();
        if (distributedEvents != null && distributedEvents.Any())
        {
            changeReport.DistributedEvents
                .AddRange(distributedEvents.Select(eventData => 
                	new DomainEventEntry(
                        entityAsObj, 
                        eventData)));
            
            generatesDomainEventsEntity.ClearDistributedEvents();
        }
    }
}

```

###### 2.4.3.7 设置 audit property

```c#
public abstract class AbpDbContext<TDbContext> 
{
    protected virtual void SetCreationAuditProperties(
        EntityEntry entry)
    {
        AuditPropertySetter?
            .SetCreationProperties(entry.Entity);
    }
    
    protected virtual void SetModificationAuditProperties(
        EntityEntry entry)
    {
        AuditPropertySetter?
            .SetModificationProperties(entry.Entity);
    }
    
    protected virtual void SetDeletionAuditProperties(
        EntityEntry entry)
    {
        AuditPropertySetter?
            .SetDeletionProperties(entry.Entity);
    }
}
```

###### 2.4.3.8 设置 concurrent stamp

```c#
public abstract class AbpDbContext<TDbContext> 
{
    protected virtual void UpdateConcurrencyStamp(EntityEntry entry)
    {
        var entity = entry.Entity as IHasConcurrencyStamp;
        if (entity == null)
        {
            return;
        }
        
        Entry(entity)
            .Property(x => x.ConcurrencyStamp)
            	.OriginalValue = entity.ConcurrencyStamp;
        
        entity.ConcurrencyStamp = Guid.NewGuid().ToString("N");
    }
    
    protected virtual void SetConcurrencyStampIfNull(
        EntityEntry entry)
    {
        var entity = entry.Entity as IHasConcurrencyStamp;
        if (entity == null)
        {
            return;
        }
        
        if (entity.ConcurrencyStamp != null)
        {
            return;
        }
        
        entity.ConcurrencyStamp = Guid.NewGuid().ToString("N");
    }
}

```



###### aaa delete

```c#

    
    protected virtual bool IsHardDeleted(EntityEntry entry)
    {
        var hardDeletedEntities = UnitOfWorkManager?.Current?.Items.GetOrDefault(UnitOfWorkItemNames.HardDeletedEntities) as HashSet<IEntity>;
        if (hardDeletedEntities == null)
        {
            return false;
        }
        
        return hardDeletedEntities.Contains(entry.Entity);
    }
```

#### 2.5 abp dbContext options

* abp 框架封装的统一的 dbContext options

##### 2.5.1 dbContext options

```c#
public class AbpDbContextOptions
{
    internal List<Action<AbpDbContextConfigurationContext>> 
        DefaultPreConfigureActions { get; set; }    
    internal Action<AbpDbContextConfigurationContext> 
        DefaultConfigureAction { get; set; }
    
    internal Dictionary<Type, List<object>> 
        PreConfigureActions { get; set; }    
    internal Dictionary<Type, object> 
        ConfigureActions { get; set; }
    
    public AbpDbContextOptions()
    {
        DefaultPreConfigureActions = new List<Action<AbpDbContextConfigurationContext>>();
        
        PreConfigureActions = new Dictionary<Type, List<object>>();
        ConfigureActions = new Dictionary<Type, object>();
    }
    
    public void PreConfigure(
        [NotNull] Action<AbpDbContextConfigurationContext> action)
    {
        Check.NotNull(action, nameof(action));        
        DefaultPreConfigureActions.Add(action);
    }    
    public void Configure(
        [NotNull] Action<AbpDbContextConfigurationContext> action)
    {
        Check.NotNull(action, nameof(action));        
        DefaultConfigureAction = action;
    }
    
    public void PreConfigure<TDbContext>(
        [NotNull] Action<AbpDbContextConfigurationContext<TDbContext>> action)            
        	where TDbContext : AbpDbContext<TDbContext>
    {
        Check.NotNull(action, nameof(action));
        
        var actions = PreConfigureActions.GetOrDefault(typeof(TDbContext));
        if (actions == null)
        {
            PreConfigureActions[typeof(TDbContext)] = actions = new List<object>();
        }
        
        actions.Add(action);
    }
    
    public void Configure<TDbContext>(
        [NotNull] Action<AbpDbContextConfigurationContext<TDbContext>> action)     
        	where TDbContext : AbpDbContext<TDbContext>
    {
        Check.NotNull(action, nameof(action));        
        ConfigureActions[typeof(TDbContext)] = action;
    }
}

```

##### 2.5.2 dbContext configuration context

* 包裹 ms ef core DbContextOptionsBuilder 封装

###### 2.5.2.1 dbContext configure context

```c#
public class AbpDbContextConfigurationContext : IServiceProviderAccessor
{
    public IServiceProvider ServiceProvider { get; }
    
    public string ConnectionString { get; }    
    public string ConnectionStringName { get; }
    
    public DbConnection ExistingConnection { get; }
    
    public DbContextOptionsBuilder DbContextOptions { get; protected set; }
    
    public AbpDbContextConfigurationContext(
        [NotNull] string connectionString,
        [NotNull] IServiceProvider serviceProvider,
        [CanBeNull] string connectionStringName,
        [CanBeNull]DbConnection existingConnection)
    {
        ConnectionString = connectionString;
        ServiceProvider = serviceProvider;
        ConnectionStringName = connectionStringName;
        ExistingConnection = existingConnection;
        
        DbContextOptions = new DbContextOptionsBuilder()
            .UseLoggerFactory(
            	serviceProvider
            		.GetRequiredService<ILoggerFactory>());
    }
}

```

###### 2.5.2.2 dbContext configure context <T>

```c#
public class AbpDbContextConfigurationContext<TDbContext> 
    : AbpDbContextConfigurationContext    
        where TDbContext : AbpDbContext<TDbContext>
{
    public new DbContextOptionsBuilder<TDbContext> 
        DbContextOptions => 
        	(DbContextOptionsBuilder<TDbContext>)base.DbContextOptions;

    public AbpDbContextConfigurationContext(
        string connectionString,
        [NotNull] IServiceProvider serviceProvider,
        [CanBeNull] string connectionStringName,
        [CanBeNull] DbConnection existingConnection)        
        	: base(            
                connectionString,
                serviceProvider,             
                connectionStringName,             
                existingConnection)
    {
        base.DbContextOptions = 
            new DbContextOptionsBuilder<TDbContext>()
            	.UseLoggerFactory(
            		serviceProvider
            			.GetRequiredService<ILoggerFactory>());
    }
}
```

#### 2.6 dbContext provider

* ms ef core 使用的 dbContext

##### 2.6.1 IDbContextProvider 接口

```c#
public interface IDbContextProvider<out TDbContext>        
    where TDbContext : IEfCoreDbContext
{
    TDbContext GetDbContext();
}

```

##### 2.6.2 uow dbContext provider

```c#
public class UnitOfWorkDbContextProvider<TDbContext> 
    : IDbContextProvider<TDbContext>        
        where TDbContext : IEfCoreDbContext
{
    // 注入 uow manager，conn string resolver
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IConnectionStringResolver _connectionStringResolver;    
    public UnitOfWorkDbContextProvider(
        IUnitOfWorkManager unitOfWorkManager,
        IConnectionStringResolver connectionStringResolver)
    {
        _unitOfWorkManager = unitOfWorkManager;
        _connectionStringResolver = connectionStringResolver;
    }
}

```

###### 2.6.2.1 get TDbContext

```c#
public class UnitOfWorkDbContextProvider<TDbContext> 
{
    public TDbContext GetDbContext()
    {
        // 获取当前 uow.current，
        // 如果 uow.current 为 null，抛出异常
        var unitOfWork = _unitOfWorkManager.Current;
        if (unitOfWork == null)
        {
            throw new AbpException(
                "A DbContext can only be created inside a unit of work!");
        }
        
        // 获取 conn string
        var connectionStringName = ConnectionStringNameAttribute
            .GetConnStringName<TDbContext>();           
        var connectionString = _connectionStringResolver
            .Resolve(connectionStringName);
        
        // 获取 database api，
        // 如果没有，添加
        var dbContextKey = $"typeof(TDbContext).FullName}_{connectionString}";        
        var databaseApi = unitOfWork.GetOrAddDatabaseApi(
                dbContextKey,
                () => new EfCoreDatabaseApi<TDbContext>(
                    CreateDbContext(
                        unitOfWork, 
                        connectionStringName, 
                        connectionString)));
        
        return ((EfCoreDatabaseApi<TDbContext>)databaseApi).DbContext;
    }
}

```

###### 2.6.2.2 create TDbContext 

```c#
public class UnitOfWorkDbContextProvider<TDbContext> 
{
    // 使用指定的 connStringName、connString 创建 dbContext，
    // -> createDbContext(unitOfWork)
    private TDbContext CreateDbContext(
        IUnitOfWork unitOfWork, 
        string connectionStringName, 
        string connectionString)
    {
        // 创建 creationContext，
        // with connStringName, connString
        var creationContext = new DbContextCreationContext(
            connectionStringName, 
            connectionString);
        
        using (DbContextCreationContext.Use(creationContext))
        {
            // 创建 IAbpEfCoreDbContext
            var dbContext = CreateDbContext(unitOfWork);
            
            // IAbpEfCoreDbContext.Initialize
            if (dbContext is IAbpEfCoreDbContext abpEfCoreDbContext)
            {
                abpEfCoreDbContext.Initialize(
                    new AbpEfCoreDbContextInitializationContext(
                        	unitOfWork));
            }
            
            return dbContext;
        }
    }
}

```

###### 2.6.2.3 create TDbContext with uow

```c#
public class UnitOfWorkDbContextProvider<TDbContext> 
{        
    // 使用指定的 uow 创建 dbContext
    private TDbContext CreateDbContext(IUnitOfWork unitOfWork)
    {
        
        return unitOfWork.Options.IsTransactional
            // 如果 uow 支持 transaction，
            // -> createDbContextWithTransaction(uow) 
            ? CreateDbContextWithTransaction(unitOfWork)
            // 否则，从 ioc 解析 TDbContext
            : unitOfWork.ServiceProvider
                .GetRequiredService<TDbContext>();
    }       
    
    // 创建 dbContext with transaction                    
    public TDbContext CreateDbContextWithTransaction(
        IUnitOfWork unitOfWork) 
    {
        // 获取 transaction api
        var transactionApiKey = $"EntityFrameworkCore_
        	{DbContextCreationContext.Current.ConnectionString}";
        var activeTransaction = unitOfWork
            .FindTransactionApi(transactionApiKey) 
        	as EfCoreTransactionApi;
        
        // 如果 transaction api 为 null
        if (activeTransaction == null)
        {
            // 从 ioc 解析 TDbContext
            var dbContext = unitOfWork.ServiceProvider
                .GetRequiredService<TDbContext>();
            // 创建 db transaction
            var dbtransaction = unitOfWork.Options
                .IsolationLevel.HasValue
                	? dbContext.Database
                		.BeginTransaction(
                			unitOfWork.Options.IsolationLevel.Value)
                	: dbContext.Database
                        .BeginTransaction();
            // 创建 transaction api
            unitOfWork.AddTransactionApi(
                transactionApiKey,
                new EfCoreTransactionApi(
                    dbtransaction,
                    dbContext));
            
            return dbContext;
        }
        // transaction api 不为空
        else
        {
            DbContextCreationContext.Current.ExistingConnection = 
                activeTransaction
                	.DbContextTransaction
                		.GetDbTransaction().Connection;
            
            // 从 ioc 解析 TDbContext
            var dbContext = unitOfWork.ServiceProvider
                .GetRequiredService<TDbContext>();
            
            if (dbContext.As<DbContext>()
                	.HasRelationalTransactionManager())
            {
                dbContext.Database.UseTransaction(
                    activeTransaction
                    	.DbContextTransaction
                    		.GetDbTransaction());
            }
            else
            {
                //TODO: Why not using the new created transaction?
                dbContext.Database.BeginTransaction(); 
            }
            
            activeTransaction.AttendedDbContexts.Add(dbContext);
            
            return dbContext;
        }
    }        
}

```

##### 2.6.3 dbContext creation context

```c#
public class DbContextCreationContext
{
    // 静态单例 dbContextCreationContext
    private static readonly AsyncLocal<DbContextCreationContext> _current = 
        new AsyncLocal<DbContextCreationContext>();
    public static DbContextCreationContext Current => 
        _current.Value;
        
    public DbConnection ExistingConnection { get; set; }
    
    // 注入 connStringName、connString
    public string ConnectionStringName { get; }    
    public string ConnectionString { get; }            
    public DbContextCreationContext(
        string connectionStringName, 
        string connectionString)
    {
        ConnectionStringName = connectionStringName;
        ConnectionString = connectionString;
    }
    
    // 将传入的 dbContext create context 设置为 current
    public static IDisposable Use(DbContextCreationContext context)
    {
        var previousValue = Current;
        _current.Value = context;
        return new DisposeAction(() => _current.Value = previousValue);
    }
}

```

##### 2.6.4 ef core database api

```c#
public class EfCoreDatabaseApi<TDbContext> 
    : IDatabaseApi, 
	  ISupportsSavingChanges        
          where TDbContext : IEfCoreDbContext
{
    public TDbContext DbContext { get; }    
    public EfCoreDatabaseApi(TDbContext dbContext)
    {
        DbContext = dbContext;
    }
    
    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return DbContext.SaveChangesAsync(cancellationToken);
    }
}

```

#### 2.7 注册abp db context 

##### 2.7.1 abp ef core 模块

```c#
[DependsOn(typeof(AbpDddDomainModule))]
public class AbpEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(
        ServiceConfigurationContext context)
    {
        // 注册并配置 abp dbContext options
        Configure<AbpDbContextOptions>(options =>
        	{
                options.PreConfigure(
                    abpDbContextConfigurationContext =>
                	{
                    	abpDbContextConfigurationContext
                        	.DbContextOptions
                        		.ConfigureWarnings(warnings =>
                        		{
                                	warnings.Ignore(
                                    	CoreEventId
                                        	.LazyLoadOnDisposedContextWarning);
                                });
                    });
            });
        
        // 注册 uow dbContext provider，
        // 暴露为 IDbContextProvider
        context.Services.TryAddTransient(
            typeof(IDbContextProvider<>), 
            typeof(UnitOfWorkDbContextProvider<>));
    }
}

```

### 3. practice

#### 3.1 自定义 TDbContext

* 需要定义成 DbContext 的超集，即`AbpDbContext<T>`

```c#
public class MyDbContext : AbpDbContext<MyDbContext>
{
    // ...
}

```

#### 3.2 注册 TDbContext

* 在模块中注册 

```c#
public override ConfigureService(ServiceConfigurationContext context)
{
    Configure<AbpDbContextOpitons>(options =>
    	{
            // 具体 database 的扩展方法
        });
    
    context.services.AddAbpDbContext<MyDbContext>()
    {
        // ...
    }
}

```

