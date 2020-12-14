## about localization

TODO:



相关程序集：

* Volo.Abp.Core
* Volo.Abp.Localization

----

### 1. about

* abp框架扩展了.net core localization，使用 json 文件添加 resource
* 将其作为核心服务注入到启动框架中（abp core service之一）

### 2. details

#### 2.1 localization string

* abp框架使用 .net core 定义的`LocalizedString`表示 localized string

##### 2.1.1 localized string

```c#
public class LocalizedString
{
    public string Name { get; }        
    public string Value { get; }        
    public bool ResourceNotFound { get; }        
    public string? SearchedLocation { get; }
    
    public LocalizedString(string name, string value)
        : this(name, value, resourceNotFound: false)
    {
    }                 
    public LocalizedString(string name, string value, bool resourceNotFound)
        : this(name, value, resourceNotFound, searchedLocation: null)
    {
    }        
    public LocalizedString(string name, string value, bool resourceNotFound, string? searchedLocation)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }        
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        
        Name = name;
        Value = value;
        ResourceNotFound = resourceNotFound;
        SearchedLocation = searchedLocation;
    }  
}

```

##### 2.1.2 localized string dictionary

* localized string 的容器
* localization resource contributor  使用 dictionary 提供（检索） localized string

###### 2.1.2.1 接口

```c#
public interface ILocalizationDictionary
{
    // culture_name 用作索引和唯一性标识（contributor用来判断是否加载）    
    string CultureName { get; }
    LocalizedString GetOrNull(string name);    
    void Fill(Dictionary<string, LocalizedString> dictionary);
}

```

###### 2.1.2.2 实现

```c#
public class StaticLocalizationDictionary : ILocalizationDictionary
{            
    public string CultureName { get; }
    // 实际存储 localized string 的容器
    protected Dictionary<string, LocalizedString> Dictionary { get; }
            
    public StaticLocalizationDictionary(
        string cultureName, Dictionary<string, LocalizedString> ictionary)
    {
        CultureName = cultureName;
        // 注入 dictionary？？
        Dictionary = dictionary;
    }
        
    public virtual LocalizedString GetOrNull(string name)
    {
        return Dictionary.GetOrDefault(name);
    }
    
    // 把 localized string 填充到传入的 dictionary 中
    public void Fill(Dictionary<string, LocalizedString> dictionary)
    {
        foreach (var item in Dictionary)
        {
            dictionary[item.Key] = item.Value;
        }
    }
}

```

#### 2.2 localization resource

* abp框架定义的、表示本地化资源的类，类似 .net core 中的 Resources.resx
* abp框架扩展了resource功能
  * 定义了 resource contributors 来真正解析 resource
  * 可以添加 json 文件作为 resource 提供者
  * 可以继承其他 resource 类

##### 2.2.1 localization resource

```c#
public class LocalizationResource
{
    // resource type，空的类型定义
    [NotNull]
    public Type ResourceType { get; }    
    // 用标记在 resource_type 上的 resource_name 特性的 name
    [NotNull]
    public string ResourceName => LocalizationResourceNameAttribute.GetName(ResourceType);    
    [CanBeNull]
    public string DefaultCultureName { get; set; }
    
    [NotNull]
    public LocalizationResourceContributorList Contributors { get; }
    
    [NotNull]
    public List<Type> BaseResourceTypes { get; }
    
    public LocalizationResource(
        [NotNull] Type resourceType, 
        [CanBeNull] string defaultCultureName = null,
        [CanBeNull] ILocalizationResourceContributor initialContributor = null)
    {
        ResourceType = Check.NotNull(resourceType, nameof(resourceType));
        DefaultCultureName = defaultCultureName;
        
        BaseResourceTypes = new List<Type>();
        Contributors = new LocalizationResourceContributorList();
        
        if (initialContributor != null)
        {
            Contributors.Add(initialContributor);
        }
        
        AddBaseResourceTypes();
    }
    
    protected virtual void AddBaseResourceTypes()
    {
        // 获取 resource type 标记的所有 inherite resource type 特性
        // 使用 IInheritedResourceType 接口过滤
        // 因为 attribute 同时实现了 InheritedResourceType 接口
        var descriptors = ResourceType
            .GetCustomAttributes(true)
            .OfType<IInheritedResourceTypesProvider>();
        
        foreach (var descriptor in descriptors)
        {
            // 添加 inherite resource type 特性中的所有要继承的 resource type
            foreach (var baseResourceType in descriptor.GetInheritedResourceTypes())
            {
                BaseResourceTypes.AddIfNotContains(baseResourceType);
            }
        }
    }
}

```

###### 2.2.1.1 resource name attribute

```c#
public class LocalizationResourceNameAttribute : Attribute
{
    // 标记的 resource name
    public string Name { get; }
    
    public LocalizationResourceNameAttribute(string name)
    {
        Name = name;
    }
    
    public static LocalizationResourceNameAttribute GetOrNull(Type resourceType)
    {
        return resourceType
            .GetCustomAttributes(true)
            .OfType<LocalizationResourceNameAttribute>()
            .FirstOrDefault();
    }
    
    public static string GetName(Type resourceType)
    {
        return GetOrNull(resourceType)?.Name ?? resourceType.FullName;
    }
}

```

