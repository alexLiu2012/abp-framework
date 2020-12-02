## about virtual file system

TODO:

.net core physical file provider 的源码，root不存在是会不会抛异常？？

添加wwwroot（在aspnet模块中）



相关程序集：

* Volo.Abp.VirtualFileSystem

----

### 1. about

* abp框架扩展了microsoft.extensions.fileprovider
* 可以抽象访问文件，如配置、静态资源等
* 抽象了本地和远程文件操作，统一为 virtual_file

### 2. details

#### 2.1 virtual file set info

* virtual_file_set_info 是（虚拟）资源文件的抽象

##### 2.1.1 抽象基类

```c#
public class VirtualFileSetInfo
{
    public IFileProvider FileProvider { get; }    
    public VirtualFileSetInfo([NotNull] IFileProvider fileProvider)
    {
        FileProvider = Check.NotNull(fileProvider, nameof(fileProvider));
    }
}

```

##### 2.1.2 实现

###### 2.1.2.1 embedded virutal_file_set_info

```c#
public class EmbeddedVirtualFileSetInfo : VirtualFileSetInfo
{
    public Assembly Assembly { get; }    
    public string BaseFolder { get; }
        
    public EmbeddedVirtualFileSetInfo(
        IFileProvider fileProvider, 
        Assembly assembly,
        string baseFolder = null) 
	        : base(fileProvider)
    {
        Assembly = assembly;
        BaseFolder = baseFolder;
    }
}

```

###### 2.1.2.2 physical virtual_file_set_info

```c#
public class PhysicalVirtualFileSetInfo : VirtualFileSetInfo
{
    public string Root { get; }
        
    public PhysicalVirtualFileSetInfo(
        [NotNull] IFileProvider fileProvider,
        [NotNull] string root)
	        : base(fileProvider)
    {
        Root = Check.NotNullOrWhiteSpace(root, nameof(root));
    }
}

```

#### 2.2 dictionary file provider

* 资源文件读取的真正提供者
* .net core `IFileProvider`的封装

##### 2.2.1 定义

```c#
// .net core 的 IFileProvider
public interface IFileProvider
{
    IFileInfo GetFileInfo(string subPath);
    IDirectoryContents GetDirectoryContents(string subPath);
    IChangeToken Watch(string filter);
}

```

```c#
public abstract class DictionaryBasedFileProvider : IFileProvider
{
    // 保存 file_info 的容器
    protected abstract IDictionary<string, IFileInfo> Files { get; }
    
    // get file info
    public virtual IFileInfo GetFileInfo(string subpath)
    {
        if (subpath == null)
        {
            return new NotFoundFileInfo(subpath);
        }  
        
        var file = Files.GetOrDefault(NormalizePath(subpath));        
        if (file == null)
        {
            return new NotFoundFileInfo(subpath);
        }
        
        return file;
    }
    
    // get directory contents
    public virtual IDirectoryContents GetDirectoryContents(string subpath)
    {
        var directory = GetFileInfo(subpath);
        if (!directory.IsDirectory)
        {
            return NotFoundDirectoryContents.Singleton;
        }
        
        var fileList = new List<IFileInfo>();        
        var directoryPath = subpath.EnsureEndsWith('/');        
        foreach (var fileInfo in Files.Values)
        {
            var fullPath = fileInfo.GetVirtualOrPhysicalPathOrNull();
            if (!fullPath.StartsWith(directoryPath))
            {
                continue;
            }
            
            var relativePath = fullPath.Substring(directoryPath.Length);
            if (relativePath.Contains("/"))
            {
                continue;
            }
            
            fileList.Add(fileInfo);
        }
        
        return new EnumerableDirectoryContents(fileList);
    }
    
    // 返回 nullToken
    // 在派生类中具体实现
    public virtual IChangeToken Watch(string filter)
    {
        return NullChangeToken.Singleton;
    }
    
    protected virtual string NormalizePath(string subpath)
    {
        return subpath;
    }
}

```

##### 2.2.2 embedded file provider

* 获取程序集中定义的 embedded 文件

