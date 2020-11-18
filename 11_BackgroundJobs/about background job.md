## about background job

#### 1. concept

abp框架实现了后台服务和任务的支持

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

    * get job 类型

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

    * add job 类型

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

##### 2.3 background job

* 依赖`AbpBackgroundJobsModule`
* 定义 TArgs, 一个poco类；background job <TArgs>
* 实现 IxxxDependency 实现 job（类型） 自动注入，也可以解析 job manager 并手动注入
* 与 event bus很像，TArgs=TEventArgs，IBackJob=TEventHandler，IEventBus=IBackJobManager

* ijobstore？？ef.core??