###### 2.2.1.2 inherit resource attribute

```c#
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class InheritResourceAttribute : Attribute, IInheritedResourceTypesProvider
{
    // 要继承的 resource 基类
    public Type[] ResourceTypes { get; }
    
    public InheritResourceAttribute(params Type[] resourceTypes)
    {
        ResourceTypes = resourceTypes ?? new Type[0];
    }
    
    public virtual Type[] GetInheritedResourceTypes()
    {
        return ResourceTypes;
    }
}

```

* 使用`IInheritedResourceTypesProvider`做了限定

  ```c#
  public interface IInheritedResourceTypesProvider
  {
      [NotNull]
      Type[] GetInheritedResourceTypes();
  }
  
  ```

##### 2.2.2 扩展方法

###### 2.2.2.1 添加 json resource

###### 2.2.2.2 添加基类

```c#
public static class LocalizationResourceExtensions
{
    // 添加 json 文件作为 resource 提供者
    // 在 contributors 中追加了 json string contributor
    public static LocalizationResource AddVirtualJson(
        [NotNull] this LocalizationResource localizationResource,
        [NotNull] string virtualPath)
    {
        Check.NotNull(localizationResource, nameof(localizationResource));
        Check.NotNull(virtualPath, nameof(virtualPath));
        
        localizationResource.Contributors.Add(
            new JsonVirtualFileLocalizationResourceContributor(
                virtualPath.EnsureStartsWith('/')));
        
        return localizationResource;
    }
    
    // 添加要继承的基类
    public static LocalizationResource AddBaseTypes(
        [NotNull] this LocalizationResource localizationResource,
        [NotNull] params Type[] types)
    {
        Check.NotNull(localizationResource, nameof(localizationResource));
        Check.NotNull(types, nameof(types));
        
        foreach (var type in types)
        {
            localizationResource.BaseResourceTypes.AddIfNotContains(type);
        }
        
        return localizationResource;
    }
}

```

##### 2.2.3 resource 集合

* abp框架定义了 resource 的集合`LocalizationResourceDictionary`

```c#
public class LocalizationResourceDictionary : Dictionary<Type, LocalizationResource>
{
    /* 向 dictionary 添加 resource 
       如果 resourceType 已经存在，抛出异常 */        
    public LocalizationResource Add<TResouce>(
        [CanBeNull] string defaultCultureName = null)
    {
        return Add(typeof(TResouce), defaultCultureName);
    }  
    
    public LocalizationResource Add(
        Type resourceType, [CanBeNull] string defaultCultureName = null)
    {
        if (ContainsKey(resourceType))
        {
            // 如果重复添加type抛出异常
            throw new AbpException("This resource is already added before: " + resourceType.AssemblyQualifiedName);
        }
        // 追加 localization resource
        return this[resourceType] = new LocalizationResource(resourceType, defaultCultureName);
    }
    
    /* 从 dictionary 获取 resource
       按照 type 查找，找不到抛出异常 */
    public LocalizationResource Get<TResource>()
    {
        var resourceType = typeof(TResource);
        
        var resource = this.GetOrDefault(resourceType);
        if (resource == null)
        {
            throw new AbpException("Can not find a resource with given type: " + resourceType.AssemblyQualifiedName);
        }
        
        return resource;
    }
}

```

#### 2.3 resource contributor

* abp框架定义的，为 localization resource 解析 localized string 的抽象服务

##### 2.3.1 接口

```c#
public interface ILocalizationResourceContributor
{
    void Initialize(LocalizationResourceInitializationContext context);    
    LocalizedString GetOrNull(string cultureName, string name);    
    void Fill(string cultureName, Dictionary<string, LocalizedString> dictionary);
}

```

##### 2.3.2  json file contributor

###### 2.3.2.1 基类

