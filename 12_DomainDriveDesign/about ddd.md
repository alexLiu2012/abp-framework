## about ddd

#### 1. concept

abp框架实现了ddd设计

#### 2. domain layer

##### 2.1 value object

* 定义 value_object，继承`ValueObject`

  ```c#
  public abstract class ValueObject
  {
      protected abstract IEnumerable<object> GetAtomicValues();
      
      // 重写了 equal 方法
      public bool ValueEqual(object obj) { /**/ }
  }
  
  ```

##### 2.2 entity

* entity

  抽象主键，可以是复合主键；派生的子类选优实现`GetKeys()`方法

  ```c#
  [Serializable]
  public abstract class Entity : IEntity
  {
      public abstract object[] GetKeys();
      
      public override string Tostring()
      {
          // ...
      }
      
      public bool EntityEquals(IEntity other)
      {
          return EntityHelper.EntityEquals(this, other);
      }    
  }

  ```

* entity<TKey>

  单一类型主键，常用

  ```c#
  [Serializable]
  public abstract class Entity<TKey> : Entity, IEntity<TKey>
  {
      // about key
      public virtual TKey Id { get; protected set; }
      public override object[] GetKeys()
      {
          return new object[] { Id };
  	}
      
      protected Entity() { }	// 无参的构造函数干什么用？？
      protected Entity(TKey id) { Id = id; }    
  }
  
  ```

* entity helper

  框架提供了helper类

  ```c#
  public static class EntityHelper
  {
      private static readonly ConcurrentDictionary<string, PropertyInfo> CachedIdProperties = 
          new ConcurrenctDictionary<string, PropertyInfo>();
      
      // 判断 entity 相同
      public static bool EntityEquals(IEntity entity1, IEntity entity2)
      {
          // ...
      }
      
      // 判断是 entity
      public static bool IsEntity([NotNull] Type type) { /**/ }
      // 判断是 entity<Tkey>
      public static bool IsEntityWithId([NotNull] Type type) { /**/}
      
      // 判断 entity 的 key 是默认值（即没有分配key，没有调用 saveChanges() 方法
      public static bool HasDefaultKeys(IEntity entity) { /**/ }
      // 判断 entity<TKey> 的 id 是默认值（即没有分配id，没有调用 saveChanges() 方法
      public static bool HasDefaultId<TKey>(IEntity<TKey> entity) { /**/ }    
      
      // 获取 entity<TKey> 中 TKey 的类型
      public static Type FindPrimaryKeyType<TEntity>() { /**/ }
      public static Type FindPrimaryKeyType([NotNull] Type entityType) { /**/ }
      
      // 判断 entity<TKey> 的 id 是否因为给定值，返回的是通用抽象 express
      public static Expression<Func<TEntity, bool>> CreateEqualityExpressionForId<TEntity, TKey> where TEntity : IEntity<TKey>
      {
          // ...
      }
      
      // 设置id
      public static void TrySetId(
          IEntity<TKey> entity, 
          Func<TKey> idFactory, 
          bool checkForDisableIdGenerationAttribute = false)
      {
          // ...
      }
  }
  
  ```

##### 2.3 aggregate root

aggregate_root 本质上也是 entity，仅用单一键说明

* basic aggregate root

  ```c#
  public abstract class BasicAggregateRoot<TKey> : Entity<TKey>, IAggregateRoot<TKey>, IGeneratesDomainEvents
  {
      protected BasicAggregateRoot() {}
      protected BasicAggregateRoot(TKey id) : base(id) {}
      
      // events 容器
      private readonly ICollection<object> _localEvents = new Collection<object>();
      private readonly ICollection<object> _distributedEvents = new Collection<object>();
     
      // events 增删
      public virtual IEnumerable<object> GetLocalEvents() { /**/ }
      public virtual IEnumerable<object> GetDistributedEvents() { /**/ }
      public virtual void ClearLocalEvents() { /**/ }
      public virtual void ClearDistributedEvents() { /**/ }
      public virtual void AddLocalEvent(object eventdata) { /**/ }
      public virtual void AddDistributedEvent(object eventdata){ /**/ }
  }
  
  ```

  * event 容器中的event 在 db save 相关操作时触发
  * 可能是使用 eventHelper
  * 触发后清空 event 容器？？
  * 使用abp框架定义的eventData

