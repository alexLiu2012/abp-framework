## about validation and fluent validation

相关程序集：

* Volo.Abp.Validation
* Volo.Abp.FluentValidation

----

### 1. about

* abp 框架扩张了 ms `System.ComponentModel.DataAnnotations`，实现了自动验证，
* 集成了`FluentValidation`

#### 1.1 how designed

* object valid contributor 是提供验证的具体底层服务
  * ms data annotation contributor，验证`ValidAttribute`特性和`IValidObject`接口标记
  * fluent contributor
* object validator 是 abp 定义的提供验证服务的服务接口，可以手动 valid
* 使用拦截器自动验证
  * 被验证的 class 要实现`IValidationEnable`接口
  * 使用`EnableValidationAttribute`和`DisableValidationAttribute`标记（class 或者 member）是否需要验证
  * 拦截器使用 method validator，它包裹了`IObjectValidator`
* 集成了autofac，自动扫描所有实现`IValidator`接口的 entity（class）
  * 参考 fluent validation

### 2. details

#### 2.1 validation

##### 2.1.1 valid attribute

* 使用 ms `DataAnnotations` 对 需要验证的 property 标记

##### 2.1.2 validatable object

* 使用 ms `IValidatableObject`

#### 2.2 validation contributor

* 提供验证（prop）服务的底层架构

##### 2.2.1 obj valid contributor

###### 2.2.1.1 接口

```c#
public interface IObjectValidationContributor
{
    void AddErrors(ObjectValidationContext context);
}

```

###### 2.2.1.2 obj valid context

```c#
public class ObjectValidationContext
{
    [NotNull]
    public object ValidatingObject { get; }    
    public List<ValidationResult> Errors { get; }
    
    public ObjectValidationContext([NotNull] object validatingObject)
    {
        ValidatingObject = Check.NotNull(
            validatingObject, nameof(validatingObject));
        Errors = new List<ValidationResult>();
    }
}

```

##### 2.2.2 data annotation valid contributor