```c#
public abstract class VirtualFileLocalizationResourceContributorBase 
    : ILocalizationResourceContributor
{            
    private Dictionary<string, ILocalizationDictionary> _dictionaries;
        
    private bool _subscribedForChanges;
    private readonly object _syncObj = new object();
	
    // 注入 virtual_file_provider, json文件的 virtual_path
    private readonly string _virtualPath;
    private IVirtualFileProvider _virtualFileProvider;
    protected VirtualFileLocalizationResourceContributorBase(string virtualPath)
    {
        _virtualPath = virtualPath;
    } 
        
    // 注入 context 获取 virtual file provider    
    public void Initialize(LocalizationResourceInitializationContext context)
    {
        _virtualFileProvider = context.ServiceProvider
            .GetRequiredService<IVirtualFileProvider>();
    }
        
    // 获取 localized string    
    public LocalizedString GetOrNull(string cultureName, string name)
    {
        return GetDictionaries().GetOrDefault(cultureName)?.GetOrNull(name);
    }
    // 填充 localized string 到传入的 dictionary 中   
    public void Fill(string cultureName, Dictionary<string, LocalizedString> dictionary)
    {
        GetDictionaries().GetOrDefault(cultureName)?.Fill(dictionary);
    }            
        
    /* 获取或创建 dictionary */        
    private Dictionary<string, ILocalizationDictionary> GetDictionaries()
    {
        // 如果 _dictionaries 不为空，
        // 即 json resource file 没有变动（见下文）
        var dictionaries = _dictionaries;
        if (dictionaries != null)
        {
            return dictionaries;
        }
        
        lock (_syncObj)
        {
            dictionaries = _dictionaries;
            if (dictionaries != null)
            {
                return dictionaries;
            }
            // 订阅监视
            if (!_subscribedForChanges)
            {
                // 监视 virtual path 下文件变动
                // 如果有变动（修改了resource file），_dictionaries = null
                ChangeToken.OnChange(
                    () => _virtualFileProvider.Watch(_virtualPath.EnsureEndsWith('/') + "*.*"),
                    () => { _dictionaries = null; });
                
                _subscribedForChanges = true;
            }
            // 创建 dictionaries
            dictionaries = _dictionaries = CreateDictionaries();
        }
        
        return dictionaries;
    }
    
    /* 创建 dictionary */
    private Dictionary<string, ILocalizationDictionary> CreateDictionaries()
    {
        var dictionaries = new Dictionary<string, ILocalizationDictionary>();
        
        foreach (var file in _virtualFileProvider.GetDirectoryContents(_virtualPath))
        {
            // 忽略 virtual (json) path 下的文件夹和不能读取的文件
            if (file.IsDirectory || !CanParseFile(file))
            {
                continue;
            }
            // 读取json文件内容并将内容创建 IlocalizationDictionary
            var dictionary = CreateDictionaryFromFile(file);
            
            // 如果 dictionary 已经包含 culture_name，抛出异常
            // 即不同json文件不能包含同样的culture_name
            if (dictionaries.ContainsKey(dictionary.CultureName))
            {
                throw new AbpException(/**/);
            }
            // 按 cultureName 索引，添加 ILocalizationDictionary
            dictionaries[dictionary.CultureName] = dictionary;
        }
        
        return dictionaries;
    }
        
    /* 从 json file 创建 localized dictionary */
        
    protected abstract bool CanParseFile(IFileInfo file);
        
    protected virtual ILocalizationDictionary CreateDictionaryFromFile(IFileInfo file)
    {
        using (var stream = file.CreateReadStream())
        {
            return CreateDictionaryFromFileContent(
                Utf8Helper.ReadStringFromStream(stream));
        }
    }
        
    protected abstract ILocalizationDictionary CreateDictionaryFromFileContent(string fileContent);
    }
}

```

###### 2.3.2.2 resource initial context 

```c#

```

###### 2.3.2.3 实现

```c#
public class JsonVirtualFileLocalizationResourceContributor 
    : VirtualFileLocalizationResourceContributorBase
{
    public JsonVirtualFileLocalizationResourceContributor(string virtualPath)
        : base(virtualPath)
    {
    }
    // 判断是否可以读写成 json file provider，
    // 也就是判断是否是 json 类型的文件（通过扩展名判断）
    protected override bool CanParseFile(IFileInfo file)
    {
        return file.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }
    // 使用 json content 创建 localization dictionary    
    protected override ILocalizationDictionary CreateDictionaryFromFileContent(string jsonString)
    {
        return JsonLocalizationDictionaryBuilder.BuildFromJsonString(jsonString);
    }
}

```

###### 2.3.2.4 json dictionary builder

* 从 json 文件构建 localization dictionary
  * 逆序列化错误会抛出异常
  * 没有定义 culture 抛出异常
  * text中的key没有定义value抛出异常
  * text中的key重复定义抛出异常

```c#
public static class JsonLocalizationDictionaryBuilder
{    
    public static ILocalizationDictionary BuildFromFile(string filePath)
    {
        try
        {
            return BuildFromJsonString(File.ReadAllText(filePath));
        }
        catch (Exception ex)
        {
            throw new AbpException("Invalid localization file format: " + filePath, ex);
        }
    }
        
    public static ILocalizationDictionary BuildFromJsonString(string jsonString)
    {
        JsonLocalizationFile jsonFile;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            // 解析成 JsonLocalizationFile 类型
            jsonFile = JsonSerializer.Deserialize<JsonLocalizationFile>(jsonString, options);
        }
        catch (JsonException ex)
        {
            throw new AbpException("Can not parse json string. " + ex.Message);
        }
        
        var cultureCode = jsonFile.Culture;
        if (string.IsNullOrEmpty(cultureCode))
        {
            throw new AbpException("Culture is empty in language json file.");
        }
        
        var dictionary = new Dictionary<string, LocalizedString>();
        var dublicateNames = new List<string>();
        foreach (var item in jsonFile.Texts)
        {
            if (string.IsNullOrEmpty(item.Key))
            {
                throw new AbpException("The key is empty in given json string.");
            }
            
            if (dictionary.GetOrDefault(item.Key) != null)
            {
                dublicateNames.Add(item.Key);
            }
            
            dictionary[item.Key] = new LocalizedString(
                item.Key, item.Value.NormalizeLineEndings());
        }
        
        if (dublicateNames.Count > 0)
        {
            throw new AbpException(
                "A dictionary can not contain same key twice. There are some duplicated names: " + dublicateNames.JoinAsString(", "));
        }
        
        return new StaticLocalizationDictionary(cultureCode, dictionary);
    }
}

```