* aggregate root

  增加了'属性扩展'和'存储并发'

  ```c#
  [Serializable]
  public abstract class AggregateRoot<TKey> : BasicAggregateRoot<TKey>, IHasExtraProperties, IHasConcurrencyStamp
  {
      protected AggregateRoot()
      {
          ConcurrencyStamp = Guid.NewGuid().ToString("N");
          ExtraProperties = new ExtraPropertyDictionary();
          // IHasExtraProperties的扩展方法
          this.SetDefaultsForExtraProperties();
      }
      protected AggregateRoot(Tkey id) : base(id)
      {
          ConcurrencyStamp = Guid.NewGuid().ToString("N");
          ExtraProperties = new ExtraPropertyDictionary();
          // IHasExtraProperties的扩展方法
          this.SetDefaultsForExtraProperties();
      }
      
      // protperties 容器
      public virtual ExtraPropertyDictionary ExtraProperties { get; protected set; }
      // validate
      public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
      {
          // ...
      }    
  }
  
  ```

##### 2.3.1 generates domain events

entity 实现 `IGeneratesDomainEvents`接口，用来容纳 events

与 aggregate_root 相同，在 db_save 相关操作时触发

与aggregate_root重复，未来可能删除

```c#
public interface IGeneratesDomainEvents
{
    IEnumerable<object> GetLocalEvents();
    IEnumerable<object> GetDistributedEvents();
    void ClearLocalEvents();
    void ClearDistributedEvents();
}
```

##### 2.4 auditing entity

框架扩展了带有自动审计功能的entity

* auditing 元素（定义在abp_auditing命名空间中）

  * `ICreationAuditedObject`: CreationTime, CreatorId? 
  * `ICreationAuditedObject<TUser>`: CreationTime, CreatorId?,  T_Creator
  * `IModificationAuditedObject`: LastModificationTime, LastModificationId? 
  * `IModificationAuditedObject<TUser>`: LastModificationTime, LastModificationId? , T_LastModifier
  * `IDeletionAuditedObject`: DeletionTime, IsDeleted, DeletionId?
  * `IDeletionAuditedObject<TUser>`: DeletionTime, IsDeleted, DeletionId?, T_Deleter
  * `IAuditedObject`: ICreationAuditedObject, IModificationAuditedObject
  * `IAuditedObject<TUser>`: ICreationAuditedObject<TUser>, IModificationAuditedObject<TUser>, IAuditedObject
  * `IFullAuditedObject`: IAuditedObject, IDeletionAuditedObject<TUser>
  * `IFullAuditedObject<TUser>`: IAuditedObject<TUser>, IDeletionAuditedObject<TUser>, IFullAuditedObject

* audit_entity

  * `CreationAuditedEntity`: 
  * `CreationAuditedEntityWithUser`:
  * `AuditedEntity`
  * `AuditedEntityWithUser`
  * `FullAuditedEntity`
  * `FullAuditedEntityWithUser`

* audit_aggregate

  * `CreationAuditedAggregateRoot`
  * `CreationAuditedAggregateRootWithUser`
  * `AuditedAggregateRoot`
  * `AuditedAggregateRootWithUser`
  * `FullAuditedAggregateRoot`
  * `FullAuditedAggregateRootWithUser`

* 审计元素设置接口（在abp_auditing模块中定义）

  ```c#
  public class AuditPropertySetter : IAuditPropertySetter, ITransientDependency
  {
      // 注入服务
      protected ICurrentUser CurrentUser { get; }
      protected ICurrentTenant CurrentTenant { get; }
      protected IClock Clock { get; }
      public AuditPropertySetter(ICurrentUser currentUser, ICurrentTenant currentTenant, IClock clock)
      {
          CurrentUser = currentUser;
          CurrentTenant = currentTenant;
          Clock = clock;
      }
      
      // 实现对应审计元素接口的方法
      public void SetCreationProperties(object targetObject)
      {        
          SetCreationTime(targetObject);
          SetCreatorId(targetObject);
      }
      
      public void SetModificationProperties(object targetObject)
      {        
          SetLastModificationTime(targetObject);
          SetLastModifierId(targetObject);
      }
      
      public void SetDeletionProperties(object targetObject)
      {
          SetDeletionTime(targetObject);
          SetDeletionId(targetObject);
      }
      
      // 具体方法实现 。。。
  }
  
  ```

  * 由其他object调用上述方法
    * ef core 模块中的 saveChanges() 方法调用
    * mongodb 模块中 saveChanges() 方法会调用

