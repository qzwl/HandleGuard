// 图标生成器：几何全部来自 Brand.cs，保证 exe 图标与界面 logo 是同一个图形
// 编译：csc /target:exe /out:build\icon-shot.exe src\Brand.cs src\IconShot.cs /reference:System.Drawing.dll
// 用法：icon-shot.exe <app.ico> [预览png]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace HandleGuard
{
    public static class IconShot
    {
        static Bitmap Render(int size)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                Brand.Draw(g, new Rectangle(0, 0, size, size));
            }
            return bmp;
        }

        // 32bpp BGRA DIB（含 AND 掩码），兼容所有 Windows 版本的图标加载路径
        static byte[] ToDib(Bitmap bmp)
        {
            int s = bmp.Width;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                bw.Write(40);                 // biSize
                bw.Write(s);                  // biWidth
                bw.Write(s * 2);              // biHeight（含 AND 掩码，写两倍）
                bw.Write((short)1);           // biPlanes
                bw.Write((short)32);          // biBitCount
                bw.Write(0);                  // biCompression = BI_RGB
                bw.Write(s * s * 4);          // biSizeImage
                bw.Write(0); bw.Write(0);     // 分辨率
                bw.Write(0); bw.Write(0);     // 调色板
                for (int y = s - 1; y >= 0; y--)
                {
                    for (int x = 0; x < s; x++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        bw.Write((byte)c.B);
                        bw.Write((byte)c.G);
                        bw.Write((byte)c.R);
                        bw.Write((byte)c.A);
                    }
                }
                int rowBytes = (s + 7) / 8;
                int pad = (4 - (rowBytes % 4)) % 4;
                byte[] mask = new byte[(rowBytes + pad) * s];
                bw.Write(mask);
                bw.Flush();
                return ms.ToArray();
            }
        }

        static byte[] ToPng(Bitmap bmp)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        // 多尺寸预览：放大 4 倍拼成一张，用最近邻保持像素原样，便于肉眼检查小图标是否糊
        static void MultiPreview(string path, int[] sizes)
        {
            const int scale = 4, gap = 16;
            int w = gap, h = 0;
            for (int i = 0; i < sizes.Length; i++)
            {
                w += sizes[i] * scale + gap;
                if (sizes[i] * scale > h) h = sizes[i] * scale;
            }
            using (Bitmap canvas = new Bitmap(w, h + gap * 2))
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.FromArgb(0xF2, 0xF5, 0xFA));
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                int x = gap;
                for (int i = 0; i < sizes.Length; i++)
                {
                    Bitmap bmp = Render(sizes[i]);
                    int d = sizes[i] * scale;
                    g.DrawImage(bmp, new Rectangle(x, gap + (h - d) / 2, d, d));
                    bmp.Dispose();
                    x += d + gap;
                }
                canvas.Save(path, ImageFormat.Png);
            }
        }

        public static void Main(string[] args)
        {
            string icoPath = args.Length > 0 ? args[0] : "app.ico";
            string pngPath = args.Length > 1 ? args[1] : "";
            string previewPath = args.Length > 2 ? args[2] : "";

            int[] sizes = new int[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            List<int> sz = new List<int>();
            List<byte[]> data = new List<byte[]>();

            foreach (int s in sizes)
            {
                Bitmap bmp = Render(s);
                byte[] blob = s <= 64 ? ToDib(bmp) : ToPng(bmp);
                sz.Add(s);
                data.Add(blob);
                if (s == 256 && pngPath.Length > 0) bmp.Save(pngPath, ImageFormat.Png);
                bmp.Dispose();
            }

            int count = sz.Count;
            int offset = 6 + 16 * count;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                bw.Write((short)0);
                bw.Write((short)1);
                bw.Write((short)count);
                for (int i = 0; i < count; i++)
                {
                    int s = sz[i];
                    byte w = (byte)(s >= 256 ? 0 : s);
                    bw.Write(w);
                    bw.Write(w);
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((short)1);
                    bw.Write((short)32);
                    bw.Write(data[i].Length);
                    bw.Write(offset);
                    offset += data[i].Length;
                }
                for (int i = 0; i < count; i++) bw.Write(data[i]);
                bw.Flush();
                File.WriteAllBytes(icoPath, ms.ToArray());
            }
            Console.WriteLine("图标已生成：" + Path.GetFullPath(icoPath) + "（" + count + " 个尺寸）");

            if (previewPath.Length > 0)
                MultiPreview(previewPath, new int[] { 16, 24, 32, 48, 64 });
        }
    }
}
