## about background worker and job

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

  * backgroundworker base

    ```c#
    asdfadf
    ```

    start, stop 方法使用了logger，如果没有指定会报异常；可以使用autofac的属性注入

    在quartz中因为没有使用start， stop方法，则不需要

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

##### 1.3 background job

使用 background worker 管理的jobs，和event bus区别？？？

* 定义background job 类型

  ```c#
  public abstract class BackgroundJob<TArgs> : IBackgroundJob<TArgs>
  {
      public ILogger<BackgroundJob<TArgs>> Logger { get;set; }
      protected BackgroundJob()
      {
          Logger = NullLogger<BackgroundJob<TArgs>>.Instance;        
      }
      
      public abstract void Execute(TArgs args);
  }
  
  ```

  ```c#
  public abstract class AsyncBackgroundJob<TArgs> : IAsyncBackgroundJob<TArgs>
  {
      public ILogger<AsyncBackgroundJob<TArgs>> Logger { get;set; }
      protected AsyncBackgroundJob()
      {
          Logger = NullLogger<AsyncBackgroundJob<TArgs>>.Instance;
      }
      
      public abstract Task ExecuteAsync(TArgs args);
  }
  
  ```

* 自动注册background job 类型

  * `AbpBackgroundJobOptions`是background job 的容器

    它是 job 类型的容器，具体的 job任务 存储在 `IBackgroundJobStore`中

    ```c#
    public class AbpBackgroundJobOptions
    {
        private readonly Dictionary<Tyep, BackgroundJobConfiguration> _jobConfigurationbyArgsType;
        private readonly Dictionary<string, BackgroundJobConfiguration> _jobConfigurationsByName;
        public bool IsJobExecutionEnable { get;set; } = true;
        
        public AbpBackgroundJobOptions()
        {
            _jobConfigurationsByArgsType = new Dictionary<Type, BackgroundJobConfiguration>();
            _jobConfigurationsByName = new Dictionary<string, BackgroundJobConfiguration>();
        }        
    }
    
    ```

    * get job

      ```c#
      // 通过 TArgs 类型 或者 指定的name 解析 background_job_configuration
      public class AbpBackgroundJobOptions
      {
          public BackgroundJobConfiguration GetJob<TArgs>()
          {
              return GetJob(typeof(TArgs));
          }
          
          public BackgroundJobConfiguration GetJob(Type argsType)
          {
              var jobConfiguration = _jobConfigurationsByArgsType.GetOrDefault(argsType);
              if(jobConfiguration == null)
              {
                  throw new AbpException("Undefined ...");
              }
              return jobConfiguration;
          }
          
          public BackgroundJobConfiguration GetJob(string name)
          {
              var jobConfiguration = _jobConfigurationsByName.GetOrDefault(name);
              if(jobConfiguration == null)
              {
                  throw new AbpException("Undefined ...");
              }
              return jobConfiguration;
          }
      }
      
      ```

      ```c#
      public class AbpBackgroundJobOptions
      {
          public IReadONlyList<BackgroundJobConfiguration> GetJobs()
          {
              return _jobConfigurationByArgsType.Values.ToImmutableList();
          }
      }
      
      ```

    * add job

      ```c#
      public class AbpBackgroundJobOptions
      {
          public void AddJob<TJob>()
          {
              AddJob(typeof(TJob));
          }
          
          public void AddJob(Type jobType)
          {
              AddJob(new BackgroundJobConfiguration(jobType));
          }
          
          public void AddJob(BackgroundJobConfiguration jobConfiguration)
          {
              _jobConfigurationsByArgsType[jobConfiguration.ArgsType] = jobConfiguration;
              _jobConfigurationsByName[jobConfiguration.JobName] = jobConfiguration;
          }
      }
      
      ```

  * background job configuration

    存储 background_job 类型信息的序列化类

    ```c#
    public class BackgroundConfiguration
    {
        public Type ArgsType { get; }
        public Type JobType { get; }
        public string JobName { get; }
        public BackgroundConfiguration(Type jobType)
        {
            JobType = jobType;
            // get type of TArgs or exception
            ArgsType = BackgroundArgsHelper.GetJobArgsType(jobType);
            // get type name by attribute, or fullname of type for default (no attribute)
            JobName = BackgroundNameAttribute.GetName(ArgsType);
        }
    }
    ```

  * 在abstract background job module中注册job类型

    ```c#
    public class AbpBackgroundJobsAbstractionsModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            RegisterJobs(context.Services);
        }
        
        // IBackgroundJob, IAsyncBackgroundJob 将注入 options中
        private static void RegisterJobs(IServiceCollection services)
        {
            var jobTypes = new List<Type>();
            
            services.OnRegistred(context =>
            {
                // ...
                jobTypes.Add(context.ImplementationType);                                 
            });
            
            services.Configure<AbpBackgroundJobOptions>(Options =>
            {
                foreach(var jobType in jobTypes)
                {
                    options.AddJob(jobType);
                }                                                        
            });
        }
    }
    
    ```