##### 2.5 repository

abp框架定义了 repository 接口，并提供了抽象基类

派生类中要实现具体的 crud 方法

* basic repository base

  ```c#
  public abstract class BasicRepositoryBase<TEntity> : 
  	IBasicRepository<TEntity>,
  	ITransienDependency /* */
  {
      // 注入服务
      public IServiceProvider ServiceProvider { get;set; }
      public ICancellationTokenProvider CancellationTokenProvider { get;set; }
      public BasicRepositoryBase()
      {
          CancellationTokenProvider = NullCancellationTokenProvider.Instance;
      }
          
      // 抽象的 crud 方法
      public abstract Task<TEntity> InsertAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);
      public abstract Task DeleteAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);
      public abstract Task UpdateAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);
      
      public abstract Task<List<TEntity>> GetListAsync(bool includeDetails = false, CancellationToken cancellationToken = default);
      public abstract Task<long> GetCountAsync(CancellationToken cancellationToken = default);
      public abstract Task<List<TEntity>> GetPagedListAsync(int skipCount, int maxResultCount, string sorting, bool includeDetails = false, CancellationToken cancellationToken = default);        
  }
  
  ```

  ```c#
  public abstract class BasicRepositoryBase<TEntity, TKey> : 
  	BasicRepositoryBase<TEntity>,
  	IBasicRepository<TEntity, TKey>
  {
      // get 方法, 如果没有找到对应 entity 则会抛出异常（可以重写）
      public virtual async Task<TEntity> GetAsync(TKey id, bool includeDetails = true, CancellationToken cancellatinToken = default)
      {
          var entity = await FindAsync(id, includeDetails, cancellationToken);
      }
      
      // 抽象的 find 方法，找不到不应该抛出异常，应该为null
      public abstract Task<TEntity> FindAsync(TKey id, bool includeDetails = true, CancellationToken cancellationToken = default);
        
      // delete by id        
      public virtual async Task DeleteAsync(TKey id, bool autoSave = false, CancellationToken cancellationToken = default)
      {
          var entity = await FindAsync(id, cancellationToken: cancellationToken);
          if(entity == null)
          {
              return;
          }
          await DeleteAsync(entity, autoSave, cancellationToken);
  	}
  }
  
  ```

* repository base

  实现了`IQueryable<TEntity>` 和数据过滤，需要 数据库 支持（ef core可以，mongodb未知）

  一般使用 repository_base
  
  ```c#
  public abstract clqass RepositoryBase<TEntity> : 
  	BasicRepositoryBase<TEntity>,
  	IRepository<TEntity>,
  	IUnitOfWorkManagerAccessor
  {
      // 需要在派生类中注入？
      public IDataFilter DataFiler { get;set; }
      public ICurrentTenant CurrentTenant { get;set; }
          
      public IAsyncQueryableExecuter AsyncExecuter { get;set; }
      public IUnitOfManager UnitOfWorkManager { get;set; }
          
      // queryable
      public virtual Type ElementType => GetQeuryable().ElementType;
      public virtual Expression Expression => GetQueryable().Expression;
      public virtual IQueryProvider Provider => GetQueryable().Provider;
          
      public virtual IQueryable<TEntity> WithDetails() => GetQueryable();
      public virtual IQuaryable<TEntity> WithDetails(params Expression<Func<TEntity,object>>[] propertySelectory) => GetQueryable();
      
      // 抽象的 get queryable 方法
      protected abstract IQuerable<TEntity> GetQueryable();
      
      // 数据过滤
      protected virtual TQueryable ApplyDataFilters<TQueryable>(TQueryable query)
      {
          // 过滤 isoftdelete
          if(typeof(ISoftDelete).IsAssignableFrom(typeof(IEntity)))
          {
              query = (TQueryable)query.WhereIf(
              	DataFilter.IsEnabled<ISoftDelete>(), 
                  e => ((ISoftDelete)e).IsDeleted == false);
          }
          // 过滤 tenant
          if(typeof(IMultiTenant).IsAssignableFrom(typeof(TEntity)))
          {
              var tenantId = CurrentTenant.Id;
              query = (TQueryable)query.WhereIf(
                  DataFilter.IsEnabled<IMultiTenant>(), 
                  e => ((IMultiTenant)e).TenantId == tenantId);
          }
          
          return query;
      }
          
      // ...
  }
        
  ```
  
  ```c#
  public abstract class RepositoryBase<TEntity, TKey> : 
  	RepositoryBase<TEntity>,
  	IRepository<TEntityt, TKey>
  {
      // 抽象get方法
          
      // 抽象find方法
      
      // 抽象delete方法                                  
  }

  ```
  
