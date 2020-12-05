## about caching

相关程序集：

* Volo.Abp.Caching
* Volo.Abp.Caching.StackExchangeRedis

----

### 1. about

* abp框架扩展了 .net core 的 distributed caching
* 框架内嵌了序列化器，支持泛型类型直接存储（.net core caching 只能读取 bytes[]，redis也是）

### 2. details

#### 2.1 abp cache 接口

* abp框架重新定义了`IDistributedCache<>`和`IDistributedCache<,>`接口

##### 2.1.1 generic key 接口

```c#
public interface IDistributedCache<TCacheItem, TCacheKey> where TCacheItem : class
{    
    TCacheItem Get(
        TCacheKey key,
        bool? hideErrors = null,
        bool considerUow = false);
        
    KeyValuePair<TCacheKey, TCacheItem>[] GetMany(
        IEnumerable<TCacheKey> keys,
        bool? hideErrors = null,
        bool considerUow = false);
    
    Task<KeyValuePair<TCacheKey, TCacheItem>[]> GetManyAsync(
        IEnumerable<TCacheKey> keys,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default);    
    
    Task<TCacheItem> GetAsync(
        [NotNull] TCacheKey key,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default);
    
    TCacheItem GetOrAdd(
        TCacheKey key,
        Func<TCacheItem> factory,
        Func<DistributedCacheEntryOptions> optionsFactory = null,
        bool? hideErrors = null,
        bool considerUow = false);
    
    Task<TCacheItem> GetOrAddAsync(
        [NotNull] TCacheKey key,
        Func<Task<TCacheItem>> factory,
        Func<DistributedCacheEntryOptions> optionsFactory = null,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default);
        
    void Set(
        TCacheKey key,
        TCacheItem value,
        DistributedCacheEntryOptions options = null,
        bool? hideErrors = null,
        bool considerUow = false);    
    
    Task SetAsync(
        [NotNull] TCacheKey key,
        [NotNull] TCacheItem value,
        [CanBeNull] DistributedCacheEntryOptions options = null,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default);
        
    void SetMany(
        IEnumerable<KeyValuePair<TCacheKey, TCacheItem>> items,
        DistributedCacheEntryOptions options = null,
        bool? hideErrors = null,
        bool considerUow = false);
        
    Task SetManyAsync(
        IEnumerable<KeyValuePair<TCacheKey, TCacheItem>> items,
        DistributedCacheEntryOptions options = null,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default);
    
    void Refresh(
        TCacheKey key,
        bool? hideErrors = null);
    
    Task RefreshAsync(
        TCacheKey key,
        bool? hideErrors = null,
        CancellationToken token = default);
        
    void Remove(
        TCacheKey key,
        bool? hideErrors = null,
        bool considerUow = false);    
    
    Task RemoveAsync(
        TCacheKey key,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default);
}

```

##### 2.1.2 string key 接口

```c#
public interface IDistributedCache<TCacheItem> 
    : IDistributedCache<TCacheItem, string> where TCacheItem : class
{
}

```

#### 2.2 abp distributed cache

* 在abp框架定义的 cache 接口的实现中：
  * 包裹了 .net core cache 接口，即扩展了.net core cache，
  * 包含 serializer，可以直接存、取 string 类型

##### 2.2.1 初始化

```c#
public class DistributedCache<TCacheItem, TCacheKey> 
    : IDistributedCache<TCacheItem, TCacheKey> where TCacheItem : class
{
    public const string UowCacheName = "AbpDistributedCache";   
        
    protected string CacheName { get; set; }
    protected bool IgnoreMultiTenancy { get; set; }        
    protected DistributedCacheEntryOptions DefaultCacheOptions;            
    
    // 注入服务
	private readonly AbpDistributedCacheOptions _distributedCacheOption;
    protected IDistributedCache Cache { get; }
    protected ICancellationTokenProvider CancellationTokenProvider { get; }
    protected IDistributedCacheSerializer Serializer { get; }
    protected IDistributedCacheKeyNormalizer KeyNormalizer { get; }
    protected IServiceScopeFactory ServiceScopeFactory { get; }
    protected IUnitOfWorkManager UnitOfWorkManager { get; }
    protected SemaphoreSlim SyncSemaphore { get; }        
    public ILogger<DistributedCache<TCacheItem, TCacheKey>> Logger { get; set; }
            
    public DistributedCache(
        IOptions<AbpDistributedCacheOptions> distributedCacheOption,
        IDistributedCache cache,
        ICancellationTokenProvider cancellationTokenProvider,
        IDistributedCacheSerializer serializer,
        IDistributedCacheKeyNormalizer keyNormalizer,
        IServiceScopeFactory serviceScopeFactory,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _distributedCacheOption = distributedCacheOption.Value;
        Cache = cache;
        CancellationTokenProvider = cancellationTokenProvider;
        Serializer = serializer;
        KeyNormalizer = keyNormalizer;
        ServiceScopeFactory = serviceScopeFactory;
        UnitOfWorkManager = unitOfWorkManager;
        SyncSemaphore = new SemaphoreSlim(1, 1);
        
        // 定义 null logger，使用属性注入
        Logger = NullLogger<DistributedCache<TCacheItem, TCacheKey>>.Instance;    
        
        SetDefaultOptions();
    }
        
    protected virtual void SetDefaultOptions()
    {
        // 获取 TCacheItem 标记的 cache name attribute 特性
        CacheName = CacheNameAttribute.GetCacheName(typeof(TCacheItem));
        // 获取 ignore multi tenancy attribute 标记值        
        IgnoreMultiTenancy = typeof(TCacheItem)
            .IsDefined(typeof(IgnoreMultiTenancyAttribute), true);
        // 配置 cache entry options
        DefaultCacheOptions = GetDefaultCacheEntryOptions();
    }

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
    
    /* exception handling */
    protected virtual void HandleException(Exception ex)
    {
        AsyncHelper.RunSync(() => HandleExceptionAsync(ex));
    }
        
    protected virtual async Task HandleExceptionAsync(Exception ex)
    {
        Logger.LogException(ex, LogLevel.Warning);
        
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<IExceptionNotifier>()
                .NotifyAsync(new ExceptionNotificationContext(ex, LogLevel.Warning));
        }
    }
        
    /* normalize key */
    protected virtual string NormalizeKey(TCacheKey key)
    {
        return KeyNormalizer.NormalizeKey(
            new DistributedCacheKeyNormalizeArgs(
                key.ToString(), CacheName, IgnoreMultiTenancy));
    }                                   
}

```

