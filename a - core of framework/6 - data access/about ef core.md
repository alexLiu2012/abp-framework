## about ef core

相关程序集：

* Volo.Abp.EntityFrameworkCore

----

### 1. about



### 2. details

#### 2.1 注册 ef core db

* 实现 ef core db 自动注册

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
    public Dictionary<Type, object> AbpEntityOptions { get; }
    
    public AbpDbContextRegistrationOptions(
        Type originalDbContextType, 
        IServiceCollection services)            
        	: base(originalDbContextType, services)
    {
        AbpEntityOptions = new Dictionary<Type, object>();
    }
    
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

##### 2.1.2 ef core repos registrar

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

##### 2.1.3 add abp (ef core) dbContext

```c#
public static class AbpEfCoreServiceCollectionExtensions
{
    public static IServiceCollection AddAbpDbContext<TDbContext>(        
        this IServiceCollection services,
        Action<IAbpDbContextRegistrationOptionsBuilder> optionsBuilder = null)        	
        	where TDbContext : AbpDbContext<TDbContext>
    {
        services.AddMemoryCache();
        
        var options = new AbpDbContextRegistrationOptions(
            typeof(TDbContext), 
            services);
        optionsBuilder?.Invoke(options);
        
        services.TryAddTransient(
            DbContextOptionsFactory.Create<TDbContext>);
        
        foreach (var dbContextType in options.ReplacedDbContextTypes)
        {
            services.Replace(
                ServiceDescriptor.Transient(
                    dbContextType, 
                    typeof(TDbContext)));
        }
        
        new EfCoreRepositoryRegistrar(options).AddRepositories();
        
        return services;
    }
}

```



#### 2.2 abp db context

##### 2.2.1 IEfCoreDbContext 接口

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

###### 2.2.1.1 add and add range

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

###### 2.2.1.2 attach and attach range

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

###### 2.2.1.3  remove and remove range

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

###### 2.2.1.4 update and update range

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

###### 2.2.1.5 find

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

###### 2.2.1.6 save changes

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

##### 2.2.2 IAbpEfCoreDbContext 接口

* abp 框架扩展的 (ef core) dbContext 接口

```c#
public interface IAbpEfCoreDbContext : IEfCoreDbContext
{
    void Initialize(
        AbpEfCoreDbContextInitializationContext initializationContext);
}

```

##### 2.2.3 AbpDbContext 实现

* abp 框架定义的 abpDbContext 实现

