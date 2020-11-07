## about unit of work

#### 1. concept

abp框架实现了 unit_of_work

* `IUnitOfWork`是抽象接口

  ```c#
  public interface IUnitOfWork : 
  	IDatabaseApiContainer, 
  	ITransactionApiContainer,
  	IDisposable
  {
      // outer uow
      
          
  }
  
  ```

  * `IDatabaseApiContainer`是`IDatabaseApi`的容器，

    `IDatabaseApi`后者分离了database 的操作，是个空定义

    ```c#
    public interface IDatabaseApiContainer : IServiceProviderAccessor
    {
        IDatabaseApi FindDatabaseApi(string key);
        void AddDatabaseApi(string key, IDatabaseApi api);
        IDatabaseApi GetOrAddDatabaseApi(string key, Func<IDatabaseApi> factory);
    }
    
    ```

    ```c#
    public interface IDatabaseApi
    {    
    }
    
    ```

    * 统一了数据库操作的接口

    * `ISupportsSavingChanges`提交变更，根据 database 是否支持事务选择实现

      ```c#
      public interface ISupportsSavingChanges
      {
          Task SaveChangesAsync(CancellationToken cancellationToken);
      }
      
      ```

    * `ISupportsRollback`回滚，根据 database 是否支持事务选择实现

      ```c#
      public interface ISupportsRollback
      {
          void Rollback();
          Task RollbackAsync(CancellationToken cancellationToken);
      }
      
      ```

  * `ITransactionApiContainer`是`ITransactionApi`的容器，

    `ITransactionApi`分离了事务操作

    ```c#
    public interface ITransactionApiContainer
    {
        ITransactionApi FindTransactionApi(string key);
        void AddTransactionApi(string key, ITransactionApi api);
        ITransactionApi GetOrAddTransactionApi(string key, Func<ITransactionApi> factory);
    }
    
    ```

    ```c#
    public interface ITransactionApi : IDisposable
    {
        Task CommiteAsync();
    }
    
    ```