##### 2.2.2 序列化

```c#
public class DistributedCache<TCacheItem, TCacheKey> 
{      
    // 逆序列化 cache bytes    
    [CanBeNull]
    protected virtual TCacheItem ToCacheItem([CanBeNull] byte[] bytes)
    {
        if (bytes == null)
        {
            return null;
        }        
        return Serializer.Deserialize<TCacheItem>(bytes);
    }
    
    // cache item with default
    private static KeyValuePair<TCacheKey, TCacheItem>[] ToCacheItemsWithDefaultValues(TCacheKey[] keys)
    {
        return keys.Select(key => new KeyValuePair<TCacheKey, TCacheItem>(
            key, 
            default)).ToArray();
    }
    
    // 逆序列化 cache bytes
    protected virtual KeyValuePair<TCacheKey, TCacheItem>[] ToCacheItems(
        byte[][] itemBytes, TCacheKey[] itemKeys)
    {
        if (itemBytes.Length != itemKeys.Length)
        {
            throw new AbpException("count of the item bytes should be same with the count of the given keys");
        }
        
        var result = new List<KeyValuePair<TCacheKey, TCacheItem>>();
        
        for (int i = 0; i < itemKeys.Length; i++)
        {
            result.Add(new KeyValuePair<TCacheKey, TCacheItem>(
                itemKeys[i],
                ToCacheItem(itemBytes[i])));
        }
        
        return result.ToArray();
    }
    
    // 序列化 cache itme
    protected virtual KeyValuePair<string, byte[]>[] ToRawCacheItems(
        KeyValuePair<TCacheKey, TCacheItem>[] items)
    {
        return items.Select(i => new KeyValuePair<string, byte[]>(
            NormalizeKey(i.Key),
            Serializer.Serialize(i.Value))).ToArray();
    }
}

```

##### 2.2.3 uow

```c#
public class DistributedCache<TCacheItem, TCacheKey> 
{  
    protected virtual bool ShouldConsiderUow(bool considerUow)
    {
        return considerUow && UnitOfWorkManager.Current != null;
    }
    
    protected virtual string GetUnitOfWorkCacheKey()
    {
        return UowCacheName + CacheName;
    }
    
    protected virtual Dictionary<TCacheKey, UnitOfWorkCacheItem<TCacheItem>> GetUnitOfWorkCache()
    {
        if (UnitOfWorkManager.Current == null)
        {
            throw new AbpException($"There is no active UOW.");
        }
        
        return UnitOfWorkManager.Current.GetOrAddItem(
            GetUnitOfWorkCacheKey(),
            key => new Dictionary<TCacheKey, UnitOfWorkCacheItem<TCacheItem>>());
    }
}

```

###### 2.2.3.1 uow cache item

```c#
[Serializable]
public class UnitOfWorkCacheItem<TValue> where TValue : class
{
    public bool IsRemoved { get; set; }    
    public TValue Value { get; set; }
    
    public UnitOfWorkCacheItem()
    {        
    }    
    public UnitOfWorkCacheItem(TValue value)
    {
        Value = value;
    }    
    public UnitOfWorkCacheItem(TValue value, bool isRemoved)
    {
        Value = value;
        IsRemoved = isRemoved;
    }
    
    public UnitOfWorkCacheItem<TValue> SetValue(TValue value)
    {
        Value = value;
        IsRemoved = false;
        return this;
    }
    
    public UnitOfWorkCacheItem<TValue> RemoveValue()        {
        Value = null;
        IsRemoved = true;
        return this;
    }
}

```

###### 2.2.3.2 uow cache item ext

```c#
public static class UnitOfWorkCacheItemExtensions
{
    public static TValue GetUnRemovedValueOrNull<TValue>(
        this UnitOfWorkCacheItem<TValue> item) where TValue : class
    {
        return item != null && !item.IsRemoved ? item.Value : null;
    }
}

```

##### 2.2.4 get

###### 2.2.4.1 get

```c#
public class DistributedCache<TCacheItem, TCacheKey> 
{    
    public virtual TCacheItem Get(
        TCacheKey key,
        bool? hideErrors = null,
        bool considerUow = false)
    {
        // 使用 uow cache        
        if (ShouldConsiderUow(considerUow))
        {            
            var value = GetUnitOfWorkCache().
                GetOrDefault(key)?.GetUnRemovedValueOrNull();
            if (value != null)
            {
                return value;
            }
        }
        
        // 使用 cache (.net core cache)
        byte[] cachedBytes;
        hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;
        try
        {            
            cachedBytes = Cache.Get(NormalizeKey(key));
        }
        catch (Exception ex)
        {
            // 隐藏异常
            if (hideErrors == true)
            {               
                HandleException(ex);
                // !! 返回null !!
                return null;
            }
            // 原位抛出异常
            throw;
        }        
        return ToCacheItem(cachedBytes);
    }        
}

```

###### 2.2.4.2 get async

