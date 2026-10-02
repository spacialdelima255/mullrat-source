using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace mullvad.Video
{
    internal static class HevcCompression
    {
        // Frame format: 4B width + 4B height + 2B padding + Deflate(YUV420 planes)
        // Codec name returned by client: "hevc"

        public static Bitmap Decode(byte[] data) => Decode(data, 0, data.Length);

        public static Bitmap Decode(byte[] data, int offset, int count)
        {
            using var ms = new MemoryStream(data, offset, count, false);

            var hdr = new byte[10];
            ms.Read(hdr, 0, 10);
            int width  = BitConverter.ToInt32(hdr, 0);
            int height = BitConverter.ToInt32(hdr, 4);

            int chromaW = (width  + 1) >> 1;
            int chromaH = (height + 1) >> 1;
            int yLen    = width * height;
            int uvLen   = chromaW * chromaH;
            var planes  = new byte[yLen + uvLen * 2];

            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            {
                int written = 0;
                while (written < planes.Length)
                {
                    int read = ds.Read(planes, written, planes.Length - written);
                    if (read == 0) break;
                    written += read;
                }
            }

            var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            var bd  = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            int stride = bd.Stride;
            var pixels = new byte[height * stride];

            for (int y = 0; y < height; y++)
            {
                int yOff    = y * width;
                int uvRow   = (y >> 1) * chromaW;
                int rowBase = y * stride;
                for (int x = 0; x < width; x++)
                {
                    int yy  = planes[yOff + x] - 16;
                    int uv  = uvRow + (x >> 1);
                    int cb  = planes[yLen + uv] - 128;
                    int cr  = planes[yLen + uvLen + uv] - 128;
                    int px  = rowBase + x * 3;
                    pixels[px + 0] = Clamp((298 * yy + 516 * cb            + 128) >> 8);
                    pixels[px + 1] = Clamp((298 * yy - 100 * cb - 208 * cr + 128) >> 8);
                    pixels[px + 2] = Clamp((298 * yy + 409 * cr            + 128) >> 8);
                }
            }

            Marshal.Copy(pixels, 0, bd.Scan0, pixels.Length);
            bmp.UnlockBits(bd);
            return bmp;
        }

        private static byte Clamp(int v) => (byte)Math.Max(0, Math.Min(255, v));
    }
}