```c#
public class DataAnnotationObjectValidationContributor 
    : IObjectValidationContributor, ITransientDependency
{
    public const int MaxRecursiveParameterValidationDepth = 8;
    
    // 注入服务、options
    protected IServiceProvider ServiceProvider { get; }
    protected AbpValidationOptions Options { get; }    
    public DataAnnotationObjectValidationContributor(
        IOptions<AbpValidationOptions> options,
        IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        Options = options.Value;
    }
    
    // 验证 context 中的 object
    public void AddErrors(ObjectValidationContext context)
    {
        ValidateObjectRecursively(
            context.Errors, context.ValidatingObject, currentDepth: 1);
    }
    // 验证指定的 object    
    public void AddErrors(
        List<ValidationResult> errors, 
        object validatingObject)
    {
        var properties = TypeDescriptor
            .GetProperties(validatingObject).Cast<PropertyDescriptor>();
        
        // 验证 properties (data)
        foreach (var property in properties)
        {
            AddPropertyErrors(validatingObject, property, errors);
        }
        // 验证 iValidatableObject
        if (validatingObject is IValidatableObject validatableObject)
        {
            errors.AddRange(
                validatableObject.Validate(new ValidationContext(
                    validatableObject, ServiceProvider, null)));
        }
    }   
    
    /* 验证 property 的具体方法实现 
       通过 validationAttribute.GetValidationResult 实现*/
    protected virtual void AddPropertyErrors(
        object validatingObject, 
        PropertyDescriptor property, 
        List<ValidationResult> errors)
    {
        // 获取 validationAttribute
        var validationAttributes = property
            .Attributes.OfType<ValidationAttribute>().ToArray();
        
        if (validationAttributes.IsNullOrEmpty())
        {
            return;
        }
        
        var validationContext = new ValidationContext(
            validatingObject, ServiceProvider, null)
        {
            DisplayName = property.DisplayName,
            MemberName = property.Name
        };
        
        foreach (var attribute in validationAttributes)
        {
            // 使用 validationAttribute 验证
            var result = attribute.GetValidationResult(
                	property.GetValue(validatingObject), 
                	validationContext);
            
            if (result != null)
            {
                errors.Add(result);
            }
        }
    }    
        
    /* 递归验证 object 
       可以验证 object.property 类型中标记的 validation */
    protected virtual void ValidateObjectRecursively(
        List<ValidationResult> errors, 
        object validatingObject, 
        int currentDepth)
    {
        // validing_obj 的 property 深度超出，忽略
        if (currentDepth > MaxRecursiveParameterValidationDepth)
        {
            return;
        }
        // validating_object 为 null， 忽略
        if (validatingObject == null)
        {
            return;
        }
        
        /* 验证 */
        AddErrors(errors, validatingObject);
        
        // Validate items of enumerable
        if (validatingObject is IEnumerable enumerable)
        {
            if (!(enumerable is IQueryable))
            {
                foreach (var item in enumerable)
                {
                    //Do not recursively validate for primitive objects
                    if (item == null || 
                        TypeHelper.IsPrimitiveExtended(item.GetType()))
                    {
                        break;
                    }
                    
                    ValidateObjectRecursively(errors, item, currentDepth + 1);
                }
            }
            
            return;
        }
        
        /* 验证 object.property（复杂类型） */        
        var validatingObjectType = validatingObject.GetType();
        // 如果是 nullable，忽略        
        if (TypeHelper.IsPrimitiveExtended(validatingObjectType))
        {
            return;
        }
        // 如果在 options 中注册的 ignore_type，忽略
        if (Options.IgnoredTypes.Any(t => t.IsInstanceOfType(validatingObject)))
        {
            return;
        }
        
        var properties = TypeDescriptor
            .GetProperties(validatingObject).Cast<PropertyDescriptor>();
        foreach (var property in properties)
        {
            // 标记了 disable_validation 特性，忽略
            if (property.Attributes
                .OfType<DisableValidationAttribute>().Any())
            {
                continue;
            }
            // 递归验证 properties （property是class，作为 object 被验证）
            ValidateObjectRecursively(
                errors, 
                property.GetValue(validatingObject), 
                currentDepth + 1);
        }
    }                
}

```

##### 2.2.3 fluent valid contributor

```c#
public class FluentObjectValidationContributor 
    : IObjectValidationContributor, ITransientDependency
{
    // 注入服务
    private readonly IServiceProvider _serviceProvider;    
    public FluentObjectValidationContributor(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public void AddErrors(ObjectValidationContext context)
    {
        // 从 ioc 解析 IValidator
        var serviceType = typeof(IValidator<>).MakeGenericType(
            context.ValidatingObject.GetType());        
        var validator = _serviceProvider.GetService(serviceType) as IValidator;        
        if (validator == null)
        {
            return;
        }
        
        var result = validator.Validate(context.ValidatingObject);
        
        if (!result.IsValid)
        {
            context.Errors.AddRange(
                result.Errors.Select(error =>
                	new ValidationResult(
                        error.ErrorMessage, 
                        new[] { error.PropertyName })));
        }
    }
}

```

#### 2.3 object validator

* 向上层架构提供验证服务

##### 2.3.1 obj validator

###### 2.3.1.1 接口

```c#
public interface IObjectValidator
{
    void Validate(
        object validatingObject,
        string name = null,
        bool allowNull = false
    );
    
    List<ValidationResult> GetErrors(
        object validatingObject,
        string name = null,
        bool allowNull = false);
}

```

###### 2.3.1.2 实现

