using System.Collections.Generic;
using System.IO;
using mullvad.Models;

namespace mullvad.IO
{
    /// <summary>
    /// Splits a file into fixed-size <see cref="FileChunk"/> objects for streaming transfer,
    /// and reassembles them at the destination.
    /// </summary>
    public static class FileChunker
    {
        public const int DefaultChunkSize = 64 * 1024; // 64 KB

        public static IEnumerable<FileChunk> Read(string filePath, string transferId,
            int chunkSize = DefaultChunkSize)
        {
            var info = new FileInfo(filePath);
            long fileSize = info.Length;
            long offset   = 0;
            var  buffer   = new byte[chunkSize];

            using var fs = new FileStream(filePath, FileMode.Open,
                FileAccess.Read, FileShare.Read, chunkSize, FileOptions.SequentialScan);

            int read;
            while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                var data = new byte[read];
                Buffer.BlockCopy(buffer, 0, data, 0, read);
                bool isLast = offset + read >= fileSize;

                yield return new FileChunk
                {
                    TransferId = transferId,
                    FilePath   = filePath,
                    FileSize   = fileSize,
                    Offset     = offset,
                    Data       = data,
                    IsLast     = isLast
                };

                offset += read;
            }
        }

        public static void Write(string destination, FileChunk chunk)
        {
            string? dir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var fs = new FileStream(destination, FileMode.OpenOrCreate,
                FileAccess.Write, FileShare.None);
            fs.Seek(chunk.Offset, SeekOrigin.Begin);
            fs.Write(chunk.Data, 0, chunk.Data.Length);
        }
    }
}
