## about object extending

相关程序集：

* Volo.Abp.ObjectExtending

----

### 1. about

* abp框架实现了object extending，
* 可以在不改变 origin class 的情况下扩展 class properties

### 2. details

#### 2.1 has extra properties

* 实现 object 可扩展
* 包含 `ExtraPropertyDitctionary`，存储扩展的 properties

##### 2.1.1 has extra prop

```c#
public interface IHasExtraProperties
{
    ExtraPropertyDictionary ExtraProperties { get; }
}

```

##### 2.1.2 extra prop dictionary

```c#
[Serializable]
public class ExtraPropertyDictionary : Dictionary<string, object>
{
    public ExtraPropertyDictionary()
    {        
    }
    
    public ExtraPropertyDictionary(IDictionary<string, object> dictionary) : base(dictionary)
    {
    }
}

```

##### 2.1.3 扩展方法

```c#
public static class HasExtraPropertiesExtensions
{
    // 有 name == name 的 extra property
    public static bool HasProperty(
        this IHasExtraProperties source, string name)
    {
        return source.ExtraProperties.ContainsKey(name);
    }
    
    // get property (object)，
    // 没找到返回 default value
    public static object GetProperty(
        this IHasExtraProperties source, 
        string name, 
        object defaultValue = null)
    {
        return source.ExtraProperties?.GetOrDefault(name)
            ?? defaultValue;
    }    
    // get TProperty，
    // 没找到返回 default value
    public static TProperty GetProperty<TProperty>(
        this IHasExtraProperties source, 
        string name, 
        Property defaultValue = default)
    {        
        var value = source.GetProperty(name);
        if (value == null)
        {            
            return defaultValue;
        }
        
        if (TypeHelper.IsPrimitiveExtended(
            	typeof(TProperty), includeEnums: true))
        {
            var conversionType = typeof(TProperty);
            // 如果是 可空
            if (TypeHelper.IsNullable(conversionType))
            {
                conversionType = conversionType
                    .GetFirstGenericArgumentIfNullable();
            }
            // 如果是 guid
            if (conversionType == typeof(Guid))
            {
                return (TProperty)TypeDescriptor
                    .GetConverter(conversionType)
                    	.ConvertFromInvariantString(value.ToString());
            }
            // 不支持复杂类型扩展！
            return (TProperty)Convert.ChangeType(
                value, conversionType, CultureInfo.InvariantCulture);
        }

        throw new AbpException( /* */ );
    }
    
    // set property
    public static TSource SetProperty<TSource>(
        this TSource source,
        string name,
        object value,
        bool validate = true) where TSource : IHasExtraProperties
    {
        if (validate)
        {
            ExtensibleObjectValidator.CheckValue(source, name, value);
        }        
        source.ExtraProperties[name] = value;        
        return source;
    }
    
    // remove property
    public static TSource RemoveProperty<TSource>(
        this TSource source, 
        string name) where TSource : IHasExtraProperties
    {
        source.ExtraProperties.Remove(name);
        return source;
    }
    
    // 
    public static TSource SetDefaultsForExtraProperties<TSource>(
        this TSource source, 
        Type objectType = null) where TSource : IHasExtraProperties
    {
        if (objectType == null)
        {
            objectType = typeof(TSource);
        }
        
        var properties = ObjectExtensionManager.Instance
            .GetProperties(objectType);
        
        foreach (var property in properties)
        {
            // 如果 dict 中没有 name == name 的 property，忽略
            if (source.HasProperty(property.Name))
            {
                continue;
            }
            // 否则添加 name == name 的 property， value == default_value
            source.ExtraProperties[property.Name] = property.GetDefaultValue();         }
            return source;
        }

        public static void SetDefaultsForExtraProperties(
            object source, Type objectType)
        {
            if (!(source is IHasExtraProperties))
            {
                throw new ArgumentException($"Given {nameof(source)} object does not implement the {nameof(IHasExtraProperties)} interface!", nameof(source));
            }

            ((IHasExtraProperties) source)
            	.SetDefaultsForExtraProperties(objectType);
        }
}

```

#### 2.2 extensible object

* 可扩展的 object 且实现了`IValidatableObject`接口
* 可以作为基类

