using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.Hashing
{
    public class PBE
    {
        private byte[] Ciphertext { get; }

        private byte[] GlobalSalt { get; }

        private byte[] MasterPass { get; }

        private byte[] EntrySalt { get; }

        private byte[] PartIv { get; }

        public PBE(byte[] ciphertext, byte[] globalSalt, byte[] masterPassword, byte[] entrySalt, byte[] partIv)
        {
            Ciphertext = ciphertext;
            GlobalSalt = globalSalt;
            MasterPass = masterPassword;
            EntrySalt = entrySalt;
            PartIv = partIv;
        }

        public byte[] Compute()
        {
            byte[] array = new byte[GlobalSalt.Length + MasterPass.Length];
            Buffer.BlockCopy(GlobalSalt, 0, array, 0, GlobalSalt.Length);
            Buffer.BlockCopy(MasterPass, 0, array, GlobalSalt.Length, MasterPass.Length);
            byte[] password = new SHA1Managed().ComputeHash(array);
            byte[] array2 = new byte[2] { 4, 14 };
            byte[] array3 = new byte[array2.Length + PartIv.Length];
            Buffer.BlockCopy(array2, 0, array3, 0, array2.Length);
            Buffer.BlockCopy(PartIv, 0, array3, array2.Length, PartIv.Length);
            byte[] bytes = new PBKDF2(new HMACSHA256(), password, EntrySalt, 1).GetBytes(32);
            return new AesManaged
            {
                Mode = CipherMode.CBC,
                BlockSize = 128,
                KeySize = 256,
                Padding = PaddingMode.Zeros
            }.CreateDecryptor(bytes, array3).TransformFinalBlock(Ciphertext, 0, Ciphertext.Length);
        }
    }
}