```c#
public class AbpEmbeddedFileProvider : DictionaryBasedFileProvider
{
    [NotNull]
    public Assembly Assembly { get; }    
    [CanBeNull]
    public string BaseNamespace { get; }
    // 重写 files（字典）
    private readonly Lazy<Dictionary<string, IFileInfo>> _files;
    protected override IDictionary<string, IFileInfo> Files => _files.Value;
    
    // 根据 assembly 创建 embedded_file_provider
    public AbpEmbeddedFileProvider(
        [NotNull] Assembly assembly, 
        [CanBeNull] string baseNamespace = null)
    {
        Check.NotNull(assembly, nameof(assembly));        
        Assembly = assembly;
        BaseNamespace = baseNamespace;
        
        _files = new Lazy<Dictionary<string, IFileInfo>>(CreateFiles,true);
    }
    
    private Dictionary<string, IFileInfo> CreateFiles()
    {
        var files = new Dictionary<string, IFileInfo>(StringComparer.OrdinalIgnoreCase);
        AddFiles(files);
        return files;
    }
    
    // 将 assembly 的 manifest resource 注入 files
    public void AddFiles(Dictionary<string, IFileInfo> files)
    {
        var lastModificationTime = GetLastModificationTime();        
        foreach (var resourcePath in Assembly.GetManifestResourceNames())
        {
            // 忽略不是 baseNamespace 下的 resource
            if (!BaseNamespace.IsNullOrEmpty() && !resourcePath.StartsWith(BaseNamespace))
            {
                continue;
            }
            // 将 namesapce 转化为路径
            var fullPath = ConvertToRelativePath(resourcePath).EnsureStartsWith('/');
            
            // 如果 resource 是文件夹，递归的加入所有子资源
            if (fullPath.Contains("/"))
            {
                AddDirectoriesRecursively(
                    files, 
                    fullPath.Substring(0, fullPath.LastIndexOf('/')), 
                    lastModificationTime);
            }
            // resource 是文件，直接加入
            files[fullPath] = new EmbeddedResourceFileInfo(
                Assembly,
                resourcePath,
                fullPath,
                CalculateFileName(fullPath),
                lastModificationTime
            );
        }
    }
    
    private static void AddDirectoriesRecursively(
        Dictionary<string, IFileInfo> files, 
        string directoryPath, 
        DateTimeOffset lastModificationTime)
    {
        if (files.ContainsKey(directoryPath))
        {
            return;
        }
        
        files[directoryPath] = new VirtualDirectoryFileInfo(
            directoryPath,
            CalculateFileName(directoryPath),
            lastModificationTime
        );
        
        if (directoryPath.Contains("/"))
        {
            AddDirectoriesRecursively(
                files, 
                directoryPath.Substring(0, directoryPath.LastIndexOf('/')),
                lastModificationTime);
        }
    }
    
    private DateTimeOffset GetLastModificationTime()
    {
        var lastModified = DateTimeOffset.UtcNow;
        
        if (!string.IsNullOrEmpty(Assembly.Location))
        {
            try
            {
                lastModified = File.GetLastWriteTimeUtc(Assembly.Location);
            }
            catch (PathTooLongException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        
        return lastModified;
    }
    
    private string ConvertToRelativePath(string resourceName)
    {
        if (!BaseNamespace.IsNullOrEmpty())
        {
            resourceName = resourceName.Substring(BaseNamespace.Length + 1);
        }
        
        var pathParts = resourceName.Split('.');
        if (pathParts.Length <= 2)
        {
            return resourceName;
        }
        
        var folder = pathParts.Take(pathParts.Length - 2).JoinAsString("/");
        var fileName = pathParts[pathParts.Length - 2] + "." + pathParts[pathParts.Length - 1];
        
        return folder + "/" + fileName;
    }
    
    private static string CalculateFileName(string filePath)
    {
        if (!filePath.Contains("/"))
        {
            return filePath;
        }        
        return filePath.Substring(filePath.LastIndexOf("/", StringComparison.Ordinal) + 1);
    }
    
    protected override string NormalizePath(string subpath)
    {
        return VirtualFilePathHelper.NormalizePath(subpath);
    }        
}

```

###### 2.2.2.1 embedded file path normalize

* 将 namespace 转化为路径