###### 2.3.2.5 localization json file 类型

```c#
public class JsonLocalizationFile
{
    /// <summary>
    /// Culture name; eg : en , en-us, zh-CN
    /// </summary>
    public string Culture { get; set; }    
    public Dictionary<string, string> Texts { get; set; }
    
    public JsonLocalizationFile()
    {
        Texts = new Dictionary<string, string>();
    }
}

```

#### 2.4 注册 resources

##### 2.4.1 定义 resource type

* resource_type 是区分 resource 的空类型
* resource_type用来索引`LocalizationResource`
* 用 resource_name_attribute 标记 resource name

###### 2.4.1.1 自定义 resource

```c#
[LocalizationResourceName("MyLocalization")]
public class MyLocalizationResource
{    
}

```

###### 2.4.1.2 default resource

```c#
[LocalizationResourceName("Default")]
public class DefaultResource
{    
}

```

##### 2.4.2 定义 resource json

* 必须符合`JsonLocalizationFile`格式
* culture 不能缺失
* text中的key不能重复，value不能为空

```json
{
  "culture": "en",
  "texts": {
    "DisplayName:Abp.Localization.DefaultLanguage": "Default language",
    "Description:Abp.Localization.DefaultLanguage": "The default language of the application."
  }
}

```

##### 2.4.3 配置 localization options

###### 2.4.3.1 abp localization options

```c#
public class AbpLocalizationOptions
{
    public Type DefaultResourceType { get; set; }
    
    public LocalizationResourceDictionary Resources { get; }            
    public ITypeList<ILocalizationResourceContributor> GlobalContributors { get; }
    
    public List<LanguageInfo> Languages { get; }    
    public Dictionary<string, List<NameValue>> LanguagesMap  { get; }    
    public Dictionary<string, List<NameValue>> LanguageFilesMap { get; }
    
    public AbpLocalizationOptions()
    {
        Resources = new LocalizationResourceDictionary();
        GlobalContributors = new TypeList<ILocalizationResourceContributor>();
        Languages = new List<LanguageInfo>();
        LanguagesMap = new Dictionary<string, List<NameValue>>();
        LanguageFilesMap = new Dictionary<string, List<NameValue>>();
    }
}

```

###### 2.4.3.2 options扩展

```c#
public static class AbpLocalizationOptionsExtensions
{
    public static AbpLocalizationOptions AddLanguagesMapOrUpdate(
        this AbpLocalizationOptions localizationOptions,
        string packageName,
        params NameValue[] maps)
    {
        foreach (var map in maps)
        {
            AddOrUpdate(localizationOptions.LanguagesMap, packageName, map);
        }        
        return localizationOptions;
    }

    public static string GetLanguagesMap(
        this AbpLocalizationOptions localizationOptions, 
        string packageName,
        string language)
    {
        return localizationOptions.LanguagesMap
            .TryGetValue(packageName, out var maps)
            	? maps.FirstOrDefault(x => x.Name == language)?.Value ?? language
            	: language;
    }

    public static string GetCurrentUICultureLanguagesMap(
        this AbpLocalizationOptions localizationOptions, 
        string packageName)
    {
        return GetLanguagesMap(
            localizationOptions, packageName, CultureInfo.CurrentUICulture.Name);
    }

    public static AbpLocalizationOptions AddLanguageFilesMapOrUpdate(
        this AbpLocalizationOptions localizationOptions,
        string packageName,
        params NameValue[] maps)
    {
        foreach (var map in maps)
        {
            AddOrUpdate(localizationOptions.LanguageFilesMap, packageName, map);
        }
        
        return localizationOptions;
    }

    public static string GetLanguageFilesMap(
        this AbpLocalizationOptions localizationOptions, 
        string packageName,
        string language)
    {
        return localizationOptions.LanguageFilesMap
            .TryGetValue(packageName, out var maps)
            	? maps.FirstOrDefault(x => x.Name == language)?.Value ?? language
                : language;
    }

    public static string GetCurrentUICultureLanguageFilesMap(
        this AbpLocalizationOptions localizationOptions, 
        string packageName)
    {
        return GetLanguageFilesMap(
            localizationOptions, packageName, CultureInfo.CurrentUICulture.Name);
    }

    private static void AddOrUpdate(
        IDictionary<string, List<NameValue>> maps, string packageName, NameValue value)
    {
        if (maps.TryGetValue(packageName, out var existMaps))
        {
            existMaps.GetOrAdd(x => x.Name == value.Name, () => value).Value = value.Value;
        }
        else
        {
            maps.Add(packageName, new List<NameValue> {value});
        }
    }
}

```

