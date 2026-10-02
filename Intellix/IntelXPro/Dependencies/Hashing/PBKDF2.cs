using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.Hashing
{
    public class PBKDF2
    {
        private readonly int _blockSize;

        private uint _blockIndex = 1u;

        private byte[] _bufferBytes;

        private int _bufferStartIndex;

        private int _bufferEndIndex;

        private HMAC Algorithm { get; }

        private byte[] Salt { get; }

        private int IterationCount { get; }

        public PBKDF2(HMAC algorithm, byte[] password, byte[] salt, int iterations)
        {
            Algorithm = algorithm ?? throw new ArgumentNullException("algorithm", "Algorithm cannot be null.");
            Algorithm.Key = password ?? throw new ArgumentNullException("password", "Password cannot be null.");
            Salt = salt ?? throw new ArgumentNullException("salt", "Salt cannot be null.");
            IterationCount = iterations;
            _blockSize = Algorithm.HashSize / 8;
            _bufferBytes = new byte[_blockSize];
        }

        public byte[] GetBytes(int count)
        {
            byte[] array = new byte[count];
            int i = 0;
            int num = _bufferEndIndex - _bufferStartIndex;
            if (num > 0)
            {
                if (count < num)
                {
                    Buffer.BlockCopy(_bufferBytes, _bufferStartIndex, array, 0, count);
                    _bufferStartIndex += count;
                    return array;
                }
                Buffer.BlockCopy(_bufferBytes, _bufferStartIndex, array, 0, num);
                _bufferStartIndex = (_bufferEndIndex = 0);
                i += num;
            }
            for (; i < count; i += _blockSize)
            {
                int num2 = count - i;
                _bufferBytes = Func();
                if (num2 > _blockSize)
                {
                    Buffer.BlockCopy(_bufferBytes, 0, array, i, _blockSize);
                    continue;
                }
                Buffer.BlockCopy(_bufferBytes, 0, array, i, num2);
                _bufferStartIndex = num2;
                _bufferEndIndex = _blockSize;
                return array;
            }
            return array;
        }

        private byte[] Func()
        {
            byte[] array = new byte[Salt.Length + 4];
            Buffer.BlockCopy(Salt, 0, array, 0, Salt.Length);
            Buffer.BlockCopy(GetBytesFromInt(_blockIndex), 0, array, Salt.Length, 4);
            byte[] array2 = Algorithm.ComputeHash(array);
            byte[] array3 = array2;
            for (int i = 2; i <= IterationCount; i++)
            {
                array2 = Algorithm.ComputeHash(array2, 0, array2.Length);
                for (int j = 0; j < _blockSize; j++)
                {
                    array3[j] ^= array2[j];
                }
            }
            if (_blockIndex == uint.MaxValue)
            {
                throw new InvalidOperationException("Derived key too long.");
            }
            _blockIndex++;
            return array3;
        }

        private static byte[] GetBytesFromInt(uint i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            if (!BitConverter.IsLittleEndian)
            {
                return bytes;
            }
            return new byte[4]
            {
            bytes[3],
            bytes[2],
            bytes[1],
            bytes[0]
            };
        }
    }
}