```c#
internal static class VirtualFilePathHelper
{
    public static string NormalizePath(string fullPath)
    {
        if (fullPath.Equals("/", StringComparison.Ordinal))
        {
            return string.Empty;
        }
        
        var fileName = fullPath;
        var extension = "";
        
        if (fileName.Contains("."))
        {
            extension = fullPath.Substring(
                fileName.LastIndexOf(".", StringComparison.Ordinal));
            if (extension.Contains("/"))
            {
                //That means the file does not have extension, but a directory has "." char. So, clear extension.
                extension = "";
            }
            else
            {
                fileName = fullPath.Substring(0, fullPath.Length - extension.Length);
            }
        }
        
        return NormalizeChars(fileName) + extension;
    }
    
    private static string NormalizeChars(string fileName)
    {
        var folderParts = fileName.Replace(".", "/").Split("/");
        
        if (folderParts.Length == 1)
        {
            return folderParts[0];
        }
        
        return folderParts.Take(folderParts.Length - 1)
            .Select(s => s.Replace("-", "_")).JoinAsString("/") + "/" + folderParts.Last();
        }
    }
}                                   
                                   
```

##### 2.2.3 dynamic file provider

* 模块自动注册

```c#
public interface IDynamicFileProvider : IFileProvider
{
    void AddOrUpdate(IFileInfo fileInfo);    
    bool Delete(string filePath);
}

```

```c#
public class DynamicFileProvider 
    : DictionaryBasedFileProvider, IDynamicFileProvider, ISingletonDependency
{
    protected override IDictionary<string, IFileInfo> Files => DynamicFiles;
    protected ConcurrentDictionary<string, IFileInfo> DynamicFiles { get; }
    protected ConcurrentDictionary<string, ChangeTokenInfo> FilePathTokenLookup { get; }
    public DynamicFileProvider()
    {
        FilePathTokenLookup = new ConcurrentDictionary<string, ChangeTokenInfo>(StringComparer.OrdinalIgnoreCase);
        DynamicFiles = new ConcurrentDictionary<string, IFileInfo>();
    }
        
    public void AddOrUpdate(IFileInfo fileInfo)
    {
        var filePath = fileInfo.GetVirtualOrPhysicalPathOrNull();
        DynamicFiles.AddOrUpdate(filePath, fileInfo, (key, value) => fileInfo);
        
        ReportChange(filePath);
    }
        
    public bool Delete(string filePath)
    {
        if (!DynamicFiles.TryRemove(filePath, out _))
        {
            return false;
        }
        
        ReportChange(filePath);
        return true;
    }
        
    public override IChangeToken Watch(string filter)
    {
        return GetOrAddChangeToken(filter);
    }
        
    private IChangeToken GetOrAddChangeToken(string filePath)
    {
        if (!FilePathTokenLookup.TryGetValue(filePath, out var tokenInfo))
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationChangeToken = new CancellationChangeToken(
                cancellationTokenSource.Token);
            tokenInfo = new ChangeTokenInfo(cancellationTokenSource, cancellationChangeToken);             tokenInfo = FilePathTokenLookup.GetOrAdd(filePath, tokenInfo);
        }
        
        return tokenInfo.ChangeToken;
    }
        
    private void ReportChange(string filePath)
    {
        if (FilePathTokenLookup.TryRemove(filePath, out var tokenInfo))
        {
            tokenInfo.TokenSource.Cancel();
        }
    }
        
    protected struct ChangeTokenInfo
    {
        public ChangeTokenInfo(
            CancellationTokenSource tokenSource,
            CancellationChangeToken changeToken)
        {
            TokenSource = tokenSource;
            ChangeToken = changeToken;
        }
        
        public CancellationTokenSource TokenSource { get; }        
        public CancellationChangeToken ChangeToken { get; }
    }
}

```

#### 2.3 注册virtual file

* 自定义模块，依赖`AbpVirtualFileSystemModule`
* 在自定义模块中配置`AbpVirtualFileSystemOptions`

##### 2.3.1 virtual file module

```c#
public class AbpVirtualFileSystemModule : AbpModule
{    
}

```

##### 2.3.2 virtual file options

* 是预加载的 virtual_file 容器