###### 2.4.3.3 向 options 中添加 resource

* 在自定义模块中的`ConfigureService()`方法中注册

* 见 2.4.4
* 通常会先注册 virtual file

##### 2.4.4 模块

```c#
[DependsOn(typeof(AbpVirtualFileSystemModule),
           typeof(AbpSettingsModule),
           typeof(AbpLocalizationAbstractionsModule))]
public class AbpLocalizationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        /*
        AbpStringLocalizerFactory.Replace(context.Services);
        */
        
        // 添加 virtual file
        Configure<AbpVirtualFileSystemOptions>(options =>
        	{
                options.FileSets.AddEmbedded<AbpLocalizationModule>("Volo.Abp", "Volo/Abp");
            });        
        
        // 向 localization options 中注册 localization resource
        Configure<AbpLocalizationOptions>(options =>
            {
                options.Resources.Add<DefaultResource>("en");
                options.Resources
                    .Add<AbpLocalizationResource>("en")
                    .AddVirtualJson("/Localization/Resources/AbpLocalization");
            });
    }
}

```

#### 2.5 string localizer

* abp框架使用 .net core 定义的`IStringLocalizer`向上层架构提供服务

##### 2.5.1 接口

###### 2.5.1.1 `IStringLocalizer`

```c#
// .net core extensions
public interface IStringLocalizer
{        
    LocalizedString this[string name] { get; }        
    LocalizedString this[string name, params object[] arguments] { get; }         
    IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures);    
}

```

###### 2.5.1.2 扩展

```c#
// .net core extensions
public static class StringLocalizerExtensions
{    
    public static LocalizedString GetString(
        this IStringLocalizer stringLocalizer,
        string name)
    {
        if (stringLocalizer == null)
        {
            throw new ArgumentNullException(nameof(stringLocalizer));
        }        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        return stringLocalizer[name];
    }
    
    public static LocalizedString GetString(
        this IStringLocalizer stringLocalizer,
        string name,
        params object[] arguments)
    {
        if (stringLocalizer == null)
        {
            throw new ArgumentNullException(nameof(stringLocalizer));
        }        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        return stringLocalizer[name, arguments];
    }
        
    public static IEnumerable<LocalizedString> GetAllStrings(this IStringLocalizer stringLocalizer)
    {
        if (stringLocalizer == null)
        {
            throw new ArgumentNullException(nameof(stringLocalizer));
        }
        
        return stringLocalizer.GetAllStrings(includeParentCultures: true);
    }
}

```

##### 2.5.2 net core 实现

###### 2.5.2.1 `StringLocalizer`

* .net core 定义的、上层架构使用的服务的封装
* 利用传入的`IStringLocalizerFactory`创建内容真正的`IStringLocalizer`

###### 2.5.2.2 `ResourceManagerStringLocalizer`

* .net core定义的、真正的 localizer

##### 2.5.3 abp扩展

* abp框架对`IStringLocalizer`进行了扩展，支持 default resource
* abp框架实现了 localizer

###### 2.5.3.1 接口扩展

```c#
public static class AbpStringLocalizerExtensions
{
    // 获取 inner localizer，即 .net core 的实现
    [NotNull]
    public static IStringLocalizer GetInternalLocalizer(
        [NotNull] this IStringLocalizer stringLocalizer)
    {
        Check.NotNull(stringLocalizer, nameof(stringLocalizer));        
        var localizerType = stringLocalizer.GetType();
        
        // 如果 localizerType 不是 StringLocalizer，
        // 即 localizerType 就是 original IStringLocalizer
        if (!ReflectionHelper.IsAssignableToGenericType(localizerType, typeof(StringLocalizer<>)))
        {
            return stringLocalizer;
        }
        
        // localizerType 是 StringLocalizer,
        // 返回 _localizer
        var localizerField = localizerType.GetField(
            "_localizer",
            BindingFlags.Instance | 
            BindingFlags.NonPublic);

        if (localizerField == null)
        {
            throw new AbpException($"Could not find the _localizer field inside the {typeof(StringLocalizer<>).FullName} class. Probably its name has changed. Please report this issue to the ABP framework.");
        }

        return localizerField.GetValue(stringLocalizer) as IStringLocalizer;
    }
    
    // 扩展的get all，包括继承的基类
    public static IEnumerable<LocalizedString> GetAllStrings(
        this IStringLocalizer stringLocalizer,
        bool includeParentCultures,
        bool includeBaseLocalizers)
    {
        var internalLocalizer = (ProxyHelper.UnProxy(stringLocalizer) as IStringLocalizer).GetInternalLocalizer();
        if (internalLocalizer is IStringLocalizerSupportsInheritance stringLocalizerSupportsInheritance)
        {
            return stringLocalizerSupportsInheritance.GetAllStrings(
                includeParentCultures,
                includeBaseLocalizers
            );
        }
        
        return stringLocalizer.GetAllStrings(
            includeParentCultures
        );
    }
}

```