```c#
public class DistributedCache<TCacheItem, TCacheKey> 
{ 
    public virtual async Task<TCacheItem> GetAsync(
        TCacheKey key,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default)
    {
        // 使用 uow cache        
        if (ShouldConsiderUow(considerUow))
        {
            var value = GetUnitOfWorkCache()
                .GetOrDefault(key)?.GetUnRemovedValueOrNull();
            if (value != null)
            {
                return value;
            }
        }
        
        // 使用 cache(.net core cache)
        byte[] cachedBytes;
        hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;
        try
        {
            cachedBytes = await Cache.GetAsync(
                NormalizeKey(key),
                CancellationTokenProvider.FallbackToProvider(token));
        }
        catch (Exception ex)
        {
            // 隐藏异常
            if (hideErrors == true)
            {
                await HandleExceptionAsync(ex);
                // !! 返回 null !!
                return null;
            }
            // 原位抛出异常
            throw;
        }
        return ToCacheItem(cachedBytes);
    }
}

```



###### 2.2.4.3 get many

```c#
public class DistributedCache<TCacheItem, TCacheKey> 
{ 
    public virtual KeyValuePair<TCacheKey, TCacheItem>[] GetMany(
        IEnumerable<TCacheKey> keys,
        bool? hideErrors = null,
        bool considerUow = false)
    {
        var keyArray = keys.ToArray();        
        var cachedValues = new List<KeyValuePair<TCacheKey, TCacheItem>>();
        
        // cache 不支持 multiItems，转到 fallback() 方法
        var cacheSupportsMultipleItems = Cache as ICacheSupportsMultipleItems;
        if (cacheSupportsMultipleItems == null)
        {
            return GetManyFallback(keyArray, hideErrors, considerUow);
        }
        
        // 使用 uow cache 查询
        var notCachedKeys = new List<TCacheKey>();
        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();
            foreach (var key in keyArray)
            {
                var value = uowCache.GetOrDefault(key)?.GetUnRemovedValueOrNull();
                if (value != null)
                {
                    cachedValues.Add(
                        new KeyValuePair<TCacheKey, TCacheItem>(key, value));
                }
            }
            // 没查到的key
            notCachedKeys = keyArray.Except(cachedValues.Select(x => x.Key)).ToList();
            // 没有没查到的key
            if (!notCachedKeys.Any())
            {
                // 返回 values
                return cachedValues.ToArray();
            }
        }
        
        // 使用 cache (.net core cache)
        hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;
        byte[][] cachedBytes;
        // 没有从 uow cache 中查到的 key
        var readKeys = notCachedKeys.Any() ? notCachedKeys.ToArray() : keyArray;
        try
        {
            cachedBytes = cacheSupportsMultipleItems
                .GetMany(readKeys.Select(NormalizeKey));
        }
        catch (Exception ex)
        {
            if (hideErrors == true)
            {
                HandleException(ex);	
                // 不是返回 null？？
                return ToCacheItemsWithDefaultValues(keyArray);
            }
            // 原位抛出异常
            throw;
        }
        
        return cachedValues.Concat(ToCacheItems(cachedBytes, readKeys)).ToArray();
    }
    
    // 如果 cache 不支持 multiItems，
    // 使用 linq 遍历查询 cache items
    protected virtual KeyValuePair<TCacheKey, TCacheItem>[] GetManyFallback(
        TCacheKey[] keys,
        bool? hideErrors = null,
        bool considerUow = false)
    {
        hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;        
        try
        {
            return keys.Select(key => new KeyValuePair<TCacheKey, TCacheItem>(
                key,
                Get(key, false, considerUow))).ToArray();
        }
        catch (Exception ex)
        {            
            if (hideErrors == true)
            {
                HandleException(ex);
                // 不返回 null ？？
                return ToCacheItemsWithDefaultValues(keys);
            }
            // 原位抛出异常
            throw;
        }
    }
}
   
```

###### 2.2.4.4 get many async

```c#
public class DistributedCache<TCacheItem, TCacheKey> 
{
    public virtual async Task<KeyValuePair<TCacheKey, TCacheItem>[]> GetManyAsync(     
        IEnumerable<TCacheKey> keys,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default)
    {
        var keyArray = keys.ToArray();
        var cachedValues = new List<KeyValuePair<TCacheKey, TCacheItem>>();
        
        // 如果 cache 不支持 multiItems，转到 fallback() 方法
        var cacheSupportsMultipleItems = Cache as ICacheSupportsMultipleItems;
        if (cacheSupportsMultipleItems == null)
        {
            return await GetManyFallbackAsync(
                keyArray, hideErrors, considerUow, token);
        }
        
        // 使用 uow cache
        var notCachedKeys = new List<TCacheKey>();        
        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();
            foreach (var key in keyArray)
            {
                var value = uowCache.GetOrDefault(key)?.GetUnRemovedValueOrNull();
                if (value != null)
                {
                    cachedValues.Add(
                        new KeyValuePair<TCacheKey, TCacheItem>(key, value));
                }
            }
            // 没查到的key
            notCachedKeys = keyArray.Except(cachedValues.Select(x => x.Key)).ToList();
			// 没有没查到的key
            if (!notCachedKeys.Any())
            {
                // 返回 values
                return cachedValues.ToArray();
            }
        }
        
        // 使用 cache (.net core cache)
        hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;
        byte[][] cachedBytes;
        // 没有从 uow cache 中查到的 key
        var readKeys = notCachedKeys.Any() ? notCachedKeys.ToArray() : keyArray;        
        try
        {
            cachedBytes = await cacheSupportsMultipleItems.GetManyAsync(
                readKeys.Select(NormalizeKey),
                CancellationTokenProvider.FallbackToProvider(token));
        }
        catch (Exception ex)
        {
            // 隐藏异常
            if (hideErrors == true)
            {
                await HandleExceptionAsync(ex);
                // 不返回 null ？？
                return ToCacheItemsWithDefaultValues(keyArray);
            }
            // 原位抛出异常
            throw;
        }
        
        return cachedValues.Concat(ToCacheItems(cachedBytes, readKeys)).ToArray();
    }
    
    // 如果 cache 不支持 multiItems，
    // 使用 linq 遍历查询 cache items
    protected virtual async Task<KeyValuePair<TCacheKey, TCacheItem>[]> GetManyFallbackAsync(
        TCacheKey[] keys,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default)
    {
        hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;        
        try
        {
            var result = new List<KeyValuePair<TCacheKey, TCacheItem>>();            
            foreach (var key in keys)
            {
                /* get many 定义的，for reference
                return keys.Select(key => new KeyValuePair<TCacheKey, TCacheItem>(
                key,
                Get(key, false, considerUow))).ToArray();
                */
                
                result.Add(new KeyValuePair<TCacheKey, TCacheItem>(
                    key,
                    await GetAsync(key, false, considerUow, token: token)));
            }            
            return result.ToArray();
        }
        catch (Exception ex)
        {
            // 隐藏异常
            if (hideErrors == true)
            {
                await HandleExceptionAsync(ex);
                // 不返回 null ？？
                return ToCacheItemsWithDefaultValues(keys);
            }
            // 原位抛出异常
            throw;
        }
    }        
}

```