```c#
public class ObjectValidator : IObjectValidator, ITransientDependency
{
    // 注入服务
    protected IServiceScopeFactory ServiceScopeFactory { get; }
    protected AbpValidationOptions Options { get; }    
    public ObjectValidator(
        IOptions<AbpValidationOptions> options, 
        IServiceScopeFactory serviceScopeFactory)
    {
        ServiceScopeFactory = serviceScopeFactory;
        Options = options.Value;
    }
    
    // 手动验证，
    // 如果有验证错误，抛出异常
    public virtual void Validate(
        object validatingObject, 
        string name = null, 
        bool allowNull = false)
    {
        var errors = GetErrors(validatingObject, name, allowNull);
        
        if (errors.Any())
        {
            throw new AbpValidationException(
                "Object state is not valid! See ValidationErrors for details.",
                errors);
        }
    }
    
    public virtual List<ValidationResult> GetErrors(
        object validatingObject, 
        string name = null, 
        bool allowNull = false)
    {
        if (validatingObject == null)
        {
            if (allowNull)
            {
                //TODO: Returning an array would be more performent
                return new List<ValidationResult>(); 
            }
            else
            {
                return new List<ValidationResult>
                {
                    name == null
                        ? new ValidationResult("Given object is null!")
                        : new ValidationResult(name + " is null!", new[] {name})
                };
            }
        }
        
        // 使用 valid contributor 验证     
        var context = new ObjectValidationContext(validatingObject);        
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            foreach (var contributorType in
                     Options.ObjectValidationContributors)
            {
                var contributor = (IObjectValidationContributor)
                    scope.ServiceProvider.GetRequiredService(contributorType);               
                contributor.AddErrors(context);
            }
        }
        
        return context.Errors;
    }
}

```

##### 2.3.2 abp validation options

* ignore type 容器
* validation contributor 容器

```c#
public class AbpValidationOptions
{
    public List<Type> IgnoredTypes { get; }    
    public ITypeList<IObjectValidationContributor> 
        ObjectValidationContributors { get; set; }
    
    public AbpValidationOptions()
    {
        IgnoredTypes = new List<Type>();
        ObjectValidationContributors = new TypeList<IObjectValidationContributor>();
    }
}

```

#### 2.4 method invocation validator

* 适配拦截器的 validator
* 包裹`IObjectValidator`

##### 2.4.1 接口

```c#
public interface IMethodInvocationValidator
{
    void Validate(MethodInvocationValidationContext context);
}

```

##### 2.4.2 method validation context

```c#
public class MethodInvocationValidationContext : AbpValidationResult
{
    public object TargetObject { get; }    
    public MethodInfo Method { get; }    
    public object[] ParameterValues { get; }    
    public ParameterInfo[] Parameters { get; }
    
    public MethodInvocationValidationContext(object targetObject, MethodInfo method, object[] parameterValues)
    {
        TargetObject = targetObject;
        Method = method;
        ParameterValues = parameterValues;
        Parameters = method.GetParameters();
    }
}

```

##### 2.4.3 实现

###### 2.4.3.1 intialize

```c#
public class MethodInvocationValidator 
    : IMethodInvocationValidator, ITransientDependency
{
    private readonly IObjectValidator _objectValidator;    
    public MethodInvocationValidator(IObjectValidator objectValidator)
    {
        _objectValidator = objectValidator;
    }    
}

```

###### 2.4.3.2 validate

```c#
public class MethodInvocationValidator 
{
    public virtual void Validate(MethodInvocationValidationContext context)
    {
        Check.NotNull(context, nameof(context));
        // method parameterInfo 为 null，忽略
        if (context.Parameters.IsNullOrEmpty())
        {
            return;
        }
        // method 不是 public，忽略
        if (!context.Method.IsPublic)
        {
            return;
        }
        // 判断为 disable
        if (IsValidationDisabled(context))
        {
            return;
        }
        // parametersValue 不足，抛出异常
        if (context.Parameters.Length != context.ParameterValues.Length)
        {
            throw new Exception("Method parameter count does not match with argument count!");
        }        
        //todo: consider to remove this condition
        // 如有仅有一个参数且为null，抛出异常
        if (context.Errors.Any() && HasSingleNullArgument(context))
        {
            ThrowValidationError(context);
        }
        
        // 验证
        AddMethodParameterValidationErrors(context);
        
        // 如果有错误，抛出异常
        if (context.Errors.Any())
        {
            ThrowValidationError(context);
        }
    }        
}
```

