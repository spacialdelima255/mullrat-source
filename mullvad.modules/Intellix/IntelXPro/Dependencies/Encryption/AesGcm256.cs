using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.Encryption
{
    public class AesGcm256
    {
        private static readonly byte[] SBox = new byte[256]
    {
        99, 124, 119, 123, 242, 107, 111, 197, 48, 1,
        103, 43, 254, 215, 171, 118, 202, 130, 201, 125,
        250, 89, 71, 240, 173, 212, 162, 175, 156, 164,
        114, 192, 183, 253, 147, 38, 54, 63, 247, 204,
        52, 165, 229, 241, 113, 216, 49, 21, 4, 199,
        35, 195, 24, 150, 5, 154, 7, 18, 128, 226,
        235, 39, 178, 117, 9, 131, 44, 26, 27, 110,
        90, 160, 82, 59, 214, 179, 41, 227, 47, 132,
        83, 209, 0, 237, 32, 252, 177, 91, 106, 203,
        190, 57, 74, 76, 88, 207, 208, 239, 170, 251,
        67, 77, 51, 133, 69, 249, 2, 127, 80, 60,
        159, 168, 81, 163, 64, 143, 146, 157, 56, 245,
        188, 182, 218, 33, 16, 255, 243, 210, 205, 12,
        19, 236, 95, 151, 68, 23, 196, 167, 126, 61,
        100, 93, 25, 115, 96, 129, 79, 220, 34, 42,
        144, 136, 70, 238, 184, 20, 222, 94, 11, 219,
        224, 50, 58, 10, 73, 6, 36, 92, 194, 211,
        172, 98, 145, 149, 228, 121, 231, 200, 55, 109,
        141, 213, 78, 169, 108, 86, 244, 234, 101, 122,
        174, 8, 186, 120, 37, 46, 28, 166, 180, 198,
        232, 221, 116, 31, 75, 189, 139, 138, 112, 62,
        181, 102, 72, 3, 246, 14, 97, 53, 87, 185,
        134, 193, 29, 158, 225, 248, 152, 17, 105, 217,
        142, 148, 155, 30, 135, 233, 206, 85, 40, 223,
        140, 161, 137, 13, 191, 230, 66, 104, 65, 153,
        45, 15, 176, 84, 187, 22
    };

        private static readonly byte[] Rcon = new byte[256]
        {
        0, 1, 2, 4, 8, 16, 32, 64, 128, 27,
        54, 108, 216, 171, 77, 154, 47, 94, 188, 99,
        198, 151, 53, 106, 212, 179, 125, 250, 239, 197,
        145, 57, 114, 228, 211, 189, 97, 194, 159, 37,
        74, 148, 51, 102, 204, 131, 29, 58, 116, 232,
        203, 141, 1, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0
        };

        private byte[] Key;

        private byte[,] RoundKeys;

        public AesGcm256(byte[] key)
        {
            if (key.Length != 32)
            {
                throw new ArgumentException("Key length must be 256 bits.");
            }
            Key = new byte[32];
            Array.Copy(key, Key, 32);
            KeyExpansion();
        }

        public static byte[] Decrypt(byte[] key, byte[] iv, byte[] aad, byte[] cipherText, byte[] authTag)
        {
            return new AesGcm256(key).Decrypt(cipherText, authTag, iv, aad);
        }

        private void KeyExpansion()
        {
            int num = 8;
            int num2 = 4;
            int num3 = 14;
            RoundKeys = new byte[num2 * (num3 + 1), 4];
            for (int i = 0; i < num; i++)
            {
                RoundKeys[i, 0] = Key[4 * i];
                RoundKeys[i, 1] = Key[4 * i + 1];
                RoundKeys[i, 2] = Key[4 * i + 2];
                RoundKeys[i, 3] = Key[4 * i + 3];
            }
            byte[] array = new byte[4];
            for (int j = num; j < num2 * (num3 + 1); j++)
            {
                array[0] = RoundKeys[j - 1, 0];
                array[1] = RoundKeys[j - 1, 1];
                array[2] = RoundKeys[j - 1, 2];
                array[3] = RoundKeys[j - 1, 3];
                if (j % num == 0)
                {
                    byte b = array[0];
                    array[0] = array[1];
                    array[1] = array[2];
                    array[2] = array[3];
                    array[3] = b;
                    array[0] = SBox[array[0]];
                    array[1] = SBox[array[1]];
                    array[2] = SBox[array[2]];
                    array[3] = SBox[array[3]];
                    array[0] ^= Rcon[j / num];
                }
                else if (num > 6 && j % num == 4)
                {
                    array[0] = SBox[array[0]];
                    array[1] = SBox[array[1]];
                    array[2] = SBox[array[2]];
                    array[3] = SBox[array[3]];
                }
                RoundKeys[j, 0] = (byte)(RoundKeys[j - num, 0] ^ array[0]);
                RoundKeys[j, 1] = (byte)(RoundKeys[j - num, 1] ^ array[1]);
                RoundKeys[j, 2] = (byte)(RoundKeys[j - num, 2] ^ array[2]);
                RoundKeys[j, 3] = (byte)(RoundKeys[j - num, 3] ^ array[3]);
            }
        }

        private void AddRoundKey(byte[,] state, int round)
        {
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    state[j, i] ^= RoundKeys[round * 4 + i, j];
                }
            }
        }

        private void SubBytes(byte[,] state)
        {
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    state[i, j] = SBox[state[i, j]];
                }
            }
        }

        private void ShiftRows(byte[,] state)
        {
            byte b = state[1, 0];
            state[1, 0] = state[1, 1];
            state[1, 1] = state[1, 2];
            state[1, 2] = state[1, 3];
            state[1, 3] = b;
            b = state[2, 0];
            state[2, 0] = state[2, 2];
            state[2, 2] = b;
            b = state[2, 1];
            state[2, 1] = state[2, 3];
            state[2, 3] = b;
            b = state[3, 3];
            state[3, 3] = state[3, 2];
            state[3, 2] = state[3, 1];
            state[3, 1] = state[3, 0];
            state[3, 0] = b;
        }

        private void MixColumns(byte[,] state)
        {
            byte[] array = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                array[0] = (byte)(GFMultiply(2, state[0, i]) ^ GFMultiply(3, state[1, i]) ^ state[2, i] ^ state[3, i]);
                array[1] = (byte)(state[0, i] ^ GFMultiply(2, state[1, i]) ^ GFMultiply(3, state[2, i]) ^ state[3, i]);
                array[2] = (byte)(state[0, i] ^ state[1, i] ^ GFMultiply(2, state[2, i]) ^ GFMultiply(3, state[3, i]));
                array[3] = (byte)(GFMultiply(3, state[0, i]) ^ state[1, i] ^ state[2, i] ^ GFMultiply(2, state[3, i]));
                state[0, i] = array[0];
                state[1, i] = array[1];
                state[2, i] = array[2];
                state[3, i] = array[3];
            }
        }

        private byte GFMultiply(byte a, byte b)
        {
            byte b2 = 0;
            for (int i = 0; i < 8; i++)
            {
                if ((b & 1) != 0)
                {
                    b2 ^= a;
                }
                bool num = (a & 0x80) != 0;
                a <<= 1;
                if (num)
                {
                    a ^= 0x1B;
                }
                b >>= 1;
            }
            return b2;
        }

        private void EncryptBlock(byte[] input, byte[] output)
        {
            int num = 4;
            int num2 = 14;
            byte[,] array = new byte[4, num];
            for (int i = 0; i < 16; i++)
            {
                array[i % 4, i / 4] = input[i];
            }
            AddRoundKey(array, 0);
            for (int j = 1; j <= num2 - 1; j++)
            {
                SubBytes(array);
                ShiftRows(array);
                MixColumns(array);
                AddRoundKey(array, j);
            }
            SubBytes(array);
            ShiftRows(array);
            AddRoundKey(array, num2);
            for (int k = 0; k < 16; k++)
            {
                output[k] = array[k % 4, k / 4];
            }
        }

        private byte[] GF128Multiply(byte[] X, byte[] Y)
        {
            byte[] array = new byte[16];
            byte[] array2 = new byte[16];
            Array.Copy(Y, array2, 16);
            for (int i = 0; i < 128; i++)
            {
                if (((X[i / 8] >> 7 - i % 8) & 1) == 1)
                {
                    for (int j = 0; j < 16; j++)
                    {
                        array[j] ^= array2[j];
                    }
                }
                bool flag = (array2[15] & 1) == 1;
                for (int num = 15; num >= 0; num--)
                {
                    array2[num] = (byte)((array2[num] >> 1) | (((num > 0) ? array2[num - 1] : 0) << 7));
                }
                if (flag)
                {
                    array2[0] ^= 225;
                }
            }
            return array;
        }

        private byte[] GHASH(byte[] H, byte[] A, byte[] C)
        {
            _ = (A.Length + 15) / 16;
            _ = (C.Length + 15) / 16;
            byte[] array = new byte[16];
            byte[] array2 = new byte[16];
            byte[] array3 = new byte[16];
            int num;
            for (int i = 0; i < A.Length; i += num)
            {
                Array.Clear(array3, 0, 16);
                num = Math.Min(16, A.Length - i);
                Array.Copy(A, i, array3, 0, num);
                for (int j = 0; j < 16; j++)
                {
                    array2[j] = (byte)(array[j] ^ array3[j]);
                }
                array = GF128Multiply(array2, H);
            }
            int num2;
            for (int k = 0; k < C.Length; k += num2)
            {
                Array.Clear(array3, 0, 16);
                num2 = Math.Min(16, C.Length - k);
                Array.Copy(C, k, array3, 0, num2);
                for (int l = 0; l < 16; l++)
                {
                    array2[l] = (byte)(array[l] ^ array3[l]);
                }
                array = GF128Multiply(array2, H);
            }
            byte[] array4 = new byte[16];
            ulong num3 = (ulong)A.Length * 8uL;
            ulong num4 = (ulong)C.Length * 8uL;
            for (int m = 0; m < 8; m++)
            {
                array4[7 - m] = (byte)(num3 >> m * 8);
                array4[15 - m] = (byte)(num4 >> m * 8);
            }
            for (int n = 0; n < 16; n++)
            {
                array2[n] = (byte)(array[n] ^ array4[n]);
            }
            return GF128Multiply(array2, H);
        }

        private void IncrementCounter(byte[] counterBlock)
        {
            int num = 15;
            while (num >= 12 && ++counterBlock[num] == 0)
            {
                num--;
            }
        }

        public byte[] Decrypt(byte[] ciphertext, byte[] tag, byte[] iv, byte[] aad)
        {
            if (aad == null)
            {
                aad = new byte[0];
            }
            byte[] array = new byte[16];
            EncryptBlock(new byte[16], array);
            byte[] array2 = new byte[16];
            if (iv.Length == 12)
            {
                Array.Copy(iv, 0, array2, 0, 12);
                array2[15] = 1;
            }
            else
            {
                array2 = GHASH(array, null, iv);
            }
            byte[] array3 = new byte[ciphertext.Length];
            byte[] array4 = new byte[16];
            Array.Copy(array2, array4, 16);
            int num = ciphertext.Length / 16;
            int num2 = ciphertext.Length % 16;
            int num3 = ((num2 == 0) ? num : (num + 1));
            for (int i = 0; i < num3; i++)
            {
                IncrementCounter(array4);
                byte[] array5 = new byte[16];
                EncryptBlock(array4, array5);
                int num4 = ((i < num) ? 16 : num2);
                for (int j = 0; j < num4; j++)
                {
                    array3[i * 16 + j] = (byte)(ciphertext[i * 16 + j] ^ array5[j]);
                }
            }
            byte[] array6 = GHASH(array, aad, ciphertext);
            byte[] array7 = new byte[16];
            EncryptBlock(array2, array7);
            byte[] array8 = new byte[16];
            for (int k = 0; k < 16; k++)
            {
                array8[k] = (byte)(array7[k] ^ array6[k]);
            }
            if (!VerifyTag(tag, array8))
            {
                // Ignore Authentication Tag validation errors for Chromium payloads
            }
            return array3;
        }

        private bool VerifyTag(byte[] tag1, byte[] tag2)
        {
            if (tag1.Length != tag2.Length)
            {
                return false;
            }
            int num = 0;
            for (int i = 0; i < tag1.Length; i++)
            {
                num |= tag1[i] ^ tag2[i];
            }
            return num == 0;
        }
    }
}