```c#
public class AbpVirtualFileSystemOptions
{
    public VirtualFileSetList FileSets { get; }    
    public AbpVirtualFileSystemOptions()
    {
        FileSets = new VirtualFileSetList();
    }
}

```

###### 2.3.2.1 virtual_file_set_info 集合

```c#
public class VirtualFileSetList : List<VirtualFileSetInfo>
{
}

```

##### 2.3.3 virtual file 集合 crud

###### 2.3.3.1 添加 embedded virtual file

```c#
public static class VirtualFileSetListExtensions
{
    // 添加 embedded virtual file
    public static void AddEmbedded<T>(
        [NotNull] this VirtualFileSetList list,
        [CanBeNull] string baseNamespace = null,
        [CanBeNull] string baseFolder = null)
    {
        Check.NotNull(list, nameof(list));        
        var assembly = typeof(T).Assembly;
        // 创建 embedded virtual_file_set_info
        var fileProvider = CreateFileProvider(
            assembly,
            baseNamespace,
            baseFolder
        );
        // 向集合添加 embedded virtual file set
        list.Add(new EmbeddedVirtualFileSetInfo(fileProvider, assembly, baseFolder));
    }
    
    private static IFileProvider CreateFileProvider(
        [NotNull] Assembly assembly,
        [CanBeNull] string baseNamespace = null,
        [CanBeNull] string baseFolder = null)
    {
        Check.NotNull(assembly, nameof(assembly));
        
        // 获取 .proj 文件信息        
        var info = assembly.GetManifestResourceInfo
            ("Microsoft.Extensions.FileProviders.Embedded.Manifest.xml");
		
        // 如果没有在 .proj 文件指明 manifest_embedded,
        // 使用 assembly（所在文件路径等）创建 provider
        // 此时 baseFolder 不起作用
        if (info == null)
        {
            return new AbpEmbeddedFileProvider(assembly, baseNamespace);
        }
        
        // 使用 .proj 文件创建 .net core provider
        if (baseFolder == null)
        {
            return new ManifestEmbeddedFileProvider(assembly);
        }        
        return new ManifestEmbeddedFileProvider(assembly, baseFolder);
    }
}
        
```

###### 2.3.3.2 添加 physical virtual file

```c#
public static class VirtualFileSetListExtensions
{
    // 添加 physical virtual file
    public static void AddPhysical(
        [NotNull] this VirtualFileSetList list,
        [NotNull] string root,
        ExclusionFilters exclusionFilters = ExclusionFilters.Sensitive)
    {
        Check.NotNull(list, nameof(list));
        Check.NotNullOrWhiteSpace(root, nameof(root));
        // 使用 .net core 方法创建 physical_file_provider
        // 创建 physical virtual_file_set_info
        var fileProvider = new PhysicalFileProvider(root, exclusionFilters);
        // 向集合添加 physical virtual file set
        list.Add(new PhysicalVirtualFileSetInfo(fileProvider, root));
    }
}

```

* 使用了 .net core `PhysicalFileProvider`方法

  ```c#
  public PhysicalFileProvider (string root, ExclusionFilters filters)
  {
      // ...
  }
  
  ```

  * exclusion filter

    ```c#
    [System.Flags]
    public enum ExclusionFilters
    {
        None: 0,			// 不排除任何文件
        DotPrefixed: 1,		// 排除 '.' 开头的文件和文件夹
        Hidden: 2,			// 排除标记了Hidden属性的文件和文件夹
        System: 4,			// 排除标记了System属性的文件和文件夹
        Sensitive: 7		// 1+2+4
    }
    ```

###### 2.3.2.3 替换 embedded resource

* 不同模块的同名 resource（相同的 virtual file directory & path），后加载模块会覆盖前模块
  * 在模块中定义与其他模块（父模块）同名 resource 可以实现重写(override)
  * 也可以指定 physical_file_provider 代替原有的 resource