##### 2.2.5 set

###### 2.2.5.1 set

```c#
public class DistributedCache<TCacheItem, TCacheKey> 
{
    public virtual void Set(
        TCacheKey key,
        TCacheItem value,
        DistributedCacheEntryOptions options = null,
        bool? hideErrors = null,
        bool considerUow = false)
    {
        // 如果使用 uow cache    
        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();
            if (uowCache.TryGetValue(key, out _))
            {
                uowCache[key].SetValue(value);
            }
            else
            {
                uowCache.Add(key, new UnitOfWorkCacheItem<TCacheItem>(value));
            }
            
            // ReSharper disable once PossibleNullReferenceException
            UnitOfWorkManager.Current.OnCompleted(() =>
            	{
                    SetRealCache();
                    return Task.CompletedTask;
                });
        }
        else
        {
            SetRealCache();
        }
        
        // 使用 cache (.net core cache)
        void SetRealCache()
        {
            hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;        
            try
            {
                Cache.Set(
                    NormalizeKey(key),
                    Serializer.Serialize(value),
                    options ?? DefaultCacheOptions);
            }
            catch (Exception ex)
            {
                if (hideErrors == true)
                {
                    HandleException(ex);
                    return;
                }            
                throw;
            }
        }
    }
}

```

###### 2.2.5.2 set async

```c#
public class DistributedCache<TCacheItem, TCacheKey> 
{
    public virtual async Task SetAsync(
        TCacheKey key,
        TCacheItem value,
        DistributedCacheEntryOptions options = null,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default)
    {                
        // 如果使用 uow cache
        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();
            if (uowCache.TryGetValue(key, out _))
            {
                uowCache[key].SetValue(value);
            }
            else
            {
                uowCache.Add(key, new UnitOfWorkCacheItem<TCacheItem>(value));
            }
            
            // ReSharper disable once PossibleNullReferenceException
            UnitOfWorkManager.Current.OnCompleted(SetRealCache);
        }
        else
        {
            await SetRealCache();
        }
        
        // 使用 cache (.net core cache)
        async Task SetRealCache()
        {
            hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;            
            try
            {
                await Cache.SetAsync(
                    NormalizeKey(key),
                    Serializer.Serialize(value),
                    options ?? DefaultCacheOptions,
                    CancellationTokenProvider.FallbackToProvider(token)
                );
            }
            catch (Exception ex)
            {
                if (hideErrors == true)
                {
                    await HandleExceptionAsync(ex);
                    return;
                }                
                throw;
            }
        }
    }        
}

```

###### 2.2.5.3 set many

```c#
public class DistributedCache<TCacheItem, TCacheKey> 
{
    public void SetMany(
        IEnumerable<KeyValuePair<TCacheKey, TCacheItem>> items,
        DistributedCacheEntryOptions options = null,
        bool? hideErrors = null,
        bool considerUow = false)
    {
        var itemsArray = items.ToArray();
        
        // 如果 cache 不支持 multiItems，转到 fallback() 方法
        var cacheSupportsMultipleItems = Cache as ICacheSupportsMultipleItems;
        if (cacheSupportsMultipleItems == null)
        {
            SetManyFallback(
                itemsArray, options, hideErrors, considerUow);            
            return;
        }
        
        // 如果使用 uow cache        
        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();            
            foreach (var pair in itemsArray)
            {
                if (uowCache.TryGetValue(pair.Key, out _))
                {
                    uowCache[pair.Key].SetValue(pair.Value);
                }
                else
                {
                    uowCache.Add(pair.Key, new UnitOfWorkCacheItem<TCacheItem>(pair.Value));
                }
            }
            
            // ReSharper disable once PossibleNullReferenceException
            UnitOfWorkManager.Current.OnCompleted(() =>
            	{
                    SetRealCache();
                    return Task.CompletedTask;
                });
        }
        else
        {
            SetRealCache();
        }
        
        // 使用 cache (.net core cache)
        void SetRealCache()
        {
            hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;            
            try
            {
                cacheSupportsMultipleItems.SetMany(
                    ToRawCacheItems(itemsArray),
                    options ?? DefaultCacheOptions
                );
            }
            catch (Exception ex)
            {
                if (hideErrors == true)
                {
                    HandleException(ex);
                    return;
                }                
                throw;
            }
        }
    }
    
    // 如果 cache 不支持 multiItems，
    // 遍历 cache items 插入 cached items
    protected virtual void SetManyFallback(
        KeyValuePair<TCacheKey, TCacheItem>[] items,
        DistributedCacheEntryOptions options = null,
        bool? hideErrors = null,
        bool considerUow = false)
    {
        hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;
        
        try
        {
            foreach (var item in items)
            {
                Set(item.Key, item.Value, options, false, considerUow);
            }
        }
        catch (Exception ex)
        {
            if (hideErrors == true)
            {
                HandleException(ex);
                return;
            }            
            throw;
        }
    }            
}

```

###### 2.2.5.4 set many async