```c#
[Serializable]
public class ExtensibleObject : IHasExtraProperties, IValidatableObject
{
    // extra properties 容器
    public ExtraPropertyDictionary ExtraProperties { get; protected set; }
    
    public ExtensibleObject() : this(true)
    {        
    }

    public ExtensibleObject(bool setDefaultsForExtraProperties)
    {
        ExtraProperties = new ExtraPropertyDictionary();
        
        if (setDefaultsForExtraProperties)
        {
            this.SetDefaultsForExtraProperties(
                ProxyHelper.UnProxy(this).GetType());
        }
    }
    
    // validate
    public virtual IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        return ExtensibleObjectValidator
            .GetValidationErrors(this, validationContext);
    }
}

```

#### 2.3 object extension manager

* abp框架定义单独管理 object extension 的 manager，
* 静态 singleton

##### 2.3.1 obj ext. manager

###### 2.3.1.1 manager

```c#
public class ObjectExtensionManager
{
    // singleton
    public static ObjectExtensionManager Instance { get; protected set; } = new ObjectExtensionManager();
        
    // object_extension 集合
    protected ConcurrentDictionary<Type, ObjectExtensionInfo> ObjectsExtensions { get; }
    // ？？？
    [NotNull]
    public ConcurrentDictionary<object, object> Configuration { get; }
    
    // 注入        
    protected internal ObjectExtensionManager()
    {
        ObjectsExtensions = new ConcurrentDictionary<Type, ObjectExtensionInfo>();
        Configuration = new ConcurrentDictionary<object, object>();
    }
    
    /* 添加 object_extension */   
    [NotNull]
    public virtual ObjectExtensionManager AddOrUpdate(
        [NotNull] Type[] types,
        [CanBeNull] Action<ObjectExtensionInfo> configureAction = null)
    {
        Check.NotNull(types, nameof(types));        
        foreach (var type in types)
        {
            AddOrUpdate(type, configureAction);
        }        
        return this;
    }        
    [NotNull]
    public virtual ObjectExtensionManager AddOrUpdate(
        [NotNull] Type type,
        [CanBeNull] Action<ObjectExtensionInfo> configureAction = null)
    {
        Check.AssignableTo<IHasExtraProperties>(type, nameof(type));
        
        var extensionInfo = ObjectsExtensions.GetOrAdd(
            type,
            _ => new ObjectExtensionInfo(type)
        );              
        configureAction?.Invoke(extensionInfo);
        
        return this;
    }   
    [NotNull]
    public virtual ObjectExtensionManager AddOrUpdate<TObject>(
        [CanBeNull] Action<ObjectExtensionInfo> configureAction = null) 
        	where TObject : IHasExtraProperties
    {
        return AddOrUpdate(typeof(TObject), configureAction);
    }    
    
    
    /* 获取 obj_ext */
    [CanBeNull]
    public virtual ObjectExtensionInfo GetOrNull<TObject>() 
        where TObject : IHasExtraProperties
    {
        return GetOrNull(typeof(TObject));
    }    
    [CanBeNull]
    public virtual ObjectExtensionInfo GetOrNull([NotNull] Type type)
    {
        Check.AssignableTo<IHasExtraProperties>(type, nameof(type));
        
        return ObjectsExtensions.GetOrDefault(type);
    }    
    [NotNull]
    public virtual ImmutableList<ObjectExtensionInfo> GetExtendedObjects()
    {
        return ObjectsExtensions.Values.ToImmutableList();
    }
}

```

###### 2.3.1.2 manager extension

