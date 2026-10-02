using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace IntelXPro.src.IntelXPro.Dependencies.Data
{
    internal sealed class InMemoryZip : IDisposable
    {
        private readonly ConcurrentDictionary<string, byte[]> _entries = new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private readonly object _buildLock = new object();
        private bool _disposed;

        public int Count => _entries.Count;

        private static string NormalizeEntryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Entry name is null or empty", "name");
            }
            name = name.Replace('\\', '/').Trim('/');
            if (name.Length != 0)
            {
                return name;
            }
            throw new ArgumentException("Invalid entry name", "name");
        }

        public void AddFile(string entryPath, byte[] content)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("InMemoryZip");
            }
            if (content != null && content.Length != 0)
            {
                string key = NormalizeEntryName(entryPath);
                byte[] copy = new byte[content.Length];
                Buffer.BlockCopy(content, 0, copy, 0, content.Length);
                _entries.AddOrUpdate(key, copy, (string text, byte[] old) => copy);
            }
        }

        public void AddTextFile(string entryPath, string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                AddFile(entryPath, Encoding.UTF8.GetBytes(text));
            }
        }

        public void AddDirectoryFiles(string sourceDirectory, string targetEntryDirectory = "", bool recursive = true)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("InMemoryZip");
            }
            if (string.IsNullOrEmpty(sourceDirectory))
            {
                throw new ArgumentException("sourceDirectory");
            }
            if (!Directory.Exists(sourceDirectory))
            {
                return;
            }
            SearchOption searchOption = (recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            string[] files = Directory.GetFiles(sourceDirectory, "*", searchOption);
            foreach (string text in files)
            {
                string text2 = text.Substring(sourceDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string text3 = (string.IsNullOrEmpty(targetEntryDirectory) ? text2 : Path.Combine(targetEntryDirectory, text2));
                text3 = text3.Replace('\\', '/');
                try
                {
                    byte[] content = File.ReadAllBytes(text);
                    AddFile(text3, content);
                }
                catch
                {
                }
            }
        }

        public byte[] ToArray(CompressionLevel compression = CompressionLevel.Fastest)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("InMemoryZip");
            }
            lock (_buildLock)
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (ZipArchive zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                    {
                        foreach (var entry in _entries)
                        {
                            ZipArchiveEntry zipEntry = zipArchive.CreateEntry(entry.Key, compression);
                            using (Stream stream = zipEntry.Open())
                            {
                                byte[] value = entry.Value;
                                stream.Write(value, 0, value.Length);
                            }
                        }
                    }
                    return memoryStream.ToArray();
                }
            }
        }

        public void Clear()
        {
            _entries.Clear();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _entries.Clear();
            }
        }
    }
}