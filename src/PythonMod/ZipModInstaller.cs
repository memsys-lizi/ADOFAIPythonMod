using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;

namespace PythonMod
{
    public static class ZipModInstaller
    {
        public static void Install(string zipPath, string modsDir)
        {
            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            {
                throw new FileNotFoundException("zip 文件不存在。", zipPath);
            }

            Directory.CreateDirectory(modsDir);
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var manifestEntry = archive.GetEntry("pythonmod.json");
                if (manifestEntry == null)
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith("/pythonmod.json", StringComparison.OrdinalIgnoreCase))
                        {
                            manifestEntry = entry;
                            break;
                        }
                    }
                }

                if (manifestEntry == null)
                {
                    throw new InvalidDataException("zip 中缺少 pythonmod.json。");
                }

                PythonModManifest manifest;
                using (var reader = new StreamReader(manifestEntry.Open()))
                {
                    manifest = JsonConvert.DeserializeObject<PythonModManifest>(reader.ReadToEnd());
                }

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
                {
                    throw new InvalidDataException("pythonmod.json 缺少 id。");
                }

                var targetDir = Path.Combine(modsDir, manifest.Id);
                Directory.CreateDirectory(targetDir);
                var rootPrefix = manifestEntry.FullName.EndsWith("pythonmod.json", StringComparison.OrdinalIgnoreCase)
                    ? manifestEntry.FullName.Substring(0, manifestEntry.FullName.Length - "pythonmod.json".Length)
                    : "";

                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    var relative = entry.FullName.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                        ? entry.FullName.Substring(rootPrefix.Length)
                        : entry.FullName;
                    relative = relative.Replace('/', Path.DirectorySeparatorChar);
                    var destination = Path.GetFullPath(Path.Combine(targetDir, relative));
                    if (!destination.StartsWith(Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("zip 中包含路径穿越文件。");
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    entry.ExtractToFile(destination, true);
                }
            }
        }
    }
}