###### 2.5.3.2 abp dict string localizer

```c#
public class AbpDictionaryBasedStringLocalizer 
    : IStringLocalizer, IStringLocalizerSupportsInheritance
{
    // 注入 localization resource，base string localizer
    public LocalizationResource Resource { get; }
    public List<IStringLocalizer> BaseLocalizers { get; }    
    public AbpDictionaryBasedStringLocalizer(
        LocalizationResource resource, List<IStringLocalizer> baseLocalizers)
    {
        Resource = resource;
        BaseLocalizers = baseLocalizers;
    }
    
    // 索引器方法
    public virtual LocalizedString this[string name] => 
        GetLocalizedString(name);
    public virtual LocalizedString this[string name, params object[] arguments] =>
        GetLocalizedStringFormatted(name, arguments);    
        
    // get all 方法
    // 会使用系统的 CultureInfo
    public IEnumerable<LocalizedString> GetAllStrings(
        bool includeParentCultures)
    {
        return GetAllStrings(
            CultureInfo.CurrentUICulture.Name, includeParentCultures);
    }
    public IEnumerable<LocalizedString> GetAllStrings(
        bool includeParentCultures, bool includeBaseLocalizers)
    {
        return GetAllStrings(
            CultureInfo.CurrentUICulture.Name,
            includeParentCultures,
            includeBaseLocalizers);
    }

    /* get localized string具体的实现方法 */
        
    // 格式化的 localized string 方法   
    // 如果没有指定，使用系统 CultureInfo
    // ->
    protected virtual LocalizedString GetLocalizedStringFormatted(
        string name, params object[] arguments)
    {
        return GetLocalizedStringFormatted(
            name, CultureInfo.CurrentUICulture.Name, arguments);
    }
    protected virtual LocalizedString GetLocalizedStringFormatted(
        string name, string cultureName, params object[] arguments)
    {
        var localizedString = GetLocalizedString(name, cultureName);
        return new LocalizedString(
            name, 
            string.Format(localizedString.Value, arguments), 
            localizedString.ResourceNotFound, 
            localizedString.SearchedLocation);
    }

    // get localized string 方法
    // 如果没有指定，使用系统 CultureInfo
    // ->
    protected virtual LocalizedString GetLocalizedString(string name)
    {
        return GetLocalizedString(name, CultureInfo.CurrentUICulture.Name);
    }
    protected virtual LocalizedString GetLocalizedString(string name, string cultureName)     {
        // 从 contributors 获取 localized string
        var value = GetLocalizedStringOrNull(name, cultureName);
        
        // 如果没找到，从继承的 localizer 基类中查找
        if (value == null)
        {
            foreach (var baseLocalizer in BaseLocalizers)
            {
                using (CultureHelper.Use(CultureInfo.GetCultureInfo(cultureName)))
                {
                    var baseLocalizedString = baseLocalizer[name];
                    if (baseLocalizedString != null && 
                        !baseLocalizedString.ResourceNotFound)
                    {
                        return baseLocalizedString;
                    }
                }
            }
            
            return new LocalizedString(name, name, resourceNotFound: true);
        }
        
        return value;
    }

    // 从 contributors 中 get localized string
    // 需要指定 cultrueName，从上层获取（没有指定的话是系统的 CultureInfo）        
    protected virtual LocalizedString GetLocalizedStringOrNull(
        string name, string cultureName, bool tryDefaults = true)
    {
        //Try to get from original dictionary (with country code)
        var strOriginal = Resource.Contributors.GetOrNull(cultureName, name);
        if (strOriginal != null)
        {
            return strOriginal;
        }
        
        if (!tryDefaults)
        {
            return null;
        }
        
        //Try to get from same language dictionary (without country code)
        if (cultureName.Contains("-")) //Example: "tr-TR" (length=5)
        {
            var strLang = Resource.Contributors.
                GetOrNull(CultureHelper.GetBaseCultureName(cultureName), name);
            if (strLang != null)
            {
                return strLang;
            }
        }

        //Try to get from default language
        if (!Resource.DefaultCultureName.IsNullOrEmpty())
        {
            var strDefault = Resource.Contributors
                .GetOrNull(Resource.DefaultCultureName, name);
            if (strDefault != null)
            {
                return strDefault;
            }
        }
        
        //Not found
        return null;
    }

        
    /* get all string 的具体实现方法*/     
                
    protected virtual IReadOnlyList<LocalizedString> GetAllStrings(
        string cultureName,
        bool includeParentCultures = true,
        bool includeBaseLocalizers = true)
    {        
        var allStrings = new Dictionary<string, LocalizedString>();
        
        // 获取基类 localized string        
        if (includeBaseLocalizers)
        {
            foreach (var baseLocalizer in BaseLocalizers.Select(l => l))
            {
                // 因为基类可能是 .net core 实现，
                // using块包裹cultureName临时值
                using (CultureHelper.Use(CultureInfo.GetCultureInfo(cultureName)))
                {
                    //TODO: Try/catch is a workaround here!
                    try
                    {
                        var baseLocalizedString = baseLocalizer
                            .GetAllStrings(includeParentCultures);
                        foreach (var localizedString in baseLocalizedString)
                        {
                            allStrings[localizedString.Name] = localizedString;
                        }
                    }
                    catch (MissingManifestResourceException)
                    {                        
                    }
                }
            }
        }
        
        // 获取 parent culture 的 localized string
        if (includeParentCultures)
        {
            //Fill all strings from default culture
            if (!Resource.DefaultCultureName.IsNullOrEmpty())
            {
                Resource.Contributors.Fill(
                    Resource.DefaultCultureName, allStrings);
            }
            
            //Overwrite all strings from the language based on country culture
            if (cultureName.Contains("-"))
            {
                Resource.Contributors.Fill(
                    CultureHelper.GetBaseCultureName(cultureName), allStrings);
            }
        }
        
        //Overwrite all strings from the original culture
        Resource.Contributors.Fill(cultureName, allStrings);
        
        return allStrings.Values.ToImmutableList();
    }
}    
    
```