###### 2.4.3.3 is validation disable

```c#
public class MethodInvocationValidator 
{
    // 是否忽略 method validation
    protected virtual bool IsValidationDisabled(
        MethodInvocationValidationContext context)
    {
        // 在 method 上标记了 enable validation 特性
        if (context.Method.IsDefined(
            typeof(EnableValidationAttribute), true))
        {
            return false;
        }
        // 在 method 上标记了 disable validation 特性
        if (ReflectionHelper
            	.GetSingleAttributeOfMemberOrDeclaringTypeOrDefault
            		<DisableValidationAttribute>(context.Method) != null)
        {
            return true;
        }
        // 默认（没有标记特性）不忽略
        return false;
    }
    
    protected virtual bool HasSingleNullArgument(
        MethodInvocationValidationContext context)
    {
        return context.Parameters.Length == 1 && 
               ontext.ParameterValues[0] == null;
    }
    
    // 抛出验证异常
    protected virtual void ThrowValidationError(
        MethodInvocationValidationContext context)
    {
        throw new AbpValidationException(
            "Method arguments are not valid! See ValidationErrors for details.",
            context.Errors);
    }
    
    // 验证 method 的 parameters    
    protected virtual void AddMethodParameterValidationErrors(
        MethodInvocationValidationContext context)
    {
        for (var i = 0; i < context.Parameters.Length; i++)
        {
            AddMethodParameterValidationErrors(
                context, 
                context.Parameters[i], 
                context.ParameterValues[i]);
        }
    }    
    protected virtual void AddMethodParameterValidationErrors(
        IAbpValidationResult context, 
        ParameterInfo parameterInfo, 
        object parameterValue)
    {
        // 是否 allow null
        var allowNulls = parameterInfo.IsOptional ||		// 可选
            			 parameterInfo.IsOut ||				// out
                         TypeHelper.IsPrimitiveExtended(	// nullable
            				parameterInfo.ParameterType, 
            				includeEnums: true);
        
        context.Errors.AddRange(
            _objectValidator.GetErrors(
                parameterValue,
                parameterInfo.Name,
                allowNulls));
    }
}

```

##### 2.4.4 enable/disable valid

* 标记是否使用 validation

###### 2.4.4.1 enable attribute

```c#
[AttributeUsage(AttributeTargets.Method)]
public class EnableValidationAttribute : Attribute
{    
}

```

###### 2.4.4.2 disable attribue

```c#
[AttributeUsage(AttributeTargets.Method | 
                AttributeTargets.Class | 
                AttributeTargets.Property)]
public class DisableValidationAttribute : Attribute
{    
}

```

#### 2.5 validation intercept

* abp框架可以自动验证
* 被验证object 需要实现`IValidationEnable`接口

##### 2.5.1 valid interceptor

```c#
public class ValidationInterceptor : AbpInterceptor, ITransientDependency
{
    private readonly IMethodInvocationValidator _methodInvocationValidator;    
    public ValidationInterceptor(IMethodInvocationValidator methodInvocationValidator)
    {
        _methodInvocationValidator = methodInvocationValidator;
    }
    
    public async override Task InterceptAsync(IAbpMethodInvocation invocation)
    {
        Validate(invocation);
        await invocation.ProceedAsync();
    }
    
    protected virtual void Validate(IAbpMethodInvocation invocation)
    {
        _methodInvocationValidator.Validate(
            new MethodInvocationValidationContext(
                invocation.TargetObject,
                invocation.Method,
                invocation.Arguments));
    }
}

```

##### 2.5.2 valid interceptor registrar

