## about background worker

#### 1. concept

abp框架实现了后台服务和任务的支持

##### 1.1 background worker

worker是实现了 microsoft IHostedService 的服务，做了一些封装

* `IBackgroundWorker` -> `BackgroundWorkerbase` -> `PeriodBackgroundWorkerBase` + `AsyncPeriodBackgroundWorkerBase`

  * `IBackgroundWorker`是自动注册的单例

    ```c#
    public interface IBackgroundWorker : IRunnable, ISingletonDependency
    {    
    }
    
    ```

  * 自定义worker可以继承自预定义基类

  ```c#
  public abstract class PeriodBackgroundWorkerBase : BackgroundWorkerBase
  {
      // 注入
      protected IServiceScopeFactory ServiceScopeFactory { get; }
      protected AbpTimer Timer { get; }
      protected PeriodBackgroundWorkerBase(AbpTimer timer, IServiceScopeFactory serviceScopeFactory)
      {
          ServiceScopeFactory = serviceScopeFactory;
          // 必须在构造时指定timer，不能更改
          Timer = timer;
          Timer.Elapsed += Timer_Elapsed;
      }
      
      // 启动
      public async override Task StartAsync(CancellationToken cancellationToken = default)
      {
          await base.StartAsync(cancellationToken);	// base 中只写了日志
          Timer.Start(cancellationToken);
      }
      // 结束
      public async override Task StopAsync(CancellationToken cancellationToken = default)
      {
          Timer.Stop(cancellationToken);		// base 中只写了日志
          await base.StopAsync(cancellationToken);
      }
      
      private void Timer_Elapsed(object sender, System.EventArgs e)
      {
          using (var scope = ServiceScopeFactory.CreateScope())
          {
              try
              {                
                  DoWork(new PeriodicBackgroundWorkerContext(scope.ServiceProvider));
              }
              catch (Exception ex)
              {
                  // IExceptionNotifier ???
                  scope.ServiceProvider
                      .GetRequiredService<IExceptionNotifier>()
                      .NotifyAsync(new ExceptionNotificationContext(ex));
                  
                  Logger.LogException(ex);
              }
          }
      }        
      
      // 具体worker，在派生类中定义
      // 通过context.IServiceProvider可以获取Ioc，性能？？
      protected abstract void DoWork(PeriodicBackgroundWorkerContext workerContext);
  }
  
  ```

  ```c#
  public abstract class AsyncPeriodBackgroundWorkerBase : BackgroundWorkerbase
  {
      // 与 PeriodBackgroundWorkerBase 定义相同
      
      // do work 变成了异步方法
      private void Timer_Elapsed(object sender, System.EventArgs e)
      {
          // ...
          try
          {
              AsyncHelper.RunAsync(() => DoWorkAsync(/**/));
          }
      }
      
      // 具体worker，在派生类中定义
      // 通过context.IServiceProvider可以获取Ioc，性能？？
      protected abstract Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext);
  }
  
  ```

  * background worker base

    ```c#
    public abstract class BackgrondWorkerBase : IBackgroundWorker
    {
        // 属性注入
        public IServiceProvider ServiceProvider { get;set; }
        
        // 懒加载 ILoggerFactory
        public ILoggerFactory LoggerFactory => LazyGetRequiredService(ref _loggerFactory);
        private ILoggerFactory _loggerFactory;
        
        // 懒加载 ILogger<T>, T是自身
        protected ILogger => _lazyLogger.Value;
        private Lazy<ILogger> _lazyLogger => new Lazy<ILogger>(() => 
        	LoggerFactory?.CreateLogger(GetType().FullName) ?? NullLogger.Instance, true);
        
        // ... lazy get service
        
        public virtual Task StartAsync(CancellationToken ...) { /* logger */ }
        public virtual Task StopAsync(CancellationToken ...) { /* logger */ }   
    }
    
    ```

    * `StartAsync()`, `StopAsync()` 方法使用了Logger，如果没有指定会报异常；也可以在派生类中重写上述方法
    * 可以使用 autofac 的属性注入
    * 在quartz中因为没有使用`StartAsync()`, `StopAsync()`方法， 不会出现异常

  * 上下文参数

    ```c#
    public class PeriodBackgroundWorkerContext
    {
        public IServiceProvider ServiceProvider { get; }
        public PeriodBackgroundWorkerContext(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }
    }
    
    ```