```c#
public static class ObjectExtensionManagerExtensions
{
    /* 添加 obj_ext_property */
    // 向多个 obj 添加 property
    [NotNull]
    public static ObjectExtensionManager AddOrUpdateProperty<TProperty>(
        [NotNull] this ObjectExtensionManager objectExtensionManager,
        [NotNull] Type[] objectTypes,
        [NotNull] string propertyName,
        [CanBeNull] Action<ObjectExtensionPropertyInfo> configureAction = null)
    {
        return objectExtensionManager.AddOrUpdateProperty(
            objectTypes,
            typeof(TProperty),
            propertyName, 
            configureAction);
    }    
    [NotNull]
    public static ObjectExtensionManager AddOrUpdateProperty(
        [NotNull] this ObjectExtensionManager objectExtensionManager,
        [NotNull] Type[] objectTypes,
        [NotNull] Type propertyType,
        [NotNull] string propertyName,
        [CanBeNull] Action<ObjectExtensionPropertyInfo> configureAction = null)
    {
        Check.NotNull(objectTypes, nameof(objectTypes));        
        foreach (var objectType in objectTypes)
        {
            objectExtensionManager.AddOrUpdateProperty(
                objectType,
                propertyType,
                propertyName,
                configureAction);
        }        
        return objectExtensionManager;
    }   
    // 向一个 obj 添加 property
    [NotNull]
    public static ObjectExtensionManager AddOrUpdateProperty<TObject, TProperty>(
        [NotNull] this ObjectExtensionManager objectExtensionManager,
        [NotNull] string propertyName,
        [CanBeNull] Action<ObjectExtensionPropertyInfo> configureAction = null) 
        	where TObject : IHasExtraProperties
    {
        return objectExtensionManager.AddOrUpdateProperty(
            typeof(TObject),
            typeof(TProperty),
            propertyName,
            configureAction);
    }        
    [NotNull]
    public static ObjectExtensionManager AddOrUpdateProperty(
        [NotNull] this ObjectExtensionManager objectExtensionManager,
        [NotNull] Type objectType,
        [NotNull] Type propertyType,
        [NotNull] string propertyName,
        [CanBeNull] Action<ObjectExtensionPropertyInfo> configureAction = null)
    {
        Check.NotNull(objectExtensionManager, nameof(objectExtensionManager));
        
        return objectExtensionManager.AddOrUpdate(
            objectType,
            options =>
            {
                options.AddOrUpdateProperty(
                    propertyType,
                    propertyName,
                    configureAction);
            });
    }
    
    /* 获取 obj_ext_property */
    public static ObjectExtensionPropertyInfo GetPropertyOrNull<TObject>(
        [NotNull] this ObjectExtensionManager objectExtensionManager,
        [NotNull] string propertyName)
    {
        return objectExtensionManager.GetPropertyOrNull(
            typeof(TObject),
            propertyName);
    }    
    public static ObjectExtensionPropertyInfo GetPropertyOrNull(
        [NotNull] this ObjectExtensionManager objectExtensionManager,
        [NotNull] Type objectType,
        [NotNull] string propertyName)
    {
        Check.NotNull(objectExtensionManager, nameof(objectExtensionManager));
        Check.NotNull(objectType, nameof(objectType));
        Check.NotNull(propertyName, nameof(propertyName));
        
        return objectExtensionManager.GetOrNull(objectType)?
            .GetPropertyOrNull(propertyName);
    }    
    
    /* get all */
    private static readonly ImmutableList<ObjectExtensionPropertyInfo> EmptyPropertyList = 
        new List<ObjectExtensionPropertyInfo>().ToImmutableList();
    
    public static ImmutableList<ObjectExtensionPropertyInfo> GetProperties<TObject>(
        [NotNull] this ObjectExtensionManager objectExtensionManager)
    {
        return objectExtensionManager.GetProperties(typeof(TObject));
    }
    
    public static ImmutableList<ObjectExtensionPropertyInfo> GetProperties(
        [NotNull] this ObjectExtensionManager objectExtensionManager,
        [NotNull] Type objectType)
    {
        Check.NotNull(objectExtensionManager, nameof(objectExtensionManager));
        Check.NotNull(objectType, nameof(objectType));
        
        var extensionInfo = objectExtensionManager.GetOrNull(objectType);
        if (extensionInfo == null)
        {
            return EmptyPropertyList;
        }
        
        return extensionInfo.GetProperties();
    }
}

```

##### 2.3.2 obj extension info

* object 的 extension info，
* 每个 object_type 可以有多个 extension_info

###### 2.3.2.1 obj ext. info

```c#
public class ObjectExtensionInfo
{        
    [NotNull]
    public Type Type { get; }    
    [NotNull]
    protected ConcurrentDictionary<string, ObjectExtensionPropertyInfo> Properties { get; }     
    [NotNull]
    public List<Action<ObjectExtensionValidationContext>> Validators { get; }
    
    [NotNull]
    public ConcurrentDictionary<object, object> Configuration { get; }
        
    public ObjectExtensionInfo([NotNull] Type type)
    {
        Type = Check.AssignableTo<IHasExtraProperties>(type, nameof(type));
        Properties = new ConcurrentDictionary<string, ObjectExtensionPropertyInfo>();        
        Validators = new List<Action<ObjectExtensionValidationContext>>();
        
        Configuration = new ConcurrentDictionary<object, object>();
    }
    
    // check 是否包含 name == propertyName 的 property
    public virtual bool HasProperty(string propertyName)
    {
        return Properties.ContainsKey(propertyName);
    }
    
    /* add property */
    [NotNull]
    public virtual ObjectExtensionInfo AddOrUpdateProperty<TProperty>(
        [NotNull] string propertyName,
        [CanBeNull] Action<ObjectExtensionPropertyInfo> configureAction = null)
    {
        return AddOrUpdateProperty(
            typeof(TProperty),
            propertyName,
            configureAction
        );
    }    
    [NotNull]
    public virtual ObjectExtensionInfo AddOrUpdateProperty(
        [NotNull] Type propertyType,
        [NotNull] string propertyName,
        [CanBeNull] Action<ObjectExtensionPropertyInfo> configureAction = null)
    {
        Check.NotNull(propertyType, nameof(propertyType));
        Check.NotNull(propertyName, nameof(propertyName));
        
        var propertyInfo = Properties.GetOrAdd(
            propertyName,
            _ => new ObjectExtensionPropertyInfo(
                 	this, propertyType, propertyName));
        
        configureAction?.Invoke(propertyInfo);
        
        return this;
    }
    
    /* get property */
    [NotNull]
    public virtual ImmutableList<ObjectExtensionPropertyInfo> GetProperties()
    {
        return Properties.Values.ToImmutableList();
    }
	
    /* get properties */
    [CanBeNull]
    public virtual ObjectExtensionPropertyInfo GetPropertyOrNull(
        [NotNull] string propertyName)
    {
        Check.NotNullOrEmpty(propertyName, nameof(propertyName));        
        return Properties.GetOrDefault(propertyName);
    }
}

```

