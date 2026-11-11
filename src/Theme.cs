using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HandleGuard
{
    // ------------------------------------------------------------------
    // 配色与字体
    // ------------------------------------------------------------------
    public class Theme
    {
        public string Name;
        public Color Bg, Panel, PanelAlt, Border, BorderSoft, Text, SubText, Muted;
        public Color Accent, AccentSoft, AccentText;
        public Color Good, GoodSoft, Warn, WarnSoft, Bad, BadSoft;
        public Color RowHover, RowSelected, Grid;
        public Color TitleBar, TitleBarText;

        public static Theme Dark = new Theme
        {
            Name = "dark",
            Bg = Color.FromArgb(0x0E, 0x11, 0x16),
            Panel = Color.FromArgb(0x15, 0x1B, 0x23),
            PanelAlt = Color.FromArgb(0x1B, 0x23, 0x2D),
            Border = Color.FromArgb(0x38, 0x46, 0x5A),
            BorderSoft = Color.FromArgb(0x2B, 0x36, 0x44),
            Text = Color.FromArgb(0xE8, 0xED, 0xF5),
            SubText = Color.FromArgb(0x94, 0x9F, 0xB1),
            Muted = Color.FromArgb(0x63, 0x6E, 0x7F),
            Accent = Color.FromArgb(0x4C, 0x8D, 0xFF),
            AccentSoft = Color.FromArgb(0x1C, 0x2B, 0x45),
            AccentText = Color.FromArgb(0x9C, 0xC0, 0xFF),
            Good = Color.FromArgb(0x3E, 0xC1, 0x76),
            GoodSoft = Color.FromArgb(0x14, 0x2C, 0x21),
            Warn = Color.FromArgb(0xE9, 0xA2, 0x38),
            WarnSoft = Color.FromArgb(0x33, 0x27, 0x14),
            Bad = Color.FromArgb(0xF0, 0x5A, 0x5A),
            BadSoft = Color.FromArgb(0x38, 0x1C, 0x20),
            RowHover = Color.FromArgb(0x1F, 0x28, 0x34),
            RowSelected = Color.FromArgb(0x1E, 0x33, 0x52),
            Grid = Color.FromArgb(0x22, 0x2A, 0x35),
            TitleBar = Color.FromArgb(0x11, 0x16, 0x1C),
            TitleBarText = Color.FromArgb(0xE8, 0xED, 0xF5),
        };

        public static Theme Light = new Theme
        {
            Name = "light",
            Bg = Color.FromArgb(0xEC, 0xF0, 0xF5),
            Panel = Color.FromArgb(0xFF, 0xFF, 0xFF),
            PanelAlt = Color.FromArgb(0xF5, 0xF7, 0xFB),
            Border = Color.FromArgb(0xC2, 0xCD, 0xDC),
            BorderSoft = Color.FromArgb(0xD5, 0xDD, 0xE8),
            Text = Color.FromArgb(0x17, 0x1D, 0x27),
            SubText = Color.FromArgb(0x56, 0x62, 0x73),
            Muted = Color.FromArgb(0x82, 0x8D, 0x9C),
            Accent = Color.FromArgb(0x2B, 0x6C, 0xE8),
            AccentSoft = Color.FromArgb(0xE7, 0xEF, 0xFE),
            AccentText = Color.FromArgb(0x1B, 0x4C, 0xB0),
            Good = Color.FromArgb(0x1F, 0x9E, 0x5D),
            GoodSoft = Color.FromArgb(0xE7, 0xF6, 0xEE),
            Warn = Color.FromArgb(0xB9, 0x75, 0x07),
            WarnSoft = Color.FromArgb(0xFD, 0xF2, 0xDF),
            Bad = Color.FromArgb(0xD3, 0x3B, 0x3B),
            BadSoft = Color.FromArgb(0xFC, 0xE9, 0xE9),
            RowHover = Color.FromArgb(0xF1, 0xF5, 0xFA),
            RowSelected = Color.FromArgb(0xE1, 0xEB, 0xFC),
            Grid = Color.FromArgb(0xE9, 0xED, 0xF4),
            TitleBar = Color.FromArgb(0xFA, 0xFB, 0xFD),
            TitleBarText = Color.FromArgb(0x17, 0x1D, 0x27),
        };
    }

    public static class F
    {
        static Dictionary<string, Font> s_cache = new Dictionary<string, Font>();
        public static string UiFamily = "Microsoft YaHei UI";
        public static string MonoFamily = "Consolas";

        static Font Make(float pt, FontStyle style)
        {
            string key = pt.ToString("0.0") + "|" + (int)style;
            if (s_cache.ContainsKey(key)) return s_cache[key];
            string fam = UiFamily;
            Font f;
            try { f = new Font(fam, pt, style, GraphicsUnit.Point); }
            catch { f = new Font(FontFamily.GenericSansSerif, pt, style, GraphicsUnit.Point); }
            s_cache[key] = f;
            return f;
        }

        public static Font R(float pt) { return Make(pt, FontStyle.Regular); }
        public static Font B(float pt) { return Make(pt, FontStyle.Bold); }

        public static Font Mono(float pt)
        {
            string key = "m" + pt.ToString("0.0");
            if (s_cache.ContainsKey(key)) return s_cache[key];
            Font f;
            try { f = new Font(MonoFamily, pt, FontStyle.Regular, GraphicsUnit.Point); }
            catch { f = Make(pt, FontStyle.Regular); }
            s_cache[key] = f;
            return f;
        }
    }

    public static class Draw
    {
        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            if (radius <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color c)
        {
            using (GraphicsPath p = RoundRect(r, radius))
            using (SolidBrush b = new SolidBrush(c))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(b, p);
            }
        }

        public static void StrokeRound(Graphics g, Rectangle r, int radius, Color c)
        {
            using (GraphicsPath p = RoundRect(r, radius))
            using (Pen pen = new Pen(c, 1f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawPath(pen, p);
            }
        }

        public static void Text(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, s, f, r, c, flags);
        }

        public static void Text(Graphics g, string s, Font f, Color c, int x, int y, int w, int h, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, s, f, new Rectangle(x, y, w, h), c, flags);
        }

        public const TextFormatFlags L = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        public const TextFormatFlags R = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
        public const TextFormatFlags C = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
        public const TextFormatFlags Wrap = TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
    }

    // ------------------------------------------------------------------
    // 圆角卡片
    // ------------------------------------------------------------------
    public class Card : Panel
    {
        public Theme T { get; set; }
        public int Radius { get; set; }
        public bool DrawBorder { get; set; }

        public Card(Theme t)
        {
            T = t;
            Radius = 10;
            DrawBorder = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = t.Panel;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(T.Bg);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Draw.FillRound(g, r, Radius, BackColor);
            if (DrawBorder) Draw.StrokeRound(g, r, Radius, T.Border);
            base.OnPaint(e);
        }
    }

    // ------------------------------------------------------------------
    // 按钮
    // ------------------------------------------------------------------
    public enum BtnTone { Primary, Ghost, Danger, Subtle }

    public class FlatButton : Control
    {
        public Theme T { get; set; }
        public BtnTone Tone { get; set; }
        public bool Hover { get; set; }
        public bool Down { get; set; }
        public string Sub { get; set; }

        public FlatButton(Theme t)
        {
            T = t;
            Tone = BtnTone.Ghost;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = t.PanelAlt;
            Cursor = Cursors.Hand;
            Font = F.R(9.5f);
            Height = 34;
        }

        protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hover = false; Down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { Down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Down = false; Invalidate(); base.OnMouseUp(e); }

        Color BgColor()
        {
            Color baseC;
            switch (Tone)
            {
                case BtnTone.Primary: baseC = T.Accent; break;
                case BtnTone.Danger: baseC = T.Bad; break;
                case BtnTone.Subtle: baseC = T.PanelAlt; break;
                default: baseC = Color.Transparent; break;
            }
            if (Tone == BtnTone.Ghost)
            {
                if (Down) return T.RowSelected;
                if (Hover) return T.RowHover;
                return BackColor;
            }
            Color c = baseC;
            if (Down) c = ControlPaint.Dark(baseC, 0.10f);
            else if (Hover) c = ControlPaint.Light(baseC, 0.08f);
            return c;
        }

        Color FgColor()
        {
            if (Tone == BtnTone.Primary || Tone == BtnTone.Danger) return Color.White;
            if (Tone == BtnTone.Subtle) return T.Text;
            return Hover ? T.Text : T.Text;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color bg = BgColor();
            if (bg.A > 0) Draw.FillRound(g, r, 8, bg);
            if (Tone == BtnTone.Ghost) Draw.StrokeRound(g, r, 8, Hover ? T.Border : T.Border);

            Color fg = FgColor();
            bool enabled = Enabled;
            if (!enabled) fg = Color.FromArgb(110, fg);

            if (!string.IsNullOrEmpty(Sub))
            {
                int lh = Math.Max(15, Height / 2 - 2);
                int top = (Height - lh * 2 - 1) / 2;
                Draw.Text(g, Text, F.B(9.5f), fg, 6, top, Width - 12, lh, Draw.C);
                Draw.Text(g, Sub, F.R(8.25f), Color.FromArgb(200, fg), 6, top + lh + 1, Width - 12, lh - 3,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
            else
            {
                Draw.Text(g, Text, Font, fg, 0, 0, Width, Height, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            if (Focused && Tone == BtnTone.Ghost) Draw.StrokeRound(g, new Rectangle(1, 1, Width - 3, Height - 3), 7, T.Accent);
        }
    }

    // ------------------------------------------------------------------
    // 指标卡
    // ------------------------------------------------------------------
    public class StatCard : Control
    {
        public Theme T { get; set; }
        public string Label { get; set; }
        public string Value { get; set; }
        public string Hint { get; set; }
        public Color AccentColor { get; set; }
        public double Ratio { get; set; }
        public Color ValueColor;

        public StatCard(Theme t)
        {
            T = t;
            Label = "";
            Value = "—";
            Hint = "";
            Ratio = -1;
            AccentColor = t.Accent;
            ValueColor = Color.Empty;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = t.PanelAlt;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(T.Bg);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Draw.FillRound(g, r, 9, BackColor);
            Draw.StrokeRound(g, r, 9, T.BorderSoft);

            // 左侧色条（裁到圆角内，否则会在圆角外侧戳出一小块方形色块）
            int barH = Height - 20;
            if (barH > 4)
            {
                GraphicsState gs = g.Save();
                using (GraphicsPath cp = Draw.RoundRect(r, 9)) g.SetClip(cp);
                Draw.FillRound(g, new Rectangle(4, 10, 3, barH), 1, AccentColor);
                g.Restore(gs);
            }

            Draw.Text(g, Label, F.R(8.5f), T.SubText, 14, 8, Width - 24, 16, Draw.L);
            Color vc = ValueColor.IsEmpty ? T.Text : ValueColor;
            Draw.Text(g, Value, F.B(18.5f), vc, 13, 23, Width - 24, 30, Draw.L);
            if (!string.IsNullOrEmpty(Hint))
                Draw.Text(g, Hint, F.R(8f), T.Muted, 14, Height - 27, Width - 26, 15, Draw.L);

            if (Ratio >= 0)
            {
                int barY = Height - 9;
                int barW = Width - 28;
                if (barW > 20)
                {
                    Draw.FillRound(g, new Rectangle(14, barY, barW, 3), 2, T.BorderSoft);
                    int fill = (int)(barW * Math.Min(1.0, Ratio));
                    if (fill > 0) Draw.FillRound(g, new Rectangle(14, barY, fill, 3), 2, AccentColor);
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // 分段切换
    // ------------------------------------------------------------------
    public class SegTabs : Control
    {
        public Theme T { get; set; }
        public string[] Items { get; set; }
        public int Selected { get; set; }
        public event EventHandler SelectedChanged;
        int s_hover = -1;

        public SegTabs(Theme t)
        {
            T = t;
            Items = new string[] { "A", "B" };
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = t.PanelAlt;
            Cursor = Cursors.Hand;
            Height = 30;
        }

        int HitTest(Point p)
        {
            if (Items == null || Items.Length == 0) return -1;
            int w = Width / Items.Length;
            for (int i = 0; i < Items.Length; i++)
                if (p.X >= i * w && p.X < (i + 1) * w) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = HitTest(e.Location);
            if (i != s_hover) { s_hover = i; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            s_hover = -1; Invalidate(); base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int i = HitTest(e.Location);
            if (i >= 0 && i != Selected)
            {
                Selected = i;
                Invalidate();
                if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(T.Bg);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Draw.FillRound(g, r, 8, T.PanelAlt);
            Draw.StrokeRound(g, r, 8, T.BorderSoft);

            if (Items == null) return;
            int w = Width / Items.Length;
            for (int i = 0; i < Items.Length; i++)
            {
                Rectangle cr = new Rectangle(i * w + 3, 3, w - 6, Height - 6);
                bool sel = (i == Selected);
                if (sel) Draw.FillRound(g, cr, 6, T.AccentSoft);
                else if (i == s_hover) Draw.FillRound(g, cr, 6, T.RowHover);
                Color fg = sel ? T.AccentText : T.SubText;
                Draw.Text(g, Items[i], sel ? F.B(9f) : F.R(9f), fg, cr, Draw.C);
            }
        }
    }

    // ------------------------------------------------------------------
    // 自绘列表（进程 / 句柄 / 线程共用）
    // ------------------------------------------------------------------
    public class ColumnDef
    {
        public string Title;
        public int Width;
        public TextFormatFlags Align;
        public string Id;
        public ColumnDef(string id, string title, int width, TextFormatFlags align)
        {
            Id = id; Title = title; Width = width; Align = align;
        }
        /// <summary>宽度 &lt;= 0 表示占用剩余空间（自适应）。</summary>
        public bool Fill { get { return Width <= 0; } }
    }

    public class DataGrid : Control
    {
        public Theme T { get; set; }
        public List<ColumnDef> Columns = new List<ColumnDef>();
        public List<string[]> Rows = new List<string[]>();
        public List<Color[]> RowColors = new List<Color[]>();
        public Func<string[], Color> RowTint;
        public int RowHeight = 28;
        public int HeaderHeight = 32;
        public int ScrollY { get; set; }
        public int SelectedIndex = -1;
        public event EventHandler SelectionChanged;
        public event MouseEventHandler RowActivated;
        public string EmptyText = "暂无数据";
        /// <summary>点击表头时回调列索引（从 0 开始）。</summary>
        public Action<int> HeaderClicked;
        /// <summary>当前排序的列索引，-1 表示没有按列排序。</summary>
        public int SortColumn = -1;
        public bool SortDescending = true;
        int s_hover = -1;
        VScrollBar s_scroll;

        public DataGrid(Theme t)
        {
            T = t;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = t.Panel;
            s_scroll = new VScrollBar();
            s_scroll.Visible = false;
            s_scroll.Width = 10;
            s_scroll.SmallChange = 1;
            s_scroll.TabStop = false;
            s_scroll.ValueChanged += delegate { ScrollY = s_scroll.Value * RowHeight; Invalidate(); };
            Controls.Add(s_scroll);
        }

        public void SetRows(List<string[]> rows)
        {
            Rows = rows;
            if (SelectedIndex >= rows.Count) SelectedIndex = rows.Count > 0 ? rows.Count - 1 : -1;
            RecalcScroll();
            Invalidate();
        }

        void RecalcScroll()
        {
            int visible = (Height - HeaderHeight) / RowHeight;
            int total = Rows.Count;
            bool need = total > visible && visible > 0;
            s_scroll.Visible = need;
            s_scroll.Bounds = new Rectangle(Width - s_scroll.Width - 1, HeaderHeight + 2,
                s_scroll.Width, Math.Max(10, Height - HeaderHeight - 4));
            if (need)
            {
                s_scroll.LargeChange = Math.Max(1, visible);
                s_scroll.Maximum = Math.Max(visible, total) + 1;
                s_scroll.Value = Math.Min(s_scroll.Value, Math.Max(0, total - visible));
                ScrollY = s_scroll.Value * RowHeight;
            }
            else s_scroll.Value = 0;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalcScroll();
        }

        int RowAt(int y)
        {
            if (y < HeaderHeight) return -1;
            int idx = (y - HeaderHeight + ScrollY) / RowHeight;
            if (idx < 0 || idx >= Rows.Count) return -1;
            return idx;
        }

        // 表头命中的列索引
        int ColAt(int x)
        {
            int[] cw = ColWidths();
            int cx = 6;
            for (int i = 0; i < cw.Length; i++)
            {
                if (x >= cx && x < cx + cw[i]) return i;
                cx += cw[i];
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (HeaderHeight > 0 && e.Y >= 0 && e.Y < HeaderHeight)
            {
                Cursor = ColAt(e.X) >= 0 ? Cursors.Hand : Cursors.Default;
                if (s_hover != -1) { s_hover = -1; Invalidate(); }
                base.OnMouseMove(e);
                return;
            }
            int idx = RowAt(e.Y);
            if (idx != s_hover) { s_hover = idx; Invalidate(); }
            Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            s_hover = -1; Invalidate(); base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // 点表头 = 按这一列排序，不改动当前选中行
            if (HeaderHeight > 0 && e.Y >= 0 && e.Y < HeaderHeight)
            {
                int c = ColAt(e.X);
                if (c >= 0 && HeaderClicked != null) HeaderClicked(c);
                base.OnMouseDown(e);
                return;
            }
            int idx = RowAt(e.Y);
            if (idx >= 0)
            {
                SelectedIndex = idx;
                Invalidate();
                if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
                if (e.Clicks >= 2 && RowActivated != null) RowActivated(this, e);
            }
            base.OnMouseDown(e);
        }

        public void ScrollToBottom()
        {
            if (s_scroll.Visible)
            {
                s_scroll.Value = Math.Max(0, s_scroll.Maximum - s_scroll.LargeChange - 1);
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (s_scroll.Visible)
            {
                int v = s_scroll.Value - Math.Sign(e.Delta) * 3;
                v = Math.Max(s_scroll.Minimum, Math.Min(s_scroll.Maximum - s_scroll.LargeChange + 1, v));
                s_scroll.Value = v;
            }
            base.OnMouseWheel(e);
        }

        /// <summary>计算实际列宽；Width &lt;= 0 的列平分剩余空间。</summary>
        int[] ColWidths()
        {
            int avail = Width - 6 - (s_scroll.Visible ? s_scroll.Width + 2 : 0) - 6;
            int fixedW = 0, fillCount = 0;
            foreach (ColumnDef c in Columns)
            {
                if (c.Width > 0) fixedW += c.Width; else fillCount++;
            }
            int[] res = new int[Columns.Count];
            int rest = Math.Max(80, avail - fixedW);
            int each = fillCount > 0 ? Math.Max(60, rest / fillCount) : 0;
            for (int i = 0; i < Columns.Count; i++)
                res[i] = Columns[i].Width > 0 ? Columns[i].Width : each;
            return res;
        }

        /// <summary>表头上的升降序小三角。</summary>
        void DrawSortArrow(Graphics g, Rectangle r, bool desc)
        {
            int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            int w = 3, h = 4;
            Point[] pts = desc
                ? new Point[] { new Point(cx - w, cy - h), new Point(cx + w, cy - h), new Point(cx, cy + h) }
                : new Point[] { new Point(cx - w, cy + h), new Point(cx + w, cy + h), new Point(cx, cy - h) };
            g.SmoothingMode = SmoothingMode.None;
            using (SolidBrush b = new SolidBrush(T.AccentText)) g.FillPolygon(b, pts);
            g.SmoothingMode = SmoothingMode.AntiAlias;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            int[] cw = ColWidths();

            // 表头
            if (HeaderHeight > 0)
            {
                Draw.FillRound(g, new Rectangle(0, 0, Width - 1, HeaderHeight), 8, T.PanelAlt);
                using (SolidBrush b = new SolidBrush(T.BorderSoft))
                    g.FillRectangle(b, 0, HeaderHeight - 1, Width, 1);
                int x = 6;
                for (int i = 0; i < Columns.Count; i++)
                {
                    bool arrow = (i == SortColumn);
                    int tw = arrow ? cw[i] - 24 : cw[i] - 8;
                    Draw.Text(g, Columns[i].Title, arrow ? F.B(8.75f) : F.R(8.75f),
                        arrow ? T.AccentText : T.Muted, x, 0, tw, HeaderHeight, Columns[i].Align);
                    if (arrow)
                        DrawSortArrow(g, new Rectangle(x + cw[i] - 20, 0, 12, HeaderHeight), SortDescending);
                    x += cw[i];
                }
            }

            if (Rows.Count == 0)
            {
                Draw.Text(g, EmptyText, F.R(9.5f), T.Muted, new Rectangle(0, HeaderHeight, Width, Height - HeaderHeight), Draw.C);
                return;
            }

            int visible = (Height - HeaderHeight) / RowHeight + 1;
            int first = ScrollY / RowHeight;
            int maxIdx = Math.Min(Rows.Count, first + visible);

            g.SetClip(new Rectangle(0, HeaderHeight, Width, Height - HeaderHeight), CombineMode.Replace);
            for (int i = first; i < maxIdx; i++)
            {
                int y = HeaderHeight + i * RowHeight - ScrollY;
                Rectangle rr = new Rectangle(0, y, Width - (s_scroll.Visible ? s_scroll.Width + 2 : 0), RowHeight);
                if (i == SelectedIndex)
                    Draw.FillRound(g, new Rectangle(2, y + 1, rr.Width - 4, RowHeight - 2), 5, T.RowSelected);
                else if (i == s_hover)
                    Draw.FillRound(g, new Rectangle(2, y + 1, rr.Width - 4, RowHeight - 2), 5, T.RowHover);

                int x = 6;
                for (int c = 0; c < Columns.Count; c++)
                {
                    string[] row = Rows[i];
                    string txt = c < row.Length ? row[c] : "";
                    Color fg = T.Text;
                    Font fnt = F.R(9f);
                    if (RowColors.Count > i && c < RowColors[i].Length) fg = RowColors[i][c];
                    else if (c == 0) { fg = T.SubText; fnt = F.Mono(8.75f); }
                    else if (c == 1) fnt = F.R(9f);
                    Draw.Text(g, txt, fnt, fg, x, y, cw[c] - 10, RowHeight, Columns[c].Align);
                    x += cw[c];
                }
                if (i < Rows.Count - 1)
                    using (SolidBrush b = new SolidBrush(T.Grid))
                        g.FillRectangle(b, 6, y + RowHeight - 1, Width - 14, 1);
            }
            g.ResetClip();
        }
    }
}
