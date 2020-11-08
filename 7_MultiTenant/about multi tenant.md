## about multi tenant

#### 1. concept

abp框架实现了 multi_tenant

##### 1.1 存储 tenant 

* `ITenantStore`是存储 tenant 的抽象定义

  ```c#
  public interface ITenantStore
  {
      TenantConfiguration Find(string name);
      TenantConfiguration Find(Guid id);
      
      Task<TenantConfiguration> FindAsync(string name);
      Task<TanantConfiguration> FindAsync(Guid id);
  }
  
  ```

  `TenantConfiguraion`是可以序列化的 tenant 信息

  ```c#
  [Serializable]
  public class TenantConfiguration
  {
      public Guid Id { get;set; }
      public string Name { get;set; }
      public ConnectionStrings ConnectionStrings { get;set; }
      
      public TenantConfiguration()
      {        
      }
      
      public TenantConfiguration(Guid id, [NotNull] string name)
      {
          Check.NotNull(name, nameof(name));
          Id = id;
          Name = name;
          ConnetionStrings = new ConnectionStrings();
      }
  }
  
  ```

  * 默认实现，基于`IConfiguration`

    ```c#
    [Dependency(TryRegister = true)]
    public class DefaultTenantStore : ITenantStore, ITransientDependency
    {
        // 注入 abpDefaultTenantStoreOptions
        prviate readonly AbpDefaultTenantStoreOptions _options;
        public DefaultTenantStore(IOptionsSnapshot<AbpDefaultTenantStoreOptions> options)
        {
            _option = options.Value;
        }
        
        public TenantConfiguration Find(string name)
        {
            return _options.Tenant?.FirstOrDefault(t => t.Name == name);
        }
        public TenantConfiguration Find(Guid id)
        {
            return _options.Tenant?.FirstOrDefault(t => t.Id == id);
        }
        
        public Task<TenantConfiguration> FindAsync(string name)
        {
            return Task.FromResult(Find(name));
        }
        public Task<TenantConfiguration> FindAsync(Guid id)
        {
            return Task.FromResult(Find(id));
        }
    }
    
    ```

    ```c#
    public class AbpDefaultTenantStoreOptions
    {
        public TenantConfiguration[] Tenants { get;set; }
        public AbpDefaultTenantStoreOptions()
        {
            Tenant = new TenantConfiguration[0];
        }
    }
    
    ```

  * 基于仓储的store

    // todo

##### 1.2 当前 tenant

* `CurrentTenant`是当前 tenant 的定义

  ```c#
  public class CurrentTenant : ICurrentTenant, ITransientDependency
  {
      // 实现接口
      // 获取线程副本数据
      public virtual bool IsAvailable => Id.HasValue;
      public virtual Guid? Id => _currentTenantAccessor.Current?.TenantId;
      public string Name => _currentTenantAccessor.Current?.Name;
      
      // 注入线程数据
      private readonly ICurrentTenantAccessor _currentTenantAccessor;
      public CurrentTenant(ICurrentTenantAccessor currentTenantAccessor)
      {
          _currentTenantAccessor = currentTenantAccessor;
      }
      
      public IDisposable Change(Guid? id, string name = null)
      {
          return SetCurrent(id, name);
      }
      
      private IDisposable SetCurrent(Guid? tenantId, string name = null)
      {
          var parentScope = _currentTenantAccessor.Current;
          _currentTenantAccessor.Current = new BasicTenantInfo(tenantId, name);
          return new DisposeAction(() =>
          {
              // ... ???                                     
          })
      }
  }
  
  ```

  * 通过注入的`ICurrentTenantAccessor`获取 current_tenant 信息

    自动注册为单例，保存线程的 tenant

    ```c#
    public class AsyncLocalCurrentTenantAccessor : ICurrentTenantAccessor, ISingletonDependency
    {
        private readonly AsyncLocal<BasicTenantInfo> _currentScope;
        public BasicTenantInfo Current
        {
            get => _currentScope.Value;
            set => _currentScope.Value = value;
        }
        
        public AsyncLocalCurrentTenantAccessor()
        {
            _currentScope = new AsyncLocal<BasicTenantInfo>();
        }
    }
    
    ```

  * `BasicTenantInfo`是保存的 tenant 信息的封装

    ```c#
    public class BasicTenantInfo
    {
        public Guid? TenantId { get; }
        public string Name { get; }
        
        public BasicTenantInfo(Guid? tenantId, string name = null)
        {
            TenantId = tenantId;
            Name = name;
        }
    }
    
    ```

  * 扩展方法

    ```c#
    public static class CurrentTenantExtensions
    {
        public static Guid GetId(this ICurrentTenant currentTenant)
        {
            Check.NotNull(currentTenant, nameof(currentTenant));
            
            if(currentTenant.Id == null)
            {
                throw new AbpException("Current Tenant Id is Not available!");
            }
            
            return currentTenant.Id.Value;
        }
        
        // 确定是 tenant（租户）还是 host（租主）
        public static MultiTenancySides GetMultiTenancySide(this ICurrentTenant currentTenant)
        {
            return currentTenant.Id.HasValue 
                ? MultiTenancySides.Tenant 		// id 有值是 tenant
                : MultiTenancySides.Host;		// id 无值是 host
        }
    }
    
    ```