```c#
public class DistributedCache<TCacheItem, TCacheKey> 
{
    public virtual async Task SetManyAsync(
        IEnumerable<KeyValuePair<TCacheKey, TCacheItem>> items,
        DistributedCacheEntryOptions options = null,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default)
    {
        var itemsArray = items.ToArray();
        
        // 如果 cache 不支持 multiItems，转到 fallback() 方法
        var cacheSupportsMultipleItems = Cache as ICacheSupportsMultipleItems;
        if (cacheSupportsMultipleItems == null)
        {
            await SetManyFallbackAsync(
                itemsArray, options, hideErrors, considerUow, token);            
            return;
        }
                
        // 如果使用 uow cache
        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();            
            foreach (var pair in itemsArray)
            {
                if (uowCache.TryGetValue(pair.Key, out _))
                {
                    uowCache[pair.Key].SetValue(pair.Value);
                }
                else
                {
                    uowCache.Add(pair.Key, new UnitOfWorkCacheItem<TCacheItem>(pair.Value));
                }
            }
            
            // ReSharper disable once PossibleNullReferenceException
            UnitOfWorkManager.Current.OnCompleted(SetRealCache);
        }
        else
        {
            await SetRealCache();
        }
        
        // 使用 cache (.net core cache)
        async Task SetRealCache()
        {
            hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;            
            try
            {
                await cacheSupportsMultipleItems.SetManyAsync(
                    ToRawCacheItems(itemsArray),
                    options ?? DefaultCacheOptions,
                    CancellationTokenProvider.FallbackToProvider(token)
                );
            }
            catch (Exception ex)
            {
                if (hideErrors == true)
                {
                    await HandleExceptionAsync(ex);
                    return;
                }                
                throw;
            }
        }        
    }
    
    // 如果 cache 不支持 multiItems，
    // 遍历 cache items 插入
    protected virtual async Task SetManyFallbackAsync(
        KeyValuePair<TCacheKey, TCacheItem>[] items,
        DistributedCacheEntryOptions options = null,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default)
    {
        hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;        
        try
        {
            foreach (var item in items)
            {
                await SetAsync(
                    item.Key, item.Value,options, false, considerUow, token: token);
            }
        }
        catch (Exception ex)
        {
            if (hideErrors == true)
            {
                await HandleExceptionAsync(ex);
                return;
            }            
            throw;
        }
    }        
}

```

##### 2.2.6 get or add 

```c#
public class DistributedCache<TCacheItem, TCacheKey> 
{ 
    public virtual TCacheItem GetOrAdd(
        TCacheKey key,
        Func<TCacheItem> factory,
        Func<DistributedCacheEntryOptions> optionsFactory = null,
        bool? hideErrors = null,
        bool considerUow = false)
    {
        // 如果找到，返回
        var value = Get(key, hideErrors, considerUow);
        if (value != null)
        {
            return value;
        }
        
        // 创建并返回
        using (SyncSemaphore.Lock())
        {
            // 二次查询（其他线程创建了）
            value = Get(key, hideErrors, considerUow);
            if (value != null)
            {
                return value;
            }
            
            /* 创建 cache */
            value = factory();      
            // 使用 uow cache 创建
            if (ShouldConsiderUow(considerUow))
            {
                var uowCache = GetUnitOfWorkCache();
                if (uowCache.TryGetValue(key, out var item))
                {
                    item.SetValue(value);
                }
                else
                {
                    uowCache.Add(key, new UnitOfWorkCacheItem<TCacheItem>(value));
                }
            }
            // 使用 cache (.net core cache) 创建
            Set(key, value, optionsFactory?.Invoke(), hideErrors, considerUow);
        }        
        return value;
    }
    
    public virtual async Task<TCacheItem> GetOrAddAsync(
        TCacheKey key,
        Func<Task<TCacheItem>> factory,
        Func<DistributedCacheEntryOptions> optionsFactory = null,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default)
    {
        token = CancellationTokenProvider.FallbackToProvider(token);
        
        // 如果找到，返回
        var value = await GetAsync(key, hideErrors, considerUow, token);
        if (value != null)
        {
            return value;
        }
        
        // 创建并返回
        using (await SyncSemaphore.LockAsync(token).ConfigureAwait(false))
        {
            // 二次查询（其他线程创建了）
            value = await GetAsync(key, hideErrors, considerUow, token);
            if (value != null)
            {
                return value;
            }
            
            /* 创建 cache */
            value = await factory();
            // 如果使用 uow cache
            if (ShouldConsiderUow(considerUow))
            {
                var uowCache = GetUnitOfWorkCache();
                if (uowCache.TryGetValue(key, out var item))
                {
                    item.SetValue(value);
                }
                else
                {
                    uowCache.Add(key, new UnitOfWorkCacheItem<TCacheItem>(value));
                }
            }
            // 使用 cache (.net core cache)
            await SetAsync(
                key, value, optionsFactory?.Invoke(), hideErrors, considerUow, token);      
        }
        return value;
    }
}

```

##### 2.2.7 refresh

```c#
public class DistributedCache<TCacheItem, TCacheKey> 
{
    public virtual void Refresh(
        TCacheKey key, 
        bool? hideErrors = null)
    {
        hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;        
        try
        {
            Cache.Refresh(NormalizeKey(key));
        }
        catch (Exception ex)
        {
            if (hideErrors == true)
            {
                HandleException(ex);
                return;
            }            
            throw;
        }
    }
    
    
    public virtual async Task RefreshAsync(
        TCacheKey key,
        bool? hideErrors = null,
        CancellationToken token = default)
    {
        hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;        
        try
        {
            await Cache.RefreshAsync(
                NormalizeKey(key), CancellationTokenProvider.FallbackToProvider(token));
        }
        catch (Exception ex)
        {
            if (hideErrors == true)
            {
                await HandleExceptionAsync(ex);
                return;
            }            
            throw;
        }
    }
}

```

##### 2.2.8 remove

