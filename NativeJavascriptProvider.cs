using Gcef.CustomProvider.Native;
using Jint;
using Jint.Native;
using System.Data;
using System.Dynamic;
using System.Reflection;
using System.Text;

namespace JavascriptProvider
{
	public class NativeJavascriptProvider : INativeQueryDataProvider
    {
        public static string ProviderName => "Javascript";

        public static void Configure(IFeatureCollection features)
        {
            features.Metadata().DisplayName = ProviderName;
            features.Metadata().Description = "General-purposed native JDBC connector";

            var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (Path.Combine(assemblyDirectory!, "UserGuide.md") is var userGuidePath && File.Exists(userGuidePath))
            {
                features.UserGuide().UserGuideMarkdown = File.ReadAllText(userGuidePath);
            }

            if (Path.Combine(assemblyDirectory!, "javascript_16x16.png") is var smallIconPath && File.Exists(smallIconPath))
            {
                features.Metadata().SmallIcon = GetDataURL(smallIconPath);
            }

            if (Path.Combine(assemblyDirectory!, "javascript_180x130.png") is var largeIconPath && File.Exists(largeIconPath))
            {
                features.Metadata().LargeIcon = GetDataURL(largeIconPath);
            }

            features.Get<IParameterParseFeature>().NeedFillParameters = true;
        }

        public static INativeQueryDataProvider CreateInstance() => new NativeJavascriptProvider();

        public Task ExecuteAsync(INativeQuery nativeQuery, Action<IDataReader> readerConsumer, params NativeParameter[] parameters)
        {
            if (nativeQuery == null)
            {
                throw new DataException("Native query can not be null.");
            }

            var rawCommandText = nativeQuery.QueryText ?? throw new DataException("Command text can not be null or empty.");
            var engineConfig = EngineConfig.Parse(nativeQuery.ConnectionString);

            int? rowLimit = nativeQuery.RowLimitOption?.RowLimitType switch
            {
                RowLimitType.AllRows => null,
                RowLimitType.SchemaOnly => 0,
                RowLimitType.SingleRow => 1,
                RowLimitType.SpecifiedRowLimit => nativeQuery.RowLimitOption.GetSpecifiedRowLimit,
                _ => null
            };

            using var reader = ScriptExecutor.ExecuteReader(engineConfig, rawCommandText, rowLimit);
            readerConsumer(reader);
            return Task.CompletedTask;
        }

        public Task TestConnectionAsync(string connectionString)
        {
            EngineConfig.Parse(connectionString);
            return Task.CompletedTask;
        }

        private static string GetDataURL(string imgFilePath)
        {
            return "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(imgFilePath));
        }


    }

    internal static class ScriptExecutor
    {
        public static IDataReader ExecuteReader(EngineConfig engineConfig, string script, int? rowLimit)
        {
            using var engine = new Engine(options => options
                .LimitMemory(1024 * 1024 * engineConfig.LimitMemory)
                .TimeoutInterval(TimeSpan.FromSeconds(engineConfig.TimeoutInterval))
                .MaxStatements(engineConfig.MaxStatements));

            var jsResultSet = new JsResultSet();
            engine.SetValue("resultset", jsResultSet);
            engine.SetValue("helper", JsHelper.Instance);

            engine.Execute(script);

            if (jsResultSet.Fields.Count == 0)
            {
                throw new DataException("Js script returned invalid resultset.");
            }

            return jsResultSet.CreateReader(rowLimit);
        }
    }

    internal class JsResultSet
    {
        internal List<(string Name, Type Type)> Fields { get; } = [];
        internal List<object?[]> Rows { get; } = [];

        private static readonly Dictionary<Type, Func<JsValue, object?>> _valueConverters = new()
        {
            {typeof(string), ConvertToString},
            {typeof(int), ConvertToInteger},
            {typeof(double), ConvertToDouble},
            {typeof(decimal), ConvertToDecimal},
            {typeof(bool), ConvertToBoolean},
            {typeof(DateTime), ConvertToDateTime}
        };

        //Invoked by Js
        public void schema(JsObject jsObj)
        {
            foreach (var property in jsObj.GetOwnProperties())
            {
                var name = property.Key.AsString();
                var typeString = property.Value.Value.AsString();
                var clrType = typeString switch
                {
                    "string" => typeof(string),
                    "integer" => typeof(int),
                    "double" => typeof(double),
                    "decimal" => typeof(decimal),
                    "boolean" => typeof(bool),
                    "datetime" => typeof(DateTime),
                    _ => typeof(string)
                };
                Fields.Add((name, clrType));
            }
        }

        public void row(JsValue jsValue)
        {
            if (jsValue is JsArray jsArray)
            {
                Row(jsArray);
            }
            else if (jsValue is JsObject jsObj)
            {
                Row(jsObj);
            }
            else
            {
                throw new DataException("resultset.row() function requires an array or object parameter.");
            }
        }

