namespace Tablix.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json.Serialization;
    using System.Xml.Linq;
    using Tablix.Core.Models;
    using WatsonWebserver.Core.OpenApi;
    using WatsonOpenApiParameterMetadata = WatsonWebserver.Core.OpenApi.OpenApiParameterMetadata;
    using WatsonOpenApiRequestBodyMetadata = WatsonWebserver.Core.OpenApi.OpenApiRequestBodyMetadata;
    using WatsonOpenApiResponseMetadata = WatsonWebserver.Core.OpenApi.OpenApiResponseMetadata;

    internal static class OpenApiRouteMetadataExtensions
    {
        public static OpenApiRouteMetadata WithSummary(this OpenApiRouteMetadata metadata, string summary)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            metadata.Summary = summary;
            return metadata;
        }

        public static OpenApiRouteMetadata WithSecurity(this OpenApiRouteMetadata metadata, string scheme, string[] scopes)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            metadata.Security ??= new List<string>();
            if (!String.IsNullOrWhiteSpace(scheme) && !metadata.Security.Contains(scheme))
                metadata.Security.Add(scheme);
            metadata.WithResponse(401, OpenApiResponseMetadata.Unauthorized("Authentication failed"));
            metadata.WithResponse(500, OpenApiResponseMetadata.InternalError("Internal server error"));
            return metadata;
        }
    }

    internal static class OpenApiParameterMetadata
    {
        public static WatsonOpenApiParameterMetadata Path(string name, string description)
        {
            return WatsonOpenApiParameterMetadata.Path(name, description, OpenApiSchemaMetadata.String());
        }

        public static WatsonOpenApiParameterMetadata Query(string name, string description, bool required)
        {
            return WatsonOpenApiParameterMetadata.Query(name, description, required, OpenApiSchemaMetadata.String());
        }

        public static WatsonOpenApiParameterMetadata Query<T>(string name, string description, bool required)
        {
            return WatsonOpenApiParameterMetadata.Query(name, description, required, OpenApiSchemaFactory.Create(typeof(T)));
        }
    }

    internal static class OpenApiRequestBodyMetadata
    {
        public static WatsonOpenApiRequestBodyMetadata Json<T>(string description, bool required)
        {
            return WatsonOpenApiRequestBodyMetadata.Json(OpenApiSchemaFactory.Create(typeof(T)), description, required);
        }
    }

    internal static class OpenApiResponseMetadata
    {
        public static WatsonOpenApiResponseMetadata Error(string description)
        {
            return WatsonOpenApiResponseMetadata.Json(description, OpenApiSchemaFactory.Create(typeof(ApiErrorResponse)));
        }

        public static WatsonOpenApiResponseMetadata Json<T>(string description)
        {
            return WatsonOpenApiResponseMetadata.Json(description, OpenApiSchemaFactory.Create(typeof(T)));
        }

        public static WatsonOpenApiResponseMetadata NoContent(string description)
        {
            WatsonOpenApiResponseMetadata response = WatsonOpenApiResponseMetadata.NoContent();
            response.Description = description;
            return response;
        }

        public static WatsonOpenApiResponseMetadata Text(string description)
        {
            return WatsonOpenApiResponseMetadata.Text(description);
        }

        public static WatsonOpenApiResponseMetadata NotFound(string description)
        {
            return Error(description);
        }

        public static WatsonOpenApiResponseMetadata Unauthorized(string description)
        {
            return Error(description);
        }

        public static WatsonOpenApiResponseMetadata InternalError(string description)
        {
            return Error(description);
        }
    }

    internal static class OpenApiSchemaFactory
    {
        private static readonly object _Lock = new object();
        private static readonly NullabilityInfoContext _Nullability = new NullabilityInfoContext();
        private static readonly Dictionary<Assembly, Dictionary<string, string>> _Documentation = new Dictionary<Assembly, Dictionary<string, string>>();
        private static IDictionary<string, OpenApiSchemaMetadata> _ComponentSchemas = null;
        private static Dictionary<Type, string> _ComponentNames = new Dictionary<Type, string>();
        private static HashSet<Type> _Building = new HashSet<Type>();

        public static void UseComponents(IDictionary<string, OpenApiSchemaMetadata> schemas)
        {
            lock (_Lock)
            {
                _ComponentSchemas = schemas ?? throw new ArgumentNullException(nameof(schemas));
                _ComponentNames = new Dictionary<Type, string>();
                _Building = new HashSet<Type>();
            }
        }

        public static OpenApiSchemaMetadata Create(Type type)
        {
            lock (_Lock)
            {
                return CreateSchema(type, true);
            }
        }

        private static OpenApiSchemaMetadata CreateSchema(Type type, bool allowRef)
        {
            if (type == null) return new OpenApiSchemaMetadata { Type = "object" };

            bool nullable = IsNullableType(type);
            Type schemaType = Nullable.GetUnderlyingType(type) ?? type;

            OpenApiSchemaMetadata schema;

            if (schemaType == typeof(object))
            {
                schema = new OpenApiSchemaMetadata { Type = "object", Description = "Arbitrary JSON value." };
            }
            else if (schemaType == typeof(string) || schemaType == typeof(char))
            {
                schema = OpenApiSchemaMetadata.String();
            }
            else if (schemaType == typeof(Guid))
            {
                schema = OpenApiSchemaMetadata.String("uuid");
            }
            else if (schemaType == typeof(DateTime) || schemaType == typeof(DateTimeOffset))
            {
                schema = OpenApiSchemaMetadata.String("date-time");
            }
            else if (schemaType == typeof(TimeSpan))
            {
                schema = OpenApiSchemaMetadata.String("duration");
            }
            else if (schemaType.IsEnum)
            {
                schema = OpenApiSchemaMetadata.String();
                schema.Enum = Enum.GetNames(schemaType).Cast<object>().ToList();
                schema.Description = GetDocumentationSummary(schemaType);
            }
            else if (schemaType == typeof(bool))
            {
                schema = OpenApiSchemaMetadata.Boolean();
            }
            else if (schemaType == typeof(int) || schemaType == typeof(short) || schemaType == typeof(byte) || schemaType == typeof(sbyte) || schemaType == typeof(ushort))
            {
                schema = OpenApiSchemaMetadata.Integer("int32");
            }
            else if (schemaType == typeof(long) || schemaType == typeof(uint) || schemaType == typeof(ulong))
            {
                schema = OpenApiSchemaMetadata.Integer("int64");
            }
            else if (schemaType == typeof(float))
            {
                schema = OpenApiSchemaMetadata.Number("float");
            }
            else if (schemaType == typeof(double))
            {
                schema = OpenApiSchemaMetadata.Number("double");
            }
            else if (schemaType == typeof(decimal))
            {
                schema = OpenApiSchemaMetadata.Number("decimal");
            }
            else if (schemaType == typeof(byte[]))
            {
                schema = OpenApiSchemaMetadata.String("byte");
            }
            else if (TryGetDictionaryValueType(schemaType, out Type dictionaryValueType))
            {
                schema = new OpenApiSchemaMetadata
                {
                    Type = "object",
                    Description = "String-keyed dictionary with values of type " + GetFriendlyTypeName(dictionaryValueType) + "."
                };
            }
            else if (TryGetEnumerableElementType(schemaType, out Type elementType))
            {
                schema = OpenApiSchemaMetadata.CreateArray(CreateSchema(elementType, true));
            }
            else if (allowRef && ShouldUseComponent(schemaType))
            {
                string schemaName = EnsureComponent(schemaType);
                schema = OpenApiSchemaMetadata.CreateRef(schemaName);
            }
            else
            {
                schema = BuildObjectSchema(schemaType);
            }

            if (nullable) schema.Nullable = true;
            return schema;
        }

        private static string EnsureComponent(Type type)
        {
            if (_ComponentSchemas == null) return GetSchemaName(type);

            if (_ComponentNames.TryGetValue(type, out string existingName))
                return existingName;

            string schemaName = GetSchemaName(type);
            string uniqueName = schemaName;
            int suffix = 2;
            while (_ComponentSchemas.ContainsKey(uniqueName))
            {
                uniqueName = schemaName + suffix.ToString();
                suffix++;
            }

            _ComponentNames[type] = uniqueName;

            if (_Building.Contains(type))
                return uniqueName;

            _Building.Add(type);
            try
            {
                _ComponentSchemas[uniqueName] = BuildObjectSchema(type);
            }
            finally
            {
                _Building.Remove(type);
            }

            return uniqueName;
        }

        private static OpenApiSchemaMetadata BuildObjectSchema(Type type)
        {
            OpenApiSchemaMetadata schema = new OpenApiSchemaMetadata
            {
                Type = "object",
                Description = GetDocumentationSummary(type),
                Properties = new Dictionary<string, OpenApiSchemaMetadata>(),
                Required = new List<string>()
            };

            foreach (PropertyInfo property in GetSerializableProperties(type))
            {
                string propertyName = GetJsonPropertyName(property);
                OpenApiSchemaMetadata propertySchema = CreateSchema(property.PropertyType, true);
                propertySchema.Description = GetDocumentationSummary(property) ?? propertySchema.Description;

                if (IsNullableProperty(property))
                    propertySchema.Nullable = true;

                schema.Properties[propertyName] = propertySchema;
            }

            return schema;
        }

        private static List<PropertyInfo> GetSerializableProperties(Type type)
        {
            return type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property =>
                    property.GetIndexParameters().Length == 0
                    && property.GetMethod != null
                    && property.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                .OrderBy(property => property.GetCustomAttribute<JsonPropertyOrderAttribute>()?.Order ?? 0)
                .ThenBy(property => property.Name, StringComparer.Ordinal)
                .ToList();
        }

        private static bool ShouldUseComponent(Type type)
        {
            if (type == null) return false;
            if (type.IsPrimitive || type.IsEnum) return false;
            if (type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid)) return false;
            if (type.IsArray) return false;
            if (TryGetDictionaryValueType(type, out _)) return false;
            if (TryGetEnumerableElementType(type, out _)) return false;
            if (type.Namespace != null && type.Namespace.StartsWith("System.", StringComparison.Ordinal)) return false;
            return true;
        }

        private static bool TryGetEnumerableElementType(Type type, out Type elementType)
        {
            elementType = null;
            if (type == typeof(string)) return false;

            Type enumerableType = type
                .GetInterfaces()
                .Concat(new[] { type })
                .FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (enumerableType == null) return false;
            elementType = enumerableType.GetGenericArguments()[0];
            return true;
        }

        private static bool TryGetDictionaryValueType(Type type, out Type valueType)
        {
            valueType = null;
            Type dictionaryType = type
                .GetInterfaces()
                .Concat(new[] { type })
                .FirstOrDefault(candidate =>
                    candidate.IsGenericType
                    && candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                    && candidate.GetGenericArguments()[0] == typeof(string));

            if (dictionaryType == null) return false;
            valueType = dictionaryType.GetGenericArguments()[1];
            return true;
        }

        private static bool IsNullableType(Type type)
        {
            if (type == null) return true;
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        private static bool IsNullableProperty(PropertyInfo property)
        {
            if (property.PropertyType.IsValueType)
                return Nullable.GetUnderlyingType(property.PropertyType) != null;

            try
            {
                NullabilityInfo nullabilityInfo = _Nullability.Create(property);
                return nullabilityInfo.WriteState != NullabilityState.NotNull;
            }
            catch
            {
                return true;
            }
        }

        private static string GetJsonPropertyName(PropertyInfo property)
        {
            JsonPropertyNameAttribute attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();
            return !String.IsNullOrWhiteSpace(attribute?.Name) ? attribute.Name : property.Name;
        }

        private static string GetSchemaName(Type type)
        {
            if (!type.IsGenericType) return SanitizeSchemaName(type.Name);

            string baseName = type.Name;
            int tickIndex = baseName.IndexOf('`');
            if (tickIndex >= 0) baseName = baseName.Substring(0, tickIndex);

            string suffix = String.Join("And", type.GetGenericArguments().Select(GetSchemaName));
            return SanitizeSchemaName(baseName + "Of" + suffix);
        }

        private static string SanitizeSchemaName(string value)
        {
            return new string(value.Where(ch => Char.IsLetterOrDigit(ch) || ch == '_').ToArray());
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type == null) return "object";
            if (!type.IsGenericType) return type.Name;
            string baseName = type.Name;
            int tickIndex = baseName.IndexOf('`');
            if (tickIndex >= 0) baseName = baseName.Substring(0, tickIndex);
            return baseName + "<" + String.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName)) + ">";
        }

        private static string GetDocumentationSummary(Type type)
        {
            if (type == null) return null;
            Type documentationType = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            return GetDocumentationSummary(documentationType.Assembly, "T:" + GetXmlTypeName(documentationType));
        }

        private static string GetDocumentationSummary(PropertyInfo property)
        {
            if (property == null) return null;
            Type declaringType = property.DeclaringType;
            if (declaringType == null) return null;
            if (declaringType.IsGenericType) declaringType = declaringType.GetGenericTypeDefinition();
            return GetDocumentationSummary(declaringType.Assembly, "P:" + GetXmlTypeName(declaringType) + "." + property.Name);
        }

        private static string GetDocumentationSummary(Assembly assembly, string memberName)
        {
            Dictionary<string, string> documentation = GetDocumentation(assembly);
            if (documentation.TryGetValue(memberName, out string summary))
                return summary;

            return null;
        }

        private static Dictionary<string, string> GetDocumentation(Assembly assembly)
        {
            if (_Documentation.TryGetValue(assembly, out Dictionary<string, string> existing))
                return existing;

            Dictionary<string, string> documentation = new Dictionary<string, string>();
            _Documentation[assembly] = documentation;

            string assemblyLocation = assembly.Location;
            if (String.IsNullOrWhiteSpace(assemblyLocation)) return documentation;

            string xmlFilename = Path.ChangeExtension(assemblyLocation, ".xml");
            if (!File.Exists(xmlFilename)) return documentation;

            try
            {
                XDocument document = XDocument.Load(xmlFilename);
                foreach (XElement member in document.Descendants("member"))
                {
                    string name = member.Attribute("name")?.Value;
                    string summary = NormalizeDocumentation(member.Element("summary")?.Value);
                    if (!String.IsNullOrWhiteSpace(name) && !String.IsNullOrWhiteSpace(summary))
                        documentation[name] = summary;
                }
            }
            catch
            {
            }

            return documentation;
        }

        private static string NormalizeDocumentation(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            return String.Join(" ", value.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(item => item.Trim()));
        }

        private static string GetXmlTypeName(Type type)
        {
            if (type == null) return null;
            string name = type.FullName ?? type.Name;
            return name.Replace('+', '.');
        }
    }
}
