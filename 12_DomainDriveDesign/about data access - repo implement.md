## about data access & repository implementation

#### 1. concept

abp框架实现了对数据库的访问支持

##### 1.1 注册 repository

abp框架定义了 repository 的抽象基类，需要派生具体的repository并注入到services中

abp框架实现了按照db_context自动注入

* register base

  ```c#
  public abstract class RepositoryRegistrarBase<TOptions> where TOptions : AbpCommonDbContextRegistrationOptions
  {
      // 注入 options
      public TOptions Options { get; }
      protected RepositoryRegistrarBase(TOptions options)
      {
          Options = options;
      }
      
      public virtual void AddRepositories()
      {
          // 添加自定义repository
          foreach(var customRepository in Options.CustomRepositories)
          {
              Options.Services.AddDefaultRepository(
                  customRepository.Key, 
                  customRepository.Value);
          }
          // 添加默认repository（聚合根）
          if(Options.RegisterDefaultRepositories)
          {
              RegisterDefaultRepositories();
          }
      }
      
      protected virtual void RegisterDefaultRepositories()
      {
          // 遍历 get_entities 方法获取的 db_context 内包含的全部实体
          foreach(var entityType in GetEntityTypes(
              Options.OriginalDbContextType))
          {            
              if(!ShouldRegisterDefaultRepositoryFor(entityType))
              {
                  // 判断entity类型不需要创建repository
                  continue;                
              }
              // 创建 entity_type 的 repository
              RegisterDefaultRepository(entityType);
          }
      }
      
      protected virtual void RegisterDefaultRepository(Type entityType)
      {
          // 注册 entity 对应的 repository
          Options.Services.AddDefaultRepository(
              entityType, 
          	GetDefaultRepositoryImplementationType(entityType));
      }
  }
  
  ```

  * 最终都是调用了`AddDefaultRepository()`方法

    本质就是向 services 中注册 repository 接口以及对应的实现

    ```c#
    public static class ServiceCollectionRepositoryExtensions
    {
        public static IServiceCollection AddDefaultRepository(
            this IServiceCollection services, 
            Type entityType, 
            Type repositoryImplementationType)
        {
            /* for IReadOnlyBasicRepository<TEntity> */
            // 获取 repository 接口
            var readOnlyBasicRepositoryInterface = typeof(IReadOnlyBasicRepository<>)
                .MakerGenericType(entityType);
            // 判断接口并注入服务
            if(readOnlyBasicRepositoryInterface
                   .IsAssignableFrom(repositoryImplementationType))
            {
                services.TryAddTransient(
                    readOnlyBasicRepositoryInterface, 
                    repositoryImplementationType);
            }
            
            // 其他接口类似 。。。
        }
    }
    
    ```

  * 获取 repository_implementation 类型用于服务注册

    ```c#
    public abstract class RepositoryRegistrarBase<TOptions>
    {
        protected virtual Type GetDefaultRepositoryImplementationType(Type entityType)
        {
            var primaryKeyType = EntityHelper.FindPrimaryKeyType(entityType);
            
            // 没有主键类型（复合主键）
            // 调用抽象方法 GetRepositoryType()，在具体的 db_context 中实现
            if(primaryKeyType == null)
            {
                return Options.SpecifiedDefaultRepositoryTypes
                    ? Options.DefaultRepositoryImplementationTypeWithoutKey
                    	.MakeGenericType(entityType)
                    : GetRepositoryType(Options.DefaultRepositoryDbContextType, 
                                        entityType);
            }
            
            // 有主键类型
            // 调用抽象方法 GetRepositoryType()，在具体的 db_context 中实现
            return Options.SpecifiedDefaultRepositoryTypes
                ? Options.DefaultRepositoryImplementationType.
                	.MakeGenericType(entityType, primaryKeyType)
                : GetRepositoryType(Options.DefaultRepositoryDbContextType,
                                    entityType, primaryKeyType);        
        }
    }
    
    ```

  * 判断 entity 是否需要 default_register_repository

    ```c#
    public abstract class RepositoryRegistrarBase<TOptions>
    {
        protected virtual bool ShouldRegisterDefaultRepositoryFor(Type entityType)
        {
            // options 中控制不要创建 default_repository
            if(!Options.RegisterDefaultRepositories)
            {
                return false;
            }
            // entity 已经指定了repository实现类型，不创建 default_repository
            if(Options.CustomRepositories.ContainsKey(entityType))
            {
                return false;
            }
            // 没有 include_all_entity，并且不是 aggregate_root
            // 即，为aggregate_root创建 default_repository
            // 也可以为全部entity创建 default_repository
            if(!Options.IncludeAllEntitiesForDefaultRepositories 
               && !typeof(IAggregateRoot).IsAssignableFrom(entityType))
            {
                return false;
            }
            
            return true;       
        }    
    }
    
    ```

