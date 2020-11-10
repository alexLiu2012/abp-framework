## about caching

#### 1. concept

abp框架扩展了microsoft.extensions.caching.distributed caching

ms cache value的类型是byte[]，需要自己处理序列化，不方便

abp内嵌了序列化器，扩展了泛型distributed cache

* `DistributedCache<TItem,TKey>`是扩展的 caching 统一处理类

  ```c#
  public class DistributedCache<TCacheItem, TCacheKey> : 
  	IDistributedCache<TCacheItem, TCacheKey> where TCacheItem : class
  {
      public const string UowCacheName = "AbpDistributedCache";
      public ILogger<DistributedCache<TCacheItem, TCacheKey> Logger { get;set; }
      
      protected IDistributedCache Cache { get; }
      protected IDistributedCacheSerializer Serializer { get; }
      protected IDistributedCacheKeyNormalizer { get; }
      protected IDistributedCacheEntryOptions DefaultCacheOptions;
      private readonly AbpDistributedCacheOptions _distributedCacheOptions;
          
      protected ICancellationTokenProvider CancellationTokenProvider { get; }
      protected IServiceScopeFactory ServiceScopeFactory { get; }
      protected IUnitOfWorkManager UnitOfWorkManager { get; }
      protected SemaphoreSlim SyncSemaphore { get; }
      
      protected string CacheName { get;set; }
      protected bool IgnoreMultiTenancy { get;set; }
          
      // constructor ...
          
      // set ...
          
      // get ...
  }
  ```

  * 构造函数

    ```c#
    public class DistributedCache<TCacheItem, TCacheKey>
    {
        public DistributedCache(
        	IDistributedCache cache,
        	IOptions<AbpDistributedCacheOptions> distributedCacheOption,
        	IDistributedCacheSerializer serializer,
        	IDistributedCacheKeyNormalizer keyNormalizer,
        	ICancellationTokenProvider cancellationTokenProvider,
        	IServiceScopeFactory serviceScopeFactory,
        	IUnitOfWorkManager unitOfWorkManager)
        {
            // 传入 ms distributed cache, 和 abp distributed cache 的 options
            Cache = cache;
            _distributedCacheOption = distributedCacheOption.Value;
            
            // 传入序列化器
            Serializer = serializer;
            KeyNormalizer = keyNormalizer;
            
            // 处理事务
            CancellationTokenProvider = cancellationTokenProvider;
            ServiceScopeFactory = serviceScopeFactory;
            UnitOfWorkManager = unitOfWorkManager;
            
            Logger = NullLogger<DistributedCache<TCacheItem, TCacheKey>>.Instance;
            
            SyncSemaphore = new SemaphoreSlim(1,1);
            
            SetDefaultOptions();        
        }
    }
    
    ```

  * 获取模块注册的默认options

    ```c#
    public class DistributedCache<TCacheITem, TCacheKey>
    {
        protected virtual void SetDefaultOptions()
        {
            CacheName = CacheNameAttribute.GetCacheName(typeof(TCacheItem));
            
            //IgnoreMultiTenancy
            IgnoreMultiTenancy = typeof(TCacheItem).IsDefined(typeof(IgnoreMultiTenancyAttribute), true);
            
            //Configure default cache entry options
            DefaultCacheOptions = GetDefaultCacheEntryOptions();
        }
    }
    
    ```

    ```c#
    public class DistributedCache<TCacheITem, TCacheKey>
    {
        protected virtual DistributedCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            foreach (var configure in _distributedCacheOption.CacheConfigurators)
            {
                var options = configure.Invoke(CacheName);
                if (options != null)
                {
                    return options;
                }
            }
            
            return _distributedCacheOption.GlobalCacheEntryOptions;
        }
    }
    
    ```

* 关于事务性

  * 使用 uow

    ```c#
    public class DistributedCache<TCacheITem, TCacheKey>
    {
        protected virtual bool ShouldConsiderUow(bool considerUow)
        {
            return considerUow && UnitOfWorkManager.Current != null;
        }    
    }
    
    ```

  * uow key

    ```c#
    public class DistributedCache<TCacheITem, TCacheKey>
    {
        protected virtual string GetUnitOfWorkCacheKey()
        {
            // uowCacheName 写死的 AbpDistributedCache
            // cacheName 特性标记的，默认是 cached 类名
            return UowCacheName + CacheName;
        }
    }
    ```

  * uow cache

    dict<cachekey, uowcacheitem>字典的封装

    ```c#
    public class DistributedCache<TCacheITem, TCacheKey>
    {
        protected virtual Dictionary<TCacheKey, UnitOfWorkCacheItem<TCacheItem>> GetUnitOfWorkCache()
        {
            if (UnitOfWorkManager.Current == null)
            {
                throw new AbpException($"There is no active UOW.");
            }
            
            // 向 uow.items[]中注入数据
            return UnitOfWorkManager.Current.GetOrAddItem(
                	GetUnitOfWorkCacheKey(),
                	key => new Dictionary<TCacheKey, UnitOfWorkCacheItem<TCacheItem>>());
            }
    }
    
    ```

