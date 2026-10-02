using System;
using System.IO;

namespace IntelXPro.src.IntelXPro.Dependencies.General
{
    public static class ParseKeyBlob
    {
        public static BlobParsedData Parse(byte[] blobData)
        {
            using (MemoryStream memoryStream = new MemoryStream(blobData))
            {
                using (BinaryReader binaryReader = new BinaryReader(memoryStream))
                {
                    uint count = binaryReader.ReadUInt32();
                    binaryReader.ReadBytes((int)count);
                    uint num = binaryReader.ReadUInt32();
                    _ = memoryStream.Position;
                    byte[] array = null;
                    byte[] iv = null;
                    byte[] ciphertext = null;
                    byte[] tag = null;
                    if (num == 32)
                    {
                        array = binaryReader.ReadBytes(32);
                        return new BlobParsedData
                        {
                            Flag = 32,
                            Iv = iv,
                            Ciphertext = ciphertext,
                            Tag = tag,
                            EncryptedAesKey = array
                        };
                    }
                    byte b = binaryReader.ReadByte();
                    switch (b)
                    {
                        case 1:
                        case 2:
                            iv = binaryReader.ReadBytes(12);
                            ciphertext = binaryReader.ReadBytes(32);
                            tag = binaryReader.ReadBytes(16);
                            return new BlobParsedData
                            {
                                Flag = b,
                                Iv = iv,
                                Ciphertext = ciphertext,
                                Tag = tag,
                                EncryptedAesKey = null
                            };
                        case 3:
                        case 35:
                            array = binaryReader.ReadBytes(32);
                            iv = binaryReader.ReadBytes(12);
                            ciphertext = binaryReader.ReadBytes(32);
                            tag = binaryReader.ReadBytes(16);
                            return new BlobParsedData
                            {
                                Flag = b,
                                Iv = iv,
                                Ciphertext = ciphertext,
                                Tag = tag,
                                EncryptedAesKey = array
                            };
                        default:
                            throw new Exception();
                    }
                }
            }
        }
    }
}