###### 2.5.3.3 abp localizer wrap culture

* 可以指定 culture

```c#
public class CultureWrapperStringLocalizer 
    : IStringLocalizer, IStringLocalizerSupportsInheritance
{
    private readonly string _cultureName;
    private readonly AbpDictionaryBasedStringLocalizer _innerLocalizer;
    public CultureWrapperStringLocalizer(
        string cultureName, AbpDictionaryBasedStringLocalizer innerLocalizer)
    {
        _cultureName = cultureName;
        _innerLocalizer = innerLocalizer;
    }
        
        
        
    LocalizedString IStringLocalizer.this[string name] => 
        _innerLocalizer.GetLocalizedString(name, _cultureName);

    LocalizedString IStringLocalizer.this[string name, params object[] arguments] => 
        _innerLocalizer.GetLocalizedStringFormatted(name, _cultureName, arguments);

        
        
    public IEnumerable<LocalizedString> GetAllStrings(
        bool includeParentCultures)
    {
        return _innerLocalizer.GetAllStrings(
            _cultureName, includeParentCultures);
    }
        
    public IEnumerable<LocalizedString> GetAllStrings(
        bool includeParentCultures, bool includeBaseLocalizers)
    {
        return _innerLocalizer.GetAllStrings(
            _cultureName, includeParentCultures, includeBaseLocalizers);
    }    
}

```

#### 2.6 string localizer factory

* abp框架与 .net core 一样，使用`IStringLocalizerFactory`创建`IStringLocalizer`
* 并且扩展了工厂方法，可以创建 abp string localizer 的实现

##### 2.6.1 接口

```c#
// from .net core
public interface IStringLocalizerFactory
{
    IStringLocalizer Create(Type resourceSource);
    IStringLocalizer Create(string baseName, string location);
}

```

##### 2.6.2 abp 扩展

###### 2.6.2.1 接口扩展

```c#
public interface IAbpStringLocalizerFactoryWithDefaultResourceSupport
{
    [CanBeNull]
    IStringLocalizer CreateDefaultOrNull();
}

```

```c#
public static IStringLocalizer CreateDefaultOrNull(this IStringLocalizerFactory localizerFactory)
{
    return (localizerFactory as IAbpStringLocalizerFactoryWithDefaultResourceSupport)
        ?.CreateDefaultOrNull();
}

```

###### 2.6.2.2 abp factory