###### 2.3.2.2 obj ext. valid context

```c#
public class ObjectExtensionValidationContext
{
    [NotNull]
    public ObjectExtensionInfo ObjectExtensionInfo { get; }        
    [NotNull]
    public IHasExtraProperties ValidatingObject { get; }
        
    [NotNull]
    public List<ValidationResult> ValidationErrors { get; }
        
    [NotNull]
    public ValidationContext ValidationContext { get; }        
    [CanBeNull]
    public IServiceProvider ServiceProvider => ValidationContext;
    
    public ObjectExtensionValidationContext(
        [NotNull] ObjectExtensionInfo objectExtensionInfo,
        [NotNull] IHasExtraProperties validatingObject,
        [NotNull] List<ValidationResult> validationErrors,
        [NotNull] ValidationContext validationContext)
    {
        ObjectExtensionInfo = Check.NotNull(
            objectExtensionInfo, nameof(objectExtensionInfo));
        ValidatingObject = Check.NotNull(
            validatingObject, nameof(validatingObject));
        ValidationErrors = Check.NotNull(
            validationErrors, nameof(validationErrors));
        ValidationContext = Check.NotNull(
            validationContext, nameof(validationContext));
    }
}

```

##### 2.3.3 obj extension property info

* object_extension_info 中的 property info

###### 2.3.3.1 接口

```c#
public interface IBasicObjectExtensionPropertyInfo
{
    [NotNull]
    public Type Type { get; }
    
    [NotNull]
    public string Name { get; }
    [CanBeNull]
    public ILocalizableString DisplayName { get; }
    
    [CanBeNull]
    public object DefaultValue { get; set; }        
    [CanBeNull]
    public Func<object> DefaultValueFactory { get; set; }
    
    [NotNull]
    public List<Attribute> Attributes { get; }
    
    [NotNull]
    public List<Action<ObjectExtensionPropertyValidationContext>> Validators { get; }           
}

```

###### 3.3.3.2 实现

```c#
public class ObjectExtensionPropertyInfo 
    : IHasNameWithLocalizableDisplayName, IBasicObjectExtensionPropertyInfo
{
    [NotNull]
    public ObjectExtensionInfo ObjectExtension { get; }
    
    [NotNull]
    public Type Type { get; }
    
    [NotNull]
    public string Name { get; }
    [CanBeNull]
    public ILocalizableString DisplayName { get; set; }
    
    [CanBeNull]
    public object DefaultValue { get; set; }        
    [CanBeNull]
    public Func<object> DefaultValueFactory { get; set; }
    
    [NotNull]
    public List<Attribute> Attributes { get; }    
    [NotNull]
    public List<Action<ObjectExtensionPropertyValidationContext>> Validators { get; }
                
    public bool? CheckPairDefinitionOnMapping { get; set; }
    
    [NotNull]
    public Dictionary<object, object> Configuration { get; }            
    
    [NotNull]
    public ExtensionPropertyLookupConfiguration Lookup { get; set; }
    
    public ObjectExtensionPropertyInfo(
        [NotNull] ObjectExtensionInfo objectExtension,
        [NotNull] Type type,
        [NotNull] string name)
    {
        ObjectExtension = Check.NotNull(
            objectExtension, nameof(objectExtension));
        Type = Check.NotNull(type, nameof(type));
        Name = Check.NotNull(name, nameof(name));
        
        Configuration = new Dictionary<object, object>();
        Attributes = new List<Attribute>();
        Validators = new List<Action<ObjectExtensionPropertyValidationContext>>();
        
        Attributes.AddRange(ExtensionPropertyHelper.GetDefaultAttributes(Type));
        DefaultValue = TypeHelper.GetDefaultValue(Type);
        Lookup = new ExtensionPropertyLookupConfiguration();
    }
    
    public object GetDefaultValue()
    {
        return ExtensionPropertyHelper.GetDefaultValue(
            Type, DefaultValueFactory, DefaultValue);
    }
}

```

