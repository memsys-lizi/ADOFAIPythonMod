using System;
using System.IO;
using Newtonsoft.Json;

namespace PythonMod
{
    public static class JsonFile
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.None
        };

        public static T Read<T>(string path, T fallback)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return fallback;
                }

                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<T>(json, Settings);
            }
            catch (Exception ex)
            {
                Main.Mod?.Logger.Log($"读取 JSON 失败：{path}\n{ex}");
                return fallback;
            }
        }

        public static void Write<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(value, Settings));
        }
    }
}
