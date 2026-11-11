using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace HandleGuard
{
    // 应用标识：exe 图标（src\app.ico）、标题栏左上角 logo 全部由这一份几何绘制，
    // 保证任务栏、资源管理器、界面里看到的是同一个图形。设计基准 256×256。
    public static class Brand
    {
        public static readonly Color BlueTop = Color.FromArgb(0x4C, 0x8D, 0xFF);
        public static readonly Color BlueBottom = Color.FromArgb(0x21, 0x58, 0xE0);

        static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 0) { p.AddRectangle(r); return p; }
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // 在矩形内绘制标识（居中、等比）
        public static void Draw(Graphics g, Rectangle r)
        {
            int s = Math.Min(r.Width, r.Height);
            if (s < 8) s = Math.Min(r.Width, r.Height);
            float k = s / 256f;
            int x0 = r.X + (r.Width - s) / 2;
            int y0 = r.Y + (r.Height - s) / 2;

            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool tiny = s < 34;

            Rectangle full = new Rectangle(x0, y0, s - 1, s - 1);
            int radius = (int)Math.Round(56 * k);
            using (GraphicsPath path = RoundRect(full, radius))
            {
                if (g.PixelOffsetMode != PixelOffsetMode.HighQuality)
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (LinearGradientBrush br = new LinearGradientBrush(
                    new Rectangle(x0, y0, s, s + 1), BlueTop, BlueBottom, LinearGradientMode.Vertical))
                {
                    g.FillPath(br, path);
                }
            }

            if (tiny)
            {
                // 16/24px 专用比例：H 收窄让出右侧 1px 给弧线，否则弧会和 H 粘连成一块白斑
                int bw = Math.Max(2, (int)Math.Round(s * 0.115));
                int lx = (int)Math.Round(s * 0.19);
                int rx = lx + (int)Math.Round(s * 0.28);
                int ty = (int)Math.Round(s * 0.24);
                int by = s - ty;
                int my = (int)Math.Round(s * 0.43);
                int mh = Math.Max(1, (int)Math.Round(s * 0.14));
                int hRight = rx + bw;
                using (SolidBrush white = new SolidBrush(Color.White))
                {
                    g.FillRectangle(white, x0 + lx, y0 + ty, bw, by - ty);
                    g.FillRectangle(white, x0 + rx, y0 + ty, bw, by - ty);
                    g.FillRectangle(white, x0 + lx, y0 + my, hRight - lx, mh);
                }
                // 弧：圆心与半径都按比例收，让 H 与弧的包围盒仍然居中（左留白 ≈ 右留白）
                float acx = x0 + s * 0.66f;
                float acy = y0 + s * 0.5f;
                float ar = s * 0.17f;
                float pw = Math.Max(1f, s * 0.045f);
                using (Pen pen = new Pen(Color.White, pw))
                    g.DrawArc(pen, acx - ar, acy - ar, ar * 2, ar * 2, -52f, 104f);
                g.SmoothingMode = old;
                return;
            }

            // 白色 H：两竖 + 一横。H(40~164) + 弧(174~216) 的包围盒正好落在 256 画布中轴
            int barW = (int)Math.Round(34 * k);
            if (barW < 3) barW = 3;
            int top = y0 + (int)Math.Round(58 * k);
            int bottom = y0 + (int)Math.Round(198 * k);
            int leftX = x0 + (int)Math.Round(40 * k);
            int rightX = x0 + (int)Math.Round(130 * k);
            int midY = y0 + (int)Math.Round(110 * k);
            int midH = (int)Math.Round(36 * k);
            using (SolidBrush white = new SolidBrush(Color.White))
            {
                g.FillRectangle(white, leftX, top, barW, bottom - top);
                g.FillRectangle(white, rightX, top, barW, bottom - top);
                g.FillRectangle(white, leftX, midY, rightX + barW - leftX, midH);
            }

            // 右侧细弧：像个环形仪表盘绕过 H
            float arcR = 100f * k;
            float cxArc = x0 + 113f * k;
            float cyArc = y0 + 128f * k;
            float penW = Math.Max(1f, 6.5f * k);
            using (Pen pen = new Pen(Color.White, penW))
            {
                g.DrawArc(pen, cxArc - arcR, cyArc - arcR, arcR * 2, arcR * 2, -52f, 104f);
            }

            g.SmoothingMode = old;
        }
    }
}