```c#

public class DistributedCache<TCacheItem, TCacheKey> 
{
    public virtual void Remove(
        TCacheKey key,
        bool? hideErrors = null,
        bool considerUow = false)
    {        
        // 如果使用 uow cache
        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();
            if (uowCache.TryGetValue(key, out _))
            {
                uowCache[key].RemoveValue();
            }
            
            // ReSharper disable once PossibleNullReferenceException
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
        
        // 使用 cache (.net core cache)
        void RemoveRealCache()
        {
            hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;            
            try
            {
                Cache.Remove(NormalizeKey(key));
            }
            catch (Exception ex)
            {
                if (hideErrors == true)
                {
                    HandleException(ex);
                    return;
                }                
                throw;
            }
        }
    }
    
    
    public virtual async Task RemoveAsync(
        TCacheKey key,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default)
    {
        // 如果使用 uow cache
        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();
            if (uowCache.TryGetValue(key, out _))
            {
                uowCache[key].RemoveValue();
            }
            
            // ReSharper disable once PossibleNullReferenceException
            UnitOfWorkManager.Current.OnCompleted(RemoveRealCache);
        }
        else
        {
            await RemoveRealCache();
        }
        
        // 使用 cache (.net core cache)
        async Task RemoveRealCache()
        {
            hideErrors = hideErrors ?? _distributedCacheOption.HideErrors;            
            try
            {
                await Cache.RemoveAsync(
                    NormalizeKey(key), CancellationTokenProvider.FallbackToProvider(token));
            }
            catch (Exception ex)
            {
                if (hideErrors == true)
                {
                    await HandleExceptionAsync(ex);
                    return;
                }                
                throw;
            }
        }        
    }
}

```

#### 2.3 cache serialize

##### 2.3.1 cache name attribute

```c#
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct)]
public class CacheNameAttribute : Attribute
{
    public string Name { get; }    
    public CacheNameAttribute([NotNull] string name)
    {
        Check.NotNull(name, nameof(name));        
        Name = name;
    }
    
    public static string GetCacheName(Type cacheItemType)
    {
        var cacheNameAttribute = cacheItemType
            .GetCustomAttributes(true)
            .OfType<CacheNameAttribute>()
            .FirstOrDefault();        
        if (cacheNameAttribute != null)
        {
            return cacheNameAttribute.Name;
        }        
        return cacheItemType.FullName.RemovePostFix("CacheItem");
    }
}

```

##### 2.3.2 key normalizer

```c#
public class DistributedCacheKeyNormalizer 
    : IDistributedCacheKeyNormalizer, ITransientDependency
{
    // 注入服务
    protected ICurrentTenant CurrentTenant { get; }
    protected AbpDistributedCacheOptions DistributedCacheOptions { get; }
    public DistributedCacheKeyNormalizer(
        ICurrentTenant currentTenant, 
        IOptions<AbpDistributedCacheOptions> distributedCacheOptions)
    {
        CurrentTenant = currentTenant;
        DistributedCacheOptions = distributedCacheOptions.Value;
    }

    public virtual string NormalizeKey(DistributedCacheKeyNormalizeArgs args)
    {
        var normalizedKey = $"c:{args.CacheName},k:{DistributedCacheOptions.KeyPrefix}{args.Key}";        
        if (!args.IgnoreMultiTenancy && CurrentTenant.Id.HasValue)
        {
            normalizedKey = $"t:{CurrentTenant.Id.Value},{normalizedKey}";
        }        
        return normalizedKey;
    }
}

```

###### 2.3.2.1 normalizer args

```c#
public class DistributedCacheKeyNormalizeArgs
{
    public string Key { get; }    
    public string CacheName { get; }    
    public bool IgnoreMultiTenancy { get; }    
    public DistributedCacheKeyNormalizeArgs(
        string key, 
        string cacheName, 
        bool ignoreMultiTenancy)
    {
        Key = key;
        CacheName = cacheName;
        IgnoreMultiTenancy = ignoreMultiTenancy;
    }
}

```

##### 2.3.3 serializer

```c#
public class Utf8JsonDistributedCacheSerializer 
    : IDistributedCacheSerializer, ITransientDependency
{
    protected IJsonSerializer JsonSerializer { get; }
    public Utf8JsonDistributedCacheSerializer(IJsonSerializer jsonSerializer)
    {
        JsonSerializer = jsonSerializer;
    }
        
    public byte[] Serialize<T>(T obj)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));
    }
        
    public T Deserialize<T>(byte[] bytes)\
    {
        return (T)JsonSerializer.Deserialize(typeof(T), Encoding.UTF8.GetString(bytes));
    }
}

```

#### 2.4 cache contributor

##### 2.4.1 support multi items

```c#
public interface ICacheSupportsMultipleItems
{
    byte[][] GetMany(
        IEnumerable<string> keys);
    
    Task<byte[][]> GetManyAsync(
        IEnumerable<string> keys, CancellationToken token = default);
    
    void SetMany(
        IEnumerable<KeyValuePair<string, byte[]>> items,
        DistributedCacheEntryOptions options);

    Task SetManyAsync(
        IEnumerable<KeyValuePair<string, byte[]>> items,
        DistributedCacheEntryOptions options,
        CancellationToken token = default); 
}

```

##### 2.4.1 .net core distributed cache

```c#
// .net core DistributedCacheMemory
// .net core MemeoryCache
```

##### 2.4.2 abp redis cache

