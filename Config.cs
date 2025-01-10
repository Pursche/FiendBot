using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FiendBot
{
    public class Config
    {
        private string _filePath;
        private Dictionary<string, object> _configData;

        private readonly IDeserializer _deserializer;
        private readonly ISerializer _serializer;

        public Config(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Configuration file not found: {filePath}");

            _filePath = filePath;

            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            Reload();
        }

        public T GetField<T>(string fieldPath, T defaultValue = default)
        {
            string[] keys = fieldPath.Split('.');
            object current = _configData;

            // Traverse the nested dictionaries according to the keys
            foreach (string key in keys)
            {
                if (current is not Dictionary<string, object> dict || !dict.TryGetValue(key, out current))
                {
                    return defaultValue;
                }
            }

            try
            {
                return (T)Convert.ChangeType(current, typeof(T));
            }
            catch
            {
                // Special handling for edge cases like storing a single string vs. string[]
                if (typeof(T) == typeof(string[]) && current is string singleValue)
                {
                    // If we expected an array but only have one string, wrap it into a new array
                    return (T)(object)new string[] { singleValue };
                }
                else if (typeof(T) == typeof(string[]) && current is IEnumerable<object> collection)
                {
                    // If we have a list of objects, attempt to convert each to string
                    var strings = collection.Select(obj => obj?.ToString() ?? string.Empty).ToArray();
                    return (T)(object)strings;
                }

                return defaultValue;
            }
        }

        public void SetField<T>(string fieldPath, T value)
        {
            string[] keys = fieldPath.Split('.');
            Dictionary<string, object> currentDict = _configData;

            for (int i = 0; i < keys.Length - 1; i++)
            {
                var key = keys[i];
                if (!currentDict.ContainsKey(key) || currentDict[key] is not Dictionary<string, object>)
                {
                    currentDict[key] = new Dictionary<string, object>();
                }
                currentDict = (Dictionary<string, object>)currentDict[key];
            }

            currentDict[keys[^1]] = value;
        }

        public void AddStringToList(string fieldPath, string newValue)
        {
            // Attempt to read an existing array of strings
            var currentArray = GetField<string[]>(fieldPath, Array.Empty<string>());

            // Expand it and add the new string
            var list = currentArray.ToList();
            list.Add(newValue);

            // Write it back to the config
            SetField(fieldPath, list.ToArray());
        }

        public bool RemoveStringFromList(string fieldPath, string toRemove)
        {
            var currentArray = GetField<string[]>(fieldPath, Array.Empty<string>());
            var list = currentArray.ToList();

            bool removed = list.Remove(toRemove);
            if (removed)
            {
                SetField(fieldPath, list.ToArray());
            }

            return removed;
        }

        public List<string> GetStringList(string fieldPath)
        {
            var currentArray = GetField<string[]>(fieldPath, Array.Empty<string>());
            return currentArray.ToList();
        }

        public void Reload()
        {
            var yamlContent = File.ReadAllText(_filePath);
            var rawData = _deserializer.Deserialize<object>(yamlContent);
            _configData = ConvertToDictionary(rawData);
        }

        public void Save()
        {
            var raw = ConvertFromDictionary(_configData);
            var yamlContent = _serializer.Serialize(raw);
            File.WriteAllText(_filePath, yamlContent);
        }

        private Dictionary<string, object> ConvertToDictionary(object rawData)
        {
            if (rawData is not IDictionary<object, object> rawDict)
                throw new InvalidCastException("The configuration file does not contain valid YAML data.");

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

        private object ConvertFromDictionary(Dictionary<string, object> dict)
        {
            var result = new Dictionary<object, object>();
            foreach (var kvp in dict)
            {
                if (kvp.Value is Dictionary<string, object> subDict)
                    result[kvp.Key] = ConvertFromDictionary(subDict);
                else
                    result[kvp.Key] = kvp.Value;
            }
            return result;
        }
        
    }
}
