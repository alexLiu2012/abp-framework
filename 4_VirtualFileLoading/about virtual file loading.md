## about virtual file loading

#### 1. concept

abp框架扩展了microsoft.extensions.fileprovider，可以抽象访问文件，如配置、静态资源等

* microsoft定义
  * `IFileProvider`是抽象文件容器

  ```c#
  public interface IFileProvider
  {
      // IFileInfo 访问文件
      // IDirectoryContens 访问文件夹
      // IChangeToken 修改令牌
  }
  
  ```

* abp定义

  * `VirtualFileSetList`是资源文件的抽象的集合

  ```c#
  public class VirtualFileSetList : List<VirtualFileSetInfo>
  {    
  }
  
  ```

  * `	VirtualFileSetInfo`是资源文件的抽象，它是`IFileProvider`的封装

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

* 派生出具体的文件抽象

  * embedded_file，嵌入程序集的文件

  ```c#
  public class EmbeddedVirtualFileSetInfo : VirtualFileSetInfo
  {
      public Assembly Assembly { get; }
      public string BaseFolder { get; }
      
      public EmbeddedVirtualFileSetInfo(
      	IFileProvider fileProvider,
      	Assembly assembly,
      	string baseFolder = null) : base(fileProvider)
      {
          Assembly = assembly;
          BaseFolder = baseFolder;
      }
  }
  
  ```

  * physical_file，物理文件

  ```c#
  public class PhysicalVirtualFileSetInfo : VirtualSetInfo
  {
      public string Root { get; }
      
      public PhysicalVirtualFileSetInfo(
      	[NotNull] IFileProvider fileProvider,
          [NotNull] string root) : base(fileProvider)
      {
          Root = Check.NotNullOrWhiteSpace(root, nameof(root));
      }
  }
  
  ```

* 通过扩展方法添加文件抽象

  * embedded_file

  ```c#
  public static class VirtualFileSetListExtensions
  {
      public static void AddEmbedded<T>(
      	[NotNull] this VirtualFileSetList list,
      	[CanBeNull] string baseNamespace = null,
      	[CanBeNull] string baseFolder = null)
      {
          Check.NotNull(list, nameof(list));
          
          var assembly = typeof(T).Assembly;
          var fileProvider = CreateFileProvider(assembly, baseNamesapce, baseFolder);
          
          list.Add(new EmbeddedVirtualFileSetInfo(/**/));
      }
  }
  
  ```

  ```c#
  public static class VirtualFileSetListExtensions
  {        
      private static IFileProvider CreateFileProvider(
      	[NotNull] Assembly assembly,
      	[CanBeNull] string baseNamespace = null,
      	[CanBeNull] string baseFolder = null)
      {
          Check.NotNull(assembly, nameof(assembly));
          
          var info = assembly.GetManifestResourceInfo("Microsoft.Extensioins.FileProviders.Embedded.Manifest.xml");
          
          // 如果没有 manifest.xml 文件（.csproj文件中的配置）
          if(info == null)
          {
              return new AbpEmbeddedFileProvider(assembly, baseNamespace);
          }
          
          // 根据 manifest.xml 文件创建
          if(baseFolder == null)
          {
              return new ManifestEmbeddedFileProvider(assembly)
          }
          return new ManifestEmbeddedFileProvider(assembly, baseFolder);
      }
  }
  
  ```

  **注意：如果没有配置 GenerateEmbeddedManifest=true，需要指定namespace**

  **注意：可以仅暴露部分文件夹，即 baseFolder 下的文件夹和文件**

  * physical_file

  ```c#
  public static class VirtualFileSetListExtensions
  {
      public static void AddPhysical(
      	[NotNull] this VirtualFileSetList list,
      	[NotNull] string root,
      	ExclusionFilters exclusinoFilters = ExclusionFilters.Sensitive)
      {
          Check.NotNull(list, nameof(list));
          Check.NotNullOrWhiteSpace(root, nameof(root));
          
          var fileProvider = new PhysicalFileProvider(root, exclusionFilters);
          
          list.Add(new PhysicalVirtualFileSetInfo(fileProvider, root));
      }
  }    
  
  ```

  * replace embedded file

  多个模块共享同一 virtual_file时，后者将覆盖前者配置；

  可以使用 replace_embedded_file_by_physical

  ```c#
  public static class VirtualFileSetListExtensions
  {
      public static void ReplaceEmbeddedByPhysical<T>(
      	[NotNull] this VirtualFileSetList list,
      	[NotNull] string physicalPah)
      {
          Check.NotNull(list, nameof(fileSets));
          Check.NotNullOrWhiteSpace(physicalPath, nameof(physicalPaht));
          
          var assembly = typeof(T).Assembly;
          
          for(var i = 0; i < list.Count; i++)
          {
              // 是指定的程序集中的 virtual_fileset
              if(list[i] is EmbeddedVirtualFileSetInfo embeddedVirtualFileSet &&
                embeddedVirtualFileSet.Assembly == assembly)
              {
                  var thisPath = physicalPath;
                  
                  // 如果其 base_folder 不为null，将其置于具名的 physical_path 之下
                  if(!embeddedVirtualFileSet.BaseFolder.IsNullOrEmpty())
                  {
                      thisPath = Path.Combine(thisPath, embeddedVirtualFileSet.BaseFolder);
                  }
                  
                  // 将 virtual_file 转换为 physical_file
                  list[i] = new PhyscialVirtualFileSetInfo（
                      new PhysicalFileProvider(thisPath),
                  	thisPath);
              }
          }
      }
  }
  
  ```