* background worker manager

  ```c#
  public class BackgroundWorkerManager : IBackgroundWorkManager, ISingletonDependency, IDisposable
  {
      protected bool IsRunning { get; private set; }
      private bool _isDisposed;
      private readonly List<IBackgroundWorker> _backgroundWorkers;
      
      public BackgroundWorkerManager()
      {
          _backgroundWorkers = new List<IBackgroundWorker>();
      }
      // 启动，启动manager管理的所有worker
      public virtual async Task StartAsync(CancellationToken cancellationToken = default)
      {
          IsRunning = true;
          foreach(var worker in _backgroundWorkers)
          {
              await worker.StartAsync(cancellationToken);
          }
      }
      // 停止，停止manager管理的所有worker
      public virtual async Task StopAsync(CancellationToken cancellationToken = default)
      {
          IsRunning = false;
          foreach(var worker in _backgroundWorkers)
          {
              await worker.StopAsync(cancellationToken);
          }
      }
      // 添加worker
      public virtual void Add(IBackgroundWorkder worker)
      {
          _backgroundWorkers.Add(worker);
          if(IsRunning)
          {
              AsyncHelper.RunSync(() => worker.StartAsync);
          }
      }
  }
  
  ```

* background worker module 注册服务

  ```c#
  public class AbpBackgroundWorkerModule : AbpModule
  {
      public override void OnApplicationInitialization(ApplicationInitializationContext context)
      {
          // 获取 IBackgroundWorkerManager 并启动        
      }
      
      public override void OnApplicationShutdown(ApplicationShutdownContext context)
      {
          // 获取 IBackgroundWorkerManager 并停止
      }
  }
  
  ```

##### 1.2 background worker with quartz

可以使用quartz作为`IBackgroundWorkerManager`

```c#
public class QuartzBackgroundWorkerManager : IBackgroundWorkerManager, ISingletonDependency
{
    // 注入 quartz
    private readonly IScheduler _scheduler;
    public QuartzBackgroundWorkerManager(IScheduler scheduler)
    {
        _scheduler = scheduler;
    }
    // 启动 manager，调用quartz.scheduler
    public virtual async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _scheduler.ResumeAll(cancellationToken);
    }
    // 停止 manager，调用quartz.scheduler
    public virtual async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _scheduler.PauseAll(cancellationToken);
    }            
}

```

* 添加worker

  ```c#
  public class QuartzBackgroundWorkerManager : IBackgroundWorkerManager
  {
      public virtual void Add(IBackgroundWorker worker)
      {
          AsyncHelper.RunAsync(() => ReSchedulerJobAsync(worker));
      }
      
      // 向 scheduler 添加 worker
      protected virtual async Task ReScheduleJobAsync(IBackgroundWorker worker)
      {
          if(worker is IQuartzBackgroundWorker quartzWork)
          {
              // 如果 worker 本身是 IQuartzBackgroundWorker 定义的
              Check.NotNull(quartzWork.Trigger, nameof(quartzWork.Trigger));
              Check.NotNull(quartzWork.JobDetail, nameof(quartzWork.JobDetail));
              if(quartzWork.ScheduleJob != null)
              {
                  await qaurtzWork.ScheduleJob.Invoke(_scheduler);
              }
              else
              {
                  await DefaultScheduleJobAsync(quartzWork);
              }
          }
          else
          {
              // 否则，即 worker 是由 IBackgroundWorker 定义的
              var adapterType = typeof(QuartzPeirodicBackgroundWorkerAdapter<>)
                  .MakeGenericType(worker.GetType());
              var workerAdapter = Activator.CreateInstance(adapterType);
              
              workerAdapter?.BuildWorker(worker);
              
              if(workerAdapter?.Trigger != null)
              {
                  await DefaultScheduleJobAsync(workerAdapter);
              }                
          }               
      }
  }
  
  ```

  ```c#
  // 向 quartz.scheduler 添加 worker
  // to do: about quartz ???
  protected virtual async Task DefaultScheduleJobAsync(IQuartBackgroundWorker quartzWork)
  {
      if(await _scheduler.CheckExists(quartzWork.JobDetail.Key))
      {
          // 如果 worker 已经存在，更新
          await _scheduler.Addjob(quartzWork.JobDetail, true, true);
          await _scheduler.ResuemJob(quartzWork.JobDetail.Key);
          await _scheudler.RescheduleJob(quartzWork.Trigger.Key, quartzWork.Trigger);
      }
      else
      {
          await _scheduler.ScheduleJob(quartzWork.JobDetail, quartzWork.Trigger);
      }
  }
  
  ```