* register options

  options 控制 repository 注册行为

  options 包含指定的 repository 实现类型

  ```c#
  public abstract class AbpCommonDbContextRegistrationOptions : IAbpCommonDbContextRegistrationOptionsBuilder
  {
      // 注入服务
      public Type OriginalDbContextType { get; }
      public IServiceCollection Services { get; }
      // 生成 type 容器
      public List<Type> ReplacedDbContextTypes { get; }
      public Dictionary<Type, Type> CustomRepositories { get; }
      // ??    
      public Type DefaultRepositoryDbContextType { get;private set; }
      
      public AbpCommonDbContextRegistrationOptions(Type originalDbContextType, IServiceCollection services)
      {
          OriginalDbContextType = originalDbContextType;
          Services = services;
          CustomRepositories = new Dictionary<Type, Type>();
          ReplacedDbContextType = new List<Type>();
          
          DefaultRepositoryDbContextType = originalDbContextType;
      }        
      
      // 设置replaced_db_context
      public IAbpCommonDbContextRegistrationOptionsBuilder ReplaceDbContext<TOtherDbcontext>()
      {
          return ReplaceDbContext(typeof(TOtherDbContext));
      }
      public IAbpCommonDbContextRegistrationOptionsBuilder ReplaceDbContext(Type otherDbContextType)
      {
          // null 判断
          ReplaceDbContextTypes.Add(otherDbContextType);
          return this;
      }
      
      // 设置创建 default_db_context
      
      // 设置default_repository类型
      
      
  }
  ```

##### 1.2 connection_string

abp框架将获取connection_string的工作抽离封装

* 解析器，自动注册的服务

  ```c#
  public class DefaultConnectionStringResolver 
      : IConnectionStringResolver, ITransientDependency
  {
      // 注入 db_connection_string_options
      protected AbpDbConnectionOptions Options { get; }
      public DefaultConnectionStringResolver(IOptionsSnapshot<AbpDbConnectionOptions> options)
      {
          Options = options.Value;
      }
          
      public virtual string Resolve(string connectionStringName = null)
      {
          if(!connectionStringName.IsNullOrEmpty())
          {
              var moduleConnString = 
                  Options.ConnectionStrings.GetOrDefault(connectionStringName);
              if(!moduleConnString.IsNullOrEmpty())
              {
                  return moduleConnString;
              }
              return Options.ConnectionStrings.Default;
              
              /* 可以改写如下 
              return (Options.ConnectionStrings.GetOrDefault(connectionStringName))
              	?? Options.ConnectionStrings.Default;                        
              */
          }
      }
  }
  
  ```

* conn_strings

  dictionary的封装

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

  * 标记 conn_string_name(key) 的特性

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
        // 获取特性标记的name
        public static string GetConnStringName<T>()
        {
            return GetConnStringName(typeof(T));
        }
        // 通过反射获取特性标记的name
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

* db_conn_options

  简单的options，包含conn_strings

  ```c#
  public class AbpDbConnectionOptions
  {
      public ConnectionStrings ConnetionStrings { get;set; }
      public AbpDbConnectionOptions()
      {
          ConnectionStrings = new ConnectionStrings();
      }
  }
  
  ```