* 建立缓存

  ```c#
  public class DistributedCache
  {
      public virtual void Set(
  	TCacheKey key,
  	TCacheItem value,
  	DistributedCacheEntryOptions options = null,
  	bool? hideErrors = null,
  	bool considerUow = false)
      {
          if(ShouldConsiderUow(considerUow))
          {
              // 使用 uow ...
              uowCache.Add(key, uowCacheItem);	// 在 uow 的 items 中添加数据
              UnitOfWorkManager.Current.OnCompleted(() => 
                                                    {
                                                        SetRealCache();
                                                        return Task.CompletedTask;
                                                    });
          }
          else
          {
              // 直接创建 uow
              SetRealCache();
          }
          
          void SetRealCache()
          {
              // ...
              Cache.Set(
                  NormalizeKey(key),		// 格式化key，添加了cachename、prefix、tenant id
                  Serializer.Serialize(value),
                  options ?? DefaultCacheOptions);
          }
      }
  }
  
  ```

  * 多租户支持

    ```c#
    public class DistributedCacheKeyNormalizer : IDistributedCacheKeyNormalizer, ITransientDependency
    {
        // 注入 current tenant， options
        protected ICurrentTenant CurrentTenant { get; }
        protected AbpDistributedCacheOptions DistributedCacheOptions { get; }
        public DistributedCacheKeyNormalizer(ICurrentTenant currentTenant, IOptions<AbpDistributedCacheOptions> distributedCacheOptions)
        {
            CurrentTenant = currentTenant;
            DistributedCacheOptions = distributedCacheOptions.Value;
        }
        
        public virtual string NormalizeKey(DistributedCacheKeyNormalizeArgs args)
        {
            // 格式化的key，c:'cachename',k:'prefix''key'
            var normalizedKey = $"c:{args.CacheName},k:{DistributedCacheOptions.KeyPrefix}{args.key}";
            
            // 如果支持多租户，添加租户id
            if(!args.IgnoreMultiTenancy && CurrentTenant.Id.HasValue)
            {
                normalizedKey = $"t:{CurrentTenant.Id.Value},{normalizedKey}";
            }
            
            return normalizedKey;
        }
    }
    
    ```

  * 类似方法如`SetAsync()`，`SetMany()`，`SetManyAsync()`

* 获取缓存

  ```c#
  public class DistributedCache
  {
      public virtual TCacheItem Get(TCacheKey key, bool? hideErrors = null, bool considerUow = false)
      {
          // 如果使用 uow ...
          // 从 uow.items[] 中获取数据
          // key 中没有 tenant 数据？？？
          var value = GetUnitOfWorkCache().GetOrDefault(key)?.GetUnRemovedValueOrNull();
          if(value != null)
          {
              return value;
          }
          
          // 不使用 uow，从innerCache中获取数据，byte[]数据
          // 使用格式化key查询
          cachedBytes = Cache.Get(NormalizeKey(key));
      }
  }
  
  ```

  * 类似方法如`GetAsync()`，`GetMany()`，`GetManyAsync()`

* 获取或创建

  ```c#
  public class DistributedCache
  {
      public virtual TCacheItem GetOrAdd(
      	TCacheKey key,
      	Func<TCacheItem> factory,
      	Func<DistributedCacheEntryOptions> optionsFactory = null,
      	bool? hideErrors = null,
      	bool considerUow = false)
      {
          var value = Get(key, hideErrors, considerUow);
          
          // 如果有值，直接获取 ...        
          if(value != null)
          {
              return value;
          }
          
          // 否则创建缓存，using lock，因为get可以是多人操作
          using(SyncSemaphore.Lock())
          { 
              // ...
              value = factory();
              Set(key, value, optionsFactory?.Invoke(), hideErrors, considerUow);
          }
          
          return value;
         
      }
  }
  ```

  * 类似的还有方法`GetOrAddAsync()`