###### 3.3.3.3 扩展

```c#
public static class ObjectExtensionPropertyInfoExtensions
{
    public static ValidationAttribute[] GetValidationAttributes(
        this objectExtensionPropertyInfo propertyInfo)
    {
        return propertyInfo.Attributes.OfType<ValidationAttribute>().ToArray();
    }
}

```

###### 2.3.3.4 obj ext. prop valid context

```c#
public class ObjectExtensionPropertyValidationContext
{    
    [NotNull]
    public ObjectExtensionPropertyInfo ExtensionPropertyInfo { get; }    
    
    [NotNull]
    public IHasExtraProperties ValidatingObject { get; }
    [CanBeNull]
    public object Value { get; }
    
    [NotNull]
    public List<ValidationResult> ValidationErrors { get; }
        
    [NotNull]
    public ValidationContext ValidationContext { get; }                    
    [CanBeNull]
    public IServiceProvider ServiceProvider => ValidationContext;
    
    public ObjectExtensionPropertyValidationContext(
        [NotNull] ObjectExtensionPropertyInfo objectExtensionPropertyInfo,
        [NotNull] IHasExtraProperties validatingObject,
        [NotNull] List<ValidationResult> validationErrors,
        [NotNull] ValidationContext validationContext,
        [CanBeNull] object value)
    {
        ExtensionPropertyInfo = Check.NotNull(
            objectExtensionPropertyInfo, nameof(objectExtensionPropertyInfo));
        ValidatingObject = Check.NotNull(
            validatingObject, nameof(validatingObject));
        ValidationErrors = Check.NotNull(
            validationErrors, nameof(validationErrors));
        ValidationContext = Check.NotNull(
            validationContext, nameof(validationContext));
        Value = value;
    }
}

```

#### 2.4 extensible obj mapper

* 静态工具

##### 2.4.1 mapper