* `AbpVirtualFileSystemOptions`是`VirtualFileSetList`的封装，

  在`ConfigureService()`方法中注册文件

  ```c#
  [DependsOn(typeof(AbpVirtualFileSystemModule))]
  public class MyModule : AbpModule
  {
      public override void ConfigureService(ServiceConfigurationContext context)
      {
          Configure<AbpVirtualFileSystemOptions>(options =>
          {
              // 加载程序集中的 embedded_files
          	options.FileSets.AddEmbedded<MyModule>();                                       
          });
      }
  }
  
  ```

* 使用（资源）文件的内容

  * `IVirtualFileProvider`是统一的访问抽象，已经在模块中注册

    ```c#
    public class VirtualFileProvider : IVirtualFileProvider, ISingletonDependency
    {
        // ...    
        public virtual IFileInfo GetFileInfo(string subpath) { /**/ }    
        public virtual IDirectoryContents GetDirectoryContents(string subpath) { /**/ }     
        public virtual IChangeToken Watch(string filter) { /**/ }    
    }
    
    ```

    * `IFileInfo`访问文件
    * `IDirectoryContents`访问文件夹
    * `IChangeToken`文件变动令牌

  * `IFileInfo`的扩展方法可以获取文件内容

    ```c#
    // get string
    public static class AbpFileInfoExtensions
    {
        public static string ReadAsString(this IFileInfo fileInfo) { /**/ }    
        public static string ReadAsString(this IFileInfo fileInfo, 
                                          Encoding encoding) { /**/ }
        
        public static async Task<string> ReadAsStringAsync(this IFileInfo fileInfo) { /**/ }
        public static async Task<string> ReadAsStringAsync(this IFileInfo fileInfo,
                                                           Encoding encoding) { /**/ }
    }
    
    ```

    ```c#
    // get byte[]
    public static class AbpFileInfoExtensions
    {
        public static byte[] ReadBytes(this IFileInfo fileInfo) { /**/ }
        public static byte[] ReadBytes(this IFileInfo fileInfo, Encoding encoding) { /**/ }
        
        public static async Task<byte[]> ReadBytesAsync(this IFileInfo fileInfo) { /**/ }
        public static async Task<byte[]> ReadBytesAsync(this IFileInfo fileInfo,
                                                        Encoding encoding) { /**/ }   
    }
    
    ```

#### 2. how to use

* 依赖模块

* 加载文件

  * embedded file

    适用于不变的、经常共享的资源

    * 添加 microsoft.extensions.fileprovider.embedded，

    * 在 .csproj 文件中添加

      ```c#
      <PropertyGroup>
          <GenerateEmbeddedFileManifest>true</GenerateEmbeddedFileManifest>
      </PropertyGroup>
      ```

  * physical file

    适用于配置文件

* 注入`IVirtualFileProvider`

  * 获取`IFileInfo`，使用它的扩展方法

* conflict

  `IFileProvider`是 key/value 集合，key 可能重复，从而导致 conflict

  * 对于 manifest file

    从 程序集 出发的根节点，key 是相对程序集内的path，即ide看到的；

  * 对于physical file

    配置options时指定了root，key 是root后的实际路径

  * 相同 key 的 file_info 会根据模块加载顺序覆盖

  * best practice：定义不同的 key

  