* 扩展的 repository 方法

  ```c#
  public static class RepositoryAsyncExtensions
  {
      // contains
      
      // any/all
      
      // count/long_count
      
      // first/first_or_default
      
      // last/last_or_default
      
      // single/single_or_default
      
      // min, max, sum, average
      
      // ToList/Array       
  }
  
  ```

##### 2.6 domain service

就是 domain service

```c#
public interface IDomainService : ITransientDependency
{    
}

```

```c#
public abstract class DomainService : IDomainService
{
    // 用于解析服务，必须指定，否则抛异常
    // 常用 属性注入
    public IServiceProvider ServiceProvider { get;set; }
    
    /* 懒加载方法，冲突锁 */
    protected readonly object ServiceProviderLock = new object();
    protected TService LazyGetReuiredService<TService>(ref TService reference)
    {
        if(reference == null)
        {
            lock(ServiceProviderLock)
            {
                if(reference == null)
                {
                    reference = ServiceProvider.GetRequiredService<TService>();
                }
            }
        }
    }
    
    /* 注入一些基础服务 */
    private IClock _clock;
    protected IClock Clock => LazyGetRequiredService(ref _clock);
    
    private ILoggerFactory _loggerFactory;    
    protected ILoggerFactory LoggerFactory => 
        LazyGetRequiredService(ref _loggerFactory);
    
    private Lazy<ILogger> _lazyLogger => 
        new Lazy<ILogger>(() => 
        	LoggerFactory?.CreateLogger(GetType().FullName) 
            	?? NullLogger.Instance, true);
    protected ILogger Logger => _lazyLogger.Value;

    private ICurrentTenant _currentTenant;
    protected ICurrentTenant CurrentTenant => 
        LazyGetRequiredService(ref _currentTenant);
        
    private IAsyncQueryableExecuter _asyncExecuter;    
    protected IAsyncQueryableExecuter AsyncExecuter => 
        LazyGetRequiredService(ref _asyncExecuter);
    
    public IGuidGenerator GuidGenerator { get; set; }
    protected DomainService()
    {
        GuidGenerator = SimpleGuidGenerator.Instance;
    }                    
}

```

* 必须指定 `IServiceProvider`，通常使用`autofac`属性注入
* `IAsyncQueryableExecuter`提供了异步查询功能

##### 2.7 specification

abp框架实现了规约模式

* ISpecification -> Specification -> derived specification

  ```c#
  public interface ISpecification<T>
  {
      bool IsSatisfiedBy(T Obj);
      Expression<Func<T, bool>> ToExpression();
  }
  
  ```

  ```c#
  public abstract class Specification<T> : ISpecification<T>
  {
      // 在派生 specification 中实现 to_expression
      public abstract Expression<Func<T, bool>> ToExpression();
      
      public virtual bool IsSatisfiedBy(T Obj)
      {
          return ToExpression().Compile()(obj);
      }
      public static implicit operator Expression<Func<T, bool>>(
          Specification<T> specification)
      {
          return specification.ToExpression();
      }            
  }
  
  ```

  * `NoneSpecification` ( always false )
  * `AnySpecification` ( always true )