```c#
public class AbpStringLocalizerFactory 
    : IStringLocalizerFactory, IAbpStringLocalizerFactoryWithDefaultResourceSupport
{
    // 注入服务
    protected internal AbpLocalizationOptions AbpLocalizationOptions { get; }
    protected ResourceManagerStringLocalizerFactory InnerFactory { get; }
    protected IServiceProvider ServiceProvider { get; }
        
    protected ConcurrentDictionary<Type, StringLocalizerCacheItem> LocalizerCache { get; }

    //TODO: It's better to use decorator pattern for IStringLocalizerFactory instead of getting ResourceManagerStringLocalizerFactory as a dependency.
    public AbpStringLocalizerFactory(
        ResourceManagerStringLocalizerFactory innerFactory,
        IOptions<AbpLocalizationOptions> abpLocalizationOptions,
        IServiceProvider serviceProvider)
    {
        InnerFactory = innerFactory;
        ServiceProvider = serviceProvider;
        AbpLocalizationOptions = abpLocalizationOptions.Value;
        
        // 创建 IStringLocalizer 集合
        // 使用 resource type 索引
        LocalizerCache = new ConcurrentDictionary<Type, StringLocalizerCacheItem>();
    }

        
    public IStringLocalizer CreateDefaultOrNull()
    {
        if (AbpLocalizationOptions.DefaultResourceType == null)
        {
            return null;
        }
        
        return Create(AbpLocalizationOptions.DefaultResourceType);
    }    
        
    public virtual IStringLocalizer Create(Type resourceType)
    {
        var resource = AbpLocalizationOptions.Resources.GetOrDefault(resourceType);
        
        // 如果 localization resource 中没有注册 resourceType，
        // 说明没有定义 abp resource，转到 .net core 默认实现（resource manager localizer）
        if (resource == null)
        {
            return InnerFactory.Create(resourceType);
        }
        
        // 如果有，从 cache 中获取 string localizer
        if (LocalizerCache.TryGetValue(resourceType, out var cacheItem))
        {
            return cacheItem.Localizer;
        }
        
        // 如果没有，创建新的 localizer 并存入 cache
        // （实际是直接创建 cache item，返回 localizer）
        lock (LocalizerCache)
        {
            return LocalizerCache.GetOrAdd(
                resourceType,
                _ => CreateStringLocalizerCacheItem(resource)
            ).Localizer;
        }
    }            
        
    // 创建 localizer cache item 的具体方法
    private StringLocalizerCacheItem CreateStringLocalizerCacheItem(
        LocalizationResource resource)
    {
        // resource 中添加 global contributors
        foreach (var globalContributor in AbpLocalizationOptions.GlobalContributors)
        {
            resource.Contributors.Add(
                (ILocalizationResourceContributor)Activator
                	.CreateInstance(globalContributor));
        }
        
        var context = new LocalizationResourceInitializationContext(
            resource, ServiceProvider);
		
        // initial all contributors
        foreach (var contributor in resource.Contributors)
        {
            contributor.Initialize(context);
        }
        
        return new StringLocalizerCacheItem(
            new AbpDictionaryBasedStringLocalizer(
                resource,
                resource.BaseResourceTypes.Select(Create).ToList()
            )
        );
    }
    
    /*
    public virtual IStringLocalizer Create(string baseName, string location)
    {
        //TODO: Investigate when this is called?
        
        return InnerFactory.Create(baseName, location);
    }
    */
        
    internal static void Replace(IServiceCollection services)
    {
        services.Replace(
            ServiceDescriptor.Singleton
            	<IStringLocalizerFactory, AbpStringLocalizerFactory>());
        
        services.AddSingleton<ResourceManagerStringLocalizerFactory>();
    }

    protected class StringLocalizerCacheItem
    {
        public AbpDictionaryBasedStringLocalizer Localizer { get; }
        
        public StringLocalizerCacheItem(AbpDictionaryBasedStringLocalizer localizer)
        {
            Localizer = localizer;
        }
    }
        
    
}

```

#### 2.7 注册 localizer factory

##### 2.7.1 模块

```c#
[DependsOn(typeof(AbpVirtualFileSystemModule),
           typeof(AbpSettingsModule),
           typeof(AbpLocalizationAbstractionsModule))]
public class AbpLocalizationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        AbpStringLocalizerFactory.Replace(context.Services);
        
        // ...
    }
}

```

##### 2.7.2 abp localizer factory

```c#
public class AbpStringLocalizerFactory 
{
    // ...
    
    internal static void Replace(IServiceCollection services)
    {
        // 使用 abp localizer factory 替换
        services.Replace(
            ServiceDescriptor.Singleton
            	<IStringLocalizerFactory, AbpStringLocalizerFactory>());
        
        // 注入原工厂（支持原有resource.resx解析）
        services.AddSingleton<ResourceManagerStringLocalizerFactory>();
    }
}

```

##### 2.7.3 framework

* localization 是abp框架的核心服务，在框架启动时注入

  ```c#
  
  service.AddLocalization();
  
  ```

  

#### 2.8 localizable string

另一种面向上层架构的服务接口

```c#
public interface ILocalizableString
{
    LocalizedString Localize(IStringLocalizerFactory stringLocalizerFactory);
}

```

```c#
public class LocalizableString : ILocalizableString
{
    [CanBeNull]
    public Type ResourceType { get; }
    
    [NotNull]
    public string Name { get; }
    
    public LocalizableString(Type resourceType, [NotNull] string name)
    {
        Name = Check.NotNullOrEmpty(name, nameof(name));
        ResourceType = resourceType;
    }
    
    public LocalizedString Localize(IStringLocalizerFactory stringLocalizerFactory)
    {
        return stringLocalizerFactory.Create(ResourceType)[Name];
    }
    
    public static LocalizableString Create<TResource>([NotNull] string name)
    {
        return new LocalizableString(typeof(TResource), name);
    }
}

```

* static 方法比较好用

### 3. practice

* 创建 xxxResources 文件夹，cproj 中标记为 "embedded" 类型
  * 在文件夹中定义`xxxResource`的空类型
    * 指定 resource_name 特性
  * 创建具体的资源文件，注意文件名，"en.json" 或 "zh-Hans.json"
* 在自定义模块中添加 resource 所在文件夹为 embedded virtual file
* 在自定义模块中添加 resource 并指定 json文件的文件夹（xxxResources）
* 注入`IStringLocalizer`使用，或者`LocalizableString.Create()`静态方法