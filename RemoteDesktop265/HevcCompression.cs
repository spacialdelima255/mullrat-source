using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;

namespace mullvad.Module.RemoteDesktopH265
{
    internal static class HevcCompression
    {
        public static byte[] Encode(Bitmap bitmap, int quality)
        {
            quality = Math.Max(1, Math.Min(100, quality));
            int width  = bitmap.Width;
            int height = bitmap.Height;

            var srcFmt     = bitmap.PixelFormat;
            var bd         = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, srcFmt);
            int bpp        = Image.GetPixelFormatSize(srcFmt) / 8;
            int chromaW    = (width  + 1) >> 1;
            int chromaH    = (height + 1) >> 1;
            int yLen       = width * height;
            int uvLen      = chromaW * chromaH;
            var planes     = new byte[yLen + uvLen * 2];

            unsafe
            {
                byte* src = (byte*)bd.Scan0;
                fixed (byte* output = planes)
                {
                    byte* yPlane  = output;
                    byte* cbPlane = output + yLen;
                    byte* crPlane = output + yLen + uvLen;

                    for (int y = 0; y < height; y++)
                    {
                        byte* row = src + y * bd.Stride;
                        int xFp   = 0;
                        int xStep = (int)(((long)width << 16) / width);
                        for (int x = 0; x < width; x++)
                        {
                            byte* px = row + (xFp >> 16) * bpp;
                            int b = px[0], g = px[1], r = px[2];
                            yPlane[y * width + x] = (byte)Math.Max(0, Math.Min(255,
                                ((66 * r + 129 * g + 25 * b + 128) >> 8) + 16));
                            if ((x & 1) == 0 && (y & 1) == 0)
                            {
                                int uv = (y >> 1) * chromaW + (x >> 1);
                                cbPlane[uv] = (byte)Math.Max(0, Math.Min(255,
                                    ((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128));
                                crPlane[uv] = (byte)Math.Max(0, Math.Min(255,
                                    ((112 * r - 94 * g - 18 * b + 128) >> 8) + 128));
                            }
                            xFp += xStep;
                        }
                    }
                }
            }
            bitmap.UnlockBits(bd);

            using var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(width),  0, 4);
            ms.Write(BitConverter.GetBytes(height), 0, 4);
            ms.WriteByte(0);
            ms.WriteByte(0);
            using (var ds = new DeflateStream(ms, CompressionLevel.Fastest, true))
                ds.Write(planes, 0, planes.Length);
            return ms.ToArray();
        }
    }
}
