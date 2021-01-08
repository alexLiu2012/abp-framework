## about module test

相关程序集：

* Volo.Abp.TestBase

----

### 1. about

#### 1.1 summary

* abp 框架完成了测试功能
  * 使用 xunit 作为测试kuangjia
  * 使用 NSubstitue 作为 mock
  * 使用 shouldly 作为 断言（assert）

#### 1.2 how designed



### 2. details

#### 2.1 test base

##### 2.1.1 aa

```c#
public abstract class AbpTestBaseWithServiceProvider
{
    protected abstract IServiceProvider ServiceProvider { get; }
    
    protected virtual T GetService<T>()
    {
        return ServiceProvider.GetService<T>();
    }
    
    protected virtual T GetRequiredService<T>()
    {
        return ServiceProvider.GetRequiredService<T>();
    }
}

```

##### 2.1.2 bb

```c#
public abstract class AbpIntegratedTest<TStartupModule> : 
	AbpTestBaseWithServiceProvider, IDisposable        
        where TStartupModule : IAbpModule
{        
    protected override IServiceProvider ServiceProvider => 
        Application.ServiceProvider;
            
    protected IAbpApplication Application { get; }
            
    protected IServiceProvider RootServiceProvider { get; }
    
    protected IServiceScope TestServiceScope { get; }
    
    protected AbpIntegratedTest()
    {
        // 创建 service collection
        var services = CreateServiceCollection();
        
        // pre configure services
        BeforeAddApplication(services);
        
        // 创建 abp application
        var application = services.AddApplication<TStartupModule>(
            SetAbpApplicationCreationOptions);
        Application = application;
        
        // post configure services
        AfterAddApplication(services);
        
        // 创建 service providerr
        RootServiceProvider = CreateServiceProvider(services);
        // 创建 service scope
        TestServiceScope = RootServiceProvider.CreateScope();
        // 用 scope.serviceProvider 初始化 abp applicatino
        application.Initialize(TestServiceScope.ServiceProvider);
    }
    
    protected virtual IServiceCollection CreateServiceCollection()
    {
        return new ServiceCollection();
    }
    
    // 配置 abp application creation options
    // 可以在派生类中重写（override）
    protected virtual void SetAbpApplicationCreationOptions(
        AbpApplicationCreationOptions options)
    {        
    }
    // pre configure services
    // 可以在派生类中重写（override）
    protected virtual void BeforeAddApplication(
        IServiceCollection services)
    {        
    }
    // post configure services
    // 可以在派生类中重写（override）        
    protected virtual void AfterAddApplication(
        IServiceCollection services)
    {        
    }
    
    protected virtual IServiceProvider CreateServiceProvider(
        IServiceCollection services)
    {
        return services.BuildServiceProviderFromFactory();
    }
    
    public virtual void Dispose()
    {
        Application.Shutdown();
        
        TestServiceScope.Dispose();
        Application.Dispose();
    }
}

```

#### 2.2 test counter

##### 2.2.1 接口

```c#
public interface ITestCounter
{
    int Add(string name, int count);    
    int Decrement(string name);    
    int Increment(string name);    
    int GetValue(string name);
}

```

##### 2.2.2 实现

```c#
public class TestCounter : 
	ITestCounter, 
	ISingletonDependency
{
    private readonly Dictionary<string, int> _values;    
    public TestCounter()
    {
        _values = new Dictionary<string, int>();
    }
    
    public int Increment(string name)
    {
        return Add(name, 1);
    }
    
    public int Decrement(string name)
    {
        return Add(name, -1);
    }
    
    public int Add(string name, int count)
    {
        lock (_values)
        {            
            var newValue = _values.GetOrDefault(name) + count;
            _values[name] = newValue;
            return newValue;
        }
    }
    
    public int GetValue(string name)
    {
        lock (_values)
        {
            return _values.GetOrDefault(name);
        }
    }
}

```

#### 2.3 infrastructure used

##### 2.3.1 xunit

* 测试框架

##### 2.3.2 NSubstitute

* mock 工具

* 使用其语法生成 mocked object 

  ```c#
  var obj = Substitute.For<T>();
  obj
  ```

  https://nsubstitute.github.io/help.html

* 断言工具

* 对象（变量、方法）增加的扩展方法`Shouldxxx()`

  https://docs.shouldly.io/documentation/getting-started

### 3. practice

* 添加 tested module 引用
* 定义 test class 继承 Integrate<TModule>
* 在 test class 中，arrange、act、assert