```c#
public static class ExtensibleObjectMapper
{    
    public static void MapExtraPropertiesTo<TSource, TDestination>(
        [NotNull] TSource source,
        [NotNull] TDestination destination,
        MappingPropertyDefinitionChecks? definitionChecks = null,
        string[] ignoredProperties = null)         
        	where TSource : IHasExtraProperties             
            where TDestination : IHasExtraProperties
    {
        Check.NotNull(source, nameof(source));
        Check.NotNull(destination, nameof(destination));
        
        ExtensibleObjectMapper.MapExtraPropertiesTo(
            typeof(TSource),
            typeof(TDestination),
            source.ExtraProperties,
            destination.ExtraProperties,
            definitionChecks,
            ignoredProperties);
    }
        
    public static void MapExtraPropertiesTo<TSource, TDestination>(
        [NotNull] Dictionary<string, object> sourceDictionary,
        [NotNull] Dictionary<string, object> destinationDictionary,
        MappingPropertyDefinitionChecks? definitionChecks = null,
        string[] ignoredProperties = null)        
        	where TSource : IHasExtraProperties    
            where TDestination : IHasExtraProperties
    {
        MapExtraPropertiesTo(
            typeof(TSource),
            typeof(TDestination),
            sourceDictionary,
            destinationDictionary,
            definitionChecks,
            ignoredProperties);
    }
    
    // 真正 mapping 的方法
    public static void MapExtraPropertiesTo(
        [NotNull] Type sourceType,
        [NotNull] Type destinationType,
        [NotNull] Dictionary<string, object> sourceDictionary,
        [NotNull] Dictionary<string, object> destinationDictionary,
        MappingPropertyDefinitionChecks? definitionChecks = null,
        string[] ignoredProperties = null)
    {
        Check.AssignableTo<IHasExtraProperties>(sourceType, nameof(sourceType));
        Check.AssignableTo<IHasExtraProperties>(destinationType, nameof(destinationType));
        Check.NotNull(sourceDictionary, nameof(sourceDictionary));
        Check.NotNull(destinationDictionary, nameof(destinationDictionary));
        
        var sourceObjectExtension = ObjectExtensionManager.Instance.GetOrNull(sourceType);
        var destinationObjectExtension = ObjectExtensionManager.Instance.
            GetOrNull(destinationType);
        
        foreach (var keyValue in sourceDictionary)
        {
            if (CanMapProperty(
                	keyValue.Key,
                	sourceObjectExtension,
                	destinationObjectExtension,
                	definitionChecks,
                	ignoredProperties))
            {
                destinationDictionary[keyValue.Key] = keyValue.Value;
            }
        }
    }
    
    public static bool CanMapProperty<TSource, TDestination>(
        [NotNull] string propertyName,
        MappingPropertyDefinitionChecks? definitionChecks = null,
        string[] ignoredProperties = null)
    {
        return CanMapProperty(
            typeof(TSource),
            typeof(TDestination),
            propertyName,
            definitionChecks,
            ignoredProperties);
    }
    
    public static bool CanMapProperty(
        [NotNull] Type sourceType,
        [NotNull] Type destinationType,
        [NotNull] string propertyName,
        MappingPropertyDefinitionChecks? definitionChecks = null,
        string[] ignoredProperties = null)
    {
        Check.AssignableTo<IHasExtraProperties>(sourceType, nameof(sourceType));
        Check.AssignableTo<IHasExtraProperties>(destinationType, nameof(destinationType));
        Check.NotNull(propertyName, nameof(propertyName));
        
        var sourceObjectExtension = ObjectExtensionManager.Instance.GetOrNull(sourceType);
        var destinationObjectExtension = ObjectExtensionManager.Instance
            .GetOrNull(destinationType);
        
        return CanMapProperty(
            propertyName,
            sourceObjectExtension,
            destinationObjectExtension,
            definitionChecks,
            ignoredProperties);
    }
    
    // 执行 mapping check 的方法
    private static bool CanMapProperty(
        [NotNull] string propertyName,
        [CanBeNull] ObjectExtensionInfo sourceObjectExtension,
        [CanBeNull] ObjectExtensionInfo destinationObjectExtension,
        MappingPropertyDefinitionChecks? definitionChecks = null,
        string[] ignoredProperties = null)
    {
        Check.NotNull(propertyName, nameof(propertyName));
        
        // 标记了 ignore property，忽略
        if (ignoredProperties != null &&
            ignoredProperties.Contains(propertyName))
        {
            return false;
        }
        
        if (definitionChecks != null)
        {
            if (definitionChecks.Value.HasFlag(MappingPropertyDefinitionChecks.Source))
            {
                if (sourceObjectExtension == null)
                {
                    return false;
                }                
                if (!sourceObjectExtension.HasProperty(propertyName))
                {
                    return false;
                }
            }
            
            if (definitionChecks.Value.HasFlag(MappingPropertyDefinitionChecks.Destination))
            {
                if (destinationObjectExtension == null)
                {
                    return false;
                }                
                if (!destinationObjectExtension.HasProperty(propertyName))
                {
                    return false;
                }
            }
            
            return true;
        }
        else
        {
            var sourcePropertyDefinition = sourceObjectExtension?
                .GetPropertyOrNull(propertyName);
            var destinationPropertyDefinition = destinationObjectExtension?
                .GetPropertyOrNull(propertyName);
            
            if (sourcePropertyDefinition != null)
            {
                if (destinationPropertyDefinition != null)
                {
                    return true;
                }                
                if (sourcePropertyDefinition.CheckPairDefinitionOnMapping == false)
                {
                    return true;
                }
            }
            else if (destinationPropertyDefinition != null)
            {
                if (destinationPropertyDefinition.CheckPairDefinitionOnMapping == false)
                {
                    return true;
                }
            }
            
            return false;
        }
    }
}

```

##### 2.4.2 mapping prop def. check

```c#
[Flags]
public enum MappingPropertyDefinitionChecks : byte
{    
    /// No check. Copy all extra properties from the source to the destination.    
    None = 0,
    
    /// Copy the extra properties defined for the source class.    
    Source = 1,
        
    /// Copy the extra properties defined for the destination class.    
    Destination = 2,
        
    /// Copy extra properties defined for both of the source and destination classes.    
    Both = Source | Destination
}

```

#### 2.5 extensible obj validator

* 静态工具

##### 2.5.1 check 

* -> get validation errors

