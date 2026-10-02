using System;
using System.IO;

namespace mullvad.Helpers
{
    public static class PathHelper
    {
        private static readonly char[] _invalid = Path.GetInvalidFileNameChars();

        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "_";
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name) sb.Append(Array.IndexOf(_invalid, c) >= 0 ? '_' : c);
            return sb.ToString();
        }

        public static string GetTempPath(string? prefix = null, string? extension = null)
        {
            string name = (prefix ?? "tmp") + "_" + Guid.NewGuid().ToString("N");
            if (!string.IsNullOrEmpty(extension))
                name += extension.StartsWith(".") ? extension : "." + extension;
            return Path.Combine(Path.GetTempPath(), name);
        }

        public static string? MimeTypeFromExtension(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".txt"  => "text/plain",
                ".html" => "text/html",
                ".json" => "application/json",
                ".xml"  => "application/xml",
                ".pdf"  => "application/pdf",
                ".zip"  => "application/zip",
                ".png"  => "image/png",
                ".jpg"  => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif"  => "image/gif",
                ".bmp"  => "image/bmp",
                ".mp3"  => "audio/mpeg",
                ".mp4"  => "video/mp4",
                ".exe"  => "application/octet-stream",
                ".dll"  => "application/octet-stream",
                _       => null
            };
        }

        /// <summary>Returns true if <paramref name="path"/> is absolute and exists on disk.</summary>
        public static bool IsAbsoluteAndExists(string path) =>
            !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) &&
            (File.Exists(path) || Directory.Exists(path));
    }
}
