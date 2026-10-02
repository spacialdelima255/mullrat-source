using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.General
{
    public class BlobParsedData
    {
        public byte Flag { get; set; }

        public byte[] Iv { get; set; }

        public byte[] Ciphertext { get; set; }

        public byte[] Tag { get; set; }

        public byte[] EncryptedAesKey { get; set; }
    }
}