* 管理 job（入队）

  ```c#
  [Dependency(ReplaceServices = true)]
  public class DefaultBackgroundJobManager : IBackgroundJobManager, ITransientDependency
  {
      // 注入服务，job 序列化 和 仓储
      protected IClock Clock { get; }
      protected IGuidGenerator GuidGenerator { get; }
      protected IBackgroundJobSerializer Serializer { get; }
      protected IBackgroundJobStore Store { get; }
      public DefaultBackgroundJobManager(
      	IClock clock,
          IGuidGenerator guidGenerator,
      	IBackgroundJobSerializer serializer,
      	IBackgroundJobStore store)
      {
          Clock = clock;
          Serializer = serializer;
          GuidGenerator = guidGenerator;
          Store = store;
      }
      
      // 入队
      public virtual async Task<string> EnqueueAsync<TArgs>(
          TArgs args,
      	BackgroundJobPriority priority = BackgroundJobPriority.Normal,
      	TimeSpan? delay = null)
      {
          var jobName = BackgroundJobNameAttribute.GetName<TArgs>();
          var jobId = await EnqueueAsync(jobName, args, priority, delay);
          return jobId.ToString();
      }
      
      // job_args -> jobinfo -> in job store
      protected virtual async Task<Guid> EnqueueAsync(
          string jobName, 
          object args, 
          BackgroundJobPriority priority = BackgroundJobPriority.Normal, 
      	TimeSpan? delay = null)
      {
          var jobInfo = ...;	// next try time = clock.now
          if(delay.HasValue)
          {
              jobInfo.NextTryTime = Clock.Now.Add(delay.Value);
          }
          
          await Store.InsertAsync(jobInfo);
          return jobInfo.Id;
      }    	
  }
  
  ```