* quartz worker

  * `QuartzBackgroundWorkerBase`

    ```c#
    public abstract class QuartzBackgroundWorkerBase : BackgroundWorkerBase
    {
        public ITrigger Trigger { get;set; }
        public IJobDetail JobDetail { get;set; }
        public bool AutoRegister { get;set; } = true;
        public Func<IScheduler, Task> ScheduleJob { get;set; } = null;
        // 具体worker
        public abstract Task Execute(IJobExecutingContext context);
    }
    
    ```

    在派生的worker类中注入`IServiceScopeFactory`，使用`use`块解析`IServiceProvider`

  * quartz adapter

    ```c#
    public class QuartzPeriodicBackgroundWorkerAdapter<TWorker> : QuartzBackgroundWorkerBase, IQuartzBackgoundWorkeradapter where TWorker : IBackgroundWorker
    {
        // 获取原worker中的执行方法
        private readonly MethodInfo _doWorkAsyncMethod;
        private readonly MethodInfo _doWorkMethod;
        public QuartzPeriodicBackgroundWorkerAdapter()
        {
            AutoRegister = false;
            _doWorkAsyncMethod = typeof(TWorker).GetMethod("DoWorkAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            _doWorkMethod = typeof(TWorker).GetMethod("DoWork", BindingFlags.Instance | BindingFlags.NonPublic);
        }
        
        // 转换为quartz job&trigger
        public void BuildWorker(IBackgroundWorker worker)
        {
            var workerType = worker.GetType();
            // 。。。
            
            //  创建 job & trigger， identity = type.fullname
            JobDetail = JobBuilder.Create<QuartzPeriodicBackgroundWorkerAdapter<TWorker>>()
                				  .WithIdentity(workerType.Fullname)
                				  .Build();		// 注册自己为quartz job
            Trigger = TriggerBuilder.Create()
                					.WithIdentity(workerType.Fullname)
                					.WithSimpleSchedule(builder => builder.WithInterval(...))
                					.Build();
        }
        
        public async override Task Execute(IJobExecutionContext context)
        {
            // ...
        }
    }
    
    ```

* quartz background worker 模块注册

  ```c#
  public class AbpBackgroundWorkerQuartzModule : AbpModule
  {
      public override void PreConfigureServices(ServiceConfigurationContext context)
      {
          // 按照约定注册 background worker
          context.Services.AddConventionalRegistrar(new AbpQuartzConverntionalRegistrar());
      }
      public override void ConfigureServices(ServiceConfigurationContext context)
      {
          context.Services.AddSingleton(typeof(QuartzPeriodicBackgroundWorkerAdapter<>))
      }
      public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
      {
          // 启动 quartz scheduler ...
      }
      public override void OnApplicationInitialization(ApplicationInitializationContext context)
      {
          // 向 worker_manager 添加 worker ...
      }
  }
  ```

  * 按约定注册 worker 并添加到 manager

    ```c#
    public class AbpQuartzConventionalRegistrar : DefaultConventionalRegistrar
    {
        public override void AddType(IServiceColletion services, Type type)
        {
            if(!typeof(IQuartzBackgroundWorker).IsAssignableFrom(type))
            {
                return;
            }
            // 需要标记dependency特性？？
            var dependencyAttribute = GetDependencyAttributeOrNull(type);
            var lifetime = GetLifetimeOrNull(type, dependencyAttribute);
            if(lifetime == null)
            {
                return;
            }
            
            service.Add(ServiceDescriptor.Describe(typeof(IQuarzBackgroundWorker), type, lifetime.value));
        }
    }
    
    ```

    ```c#
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var quartzBackgroundWorkerOptions = context.ServiceProvider
            .GetService<IOptions<AbpBackgroundWorkerQuartzOptions>>().Value;
        // 在 quartz worker options 中控制disable 自动注册
        if (quartzBackgroundWorkerOptions.IsAutoRegisterEnabled)
        {
            var backgroundWorkerManager = ontext.ServiceProvider
                .GetService<IBackgroundWorkerManager>();
            var works = context.ServiceProvider.GetServices<IQuartzBackgroundWorker>()
                .Where(x=>x.AutoRegister);
    
            foreach (var work in works)
            {
                backgroundWorkerManager.Add(work);
            }
        }
    }
    
    ```

#### 2. how to use

##### 2.1 background worker

* 依赖`AbpBackgroundWorkerModule`
* 定义`IBackgroundWorker`，实现接口或者继承基类

* 如果使用`PeriodicBackgroundWorkerBase`或者`AsyncPeriodicBackgroundWorkerBase`，

  * 最好依赖 autofac module，因为`BackgroundWorkerBase`使用了属性注入 IServiceProivder；

  * AbpTimer是threading module中自动注入的，而 background worker 依赖了 threading module；

    timer在构造时指定了period，后期不能修改；可以通过解析服务从configuration获取，或者pre_configuration解析参数，但是麻烦

  * 需要在 OnApplication方法中调用 AddBackgroundWorker 方法添加 worker
  * 或者解析`IBackgroundManager`手动添加

##### 2.2 quartz background worker

* 依赖`AbpQuartzBackgroundWorkersQuartzModule`
* 定义 quartz background worker，实现`IQuartzBackgroundWorker`接口和`IBackgroundWorker`接口，或者继承自`QuartzBackgroundWorkerBase`基类
* worker默认是自动注册的，可以在worker中disable，或者在abp background worker quartz options中全部disable
* 在`AbpQuartzOptions`中定义了quartz的相关配置