        private void Row(JsArray jsArray)
        {
            if (jsArray.IsUndefined() || jsArray.IsNull() || jsArray.Length == 0)
            {
                return;
            }
            var newRow = new object?[Fields.Count];
            for (var i = 0; i < Fields.Count; i++)
            {
                if (jsArray.Length > i)
                {
                    var jsValue = jsArray.Get(i);
                    newRow[i] = _valueConverters[Fields[i].Type](jsValue);
                }
            }
            Rows.Add(newRow);
        }

        private void Row(JsObject jsObj)
        {
            if (jsObj.IsUndefined() || jsObj.IsNull())
            {
                return;
            }
            var newRow = new object?[Fields.Count];
            var hitAnyField = false;
            for (var i = 0; i < Fields.Count; i++)
            {
                if (jsObj.TryGetValue(Fields[i].Name, out var jsValue))
                {
                    hitAnyField = true;
                    newRow[i] = _valueConverters[Fields[i].Type](jsValue);
                }
            }
            if (hitAnyField)
            {
                Rows.Add(newRow);
            }
        }


        internal IDataReader CreateReader(int? rowLimit)
        {
            var dataTable = new DataTable();
            foreach (var field in Fields)
            {
                dataTable.Columns.Add(field.Name, field.Type);
            }
            foreach (var row in rowLimit.HasValue ? Rows.Take(rowLimit.Value) : Rows)
            {
                dataTable.Rows.Add(row);
            }
            return dataTable.CreateDataReader();
        }

        #region value type converters
        private static bool CheckNull(JsValue jsValue) => jsValue.IsUndefined() || jsValue.IsNull();
        private static object? ConvertToString(JsValue jsValue) => CheckNull(jsValue) ? null : Jint.Runtime.TypeConverter.ToString(jsValue);
        private static object? ConvertToInteger(JsValue jsValue) => CheckNull(jsValue) ? null : Jint.Runtime.TypeConverter.ToInt32(jsValue);
        private static object? ConvertToDouble(JsValue jsValue) => CheckNull(jsValue) ? null : Jint.Runtime.TypeConverter.ToNumber(jsValue);
        private static object? ConvertToDecimal(JsValue jsValue) => CheckNull(jsValue) ? null : Convert.ToDecimal(Jint.Runtime.TypeConverter.ToNumber(jsValue));
        private static object? ConvertToBoolean(JsValue jsValue) => CheckNull(jsValue) ? null : Jint.Runtime.TypeConverter.ToBoolean(jsValue);
        private static object? ConvertToDateTime(JsValue jsValue) => CheckNull(jsValue) ? null : DateTime.Parse(Jint.Runtime.TypeConverter.ToString(jsValue));
        #endregion
    }

    internal class JsHelper
    {
        internal static JsHelper Instance { get; } = new();

        private readonly HttpClient _httpClient = new();

        //Invoked by Js
        public ExpandoObject fetch(string url, ExpandoObject? options)
        {
            HttpRequestMessage message = new()
            {
                RequestUri = new Uri(url)
            };

            if (options != null)
            {
                var optionsDict = options.ToDictionary(StringComparer.InvariantCultureIgnoreCase);

                message.Method = optionsDict.TryGetValue("method", out var jsMethod) && jsMethod?.ToString() is string method ?
                    new HttpMethod(method.ToString()) : HttpMethod.Get;

                if (optionsDict.TryGetValue("headers", out var jsHeaders) && jsHeaders is ExpandoObject headers)
                {
                    foreach (var header in headers)
                    {
                        if (header.Value != null && header.Value is not JsUndefined || header.Value is not JsNull)
                        {
                            if (header.Value is IEnumerable<string> values)
                            {
                                message.Headers.Add(header.Key, values);
                            }
                            else
                            {
                                message.Headers.Add(header.Key, header.Value!.ToString());
                            }
                        }
                    }
                }

                if (!message.Headers.Contains("ContentType"))
                {
                    message.Headers.Add("ContentType", "application/json");
                }

                var contentType = message.Headers.GetValues("ContentType").First();

                if (optionsDict.TryGetValue("body", out var jsBody) && jsBody != null && jsBody is not JsUndefined && jsBody is not JsNull)
                {
                    message.Content = new StringContent(jsBody.ToString()!, Encoding.UTF8, contentType);
                }
            }


            var response = _httpClient.Send(message);
            dynamic result = new ExpandoObject();
            result.status = (int)response.StatusCode;
            result.statusText = response.StatusCode.ToString();
            result.headers = new ExpandoObject();

            foreach (var header in response.Headers)
            {
                ((IDictionary<string, object>)result.headers).Add(header.Key, header.Value);
            }

            result.body = response.Content.ReadAsStringAsync().Result;
            return result;
        }

        //Invoked by Js
        public ExpandoObject fetch(string url) => fetch(url, null);
    }
}