* 执行 job

  实质是一个 background worker，也是这么命名的

  ```c#
  public class BackgroundJobWorker : AsyncPeriodicBackgroundWorkerBAse, IBackgroundJobWorker
  {
      // 注入服务
      protected AbpBackgroundJobOptions JobOptions { get; }
      protected AbpBackgroundJobWorkerOptions WorkerOptions { get; }
      public BackgroundJobWorker(
      	AbpTimer timer,
          IServiceScopeFactory serviceScopeFactory,
      	IOptions<AbpBackgroundJobOptions> jobOptions,
      	IOptions<AbpBackgroundJobWorkerOptions> workerOptions) 
          : base(timer, servicesScopeFactory) // 本质是一个async_periodic_background_worker
      {
          WorkerOptions = workerOptions.Value;
          JobOptions = jobOptions.Value;
          Timer.Period = WorkerOptions.JobPollPeriod;
      }
      
      // periodicBackgroundWorkerContext 在 background worker manager 执行 dowork 时创建
      protected async override Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
      {
          // 获取 background jobs （具体job）
          var store = workerContext.ServiceProvider.GetRequiredService<IBackgroundJobStore>();
          var waitingJobs = await store.GetWaitingJobsAsync(WorkerOptions.MaxJobFetchCount);
          if(!waitingJobs.Any())
          {
              return;
          }
          
          var clock = workerContext.ServiceProvider
              .GetRequriedService<IClock>();
          var jobExecuter = workerContext.ServiceProvider
              .GetRequiredService<IBackgroundJobExecuter>();        
          var serializer = workerContext.ServiceProvider
              .GetRequiredService<IBackgroundJobSerializer>();
          
          foreach(var jobInfo in jobInfos)
          {
              // ... 
              try
              {
                  // 逆序列化 job args type
                  var jobConfiguration = JobOptions.GetJob(jobInfo.JobName);
                  var jobArgs = serializer.Deserialize(jobInfo.JobArgs, jobConfiguration.ArgsType);
                  var context = new JobExecutionContext(workerContext.ServiceProvider, jobConfiguration.JobType, jobArgs);
                  
                  try 
                  {
                      await jobExecuter.ExecuteAsync(context);
                      await store.DeleteAsync(jobInfo.Id);		// 执行后删除 job
                  }
                  // ...
              }
              // ...
          }
      }
  }
  
  ```

  * job executer

    ```c#
    public class BackgroundJobExecuter : IBackgroundJobExecuter, ITransientDependency
    {
        // 注入服务
        public ILogger<BackgorundJobExecuter> Logger { protected get; set; }
        protected AbpBackgroundJobOptions Options { get; }
        public BackgroundJobExecuter(IOptions<AbpBackgroundJobOptions> options)
        {
            Options = options.Value;
            Logger = NullLogger<BackgroundJobExecuter>.Instance;
        }
        
        public virtual async Task ExecuteAsync(JobExecutionContext context)
        {
            var job = context.ServiceProvider.GetService(context.JobType);
            if(job == null)
            {
                throw new AbpException("The job is not registered ...")
            }
            
            var jobExecuteMethod = 
                context.JobType.GetMethod(nameof(IBackgroundJob<object>.Execute)) ??
                context.JobType.GetMethod(nameof(IAsyncBackgroundJob<object>.ExecuteAsync));
            
            try
            {
                // 执行method 。。。
            }
            catch
            {
                // ...
            }
        }
    }
    
    ```

  * background job store

    abp框架实现了基于内存的store，或者基于其他组件如quartz、rabbitmq的持久化

    基于 ef.core 的store???

    ```c#
    public interface IBackgroundJobStore
    {
        Task<BackgroundJobInfo> FindAsync(Guid jobId);
        Task InsertAsync(BackgroundJobInfo jobInfo);
        Task<List<BackgroundJobInfo>> GetWaitingJobsAsync(int maxResultCount);
        Task DeleteAsync(Guid jobId);
        Task UpdateAsync(BackgroundJobInfo jobInfo);
    }
    
    ```

    ```c#
    public class InMemoyBackgroundJobStore : IBackgroundJobStore, ISingletonDependency
    {
        // 使用 dictionary 作仓储
        private readonly ConcurrentDictionary<Guid, BackgroundJobInfo> _jobs;
        protected IClock Clock { get; }
        public InMemoryBackgroundJobStore(IClock clock)
        {
            Clock = clock;
            _jobs = new ConcurrentDictionary<Guid, BackgroundJobInfo>();
        }
        
        // 增删改查 。。。
    }
    
    ```

#### 2. how to use

##### 2.1 background worker

* 依赖`AbpBackgroundWorkerModule`
* 获取 `IBackgroundWorkerManager`并添加`IBackgroundWorker`

* 如果使用`PeriodicBackgroundWorkerBase`或者`AsyncPeriodicBackgroundWorkerBase`，

  * `BackgroundBase`
  * 依赖 autofac module，因为`BackgroundWorkerBase`使用了属性注入；

  * AbpTimer是threading module中自动注入的，而 background worker 依赖了 threading module；

    timer在构造时指定了period，后期不能修改；可以通过解析服务从configuration获取，或者pre_configuration解析参数，但是麻烦

  * 需要在 OnApplication方法中调用 AddBackgroundWorker
  * 或者手动添加

##### 2.2 quartz background worker

* 依赖`AbpQuartzBackgroundWorkersQuartzModule`
* 