```c#
public abstract class AbpDbContext<TDbContext> 
    : DbContext, 
	  IAbpEfCoreDbContext, 
	  ITransientDependency        
          where TDbContext : DbContext
{
    protected virtual Guid? CurrentTenantId => 
        CurrentTenant?.Id;    
    protected virtual bool IsMultiTenantFilterEnabled => 
        DataFilter?.IsEnabled<IMultiTenant>() ?? false;    
    protected virtual bool IsSoftDeleteFilterEnabled => 
        DataFilter?.IsEnabled<ISoftDelete>() ?? false;
    
    public ICurrentTenant CurrentTenant { get; set; }
    
    public IGuidGenerator GuidGenerator { get; set; }
    
    public IDataFilter DataFilter { get; set; }
    
    public IEntityChangeEventHelper EntityChangeEventHelper { get; set; }
    
    public IAuditPropertySetter AuditPropertySetter { get; set; }
    
    public IEntityHistoryHelper EntityHistoryHelper { get; set; }
    
    public IAuditingManager AuditingManager { get; set; }
    
    public IUnitOfWorkManager UnitOfWorkManager { get; set; }
    
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
    
    protected AbpDbContext(DbContextOptions<TDbContext> options) : base(options)
    {
        // 属性注入
        GuidGenerator = SimpleGuidGenerator.Instance;
        EntityChangeEventHelper = NullEntityChangeEventHelper.Instance;
        EntityHistoryHelper = NullEntityHistoryHelper.Instance;
        Logger = NullLogger<AbpDbContext<TDbContext>>.Instance;
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        TrySetDatabaseProvider(modelBuilder);
        
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
    
    protected virtual void TrySetDatabaseProvider(ModelBuilder modelBuilder)
    {
        var provider = GetDatabaseProviderOrNull(modelBuilder);
        if (provider != null)
        {
            modelBuilder.SetDatabaseProvider(provider.Value);
        }
    }
    
    protected virtual EfCoreDatabaseProvider? 
        GetDatabaseProviderOrNull(ModelBuilder modelBuilder)
    {        
        switch (Database.ProviderName)	// from dbContext？？
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
    
    public async override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auditLog = AuditingManager?.Current?.Log;
            
            List<EntityChangeInfo> entityChangeList = null;
            if (auditLog != null)
            {
                entityChangeList = EntityHistoryHelper.
                    CreateChangeList(ChangeTracker.Entries().ToList());
            }
            
            var changeReport = ApplyAbpConcepts();
            
            var result = await base.SaveChangesAsync(
                acceptAllChangesOnSuccess, 
                cancellationToken);
            
            await EntityChangeEventHelper.TriggerEventsAsync(changeReport);
            
            if (auditLog != null)
            {
                EntityHistoryHelper.UpdateChangeList(entityChangeList);
                auditLog.EntityChanges.AddRange(entityChangeList);
                Logger.LogDebug($"Added {entityChangeList.Count} entity changes to the current audit log");
            }
            
            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new AbpDbConcurrencyException(ex.Message, ex);
        }
        finally
        {
            ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }
    
        /// <summary>
        /// This method will call the DbContext <see cref="SaveChangesAsync(bool, CancellationToken)"/> method directly of EF Core, which doesn't apply concepts of abp.
        /// </summary>
    public virtual Task<int> SaveChangesOnDbContextAsync(
        bool acceptAllChangesOnSuccess, 
        CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess, 
            cancellationToken);
    }
    
    public virtual void Initialize(
        AbpEfCoreDbContextInitializationContext initializationContext)
    {
        if (initializationContext
            	.UnitOfWork.Options.Timeout.HasValue &&
            Database.IsRelational() &&
            !Database.GetCommandTimeout().HasValue)
        {
            Database.SetCommandTimeout(
                TimeSpan.FromMilliseconds(
                    initializationContext.UnitOfWork.Options.Timeout.Value));
        }
        
        ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
        
        ChangeTracker.Tracked += ChangeTracker_Tracked;
    }
    
    protected virtual void ChangeTracker_Tracked(
        object sender, 
        EntityTrackedEventArgs e)
    {
        FillExtraPropertiesForTrackedEntities(e);
    }
    
    protected virtual void FillExtraPropertiesForTrackedEntities(
        EntityTrackedEventArgs e)
    {
        var entityType = e.Entry.Metadata.ClrType;
        if (entityType == null)
        {
            return;
        }
        
        if (!(e.Entry.Entity is IHasExtraProperties entity))
        {
            return;
        }
        
        if (!e.FromQuery)
        {
            return;
        }
        
        var objectExtension = ObjectExtensionManager.Instance.GetOrNull(entityType);
        if (objectExtension == null)
        {
            return;
        }
        
        foreach (var property in objectExtension.GetProperties())
        {
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
            
            var currentValue = e.Entry.CurrentValues[property.Name];
            if (currentValue != null)
            {
                entity.ExtraProperties[property.Name] = currentValue;
            }
        }
    }
    
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
    
    protected virtual void HandleExtraPropertiesOnSave(EntityEntry entry)
    {
        if (entry.State.IsIn(EntityState.Deleted, EntityState.Unchanged))
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
        
        var objectExtension = ObjectExtensionManager.Instance.GetOrNull(entityType);
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
            
            if (entryProperty.Metadata.ClrType == entityProperty.GetType())
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
    
    
    
    
    
    
    
    
    
    protected virtual void UpdateConcurrencyStamp(EntityEntry entry)
    {
        var entity = entry.Entity as IHasConcurrencyStamp;
        if (entity == null)
        {
            return;
        }
        
        Entry(entity)
            .Property(x => x.ConcurrencyStamp).OriginalValue = entity.ConcurrencyStamp;
        entity.ConcurrencyStamp = Guid.NewGuid().ToString("N");
    }
    
    protected virtual void SetConcurrencyStampIfNull(EntityEntry entry)
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
        
        ConfigureGlobalFilters<TEntity>(modelBuilder, mutableEntityType);
    }
    
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
                modelBuilder.Entity<TEntity>().HasQueryFilter(filterExpression);
            }
        }
    }
    
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
            
            var dateTimeValueConverter = new AbpDateTimeValueConverter(Clock);
            
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

###### 2.2.3.1 initialize

###### aaa. set audit property

```c#
protected virtual void CheckAndSetId(EntityEntry entry)
{
    if (entry.Entity is IEntity<Guid> entityWithGuidId)
    {
        TrySetGuidId(entry, entityWithGuidId);
    }
}

protected virtual void TrySetGuidId(EntityEntry entry, IEntity<Guid> entity)
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
        dbGeneratedAttr.DatabaseGeneratedOption != DatabaseGeneratedOption.None)
    {
        return;
    }
    
    EntityHelper.TrySetId(
        entity,
        () => GuidGenerator.Create(),
        true);
}


