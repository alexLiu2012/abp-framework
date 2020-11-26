## about auditing

#### 1. concept

abp框架支持自动审计，审计日志

* 模块中注册拦截器

  ```c#
  public class AbpAuditingModule : AbpModule
  {
      public override void PreConfigureService(ServiceConfigurationContext context)
      {        
          context.Services.OnRegistred(AuditingInterceptorRegistrar.RegisterInNeeded);
      }
  }
  
  ```

  ```c#
  public static class AuditingInterceptorRegistrar
  {
      public static void RegisterIfNeeded(IOnServiceRegistredContext context)
      {
          if(ShouldIntercept(context.ImplementationType))
          {
              context.Interceptors.TryAdd<AuditingInterceptor>();
          }
      }
      
      private static bool ShouldIntercept(Type type)
      {
          // 忽略的type不注册拦截器
          if(DynamicProxyIgnoreTypes.Contains(type))
          {
              return false;
          }
          // 类型需要注册拦截
          if(ShouldAuditTypeByDefaultOrNull(type) == true)
          {
              return true;
          }
          // 方法需要注册拦截器
          if(type.GetMethods().Any(m => m.IsDefined(typeof(AuditedAttribute), true)))
          {
              return true;
          }
          
          return false;
      }
      
      // 根据类型判断是否需要注册拦截器
      public static bool? ShouldAuditTypeByDefaultOrNull(Type type)
      {
          // 标记 audit 属性的类型需要注册拦截器
          if(type.IsDefined(typeof(AuditedAttribute), true))
          {
              return true;
          }
          // 标记 disable_audit 属性的类型不需要注册拦截器
          if(type.IsDefined(typeof(DisableAutditingAttribute), true))
          {
              return false;
          }
          // 实现 IAuditingEnabled 接口的类型需要注册拦截器
          if(typeof(IAuditingEnabled).IsAssignableFrom(type))
          {
              return true;
          }
          
          return null;
      }
  }
  ```

* 拦截器动作

  ```c#
  public class AuditingInterceptor : AbpInterceptor, ITransientDependency
  {
      // 注入服务
      private readonly IAuditingHelper _auditingHelper;
      private readonly IAuditingManager _auditingManager;
      public AuditingInterceptor(IAuditingHelper auditingHelper, IAuditingManager auditingManager)
      {
          _auditingHelper = auditingHelper;
          _auditingManager = auditingManager;
      }
      
      public override async Task InterceptAsync(IAbpMethodInvocation invocation)
      {
          // 不是拦截对象，直接执行
          // 生成了 auditLog, auditLogAction
          if(!ShouldIntercept(invocation, out var auditLog, out var auditLogAction))
          {
              await invocation.ProcessAsync();
              return;
          }
          
          // 计时
          var stopwatch = Stopwatch.StartNew();
          try
          {
              await _invocation.ProceedAsync();           
          }
          catch(Exception ex)
          {
              // 记录异常
              auditLog.Exceptions.Add(ex);
              throw;
          }
          finally
          {
              stopwatc.Stop();
              auditLogAction.ExecutinoDuration = Convert.ToInt32(stopwatch.Elapsed.TotalMilliseconds);
              auditLog.Actions.Add(auditLogAction);
          }
      }
      
      protected virtual bool ShouldIntercept(IAbpMethodInvocation invocation, out AuditLogInfo auditLog, out AuditLogActionInfo auditLogAction)
      {
          // ...
      }
  }
  
  ```

  * IAuditManager

    自动注入，创建scope，持久化，执行audit动作

    * 拦截器中没有使用 audit_manager执行audit动作
    * audit middleware 中使用了

  * IAuditHelper

    自动注入，创建 auditLogAction

* audit log 持久化

  `IAuditLogStore`定义了持久化的接口，框架给出了基于logger的实现`SimpleLogAuditingStore`