* `UnitOfWork`是 unit_of_work 的定义，自动注册为 transient

  ```c#
  public class UnitOfWork : IUnitOfWork, ITransientDependency
  {
      public Guid Id { get; } = Guid.NewGuid();
      
      public IServiceProvider ServiceProvider { get; }
      
      public IAbpUnitOfWorkOptions { get; private set; }
      private readonly AbpUnitOfWorkDefaultOptions _defaultOptions;
      
      //public IUnitOfWork Outer { get;private set; }
      
      //public Dictionary<string, object> Items { get; }
      
      private readonly Dictionary<string, IDatabaseApi> _databaseApis;
      private readonly Dictionary<string, ITransactionApi> _transactionApis;
      
      public UnitOfWork(
          IServiceProvider serviceProvider,
      	IOptions<AbpUnitOfWorkDefaultOptions> options)
      {
          ServiceProvider = serviceProvider;
          _defaultOptions = options.Value;
          
          _databaseApi = new Dictionary<string, IDatabaseApi>();
          _transactionApi = new Dictionary<string, ITransactionApi>();
          
          //Items = new Dictionary<string, object>();
      }
      
      // ...
  }
  
  ```

  * 实现`IDatabaseApiContainer`、`ITransactionApiContainer`

  * uow_complete

    ```c#
    public class UnitOfWork
    {
        public bool IsCompleted { get;private set; }
        private bool _isCompleting;
        
        public virtual async Task CompleteAsync(CancellationToke cancellationToken = default)
        {
            if(_isRolledback) return;	// 回滚后不再完成
            
            if(IsCompleted || _isCompleting)	// 防止多次提交完成
            {
                throw new AbpException("complete is called before");
            }	
            
            try
            {
                _isCompleting = true;
                await SaveChangesAsync(cancellationToken);		// db save changes
                await CommiteTransactionAsync();				// commit transaction
                IsCompleted = true;
                
                await OnCompletedAsync();	// 执行钩子动作            
            }
        }
    }
    
    ```

    * save changes

      ```c#
      public virtual async Task SaveChangesAsync(CancellationToken cancellationToken = default)
      {
          // 遍历 IDababaseApi 并调用其 SaveChangesAsync() 方法
      }
          
      ```

    * commit transaction

      ```c#
      public virtual async Task CommitTransactionAsync()
      {
          // 遍历 ITransactionApi 并调用其 roll CommitAsync() 方法
      }
      
      ```

    * complete 钩子

      ```c#
      public class UnitOfWork
      {
          proteced List<Func<Task>> CompletedHandlers { get; } = List<Func<Task>>();
          
          // 添加 completed 钩子句柄
          public void OnCompleted(Func<Task> handler)
          {
              CompletedHandlers.Add(handler);
          }
          
          // 执行钩子动作
          public virtual async Task OnCompletedAsync()
          {
              // ... 遍历 completed_handlers
              await handler.Invoke();
          }
      }
      
      ```

  * uow_rollback

    ```c#
    public class UnitOfWork
    {
        private bool _isRolledback;
        
        public virtual async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if(_isRolledback)
            {
                return;            
            }
            _isRolledback = true;
            await RollbackAllAsync(cancellationToken);
        }
    }
    
    ```

    ```c#
    protected virtual async Task RollbackAllAsync(CancellationToken cancellationToken)
    {
        // 遍历 IDatabaseApi 并调用 RollbackAsync() 方法
        // 遍历 ITransactionApi 并抵用 RollbackAsync() 方法
    }
    
    ```

  * uow_dispose

    ```c#
    public class UnitOfWork
    {
        public bool IsDispose { get;private set; }
        public virtual void Dispose()
        {
            if(IsDiposed)		// 防止多次 dispose
            {
                return;		
            }
            
            IsDispose = true;
            DisposeTransactions();		// dispose transaction
            if(!IsCompleted || _exception!=null)
            {
                OnFailed();             // 执行 failed 钩子
            }
            OnDisposed();        		// 执行 disposed 钩子
        }
    }
    
    ```

    * failed 钩子

      ```c#
      public class UnitOfWork
      {
          public event EventHandler<UnitOfWorkFailedEventArgs> Failed;
          
          protected virtual void OnFailed()
          {
              Failed.InvokeSafely(this, new UnitOfWorkFailedEventArgs(this, _exception, _isRolledback ));
          }
      }
      
      ```

    * disposed 钩子

      ```c#
      public class UnitOfWork
      {
          public event EventHandler<UnitOfWorkEventArgs> Disposed;
          
          protected virtual void OnDisposed()
          {
              Disposed.InvokeSately(this, new UnitOfWorkEventArgs(this));
          }
      }
      
      ```

  * outer uow，链式uow

    ```c#
    public class UnitOfWork
    {
        public IUnitOfWork Outer { get;set; }
        
        public virtual void SetOuter(IUnitOfWork outer)
        {
            Outer = outer;
        }
    }
    
    ```

  * items，参数字典

    ```c#
    public class UnitOfWork
    {
        public Dictionary<string, object> Items { get; }
    }
    
    public static class UnitOfWorkExtensions
    {
        public static void AddItem<TValue>(...) { ... }   
        public static TValue GetItemOrDefault<TValue>(...) { ... }
        public static TValue GetOrAddItem<TValue>(...) { ... }
        public static void RemoveItem(...) { ... }    
    }
    
    ```

* 使用`AmbientUnitOfWork`追踪 Uow 链中的当前 Uow（最外层Uow）

  自动注册的单例，且实现了IServiceAccessor

  ```c#
  public class AmbientUnitOfWork : IAmbientUnitOfWork, ISingletonDependency
  {
      public IUnitOfWork UnitOfWork => _currentUow.Value;
      
      private readonly AsyncLocal<IUnitOfWork> _currentUow;	// 线程变量的本地存储
      
      public AmbientUnitOfWork()
      {
          _currentUow = new AsyncLocal<IUnitOfWork>();
      }
      
      public void SetUnitOfWork(IUnitOfWork unitOfWork)
      {
          _currentUow.Value = unitOfWork;
      }
  }
  
  ```