```c#
public static class ExtensibleObjectValidator
{
    /* check */
    public static void CheckValue(
        [NotNull] IHasExtraProperties extensibleObject,
        [NotNull] string propertyName,
        [CanBeNull] object value)
    {
        // 验证
        var validationErrors = GetValidationErrors(
            extensibleObject, propertyName, value);
        // 抛出异常（如果有）
        if (validationErrors.Any())
        {
            throw new AbpValidationException(validationErrors);
        }
    }

    /* check 是否 valid */
    public static bool IsValid(
        [NotNull] IHasExtraProperties extensibleObject,
        [CanBeNull] ValidationContext objectValidationContext = null)
    {
        return GetValidationErrors(
            extensibleObject, 
            objectValidationContext).Any();
    }
    public static bool IsValid(
        [NotNull] IHasExtraProperties extensibleObject,
        [NotNull] string propertyName,
        [CanBeNull] object value,
        [CanBeNull] ValidationContext objectValidationContext = null)
    {
        return GetValidationErrors(
            extensibleObject,
            propertyName,
            value,
            objectValidationContext).Any();
    }
}

```

##### 2.5.2 get valid errors

* -> add validation errors

```c#
public static class ExtensibleObjectValidator
{
    // 获取 (ext_obj) validation errors（如果有） 
    [NotNull]
    public static List<ValidationResult> GetValidationErrors(
        [NotNull] IHasExtraProperties extensibleObject,
        [CanBeNull] ValidationContext objectValidationContext = null)
    {
        var validationErrors = new List<ValidationResult>();
        
        AddValidationErrors(
            extensibleObject,
            validationErrors,
            objectValidationContext);
        
        return validationErrors;
    }    
    
    // 获取 (ext_obj_property) validation errors（如果有）
    [NotNull]
    public static List<ValidationResult> GetValidationErrors(
        [NotNull] IHasExtraProperties extensibleObject,
        [NotNull] string propertyName,
        [CanBeNull] object value,
        [CanBeNull] ValidationContext objectValidationContext = null)
    {
        var validationErrors = new List<ValidationResult>();
        
        AddValidationErrors(
            extensibleObject,
            validationErrors,
            propertyName,
            value,
            objectValidationContext);
        
        return validationErrors;
    }
}

```

##### 2.5.3 add valid errors

* -> add properties valid errors
* -> execute custom object valid action

```c#
public static class ExtensibleObjectValidator
{
    /* 添加 （ext_obj) vliadation errors */
    public static void AddValidationErrors(
        [NotNull] IHasExtraProperties extensibleObject,
        [NotNull] List<ValidationResult> validationErrors,
        [CanBeNull] ValidationContext objectValidationContext = null)
    {
        Check.NotNull(extensibleObject, nameof(extensibleObject));
        Check.NotNull(validationErrors, nameof(validationErrors));
        
        // 获取或创建 object validation 上下文
        if (objectValidationContext == null)
        {
            objectValidationContext = new ValidationContext(
                extensibleObject,
                null,
                new Dictionary<object, object>());
        }
        
        // 获取 object_extension_info，
        // 如果 obj_ext_info 为 null，return
        var objectType = ProxyHelper.UnProxy(extensibleObject).GetType();        
        var objectExtensionInfo = ObjectExtensionManager.Instance
            .GetOrNull(objectType);        
        if (objectExtensionInfo == null)
        {
            return;
        }
        
        // 添加 properties 验证
        AddPropertyValidationErrors(
            extensibleObject,
            validationErrors,
            objectValidationContext,
            objectExtensionInfo);
        // 执行自定义 object 验证
        ExecuteCustomObjectValidationActions(
            extensibleObject,
            validationErrors,
            objectValidationContext,
            objectExtensionInfo);
    }   
    
    /* 添加 (ext_obj_property) validation error */
    public static void AddValidationErrors(
        [NotNull] IHasExtraProperties extensibleObject,
        [NotNull] List<ValidationResult> validationErrors,
        [NotNull] string propertyName,
        [CanBeNull] object value,
        [CanBeNull] ValidationContext objectValidationContext = null)
    {
        Check.NotNull(extensibleObject, nameof(extensibleObject));
        Check.NotNull(validationErrors, nameof(validationErrors));
        Check.NotNullOrWhiteSpace(propertyName, nameof(propertyName));
        
        // 获取或创建 object validation 上下文
        if (objectValidationContext == null)
        {
            objectValidationContext = new ValidationContext(
                extensibleObject,
                null,
                new Dictionary<object, object>());
        }
        
        // 获取 object_extension_info，
        // 如果 obj_ext_info 为 null，return
        var objectType = ProxyHelper.UnProxy(extensibleObject).GetType();        
        var objectExtensionInfo = ObjectExtensionManager.Instance
            .GetOrNull(objectType);        
        if (objectExtensionInfo == null)
        {
            return;
        }
        
        // 获取 property，
        // 如果 property 为 null，return
        var property = objectExtensionInfo.GetPropertyOrNull(propertyName);
        if (property == null)
        {
            return;
        }
        
        // 添加 property 验证
        AddPropertyValidationErrors(
            extensibleObject,
            validationErrors,
            objectValidationContext,
            property,
            value);
    }
}

```