* ICompositeSpecification -> CompositeSpecification -> derived CompositeSpecification

  ```c#
  public interface ICompositeSpecification<T> : ISpecification<T>
  {
      ISpecification<T> Left { get; }
      ISpecification<T> Right { get; }
  }
  
  ```

  ```c#
  public abstract class CompositeSpecification<T> : 
  	Specification<T>, ICompositeSpecification<T>
  {
      public ISpecification<T> Left { get; }
      public ISpecification<T> Right { get; }
      protected CompositeSpecification(
          ISpecification<T> left, ISpecification<T> right)
      {
          Left = left;
          Right = right;
      }
  }
  
  ```

* 表达式

  * and

    ```c#
    public class AndSpecification<T> : CompositeSpecification<T>
    {
        public AndSpecification(ISpecification<T> left, ISpecification<T> right)
            : base(left, right)
        {        
        }
        
        public override Expression<Func<T, bool>> ToExpression()
        {
            return Left.ToExpression().And(Right.ToExpression());
        }
    }
    ```

  * or

    ```c#
    public class OrSpecification<T> : CompositeSpecification<T>
    {
        public OrSpecification(ISpecification<T> left, ISpecification<T> right)
            : base(left, right)
        {        
        }
        
        public override Expression<Func<T, bool>> ToExpression()
        {
            return Left.ToExpression().Or(Right.ToExpression());
        }
    }
    ```

  * not

    ```c#
    public class NotSpecification<T> : Specification<T>
    {
        private readonly ISpecification<T> _specification;
        public NotSpecification(ISpecification<T> specification)
        {
            _specification = specification;
        }
        
        public override Expression<Func<T, bool>> ToExpression()
        {
            var expression = _specification.ToExpression();
            return Expression.Lambda<Func<T, bool>>(
            	Expression.Not(expression.Body),
            	expression.Parameters);
        }
    }
    ```

  * and_not

    pass "left" and not pass "right"

    ```c#
    public class AndNotSpecification<T> : CompositeSpecification<T>
    {
        public AndNotSpecification(ISpecification<T> left, ISpecification<T> right) 
            : base(left, right)
        {
        }
        
        public override Expression<Func<T, bool>> ToExpression()
        {
            var rightExpression = Right.ToExpression();
            
            var bodyNot = Expression.Not(rightExpression.Body);
            var bodyNotExpression = Expression.Lambda<Func<T, bool>>(
                bodyNot, rightExpression.Parameters);
    
            return Left.ToExpression().And(bodyNotExpression);
        }
    }
    ```

  * extension

    specification可以扩展 and，or，not 运算

    ```c#
    public static class SpecificationExtensions
    {
        public static ISpecification<T> And<T>(ISpecification<T> other) { /* */ }
        public static ISpecification<T> Or<T>(ISpecification<T> other) { /* */ }
        public static ISpecification<T> Not<T>(ISpecification<T> specification) { /* */ }
        public static ISpecification<T> AndNot<T>(ISpecification<T> other) { /* */ }
    }
    ```

#### 3. application layer



##### 1.8 application 



* db_context 实现

  * memory db

    ```c#
    public static class AbpMemoryDbServiceCollectionExtensions
    {
        public static IServiceCollection AddMemoryDbContext<TMemoryDbContext>(
        	IServiceCollection services,
        	Action<IAbpMemoryDbContextRegistrationOptionsBuilder> optionsBuilder)
            where TMemoryDbContext : MemoryDbContext
        {
            var options = new AbpMemoryDbcontextRegistrationOptions(
                typeof(TMemoryDbContext), services);
            optionsBuilder?.Invoke(options);
            // 注入 db_context
            if(options.DefaultRepositoryDbContextType != typeof(TMemoryDbContext))
            {
                services.TryAddSingleton(
                    options.DefaultRepositoryDbContextType,
                	sp => sp.GetRequiredService<TMemoryDbContext>());
            }
            // 替换 db_context，如果有
            foreach(var dbContextType in options.ReplaceDbContextTypes)
            {
                services.ReplaceDescriptor.Singleton(dbContextType, 
                	sp => sp.GetRequiredService<TMemeoryDbContext>()));
            }
            // 注册 repository
            new MemoryDbRepositoryRegistrar(options).AddRepositories();
        }
    }
    
    ```

  

  