protected virtual void SetCreationAuditProperties(EntityEntry entry)
    {
        AuditPropertySetter?.SetCreationProperties(entry.Entity);
    }
    
    protected virtual void SetModificationAuditProperties(EntityEntry entry)
    {
        AuditPropertySetter?.SetModificationProperties(entry.Entity);
    }
    
    protected virtual void SetDeletionAuditProperties(EntityEntry entry)
    {
        AuditPropertySetter?.SetDeletionProperties(entry.Entity);
    }
```

###### bbb. event

```c#
protected virtual void AddDomainEvents(EntityChangeReport changeReport, object entityAsObj)
    {
        var generatesDomainEventsEntity = entityAsObj as IGeneratesDomainEvents;
        if (generatesDomainEventsEntity == null)
        {
            return;
        }
        
        var localEvents = generatesDomainEventsEntity.GetLocalEvents()?.ToArray();
        if (localEvents != null && localEvents.Any())
        {
            changeReport.DomainEvents.AddRange(localEvents.Select(eventData => new DomainEventEntry(entityAsObj, eventData)));
            generatesDomainEventsEntity.ClearLocalEvents();
        }
        
        var distributedEvents = generatesDomainEventsEntity.GetDistributedEvents()?.ToArray();
        if (distributedEvents != null && distributedEvents.Any())
        {
            changeReport.DistributedEvents.AddRange(distributedEvents.Select(eventData => new DomainEventEntry(entityAsObj, eventData)));
            generatesDomainEventsEntity.ClearDistributedEvents();
        }
    }
```



###### 2.2.3.2 insert

```c#
protected virtual void ApplyAbpConceptsForAddedEntity(EntityEntry entry, EntityChangeReport changeReport)
    {
        CheckAndSetId(entry);
        SetConcurrencyStampIfNull(entry);
        SetCreationAuditProperties(entry);
        changeReport.ChangedEntities.Add(new EntityChangeEntry(entry.Entity, EntityChangeType.Created));
    }
```



###### 2.2.3.3 delete

```c#
protected virtual void ApplyAbpConceptsForDeletedEntity(EntityEntry entry, EntityChangeReport changeReport)
    {
        if (TryCancelDeletionForSoftDelete(entry))
        {
            UpdateConcurrencyStamp(entry);
            SetDeletionAuditProperties(entry);
        }
        
        changeReport.ChangedEntities.Add(new EntityChangeEntry(entry.Entity, EntityChangeType.Deleted));
    }
    
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



###### 2.2.3.4 update

```c#
protected virtual void ApplyAbpConceptsForModifiedEntity(EntityEntry entry, EntityChangeReport changeReport)
    {
        UpdateConcurrencyStamp(entry);
        SetModificationAuditProperties(entry);
        
        if (entry.Entity is ISoftDelete && entry.Entity.As<ISoftDelete>().IsDeleted)
        {
            SetDeletionAuditProperties(entry);
            changeReport.ChangedEntities.Add(new EntityChangeEntry(entry.Entity, EntityChangeType.Deleted));
        }
        else
        {
            changeReport.ChangedEntities.Add(new EntityChangeEntry(entry.Entity, EntityChangeType.Updated));
        }
    }
```



###### 2.2.3.5 get or find

```c#

```

#### 2.3 ef core repository

##### 2.3.1 EfCoreRepo 接口

```c#
public interface IEfCoreRepository<TEntity> 
    : IRepository<TEntity>        
        where TEntity : class, IEntity
{
    DbContext DbContext { get; }    
    DbSet<TEntity> DbSet { get; }
}

public interface IEfCoreRepository<TEntity, TKey> 
    : IEfCoreRepository<TEntity>, IRepository<TEntity, TKey>        
        where TEntity : class, IEntity<TKey>
{    
}

```

##### 2.3.2 EfCoreRepo(TEntity)

