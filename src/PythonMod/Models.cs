using System.Collections.Generic;
using Newtonsoft.Json;
using Python.Runtime;

namespace PythonMod
{
    public enum PythonChildModState
    {
        Discovered,
        Disabled,
        Loading,
        Loaded,
        Error
    }

    public sealed class PythonModManifest
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("authors")]
        public List<string> Authors { get; set; } = new List<string>();

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("entry")]
        public string Entry { get; set; }

        [JsonProperty("inject")]
        public string Inject { get; set; } = "Loaded";

        [JsonProperty("python")]
        public string Python { get; set; } = ">=3.11";

        [JsonProperty("dependencies")]
        public List<string> Dependencies { get; set; } = new List<string>();
    }

    public sealed class PythonChildMod
    {
        public PythonModManifest Manifest { get; set; }
        public string DirectoryPath { get; set; }
        public string EntryPath { get; set; }
        public bool Enabled { get; set; }
        public PythonChildModState State { get; set; }
        public string LastError { get; set; }
        public string ModuleName { get; set; }
        public PyObject Module { get; set; }
        public Dictionary<string, SettingDefinition> Settings { get; } = new Dictionary<string, SettingDefinition>();

        public string Id => Manifest?.Id ?? "";
        public string Name => string.IsNullOrEmpty(Manifest?.Name) ? Id : Manifest.Name;
    }

    public sealed class SettingDefinition
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("default")]
        public object DefaultValue { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }

        [JsonProperty("min")]
        public double? Min { get; set; }

        [JsonProperty("max")]
        public double? Max { get; set; }

        [JsonProperty("choices")]
        public List<string> Choices { get; set; } = new List<string>();
    }
}