* abp_data 模块

  ```c#
  [DependsOn(typeof(AbpObjectExtendingModule), typeof(AbpUnitOfWorkModule))]
  public class AbpDataModule : AbpModule
  {
      public override void ConfigureServices(ServiceConfigurationContext context)
      {
          // 配置、注入 db_conn_string_options
          var configuration = context.Services.GetConfiguration();        
          Configure<AbpDbConnectionOptions>(configuration);
      }
  }
  
  ```

##### 1.3 生成种子数据



#### 2. database

##### 2.1 memory db

* memory_db_context定义

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

  

* 注入 memory_db_context

  ```c#
  public static class AbpMemoryDbServiceCollectionExtensions
  {
      public static IServiceCollection AddMemoryDbContext<TMemoryDbContext>(
          this IServiceCollection services, 
          Action<IAbpMemoryDbContextRegistrationOptionsBuilder> optionsBuilder = null)
      {
          // 使用传入的action配置options
          var options = new AbpMemoryDbContextRegistrationOptions(
              typeof(TMemoryDbContext), services);
          optionsBuilder?.Invoke(options);
          
          // 设置options.DefaultRepositoryDbContextType -> TMemoryDbContext
          if(options.DefaultRepositoryDbContextType != typeof(TMemoryDbContext))
          {            
              // 最终保证 options.Default...Type 是 TMemoryDbContext
              services.TryAddSingleton(
                  options.DefaultRepositoryDbContextType, 
                  sp => sp.GetRequiredService<TMemoryDbContext>());
          }
          
          // 注册 TMemoryDbContext 代替其他服务
          foreach(var dbContextType in options.ReplacedDbContextTypes)
          {
              services.Replace(ServiceDescriptor.Singleton(
                  dbContextType, 
                  sp => sp.GetRequiredService<TMemoryDbContext>()));
          }
          
          // 创建 memory_db_repository_registrar并注入 repositories
          new MemoryDbRepositoryRegistrar(options).AddRepositories();
  
          return services;
      }
  }
  
  ```

  * options

    ```c#
    // 直接派生封装基类
    public class AbpMemoryDbContextRegistrationOptions :
    	AbpCommonDbContextRgistrationOptions, 
    	IAbpMemoryDbContextRegistrationOptionsBuilder
    {
        public AbpMemoryDbContextRegistrationOptions(
            Type originalDbContextType, IServiceCollection services) 
                : base(originalDbContextType, services) 
                {                
                }                
    }
    
    ```

  * registrar

    ```c#
    public class MemoryDbRepositoryRegistrar : 
    	RepositoryRegistrarBase<AbpMemoryDbContextRegistrationOptions>
    {
        public MemoryDbRepositoryRegistrar(AbpMemoryDbContextRegistrationOptions options) : base(options)
        {        
        }
        // 实现获取 memory_db_context 中所有实体    
        protected override IEnumerable<Type> GetEntityTypes(Type dbContextType)
        {
            var memoryDbContext = (MemoryDbContext)Activator.CreateInstance(dbContextType);
            return memoryDbContext.GetEntityTypes();
        }
        // 实现创建 repository_type 方法，即 memoryDbRepository<TEntity>    
        protected override Type GetRepositoryType(Type dbContextType, Type entityType)
        {
            return typeof(MemoryDbRepository<,>)
                .MakeGenericType(dbContextType, entityType);
        }
    	// 实现创建 repository_type 方法，即 memoryDbRepository<TEntity,TKey>
        protected override Type GetRepositoryType(
            Type dbContextType, Type entityType, Type primaryKeyType)
        {
            return typeof(MemoryDbRepository<,,>)
                .MakeGenericType(dbContextType, entityType, primaryKeyType);
        }        
    }
    
    ```

    * memory db context

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

    * memory db repository

      ```c#
      public class MemoryDbRepository<TMemeoryDbContext, TEntity> : 
      	RepositoryBase<TEntity>, IMemoryDbRepository<TEntity>
      {
          /* 注入服务、解析 */
          public virtual IMemoryDatabaseCollection<TEntity> Collection => Database.Collection<TEntity>();
          public virtual IMemoryDatabase Database => DatabaseProvider.GetDatabase();
      	protected IMemoryDatabaseProvider<TMemoryDbContext> DatabaseProvider { get; }
          
          /* 属性注入 */
          // event_bus模块中定义，实现了 ISingletonDependency
          public ILocalEventBus LocalEventBus { get; set; } 
          public IDistributedEventBus DistributedEventBus { get; set; }
          // entity_event模块定义，实现了 ITransientDependency    
          public IEntityChangeEventHelper EntityChangeEventHelper { get; set; }
          // auditing模块中定义，实现了 ITransientDependency    
          public IAuditPropertySetter AuditPropertySetter { get; set; }
          // guids模块中定义，实现了 ITransientDependency    
          public IGuidGenerator GuidGenerator { get; set; }
              
          public MemoryDbRepository(IMemoryDatabaseProvider<TMemoryDbContext> databaseProvider)
          {
              // 注入服务
              DatabaseProvider = databaseProvider;
              // 创建默认null_implementation, 属性注入
              LocalEventBus = NullLocalEventBus.Instance;
              DistributedEventBus = NullDistributedEventBus.Instance;
              EntityChangeEventHelper = NullEntityChangeEventHelper.Instance;
              
              // guid_generator, audit_setter 没有null_implementation
              // 如果没有使用 autofac 用报异常
          }
          
          /* c r u d */
          /* 最终向 memoryDb.collection 增删改查 */
          public override async Task<TEntity> InsertAsync(
          	TEntity entity, bool autoSave = false,
          	CancellationToken cancellationToken = default)
          {
              await ApplyAbpConceptsForAddedEntityAsync(entity);
              Collection.Add(entity);
              return entity;
          }
              
          public override async Task DeleteAsync(
          	TEntity entity, bool autoSave = false,
          	CancellationToken cancellationToken = default)
          {
              await ApplyAbpConceptsForDeletedEntityAsync(entity);
              if(entity is ISoftDelete softDeleteEntity && !IsHardDeleted(entity))
              {
                  softDeleteEntity.IsDeleted = true;
                  Collection.Update(entity);
              }
              else
              {
                  Collection.Remove(entity);
              }
          }
              
          public override async Task<TEntity> UpdateAsync(
          	TEntity entity, bool autoSave = false,
          	CancellationToken cancellationToken = default)
          {
              /* 可以抽离成 ApplyAbpConceptsForUpdatedEntityAsync ???
              SetModificationAuditProperties(entity);        
              if(entity is ISoftDelete softDeleteEntity && softDeleteEntity.IsDeleted)
              {
                  SetDeletionAuditProperties(entity);
                  await TriggerEntityDelteEventAsync(entity);
              }
              else
              {
                  await TriggerEntityUpdateEventAsync(entity);
              }        
              await TriggerDomainEventsAsync(entity);
              */
              
              Collection.Update(entity);
              return entity;
          }
              
          public override Task<TEntity> FindAsync(
          	Expression<Func<TEntity,bool>> predicate, bool includeDetails=true,
          	CancellationToken cancellationToke = default)
          {
              return Task.FromResult(
                  GetQueryable().Where(predicate).SingleOrDefault());
          }
          
          public override Task<List<TEntity>> GetListAsync(
              bool includeDetails = false, 
              CancellationToken cancellationToken = deault)
          {
              return Task.FromResult(GetQueryable().ToList());
          }
              
          public override Task<long> GetCountAsync(CancellationToken cancellationToken = default)
          {
              return Task.FromResult(GetQueryable().LongCount());
          }
          
          public override Task<List<TEntity>> GetPagedListAsync(
          	int skipCount, int maxResultCount, string sorting,
          	bool includeDetails = false,
          	CancellationToken cancellationToken = default)
          {
              return Task.FromResult(GetQueryable()
                                     .OrderBy(sorting)
                                     .PageBy(skipCount, maxResultCount)
                                     .ToList());
          }                
      }
      
      ```

      crud操作过程类似，都是 设定audit_property, publish entity_event & domain_event, save_changes

      abp框架对不同的crud的上述过程做了封装：

      * added entity

        ```c#
        protected virtual async Task ApplyAbpConceptsForAddedEntityAsync(TEntity entity)
        {
            CheckAndSetId(entity);
            SetCreationAuditProperites(entity);
            await TriggerEntityCreateEvents(entity);
            await TriggerDomainEventsAsync(entity);
        }
        
        ```

      * deleted entity

        ```c#
        protected viurtual async Task ApplyAbpConceptsForDeletedEntityAsync(TEntity entity)
        {
            SetDeletionAuditProperties(entity);
            await TriggerEntityDeleteEventAsync(entity);
            await TriggerDomainEventsAsync(entity);
        }
        
        ```

      * updated entity 

        框架没有抽离封装，而是直接实现在update方法中了

        ```c#
        public override async Task<TEntity> UpdateAsync(
            TEntity entity, bool autoSave = false,
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
        }
        
        ```

      ```c#
      // 带有唯一主键的 repository，常用
      public class MemoryDbRepository<TMemoryDbContext,TEntity,TKey>
      {
          public MemoryDbRepository(
              IMemoryDatabaseProvider<TMemoryDbContext> databaseProvider)           
              	: base(databaseProvider)
              {
              }
          
          /* override crud with id */
          public override Task<TEntity> InsertAsync(
              TEntity entity, bool autoSave = true,
          	CancellationToken cancellationToken = default)
          {
              SetIdIfNeeded(entity);
              return base.InsertAsync(entity, autoSave, cancellationToken);
          }
          protected virtual void SetIdIfNeeded(TEntity entity)
          {
              // 如果 TKey 是 int、long、guid，使用 in_memory_generator
              if(typeof(TKey) == typeof(int) || typeof(TKey) == typeof(long) || typeof(TKey) == typeof(Guid))
              {
                  if(EntityHelper.HasDefaultId(entity))
                  {
                      EntityHelper.TrySetId(entity, 
                      	() => Database.GenerateNextId<TEntity,TKey>());
                  }
              }
          }
          
          public virtual async Task DeleteAsync(
              TKey id, bool autoSave = false,
              CancellationToken cancellationToken = default)
          {
              return DeleteAsync(x => x.Id.Equals(id), autoSave, cancellationToken);
          }
          
          // 查找，找不到返回 null
          public virtual async Task<TEntity> FindAsync(
              TKey id, bool includeDetails = true, 
          	CancellationToken cancellationToken = default)
          {
              return Task.FromResult(GetQueryable().FirstOrDefault(e => e.Id.Equals(id)));
          }
          // 查找，找不到抛出异常
          public virtual async Task<TEntity> GetAsync(
          	TKey id, bool includeDetails = true, 
          	CancellationToken cancellationToken = default)
          {
              var entity = await FindAsync(id, includeDetails, cancellationToken);
              if(entity == null)
              {
                  throw new EntityNotFoundException(typeof(TEntity), id);
              }
              return entity;
          }
      }
      
      ```

  * 模块

    ```c#
    public class AbpMemoryDbModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            // 注入 memory_db_provider，用于解析 memory_db 实例
            context.Services.TryAddTransient(
                typeof(IMemoryDatabaseProvider<>),
                typeof(UnitOfWorkMemoryDatabaseProvider<>));
            
    		// 注入 memory_db_collection，是 db 真正存储数据的容器
            // memory_db实例化时需要使用注入的 collection
            context.Services.TryAddTransient(
            	typeof(IMemoryDatebaseCollection<>),
            	typeof(MemoryDatabaseCollection<>));
        }
    }
    
    ```

    * 模块依赖`AbpDddDomainModule`，即依赖了很多基础设施

    * 注入了memory_db_provider

      ```c#
      public class UnitOfWorkMemoryDatabaseProvider<TMemoryDbContext>
      {
          public TMemoryDbContext DbContext { get; }
          private readonly IUnitOfWorkManager _unitOfWorkManager;
          private readonly IConnectionStringResolver _connectionStringResolver;
          private readonly MemoryDatabaseManager _memeoryDatabaseManager;
          public UnitOfWorkMemoryDatabaseProvider(
          	IUnitOfWorkManager unitOfWorkManager,
              IConnectionStringResolver connectionStringResolver,
              TMemoryDbContext dbContext, 
              MemoryDatabaseManager memoryDatabaseManager)
          {
              // memory_db模块中定义，ISingletonDependency
              DbContext = dbContext;
              
              // 用于使用 uow，uo模块中定义，ISingletonDependency
              _unitOfWorkManager = unitOfWorkManager;
              // 用于解析 connect_string，data模块中定义，ITransientDependency
              _connectionStringResolver = connectionStringResolver;
              // 用于创建 memory_db，memory_db模块中定义，ISingletonDependency
              _memoryDatabaseManager = memoryDatabaseManager;
          }
          
          public IMemoryDatabase GetDatabase()
          {
              // 获取 uow
              var unitOfWork = _unitOfWorkManager.Current;
              if (unitOfWork == null)
              {
                  throw new AbpException(...);                
              }
              // 获取 connect_string
              var connectionString = _connectionStringResolver
                  .Resolve<TMemoryDbContext>();
              // 获取 database_api 的 key
              var dbContextKey = $"{typeof(TMemoryDbContext).FullName}_{connectionString}";
              
              // 从 uow 中解析 memory_db
              // 如果没有则创建
              var databaseApi = unitOfWork.GetOrAddDatabaseApi(
                  dbContextKey,
                  () => new MemoryDbDatabaseApi(
                      _memoryDatabaseManager.Get(connectionString)));
      
              return ((MemoryDbDatabaseApi)databaseApi).Database;
          }
      }
      ```

      * memory_db_manager，解析memoryDb

        ```c#
        public class MemoryDatabaseManager : ISingletonDependency
        {
            private readonly ConcurrentDictionary<string, IMemoryDatabase> _databases 
                = new ConcurrentDictionary<string, IMemoryDatabase>();
            
            private readonly IServiceProvider _serviceProvider;    
            public MemoryDatabaseManager(IServiceProvider serviceProvider)
            {
                _serviceProvider = serviceProvider;
            }
            // 根据name解析 memory_db
            public IMemoryDatabase Get(string databaseName)           
            {
                return _databases.GetOrAdd(
                    databaseName, 
                    _ => serviceProvider.GetRequiredService<IMemoryDatabase>());
            }
        }
        
        ```

      * memory_db

        ```c#
        public class MemoryDatabase : IMemoryDatabase, ITransientDependency
        {
            private readonly ConcurrentDictionary<Type, object> _sets;
            private readonly ConcurrentDictionary<Type, InMemoryIdGenerator> _entityIdGenerators;
            private readonly IServiceProvider _serviceProvider;
            public MemoryDatabase(IServiceProvider serviceProvider)
            {
                _serviceProvider = serviceProvider;
                _sets = new ConcurrentDictionary<Type, object>();
                _entityIdGenerators = new ConcurrentDictionary<Type, InMemoryIdGenerator>();
            }
            
            public IMemoryDatabaseCollection<TEntity> Collection<TEntity>()
                where TEntity : class, IEntity
            {
                return _sets.GetOrAdd(typeof(TEntity), 
                    _ => _serviceProvider.
                    	GetRequiredService<IMemoryDatabaseCollection<TEntity>>()) as
                        IMemoryDatabaseCollection<TEntity>;
            }
        
            public TKey GenerateNextId<TEntity, TKey>()
            {
                return _entityIdGenerators
                    .GetOrAdd(typeof(TEntity), () => new InMemoryIdGenerator())
                    .GenerateNext<TKey>();
            }
        }
        
        ```

        

    

##### 2.2 ef core

bbb

##### 1.4 mongo db

ccc