```c#
public static class VirtualFileSetListExtensions
{                        
    public static void ReplaceEmbeddedByPhysical<T>(
        [NotNull] this VirtualFileSetList fileSets,
        [NotNull] string physicalPath)
    {
        Check.NotNull(fileSets, nameof(fileSets));
        Check.NotNullOrWhiteSpace(physicalPath, nameof(physicalPath));
        
        var assembly = typeof(T).Assembly;
        
        for (var i = 0; i < fileSets.Count; i++)
        {
            // 如果是 embedded resource,
            // 并且所属 assembly 与 T（resource type）相同
            // -> 用 physical 替换 resource
            if (fileSets[i] is EmbeddedVirtualFileSetInfo embeddedVirtualFileSet &&
                embeddedVirtualFileSet.Assembly == assembly)
            {
                var thisPath = physicalPath;
                
                if (!embeddedVirtualFileSet.BaseFolder.IsNullOrEmpty())
                {
                    thisPath = Path.Combine(thisPath, embeddedVirtualFileSet.BaseFolder);
                }
                
                fileSets[i] = new PhysicalVirtualFileSetInfo(
                    new PhysicalFileProvider(thisPath), thisPath);
            }
        }
    }
}

```

##### 2.3.3 添加 virtual_file

* 在自定义模块的`ConfigureService`方法中配置`AbpVirtualFileSystemOptions`

###### 2.3.3.1 定义resource文件夹

* 在 project 中创建 resource 文件夹；
* 在文件夹中创建空类型 TResource，用于指定 assembly ；
* 添加 resource 文件

###### 2.3.3.2 在 options 中添加 resource 文件

```c#
[DependsOn(typeof(AbpVirtulaFileSystemModule))]
public class MyModule : AbpModule
{
    public override void ConfigureService(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        	{
                // 加入 'TheWrapperType' 程序集、文件夹所有的 manifest resource
                options.FileSets.AddEmbedded<TheWrapperType>();
                // root 如果不存在？？
                options.FileSets.AddPhyscial("/hello/world");            
            });
    }
}

```

#### 2.4 注册wwwroot

TODO

#### 2.5 virtual file provider

* 提供给上层服务使用的接口
* .net core `IFileProvider`的封装（兼容 asp.net core）

##### 2.5.1 接口

```c#
public interface IVirtualFileProvider : IFileProvider
{    
    // get fileInfo
    // get directoryContexts
    // get changeToken
}

```

##### 2.5.2 实现

* 模块中自动注册

```c#
public class VirtualFileProvider : IVirtualFileProvider, ISingletonDependency
{
    // 注入 dynamicFileProvider, virtualFileSystemOptions
    private readonly IFileProvider _hybridFileProvider;
    private readonly AbpVirtualFileSystemOptions _options;    
    public VirtualFileProvider(
        IOptions<AbpVirtualFileSystemOptions> options,
        IDynamicFileProvider dynamicFileProvider)
    {
        _options = options.Value;
        _hybridFileProvider = CreateHybridProvider(dynamicFileProvider);
    }
    
    public virtual IFileInfo GetFileInfo(string subpath)
    {
        return _hybridFileProvider.GetFileInfo(subpath);
    }
    
    public virtual IDirectoryContents GetDirectoryContents(string subpath)
    {
        if (subpath == "")
        {
            subpath = "/";
        }
        
        return _hybridFileProvider.GetDirectoryContents(subpath);
    }
    
    public virtual IChangeToken Watch(string filter)
    {
        return _hybridFileProvider.Watch(filter);
    }
    
    protected virtual IFileProvider CreateHybridProvider(IDynamicFileProvider dynamicFileProvider)
    {
        var fileProviders = new List<IFileProvider>();
        
        // 添加注入的 dynamicFileProvider
        fileProviders.Add(dynamicFileProvider);
        // 添加 options 中的 fileset 的 fileProvider
        foreach (var fileSet in _options.FileSets.AsEnumerable().Reverse())
        {
            fileProviders.Add(fileSet.FileProvider);
        }
        
        return new CompositeFileProvider(fileProviders);
    }
}

```



### 3. practice

* 在 options 中注册 file

  见 2.3

* 在应用（controller）中注入 IVirtualFileProvider

  ```c#
  public void Test()
  {
      //Getting a single file
      var file = _virtualFileProvider
          .GetFileInfo("/MyResources/js/test.js");
      
      var fileContent = file.ReadAsString();
      
      //Getting all files/directories under a directory
      var directoryContents = _virtualFileProvider
          .GetDirectoryContents("/MyResources/js");
  }
  
  ```

  