##### 2.5.4 add property valid errors

* -> add property attribute valid errors
* -> execute custom property valid errors

```c#
public static class ExtensibleObjectValidator
{                            
    /* 添加 (ext_obj_properties) property validation error */
    private static void AddPropertyValidationErrors(
        IHasExtraProperties extensibleObject,
        List<ValidationResult> validationErrors,
        ValidationContext objectValidationContext,
        ObjectExtensionInfo objectExtensionInfo)
    {
        // 获取 properties，
        // 遇 null 返回
        var properties = objectExtensionInfo.GetProperties();
        if (!properties.Any())
        {
            return;
        }
        // 遍历 property 验证
        foreach (var property in properties)
        {
            AddPropertyValidationErrors(
                extensibleObject,
                validationErrors,
                objectValidationContext,
                property,
                extensibleObject.GetProperty(property.Name));
        }
    }
    
    /* 添加 (ext_obj_property) property validation error */
    private static void AddPropertyValidationErrors(
        IHasExtraProperties extensibleObject,
        List<ValidationResult> validationErrors,
        ValidationContext objectValidationContext,
        ObjectExtensionPropertyInfo property,
        object value)
    {
        // 添加 attribute 验证 (system.validation, annova...)
        AddPropertyValidationAttributeErrors(
            extensibleObject,
            validationErrors,
            objectValidationContext,
            property,
            value);
        // 执行自定义 property 验证
        ExecuteCustomPropertyValidationActions(
            extensibleObject,
            validationErrors,
            objectValidationContext,
            property,
            value);
    }        
}

```

##### 2.5.5 add property attribute valid errors

```c#
public static class ExtensibleObjectValidator
{
    private static void AddPropertyValidationAttributeErrors(
            IHasExtraProperties extensibleObject,
            List<ValidationResult> validationErrors,
            ValidationContext objectValidationContext,
            ObjectExtensionPropertyInfo property,
            object value)
    {
        // 获取 property 标记的 property_valida_attribute
        var validationAttributes = property.GetValidationAttributes();
        // 遇 null，return
        if (!validationAttributes.Any())
        {
            return;
        }
        
        // 生成 validation context       
        var propertyValidationContext = new ValidationContext(
            extensibleObject, objectValidationContext, null)
        {
            DisplayName = property.Name,
            MemberName = property.Name
        };
        
        // 遍历 attribute，
        // 调用 attribute 的 'valid' 方法
        foreach (var attribute in validationAttributes)
        {
            var result = attribute.GetValidationResult(
                value,
                propertyValidationContext
            );            
            if (result != null)
            {
                validationErrors.Add(result);
            }
        }
    }                    
}

```

##### 2.5.6 custom obj valid

```c#
public static class ExtensibleObjectValidator
{
    private static void ExecuteCustomPropertyValidationActions(
        IHasExtraProperties extensibleObject,
        List<ValidationResult> validationErrors,
        ValidationContext objectValidationContext,
        ObjectExtensionPropertyInfo property,
        object value)
    {
        if (!property.Validators.Any())
        {
            return;
        }
        
        var context = new ObjectExtensionPropertyValidationContext(
            property,
            extensibleObject,
            validationErrors,
            objectValidationContext,
            value);
        
        foreach (var validator in property.Validators)
        {
            validator(context);
        }
    }
}

```

##### 2.5.7 custom property valid

```c#
public static class ExtensibleObjectValidator
{
    private static void ExecuteCustomObjectValidationActions(
        IHasExtraProperties extensibleObject,
        List<ValidationResult> validationErrors,
        ValidationContext objectValidationContext,
        ObjectExtensionInfo objectExtensionInfo)
    {
        if (!objectExtensionInfo.Validators.Any())
        {
            return;
        }
        
        var context = new ObjectExtensionValidationContext(
            objectExtensionInfo,
            extensibleObject,
            validationErrors,
            objectValidationContext
        );
        
        foreach (var validator in objectExtensionInfo.Validators)
        {
            validator(context);
        }
    }
}

```





### 3. practice

see the doc