* 更新

  // 不支持 uow ？？？

  ```c#
  public class DistributedCache
  {
      public virtual void Refresh(TCacheKey key, bool? hideErrors = null)
      {
          // ...
          Cache.Refresh(NormalizeKey(key));
          // ...
      }
  }
  
  ```

  * 类似的还有放方法`RefreshAsync()`

* 删除

  ```c#
  public class DistributedCache
  {
      public virtual void Remove(TCacheKey key, bool? hideErrors = null, bool considerUow = false)
      {
          if(ShouldConsiderUow(considerUow))
          {
              // ...
              uowCache[key].RemoveValue();
              
              UnitOfWorkManager.Current.OnCompleted(() =>
                                                    {
                                                        RemoveRealCache();
                                                        return Task.CompletedTask;
                                                    });            
          }
          else
          {
              RemoveRealCache();
          }
          
          void RemoveRealCache()
          {
              // ...
              Cache.Remove(NormalizeKey(key));
          }
      }
  }
  
  ```

* 注册 cache 服务

  * 模块中注册


    ```c#
    public class AbpCachingModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {        
            // 注册 ms memory cache
            context.Services.AddMemoryCache();		
            // 注册 ms distributed cache，abp的 inner cache
            context.Services.AddDistributedMemoryCache();	 
            
            // 注册 abp distributed cache
            context.Services.AddSingleton(
                typeof(IDistributedCache<>), typeof(DistributedCache<>));
            context.Services.AddSingleton(
                typeof(IDistributedCache<,>), typeof(DistributedCache<,>));
    
            context.Services.Configure<AbpDistributedCacheOptions>(cacheOptions =>
                {
                    cacheOptions.GlobalCacheEntryOptions.SlidingExpiration = 
                        TimeSpan.FromMinutes(20);
                });
        }
    }
    
    ```

  * options

    ```c#
    public class AbpDistributedcAcheOptions
    {
        public bool HideErrors { get;set; } = true;
        public string KeyPrefix { get;set; }
        public DistributedCacheEntryOptions GlobalCacheEntryOptions { get;set; }
        public List<Func<string, DistributedCacheEntryOptions>> CacheConfigurators { get;set; }
        
        public AbpDistributedCacheOptions()
        {
            CacheConfigurators = new List<Func<string, DistributedcacheEntryOptions>>();
            GlobalCacheEntryOptions = new DistributedCacheEntryOptions();
            KeyPrefix = "";
        }
    }
    
    ```

  * 使用 redis

    依赖`AbpCachingStackExchangeRedisModule`

    ```c#
    [DependsOn(typeof(AbpCachingModule))]
    public class AbpCachingStackExchangeRedisModule : AbpModule
    {
        public override void ConfigureService(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();
            
            // 从配置获取 redis_enable
            var redisEnable = configuration["Redis:IsEnable"];
            if(redisEnabled.IsNullOrEmpty() || bool.Parse(redisEnabled))
            {
                // 如果 redis enable，注册 redis cache，他会作为 ms distributed cache
                context.Service.AddStackExchangeRedisCache(options => 
                {
                    var redisConfiguration = configuration["Redis:Configuration"];
                	// ...                
                });
                // 如果 redis enable，替换 innerCache 为 abp_redis_cache
                context.Services.Replace(ServieDesciptor.Singleton<IDistributedCache, AbpRedisCache());
            }
        }
    }
    
    ```

    * 配置文件

      ```json
      {
          // ...
          "Redis": {
              "IsEnabled": "true",	// 使能
              "Configuration": "127.0.0.1:4367"	// redis地址
          }
      }
      ```

    * 配置 ms redis cache options 重置redis 配置

      ```c#
      // in microsoft.extensions.caching.redis
      public clas RedisCacheOptions
      {
          public string Configuration { get;set; }	// 连接字符串
          public string InstanceName { get;set; }		// 实例名，添加在key的前缀
          
          public ConfigurationOptions ConfigurationOptions { get; set; }	// stack exchange redis 配置项
      }
      
      ```

      ```c#
      // in stack exchange redis
      public sealed class ConfigurationOptions
      {
          // ...
          // 看 stack exchange redis 文档的解释
      }
      ```

##### 2. how to use

* 依赖模块`AbpDistributedCacheModule`
* 配置options`AbpDistributedCacheOptions`
* 在服务中注入`IDistributedCache`

best practices:

* 依赖`AbpCachingStackExchangeRedisModule`模块
* 使用redis（用configuration文件配置  ms redis cache options）
  * 如果使用redis，配置文件中的 enable = true
  * 不使用redis，配置文件中的 enable = false
* 重写 ms redis cache（如果需要）