* `UnitOfWorkManager`负责管理 unit_of_work，自动注入的单例

  ```c#
  public class UnitOfWorkManager : IUnitofWorkManager, ISingletonDependency
  {
      public IUnitOfWork Current => GetCurrentUnitOfWork();
      
      private readonly IServiceScopeFactory _serviceScopeFactory;
      private readonly IAmbientUnitOfWork _ambientUnitOfWork;
      
      public UnitOfWorkManager(
          IAmbientUnitOfWork ambientUnitOfWork,
          IServiceScopeFactory serviceScopeFactory)
      {
          _ambientUnitOfWork = ambientUnitOfWork;
          _serviceScopeFactory = serviceScopeFactory;
      }
      
      // ...
  }
  ```

  * 创建 uow

    ```c#
    public class UnitOfWork
    {
        public IUnitOfWork Begin(AbpUnitOfWorkOptions options, bool requiresNew = false)
        {
            Check.NotNull(options, nameof(options));
            
            var currentUow = Current;
            
            // 创建内嵌 unit_of_work
            if(currentUow != null && !requiresNew)
            {
                return new ChildUnitOfWork(currentUow);
            }
            // 创建 unit_of_work
            var unitOfWork = CreateNewUnitOfWork();
            unitOfWork.Initialize(options);
            
            return unitOfWork;
        }
    }
    
    ```

  * 创建reserve uow

    ```c#
    public class UnitOfWorkManager
    {
        public IUnitOfWork Reserve(AbpUnitOfWorkOptions options, bool = requiresNew = false)
        {
            Check.NotNull(reservationName, nameof(reservationName));
            
            // 找到 uow 并创建内嵌uow
            if(!requiresNew &&
              _ambientUnitOfWork.UnitOfWork != null &&
              _ambientUnitOfWork.UnitOfWork.IsReservedFor(reservationName))
            {
                return new ChildUnitOfWork(_ambientUnitOfWork.UnitOfWork);
            }
            // 创建uow并reserve
            var unitOfWork = CreateNewUnitOfWork();
            unitOfWork.Reserve(reservationName);
            
            return unitOfWork;
        }
    }
    ```

  * 冒泡寻找 reserve uow

    ```c#
    public class UnitOfWorkManager
    {
        public void BeginReserved(string reservationName, AbpUnitOfWorkOptions options)
        {
            if(!TryBeginReserved(reservationName, options))
            {
                throw new AbpExceptions($"Could not find a reserved unit of work with reservation name: {reservationName}");
            }
        }
        
        public bool TryBeginReserved(string reservationName, AbpUnitOfWorkOptions options)
        {
            Check.NotNull(reservationName, nameof(reservationName));
            
            var uow = _ambientUnitOfWork.UnitOfWork;
            
            while(uow != null && !uow.IsReservedFor(reservationName))
            {
                uow = uow.Outer;	// 冒泡搜索
            }
            
            if(uow == null)			// 没找到
            {
                return false;
            }
            
            uow.Initialize(options);	// 找到并配置
            return true;
        }
    }
    
    ```

* 拦截器

  abp框架定义了 uow 的拦截器，可以不用手动调用`Uow.CompleteAsync()`方法

  ```c#
  public class UnitOfWorkInterceptor : AbpInterceptor, ITransientDependency
  {
      private readonly IUnitOfWorkManager _unitOfWorkManager;
      private readonly AbpUnitOfWorkDefaultOptions _defaultOptions;
      
      public UnitOfWorkInterceptor(
          IUnitOfWorkManager unitOfWorkManager,
      	IOptions<AbpUnitOfWorkDefaultOptions> options)
      {
          _unitOfWorkManager = unitOfWorkManager;		// 注入
          _defaultOptions = options.Value;			// 注入
      }
      
      public async override Task InterceptAsync(IAbpMethodInvocation invocation)
      {
          // 判断方法是否需要uow
          // 并且创建 attribute （实现IUnitOfWorkEnable接口没有特性）
          if(!UnitOfWork.Helper.IsUnitOfWorkMethod(invocation.method, out var unitOfWorkAttribute))
          {
              await invocation.ProceedAsync();
              return;
          }
          
          // 创建 scope 并完成 method
          // 特性、options合并
          using(var uow = _unitOfWorkManager.Beging(CreateOptions(invoation, unitOfWorkAttribtue)))
          {
              await invocation.ProceedAsync();
              await uow.CompleteAsync();
          }
      }
  }
  ```

  * 标记方法是 uow 的

    * 通过特性标记，可以标记 class、method、interface

      ```c#
      public class MyClass
      {
          [UnitOfWork(IsTransactional=true, Timeout=20, IslolationLevel=IsolationLevel.Auto)]
          public void MyMethod(/**/) { /* ... */ }
      }
      
  ```
    
* 实现特定接口
    
  定义class级别使用 uow
    
  拦截器中确定 isolationLevel 等
    
      ```c#
      public class MyClass : IUnitOfWorkEnable
      {
          // ...
      }
  
    ```
    
  * 模块中注册了拦截器
  
    ```c#
    public class AbpUnitOfWorkModule : AbpModule
    {
        public override void PreConfigureService(ServiceConfigurationContext context)
        {
            context.Services.OnRegistred(UnitOfWorkInterceptorRegistrar.RegisterIfNeeded);
        }
    }
    
    ```
  
    

#### 2. how to use

* 依赖`AbpUnitOfWorkModule`
* 使用 uow
  * 注入`IUnitOfWorkManager`，手动 complete 方法
  * 标记 uow 特性或实现 接口，自动 complete 方法







 