```c#
public class EfCoreRepository<TDbContext, TEntity> 
    : RepositoryBase<TEntity>, 
	  IEfCoreRepository<TEntity>, 
	  IAsyncEnumerable<TEntity>        
          where TDbContext : IEfCoreDbContext        
          where TEntity : class, IEntity
{
    DbContext IEfCoreRepository<TEntity>.DbContext => 
        DbContext.As<DbContext>();
              
    public virtual DbSet<TEntity> DbSet => 
        DbContext.Set<TEntity>();        
    
    protected virtual TDbContext DbContext => 
        _dbContextProvider.GetDbContext();
    
    protected virtual AbpEntityOptions<TEntity> AbpEntityOptions => 
        _entityOptionsLazy.Value;
    
    private readonly IDbContextProvider<TDbContext> _dbContextProvider;
    private readonly Lazy<AbpEntityOptions<TEntity>> _entityOptionsLazy;
    
    public virtual IGuidGenerator GuidGenerator { get; set; }
    
    public EfCoreRepository(IDbContextProvider<TDbContext> dbContextProvider)
    {
        _dbContextProvider = dbContextProvider;
        
        GuidGenerator = SimpleGuidGenerator.Instance;
        
        _entityOptionsLazy = new Lazy<AbpEntityOptions<TEntity>>(
            () => ServiceProvider
            	.GetRequiredService<IOptions<AbpEntityOptions>>()
            		.Value.GetOrNull<TEntity>() 
            			?? AbpEntityOptions<TEntity>.Empty);
    }
    
                                
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

###### 2.3.2.1 insert

```c#
public class EfCoreRepository<TDbContext, TEntity> 
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
    
    public async override Task<TEntity> InsertAsync(
        TEntity entity, 
        bool autoSave = false, 
        CancellationToken cancellationToken = default)
    {
        CheckAndSetId(entity);
        
        var savedEntity = DbSet.Add(entity).Entity;
        
        if (autoSave)
        {
            await DbContext.SaveChangesAsync(
                GetCancellationToken(cancellationToken));
        }
        
        return savedEntity;
    }
}

```

###### 2.3.2.2 delete

```c#
public class EfCoreRepository<TDbContext, TEntity> 
{
    public async override Task DeleteAsync(
        TEntity entity, 
        bool autoSave = false, 
        CancellationToken cancellationToken = default)
    {
        DbSet.Remove(entity);
        
        if (autoSave)
        {
            await DbContext.SaveChangesAsync(
                GetCancellationToken(cancellationToken));
        }
    }
    
    public async override Task DeleteAsync(
        Expression<Func<TEntity, bool>> predicate, 
        bool autoSave = false, 
        CancellationToken cancellationToken = default)
    {
        var entities = await GetQueryable()
            .Where(predicate)
            .ToListAsync(GetCancellationToken(cancellationToken));
        
        foreach (var entity in entities)
        {
            DbSet.Remove(entity);
        }
        
        if (autoSave)
        {
            await DbContext.SaveChangesAsync(
                GetCancellationToken(cancellationToken));
        }
    }
}
```

###### 2.3.2.3 update

```c#
public class EfCoreRepository<TDbContext, TEntity> 
{
    public async override Task<TEntity> UpdateAsync(
        TEntity entity, 
        bool autoSave = false, 
        CancellationToken cancellationToken = default)
    {
        DbContext.Attach(entity);
        
        var updatedEntity = DbContext.Update(entity).Entity;
        
        if (autoSave)
        {
            await DbContext.SaveChangesAsync(
                GetCancellationToken(cancellationToken));
        }
        
        return updatedEntity;
    }
}
```



###### 2.3.2.4 get or find

```c#
public class EfCoreRepository<TDbContext, TEntity> 
{
    public override IQueryable<TEntity> WithDetails()
    {
        if (AbpEntityOptions.DefaultWithDetailsFunc == null)
        {
            return base.WithDetails();
        }
        
        return AbpEntityOptions.DefaultWithDetailsFunc(GetQueryable());
    }
    
    public override IQueryable<TEntity> WithDetails(
        params Expression<Func<TEntity, 
        object>>[] propertySelectors)
    {
        var query = GetQueryable();
        
        if (!propertySelectors.IsNullOrEmpty())
        {
            foreach (var propertySelector in propertySelectors)
            {
                query = query.Include(propertySelector);
            }
        }
        
        return query;
    }
    
    public async override Task<TEntity> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        return includeDetails
            ? await WithDetails()
            	.Where(predicate)
            	.SingleOrDefaultAsync(
            		GetCancellationToken(cancellationToken))
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
            ? await WithDetails().ToListAsync(
            	GetCancellationToken(cancellationToken))
            : await DbSet.ToListAsync(
                GetCancellationToken(cancellationToken));
    }
    
    public async override Task<long> GetCountAsync(
        CancellationToken cancellationToken = default)
    {
        return await DbSet.LongCountAsync(
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
            .PageBy(skipCount, maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
}
```

##### 2.3.3 EFCoreRepo(TEntity, TKey)

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

###### 2.3.3.1 delete

```c#
public class EfCoreRepository<TDbContext, TEntity, TKey> 
{
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

###### 2.3.3.2 find

```c#
public class EfCoreRepository<TDbContext, TEntity, TKey> 
{
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

###### 2.3.3.3 get

```c#
public class EfCoreRepository<TDbContext, TEntity, TKey> 
{
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

##### 2.3.4 entity options

###### 2.3.4.1  EntityOptions

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

###### 2.3.4.2 EntityOptions(TEntity)

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





### 3. practice