```c#
[DisableConventionalRegistration]
public class AbpRedisCache : RedisCache, ICacheSupportsMultipleItems
{
    protected static readonly string SetScript;
    protected static readonly string AbsoluteExpirationKey;
    protected static readonly string SlidingExpirationKey;
    protected static readonly string DataKey;
    protected static readonly long NotPresent;
    
    private static readonly FieldInfo RedisDatabaseField;
    private static readonly MethodInfo ConnectMethod;
    private static readonly MethodInfo ConnectAsyncMethod;
    private static readonly MethodInfo MapMetadataMethod;
    private static readonly MethodInfo GetAbsoluteExpirationMethod;
    private static readonly MethodInfo GetExpirationInSecondsMethod;
    
    private IDatabase _redisDatabase;
    protected IDatabase RedisDatabase => GetRedisDatabase();
        
    protected string Instance { get; }
    
    static AbpRedisCache()
    {
        var type = typeof(RedisCache);
        
        RedisDatabaseField = type.GetField(
            "_cache", BindingFlags.Instance | BindingFlags.NonPublic);        
        ConnectMethod = type.GetMethod(
            "Connect", BindingFlags.Instance | BindingFlags.NonPublic);
        ConnectAsyncMethod = type.GetMethod(
            "ConnectAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        MapMetadataMethod = type.GetMethod(
            "MapMetadata", BindingFlags.Instance | BindingFlags.NonPublic);
        GetAbsoluteExpirationMethod = type.GetMethod(
            "GetAbsoluteExpiration", BindingFlags.Static | BindingFlags.NonPublic);
        GetExpirationInSecondsMethod = type.GetMethod(
            "GetExpirationInSeconds", BindingFlags.Static | BindingFlags.NonPublic);
        SetScript = type.GetField(
            "SetScript", BindingFlags.Static | 
            BindingFlags.NonPublic).GetValue(null).ToString();
        AbsoluteExpirationKey = type.GetField(
            "AbsoluteExpirationKey", BindingFlags.Static | 
            BindingFlags.NonPublic).GetValue(null).ToString();
        SlidingExpirationKey = type.GetField(
            "SlidingExpirationKey", BindingFlags.Static | 
            BindingFlags.NonPublic).GetValue(null).ToString();
        DataKey = type.GetField(
            "DataKey", BindingFlags.Static | 
            BindingFlags.NonPublic).GetValue(null).ToString();
        NotPresent = type.GetField(
            "NotPresent", BindingFlags.Static | 
            BindingFlags.NonPublic).GetValue(null).To<int>();
    }
    
    public AbpRedisCache(IOptions<RedisCacheOptions> optionsAccessor)
        : base(optionsAccessor)
    {
        Instance = optionsAccessor.Value.InstanceName ?? string.Empty;
    }
    
    protected virtual void Connect()
    {
        if (GetRedisDatabase() != null)
        {
            return;
        }        
        ConnectMethod.Invoke(this, Array.Empty<object>());
    }
    
    protected virtual Task ConnectAsync(CancellationToken token = default)
    {
        if (GetRedisDatabase() != null)
        {
            return Task.CompletedTask;
        }        
        return (Task) ConnectAsyncMethod.Invoke(this, new object[] {token});
    }
    
    public byte[][] GetMany(IEnumerable<string> keys)
    {
        keys = Check.NotNull(keys, nameof(keys));        
        return GetAndRefreshMany(keys, true);
    }

    public async Task<byte[][]> GetManyAsync(
        IEnumerable<string> keys,
        CancellationToken token = default)
    {
        keys = Check.NotNull(keys, nameof(keys));        
        return await GetAndRefreshManyAsync(keys, true, token);
    }

    public void SetMany(
        IEnumerable<KeyValuePair<string, byte[]>> items,
        DistributedCacheEntryOptions options)
    {
        Connect();        
        Task.WaitAll(PipelineSetMany(items, options));
    }

    public async Task SetManyAsync(
        IEnumerable<KeyValuePair<string, byte[]>> items,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();        
        await ConnectAsync(token);        
        await Task.WhenAll(PipelineSetMany(items, options));
    }
    
    protected virtual byte[][] GetAndRefreshMany(
        IEnumerable<string> keys,
        bool getData)
    {
        Connect();
        
        var keyArray = keys.Select(key => Instance + key).ToArray();
        RedisValue[][] results;
        
        if (getData)
        {
            results = RedisDatabase.HashMemberGetMany(
                keyArray, AbsoluteExpirationKey, SlidingExpirationKey, DataKey);
        }
        else
        {
            results = RedisDatabase.HashMemberGetMany(
                keyArray, AbsoluteExpirationKey, SlidingExpirationKey);
        }
        
        Task.WaitAll(PipelineRefreshManyAndOutData(keyArray, results, out var bytes));
        
        return bytes;
    }
    
    protected virtual async Task<byte[][]> GetAndRefreshManyAsync(
        IEnumerable<string> keys,
        bool getData,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        
        await ConnectAsync(token);
        
        var keyArray = keys.Select(key => Instance + key).ToArray();
        RedisValue[][] results;
        
        if (getData)
        {
            results = await RedisDatabase.HashMemberGetManyAsync(
                keyArray, AbsoluteExpirationKey, SlidingExpirationKey, DataKey);
        }
        else
        {
            results = await RedisDatabase.HashMemberGetManyAsync(
                keyArray, AbsoluteExpirationKey, SlidingExpirationKey);
        }
        
        await Task.WhenAll(PipelineRefreshManyAndOutData(keyArray, results, out var bytes));
        
        return bytes;
    }
    
    protected virtual Task[] PipelineRefreshManyAndOutData(
        string[] keys,
        RedisValue[][] results,
        out byte[][] bytes)
    {
        bytes = new byte[keys.Length][];
        var tasks = new Task[keys.Length];
        
        for (var i = 0; i < keys.Length; i++)
        {
            if (results[i].Length >= 2)
            {
                MapMetadata(results[i], out DateTimeOffset? absExpr, out TimeSpan? sldExpr);
                
                if (sldExpr.HasValue)
                {
                    TimeSpan? expr;
                    
                    if (absExpr.HasValue)
                    {
                        var relExpr = absExpr.Value - DateTimeOffset.Now;
                        expr = relExpr <= sldExpr.Value ? relExpr : sldExpr;
                    }
                    else
                    {
                        expr = sldExpr;
                    }
                    
                    tasks[i] = RedisDatabase.KeyExpireAsync(keys[i], expr);
                }
                else
                {
                    tasks[i] = Task.CompletedTask;
                }
            }
            
            if (results[i].Length >= 3 && results[i][2].HasValue)
            {
                bytes[i] = results[i][2];
            }
            else
            {
                bytes[i] = null;
            }
        }
        
        return tasks;
    }
    
    protected virtual Task[] PipelineSetMany(
        IEnumerable<KeyValuePair<string, byte[]>> items,
        DistributedCacheEntryOptions options)
    {
        items = Check.NotNull(items, nameof(items));
        options = Check.NotNull(options, nameof(options));
        
        var itemArray = items.ToArray();
        var tasks = new Task[itemArray.Length];
        var creationTime = DateTimeOffset.UtcNow;
        var absoluteExpiration = GetAbsoluteExpiration(creationTime, options);
        
        for (var i = 0; i < itemArray.Length; i++)
        {
            tasks[i] = RedisDatabase.ScriptEvaluateAsync(
                SetScript, new RedisKey[] {Instance + itemArray[i].Key},
                new RedisValue[]
                {
                    absoluteExpiration?.Ticks ?? NotPresent,
                    options.SlidingExpiration?.Ticks ?? NotPresent, 
                    GetExpirationInSeconds(creationTime, absoluteExpiration, options) 
                        ?? NotPresent,
                    itemArray[i].Value});
        }
        
        return tasks;
    }
    
    protected virtual void MapMetadata(
        RedisValue[] results,
        out DateTimeOffset? absoluteExpiration,
        out TimeSpan? slidingExpiration)
    {
        var parameters = new object[] {results, null, null};
        MapMetadataMethod.Invoke(this, parameters);
        
        absoluteExpiration = (DateTimeOffset?) parameters[1];
        slidingExpiration = (TimeSpan?) parameters[2];
    }
    
    protected virtual long? GetExpirationInSeconds(
        DateTimeOffset creationTime,
        DateTimeOffset? absoluteExpiration,
        DistributedCacheEntryOptions options)
    {
        return (long?) GetExpirationInSecondsMethod.Invoke(
            null, new object[] {creationTime, absoluteExpiration, options});
    }
    
    protected virtual DateTimeOffset? GetAbsoluteExpiration(
        DateTimeOffset creationTime,
        DistributedCacheEntryOptions options)
    {
        return (DateTimeOffset?) GetAbsoluteExpirationMethod
            .Invoke(null, new object[] {creationTime, options});
    }
    
    private IDatabase GetRedisDatabase()
    {            
        if (_redisDatabase == null)
        {
            _redisDatabase = RedisDatabaseField.GetValue(this) as IDatabase;
        }
        
        return _redisDatabase;
    }
}

```

