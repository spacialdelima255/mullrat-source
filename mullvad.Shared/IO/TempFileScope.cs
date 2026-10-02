using System;
using System.IO;

namespace mullvad.IO
{
    /// <summary>
    /// Creates a temporary file and deletes it when disposed.
    /// </summary>
    public sealed class TempFileScope : IDisposable
    {
        public string Path { get; }

        public TempFileScope(string? extension = null)
        {
            string tmp = System.IO.Path.GetTempFileName();
            if (!string.IsNullOrEmpty(extension))
            {
                string renamed = System.IO.Path.ChangeExtension(tmp, extension);
                File.Move(tmp, renamed);
                tmp = renamed;
            }
            Path = tmp;
        }

        public void Dispose()
        {
            try { if (File.Exists(Path)) File.Delete(Path); }
            catch { /* best-effort */ }
        }
    }
}