##### 1.3 解析 tenant

* `TenantResolver`是定义了 tenant 解析器

  解析器解析tenant，并将其设置为 current_tenant

  它包含`ITenantResolveContributor`的容器

  `ITenantResolveContributor`是真正提供 tenant_info 的来源

  ```c#
  public class TenantResolver : ITenantResolver, ITransientdependency
  {
      // 注入 resolve_contributor 容器
      private readonly IServiceProvider _serviceProvider;
      private readonly AbpTenantResolveOptions _options;
      public TenantResolver(IOptions<AbpTenantResolveOptions> options)
      {
          _serviceProvider = serviceProvider;
          _options = options.Value;
      }
      
      public virtual async Task<TenantResolveResult> ResolveTenantIdOrNameAsync()
      {
          var result = new TenantResolveResult();
          
          // 遍历 abp_tenant_resolve_options 中的 contributors 对应的方法
          using(var serviceScope = _serviceProvider.CreateScope())
          {
              var context = new TenantResolveContext(serviceScope.ServiceProvider);
              
              foreach(var tenantResolveContributor in _options.TenantResolvers)
              {
                  await tenantResolveContributor.ResolveAsync(context);
                  result.AppliedResovers.Add(tenantResolveContributor.Name);
                  if(context.HasResovedTenantOrHost())
                  {
                      result.TenantIdOrName = context.TenantIdOrName;
                      break;
                  }
              }
  		}
          
          return result;
      }
  }
  
  ```

  * `TenantResolveResult`是解析结果的封装

    ```c#
    public class TenantResolveResult
    {
        public string TenantIdOrName { get;set; }
        public List<string> AppliedResolvers { get; }
        
        public TenantResolveResult()
        {
            AppliedResolvers = new List<string();
        }
    }
    
    ```

  * `TenantResolveContributorBase`是resolver 的抽象基类

    ```c#
    public abstract class TenantResolveContributorBase : ITenantResolveContributor
    {
        public abstract string Name { get; }
        public abstract Task ResovleAsync(ITenantResolveContext context);
    }
    
    ```

    `TenantResolveContext`保存了解析的上下文信息

    ```c#
    public class TenantResolveContext : ITenantResolveContext
    {
        public IServiceProvider ServiceProvider { get; }
        public string TenantIdOrName { get;set; }
        public bool Handled { get;set; }
        
        public bool HasResolvedTenantOrHost()
        {
            return Handled || TenantIdOrName != null;
        }
        
        public TenantResolveContext(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }
    }
    
    ```

  * 框架定义了基本的解析器实现

    * 从 current_user 解析
    * 从 action 解析

  * 在 mvc 中定义了更多的解析器

    * 从 header 解析
    * 从 router 解析
    * 从 form 解析
    * 从 query 解析
    * 从 cookie 解析

##### 1.3 使用 tenant

* 获取 tenant configuration

  ```c#
  public class TenantConfigurationProvider : ITenantConfigurationProvider, ITransientDependency
  {
      // 注入 tenant 相关
      protected virtual ITenantResolver TenantResolver { get; }
      protected virtual ITenantStore TenantStore { get; }
      protected virtual ITenantResolveResultAccessor TenantResolveResultAccessor { get; }
      
      public TenantConfigurationProvider(
      	ITenantResolveer tenantResolver,
      	ITenantStore tenantStore,
      	ITenantResolveResultAccessor tenantResolveResultAccessor)
      {
          TenantResolver = tenantResolver;
          TenantStore = tenantStore;
          TenantResolveResultAccessor = tenantResolveResultAccessor;
      }
      
      public virtual async Task<TenantConfiguration> GetAsync(bool saveResolveResult = false)
      {
          // ...
      }
      
      protected virtual async Task<TenantConfiguration> FindTenantAsync(string tenantIdOrName)
      {
          // ...
      }
  }
  
  ```

* 过滤数据

  定义在 efcore 中

  * entitty 需要实现`IMultiTenant`接口

  // todo

  