#### 2.5 注册 cache 服务

##### 2.5.1 abp distributed cache

###### 2.5.1.1 模块

```c#
[DependsOn(typeof(AbpThreadingModule),
           typeof(AbpSerializationModule),
           typeof(AbpUnitOfWorkModule),
           typeof(AbpMultiTenancyModule),
           typeof(AbpJsonModule))]
public class AbpCachingModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 注册 .net core cache 
        context.Services.AddMemoryCache();
        context.Services.AddDistributedMemoryCache();        
        // 注册 abp distributed cache 提供 IDistributedCache 服务
        context.Services.AddSingleton(
            typeof(IDistributedCache<>), typeof(DistributedCache<>));
        context.Services.AddSingleton(
            typeof(IDistributedCache<,>), typeof(DistributedCache<,>));
		
        // 配置 abp distributed cache options
        context.Services.Configure<AbpDistributedCacheOptions>(cacheOptions =>
            {
                // 默认过期时间 20min
                cacheOptions
                    .GlobalCacheEntryOptions
                    	.SlidingExpiration = TimeSpan.FromMinutes(20);
            });
    }
}

```

###### 2.5.1.2 options

```c#
public class AbpDistributedCacheOptions
{    
    public bool HideErrors { get; set; } = true;        
    public string KeyPrefix { get; set; }        
    public DistributedCacheEntryOptions GlobalCacheEntryOptions { get; set; }        
    public List<Func<string, DistributedCacheEntryOptions>> CacheConfigurators { get; set; } 

    public AbpDistributedCacheOptions()
    {
        KeyPrefix = "";
        GlobalCacheEntryOptions = new DistributedCacheEntryOptions();
        CacheConfigurators = new List<Func<string, DistributedCacheEntryOptions>>();    
    }
}

```

##### 2.5.2 注册abp redis cache

###### 2.5.2.1 模块

```c#
[DependsOn(typeof(AbpCachingModule))]
public class AbpCachingStackExchangeRedisModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        
        var redisEnabled = configuration["Redis:IsEnabled"];
        
        // options 为null，默认开启 redis
        if (redisEnabled.IsNullOrEmpty() || bool.Parse(redisEnabled))
        {
            context.Services.AddStackExchangeRedisCache(options =>
            	{
                    // 读取 redis 配置
                    var redisConfiguration = configuration["Redis:Configuration"];
                    if (!redisConfiguration.IsNullOrEmpty())
                    {
                        options.Configuration = redisConfiguration;
                    }
                });
            // 用 abp redis cache 代替 IDistributedCatch 服务
            context.Services.Replace(
                ServiceDescriptor.Singleton<IDistributedCache, AbpRedisCache>());
        }
    }
}

```

###### 2.5.2.2 options

```c#
// .net core StackExchangeRedisCache options

```

### 3. practice

#### 3.1 配置 cache

* 自定义模块依赖`AbpCachingStackExchangeRedisModule`

* 在模块的配置文件（appsettings.json）中增加 redis 的配置项

  ```json
  {
      // ...
      "Redis": {
          "IsEnabled": "true",	// 使能
          "Configuration": "127.0.0.1:4367"	// redis地址
      }
  }
  
  ```

* 在模块的`ConfigureService()`方法中配置 abp redis options 或者 abp distributed cache options

#### 3.2 使用 cache

* 定义需要 cache 的类型，标注 cache_name_attribute（推荐）
* 在上层架构中注入`IDistributedCache`