using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FiendBot
{
    public class ConfigManager
    {
        private readonly Dictionary<string, object> _configData;

        public ConfigManager(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Configuration file not found: {filePath}");
            }

            var yamlContent = File.ReadAllText(filePath);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var rawData = deserializer.Deserialize<object>(yamlContent);
            _configData = ConvertToDictionary(rawData);
        }

        public T GetField<T>(string fieldPath)
        {
            string[] keys = fieldPath.Split('.');
            object current = _configData;

            foreach (string key in keys)
            {
                if (current is not Dictionary<string, object> dict || !dict.TryGetValue(key, out current))
                {
                    throw new KeyNotFoundException($"Field not found in configuration: {fieldPath}");
                }
            }

            // Safely convert to the requested type:
            return (T)Convert.ChangeType(current, typeof(T));
        }

        private Dictionary<string, object> ConvertToDictionary(object rawData)
        {
            if (rawData is not IDictionary<object, object> rawDict)
            {
                throw new InvalidCastException("The configuration file does not contain valid YAML data.");
            }

            var result = new Dictionary<string, object>();
            foreach (var (key, value) in rawDict)
            {
                string stringKey = key.ToString() ?? throw new InvalidCastException("Invalid key in YAML dictionary.");
                result[stringKey] = value is IDictionary<object, object> nestedDict
                    ? ConvertToDictionary(nestedDict)
                    : value;
            }

            return result;
        }
    }
}