```c#
public static class ValidationInterceptorRegistrar
{    
    public static void RegisterIfNeeded(IOnServiceRegistredContext context)
    {
        if (ShouldIntercept(context.ImplementationType))
        {
            context.Interceptors.TryAdd<ValidationInterceptor>();
        }
    }
    
    private static bool ShouldIntercept(Type type)
    {
        // 如果 objectType 是 dynamicProxy 忽略类型，
        // 或者没有实现 IValidationEnable 接口，
        // 不注册 valid 拦截器
        return !DynamicProxyIgnoreTypes.Contains(type) && 
               typeof(IValidationEnabled).IsAssignableFrom(type);
    }
}

```

##### 2.5.3 validation enable

* 标记 object 需要自动验证

```c#
public interface IValidationEnabled
{    
}

```

#### 2.6 注册 validator

##### 2.6.1 validation 模块

```c#
[DependsOn(typeof(AbpValidationAbstractionsModule),
           typeof(AbpLocalizationModule))]
public class AbpValidationModule : AbpModule
{
    // 注册 validator 拦截器
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.OnRegistred(
            ValidationInterceptorRegistrar.RegisterIfNeeded);
        
        AutoAddObjectValidationContributors(context.Services);
    }
    
    private static void AutoAddObjectValidationContributors(IServiceCollection services)
    {
        var contributorTypes = new List<Type>();
        
        services.OnRegistred(context =>
        	{
                if (typeof(IObjectValidationContributor)
                    	.IsAssignableFrom(context.ImplementationType))
                {
                    contributorTypes.Add(context.ImplementationType);
                }
            });
        
        services.Configure<AbpValidationOptions>(options =>
            {
                // 向 abp validation options 中注册 validation_contributors
                options.ObjectValidationContributors
                    .AddIfNotContains(contributorTypes);
            });
    }
    
    // 添加 validation localization 资源    
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        	{
                options.FileSets.AddEmbedded<AbpValidationResource>();
            });
        
        Configure<AbpLocalizationOptions>(options =>
        	{
                options.Resources.Add<AbpValidationResource>("en")
                    .AddVirtualJson("/Volo/Abp/Validation/Localization");
            });
    }        
}

```

##### 2.5.2 fluent validation 

###### 2.5.2.1 fluent valid 模块

```c#
[DependsOn(typeof(AbpValidationModule))]
public class AbpFluentValidationModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddConventionalRegistrar(
            new AbpFluentValidationConventionalRegistrar());
    }
}

```

###### 2.5.2.2 fluent validation conventional registrar

```c#
public class AbpFluentValidationConventionalRegistrar 
    : ConventionalRegistrarBase
{
    public override void AddType(IServiceCollection services, Type type)
    {
        // 如果不支持 conventional registrer，忽略
        if (IsConventionalRegistrationDisabled(type))
        {
            return;
        }
        // 如果不是 IValidator 类型，忽略
        if (!typeof(IValidator).IsAssignableFrom(type))
        {
            return;
        }
        // 如果泛型 validation_type 的 <T> 参数为空，忽略
        var validatingType = GetFirstGenericArgumentOrNull(type, 1);
        if (validatingType == null)
        {
            return;
        }
        
        // 注册 type
        var serviceType = typeof(IValidator<>).MakeGenericType(validatingType);        
        TriggerServiceExposing(services, type, new List<Type>{ serviceType });        
        services.AddTransient(serviceType, type);
    }
    
    private static Type GetFirstGenericArgumentOrNull(Type type, int depth)
    {
        const int maxFindDepth = 8;
        
        if (depth >= maxFindDepth)
        {
            return null;
        }
        
        if (type.IsGenericType && type.GetGenericArguments().Length >= 1)
        {
            return type.GetGenericArguments()[0];
        }
        
        return GetFirstGenericArgumentOrNull(type.BaseType, depth + 1);
    }
}

```

### 3. practice

* 注册 fluent validation 的 validator