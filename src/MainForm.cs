using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace HandleGuard
{
    public class MainForm : Form
    {
        const int WM_NCHITTEST = 0x84;
        const int WM_NCLBUTTONDBLCLK = 0xA3;
        const int HTCAPTION = 2;
        const int HTCLIENT = 1;

        Theme T = Theme.Dark;
        float S = 1f;
        bool s_sampling;
        bool s_autoRefresh = true;
        bool s_loadingDetail;
        bool s_brandHover;
        Rectangle s_restoreBounds;
        int[] s_tempos = new int[] { 1000, 5000, 15000, 0 };
        string[] s_tempoNames = new string[] { "刷新 1 秒", "刷新 5 秒", "刷新 15 秒", "手动刷新" };
        int s_tempo;
        Dictionary<int, int[]> s_baseline = new Dictionary<int, int[]>();
        SystemSnapshot s_sys = new SystemSnapshot();
        List<ProcSample> s_samples = new List<ProcSample>();
        List<ProcSample> s_filtered = new List<ProcSample>();
        int s_sortMode = 1;
        int s_colSort = -1;
        bool s_colDesc = true;
        int s_selfPid = -1;
        int s_selPid = -1;
        int s_detailPid = -1;
        int s_handlesPid = -1;
        int s_threadsPid = -1;
        int s_profilePid = -1;
        int s_round;
        List<HandleEntry> s_handles = new List<HandleEntry>();
        List<ThreadEntry> s_threads = new List<ThreadEntry>();
        ProcProfile s_profile;
        string s_status = "";
        string s_metric = "";
        int s_statusScroll;
        int s_statusMax;
        System.Windows.Forms.Timer s_scrollTimer;
        bool s_isAdmin;
        bool s_uiReady;

        // 控件
        StatCard stHandles, stThreads, stMem, stCpu;
        Card cardMem, cardProc;
        FlatButton btnRelease, btnCache, btnStandby, btnKill, btnRestart, btnExport;
        FlatButton btnTempo, btnNow, btnDetailAction, btnDetailReload;
        Card searchBox;
        TextBox txtSearch;
        SegTabs segSort, segDetail;
        DataGrid gridProcs, gridDetail;
        System.Windows.Forms.Timer s_timer;

        Rectangle rcClose, rcMax, rcMin, rcTheme;

        public MainForm()
        {
            InitForm();
            BuildUi();
            LayoutUi();
            s_timer = new System.Windows.Forms.Timer();
            s_timer.Interval = s_tempos[0];
            s_timer.Tick += delegate { if (s_tempos[s_tempo] > 0) StartSample(); };
            s_timer.Start();
            StartSample();
        }

        int P(int v) { return (int)Math.Round(v * S); }

        void InitForm()
        {
            Text = "HandleGuard 句柄与线程治理台";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            KeyPreview = true;
            Font = F.R(9.5f);
            MinimumSize = new Size(1000, 660);
            s_isAdmin = IsAdministrator();
            try
            {
                using (Process pr = Process.GetCurrentProcess()) { s_selfPid = pr.Id; }
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HandleGuard");
                Directory.CreateDirectory(dir);
                s_themeCacheFile = Path.Combine(dir, "theme.txt");
                if (File.Exists(s_themeCacheFile) && File.ReadAllText(s_themeCacheFile).Trim() == "light")
                    T = Theme.Light;
            }
            catch { }
            // 必须放在主题确定之后：否则上次用浅色主题时，窗体底色会先按深色刷一遍，
            // 卡片四角透出的就是深色（看起来像黑块），切一次主题才被纠正。
            BackColor = T.Bg;
            // 任务栏 / Alt+Tab 用 exe 内嵌的图标，不设的话会是 WinForms 默认图标
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
        }
        string s_themeCacheFile = "";

        static bool IsAdministrator()
        {
            try
            {
                System.Security.Principal.WindowsIdentity id = System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal p = new System.Security.Principal.WindowsPrincipal(id);
                return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        protected override void OnLoad(EventArgs e)
        {
            S = DeviceDpi / 96f;
            Size = new Size(P(1320), P(840));
            LayoutUi();
            try { SendMessage(txtSearch.Handle, 0x1501, (IntPtr)1, "筛选进程名 / PID"); }
            catch { }
            base.OnLoad(e);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        // ------------------------------------------------------------------
        // 构建控件
        // ------------------------------------------------------------------
        void BuildUi()
        {
            stHandles = MkStat("系统句柄总数", "—", "所有进程持有的内核句柄", T.Accent);
            stThreads = MkStat("系统线程总数", "—", "线程暴增通常是卡顿根因", Color.FromArgb(0x8B, 0x7C, 0xFF));
            stMem = MkStat("物理内存占用", "—", "已用 / 总量", Color.FromArgb(0xE9, 0xA2, 0x38));
            stCpu = MkStat("CPU 总占用", "—", "全核平均利用率", Color.FromArgb(0x3E, 0xC1, 0x76));

            cardMem = new Card(T);
            cardMem.BackColor = T.Panel;
            cardMem.Paint += PaintCardMem;
            Controls.Add(cardMem);

            btnRelease = MkBtn("一键释放内存与缓存", BtnTone.Primary);
            btnRelease.Sub = "修剪工作集 · 清文件缓存 · 清待机列表";
            btnRelease.Height = P(44);
            btnRelease.Click += delegate { DoRelease(); };
            cardMem.Controls.Add(btnRelease);

            btnCache = MkBtn("仅清空文件缓存", BtnTone.Subtle);
            btnCache.Click += delegate { DoCache(); };
            cardMem.Controls.Add(btnCache);

            btnStandby = MkBtn("仅清空待机内存列表", BtnTone.Subtle);
            btnStandby.Click += delegate { DoStandby(); };
            cardMem.Controls.Add(btnStandby);

            cardProc = new Card(T);
            cardProc.BackColor = T.Panel;
            cardProc.Paint += PaintCardProc;
            Controls.Add(cardProc);

            btnKill = MkBtn("结束选中进程", BtnTone.Danger);
            btnKill.Click += delegate { DoKill(); };
            cardProc.Controls.Add(btnKill);

            btnRestart = MkBtn("重启选中进程", BtnTone.Subtle);
            btnRestart.Click += delegate { DoRestart(); };
            cardProc.Controls.Add(btnRestart);

            btnExport = MkBtn("导出诊断报告", BtnTone.Ghost);
            btnExport.Click += delegate { DoExport(); };
            cardProc.Controls.Add(btnExport);

            searchBox = new Card(T);
            searchBox.BackColor = T.PanelAlt;
            searchBox.Radius = 8;
            searchBox.Paint += PaintSearch;
            Controls.Add(searchBox);
            txtSearch = new TextBox();
            txtSearch.BorderStyle = BorderStyle.None;
            txtSearch.BackColor = T.PanelAlt;
            txtSearch.ForeColor = T.Text;
            txtSearch.Font = F.R(9.5f);
            txtSearch.TextChanged += delegate { ApplyFilter(); };
            searchBox.Controls.Add(txtSearch);

            segSort = new SegTabs(T);
            segSort.Items = new string[] { "句柄总量", "句柄增长", "线程增长", "CPU", "内存" };
            segSort.Selected = 1;
            segSort.SelectedChanged += delegate
            {
                s_sortMode = segSort.Selected;
                // 用预设排序时清掉表头排序列，避免两套排序互相打架
                s_colSort = -1;
                gridProcs.SortColumn = -1;
                ApplyFilter();
                gridProcs.Invalidate();
            };
            Controls.Add(segSort);

            btnTempo = MkBtn(s_tempoNames[0], BtnTone.Subtle);
            btnTempo.Click += delegate { CycleTempo(); };
            Controls.Add(btnTempo);

            btnNow = MkBtn("立即刷新", BtnTone.Ghost);
            btnNow.Click += delegate { SetStatus("正在立即刷新…"); StartSample(); };
            Controls.Add(btnNow);

            gridProcs = new DataGrid(T);
            gridProcs.Columns.Add(new ColumnDef("pid", "PID", P(62), Draw.L));
            gridProcs.Columns.Add(new ColumnDef("name", "进程", P(200), Draw.L));
            gridProcs.Columns.Add(new ColumnDef("handles", "句柄", P(74), Draw.R));
            gridProcs.Columns.Add(new ColumnDef("threads", "线程", P(66), Draw.R));
            gridProcs.Columns.Add(new ColumnDef("ws", "工作集", P(88), Draw.R));
            gridProcs.Columns.Add(new ColumnDef("cpu", "CPU", P(70), Draw.R));
            gridProcs.Columns.Add(new ColumnDef("dh", "Δ句柄", P(80), Draw.R));
            gridProcs.Columns.Add(new ColumnDef("dt", "Δ线程", P(80), Draw.R));
            gridProcs.SelectionChanged += delegate { OnProcSelected(); };
            gridProcs.HeaderClicked += delegate (int c) { OnHeaderSort(c); };
            Controls.Add(gridProcs);

            segDetail = new SegTabs(T);
            segDetail.Items = new string[] { "进程档案", "文件与网络", "句柄明细", "线程明细" };
            segDetail.SelectedChanged += delegate { ShowDetailPage(); };
            Controls.Add(segDetail);

            btnDetailReload = MkBtn("重新扫描", BtnTone.Ghost);
            btnDetailReload.Click += delegate
            {
                if (s_selPid <= 0) { SetStatus("请先在进程列表里选中一个进程"); return; }
                LoadDetail(s_selPid, true);
            };
            Controls.Add(btnDetailReload);

            btnDetailAction = MkBtn("关闭选中句柄", BtnTone.Danger);
            btnDetailAction.Click += delegate { DoDetailAction(); };
            Controls.Add(btnDetailAction);

            gridDetail = new DataGrid(T);
            Controls.Add(gridDetail);

            Controls.Add(stHandles);
            Controls.Add(stThreads);
            Controls.Add(stMem);
            Controls.Add(stCpu);
            ShowDetailPage();
            SyncButtonBackgrounds(this, T);
            s_uiReady = true;
        }

        // 让扁平按钮使用父容器底色（避免透明背景在离屏/局部重绘时出现色块）
        static void SyncButtonBackgrounds(Control root, Theme t)
        {
            foreach (Control c in root.Controls)
            {
                if (c is FlatButton)
                {
                    FlatButton b = (FlatButton)c;
                    // 不读 Parent.BackColor：窗体首次绘制前父底色可能还没同步，会得到黑块
                    b.BackColor = (b.Parent is Card) ? t.Panel : t.Bg;
                }
                SyncButtonBackgrounds(c, t);
            }
        }

        StatCard MkStat(string label, string val, string hint, Color accent)
        {
            StatCard c = new StatCard(T);
            c.Label = label;
            c.Value = val;
            c.Hint = hint;
            c.AccentColor = accent;
            return c;
        }

        FlatButton MkBtn(string text, BtnTone tone)
        {
            FlatButton b = new FlatButton(T);
            b.Text = text;
            b.Tone = tone;
            b.Height = P(34);
            b.Font = F.R(9.25f);
            return b;
        }

        // ------------------------------------------------------------------
        // 布局
        // ------------------------------------------------------------------
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Maximized)
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
            if (s_uiReady)
            {
                LayoutUi();
                Invalidate();
            }
        }

        void LayoutUi()
        {
            if (S <= 0) S = 1f;
            int W = ClientSize.Width, H = ClientSize.Height;
            if (W < 200 || H < 200) return;

            int pad = P(20);
            int top = P(72);
            int bottom = H - P(44);
            int leftW = P(320);
            int gap = P(12);
            int cardW = (leftW - gap) / 2;
            int cardH = P(86);

            stHandles.Bounds = new Rectangle(pad, top, cardW, cardH);
            stThreads.Bounds = new Rectangle(pad + cardW + gap, top, cardW, cardH);
            stMem.Bounds = new Rectangle(pad, top + cardH + gap, cardW, cardH);
            stCpu.Bounds = new Rectangle(pad + cardW + gap, top + cardH + gap, cardW, cardH);

            int y = top + cardH * 2 + gap + P(10);
            cardMem.Bounds = new Rectangle(pad, y, leftW, P(202));
            int by = P(56);
            btnRelease.Bounds = new Rectangle(P(14), by, leftW - P(28), P(44));
            btnCache.Bounds = new Rectangle(P(14), by + P(52), leftW - P(28), P(36));
            btnStandby.Bounds = new Rectangle(P(14), by + P(52) + P(44), leftW - P(28), P(36));

            int py = y + P(202) + P(10);
            int procH = Math.Max(P(150), bottom - py);
            cardProc.Bounds = new Rectangle(pad, py, leftW, procH);
            btnKill.Bounds = new Rectangle(P(14), P(56), leftW - P(28), P(40));
            btnRestart.Bounds = new Rectangle(P(14), P(56) + P(48), leftW - P(28), P(36));
            btnExport.Bounds = new Rectangle(P(14), P(56) + P(48) + P(44), leftW - P(28), P(36));

            int rx = pad + leftW + P(16);
            int rw = W - rx - pad;
            if (rw < P(300)) return;

            searchBox.Bounds = new Rectangle(rx, top, P(250), P(34));
            txtSearch.Bounds = new Rectangle(P(32), P(7), P(250) - P(42), P(20));

            btnNow.Bounds = new Rectangle(rx + rw - P(88), top, P(88), P(34));
            btnTempo.Bounds = new Rectangle(btnNow.Left - P(8) - P(112), top, P(112), P(34));
            int segW = Math.Max(P(240), btnTempo.Left - rx - P(262) - P(10));
            segSort.Bounds = new Rectangle(rx + P(262), top + P(2), segW, P(30));

            int procsTop = top + P(46);
            int procsH = (int)((bottom - procsTop) * 0.52);
            gridProcs.Bounds = new Rectangle(rx, procsTop, rw, Math.Max(P(140), procsH));

            int dy = gridProcs.Bottom + P(12);
            segDetail.Bounds = new Rectangle(rx, dy, P(360), P(32));
            btnDetailAction.Bounds = new Rectangle(rx + rw - P(140), dy, P(140), P(32));
            btnDetailReload.Bounds = new Rectangle(rx + rw - P(140) - P(96) - P(8), dy, P(96), P(32));
            gridDetail.Bounds = new Rectangle(rx, dy + P(42), rw, Math.Max(P(90), bottom - dy - P(42)));
        }

        // ------------------------------------------------------------------
        // 自绘：标题栏 / 状态栏 / 卡片标题 / 搜索框
        // ------------------------------------------------------------------
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(T.Bg);

            int W = ClientSize.Width;
            int hh = P(56);
            using (SolidBrush b = new SolidBrush(T.TitleBar))
                g.FillRectangle(b, 0, 0, W, hh);
            using (SolidBrush b = new SolidBrush(T.BorderSoft))
                g.FillRectangle(b, 0, hh - 1, W, 1);

            // logo：与 exe 图标同一份几何（Brand.cs），任务栏和界面里是同一个图形
            // 整块名称区可点击（弹出「关于」），悬停时给一个圆角底
            if (s_brandHover) Draw.FillRound(g, BrandRect(), P(6), T.RowHover);
            Rectangle logo = new Rectangle(P(20), P(12), P(32), P(32));
            Brand.Draw(g, logo);

            Draw.Text(g, "HandleGuard", F.B(12.5f), T.Text, P(58), P(11), P(160), P(20), Draw.L);
            Draw.Text(g, "句柄 / 线程治理台", F.R(8.75f), T.Muted, P(58), P(30), P(200), P(16), Draw.L);

            // 右上角按钮
            int bw = P(34), bh = P(28), by = P(14);
            rcClose = new Rectangle(W - P(16) - bw, by, bw, bh);
            rcMax = new Rectangle(rcClose.Left - P(8) - bw, by, bw, bh);
            rcMin = new Rectangle(rcMax.Left - P(8) - bw, by, bw, bh);
            rcTheme = new Rectangle(rcMin.Left - P(14) - bw, by, bw, bh);

            PaintThemeBtn(g, rcTheme);
            PaintCaptionBtn(g, rcMin, "—", T.SubText);
            PaintMaxBtn(g, rcMax);
            PaintCaptionBtn(g, rcClose, "✕", T.SubText);

            // 状态栏
            int sy = ClientSize.Height - P(36);
            using (SolidBrush b = new SolidBrush(T.TitleBar))
                g.FillRectangle(b, 0, sy, W, P(36));
            using (SolidBrush b = new SolidBrush(T.BorderSoft))
                g.FillRectangle(b, 0, sy, W, 1);

            int dotX = P(22), dotY = sy + P(16);
            using (SolidBrush b = new SolidBrush(s_isAdmin ? T.Good : T.Warn))
                g.FillEllipse(b, dotX - P(3), dotY - P(3), P(6), P(6));
            Draw.Text(g, s_isAdmin ? "管理员模式" : "普通模式 · 部分系统进程不可见",
                F.R(8.5f), s_isAdmin ? T.SubText : T.Warn, P(34), sy + P(8), P(210), P(20), Draw.L);
            Draw.Text(g, s_metric, F.R(8.5f), T.Muted, P(248), sy + P(8), P(330), P(20), Draw.L);

            // 右侧详情消息：放不下就滚动（跑马灯），不再被右边界硬截断
            int sx = W - P(20) - P(620), sw = P(620);
            int textW = TextRenderer.MeasureText(s_status, F.R(8.5f)).Width;
            if (textW > sw && s_statusMax > 0)
            {
                g.SetClip(new Rectangle(sx - P(2), sy, sw + P(4), P(36)));
                Draw.Text(g, s_status, F.R(8.5f), T.SubText, sx - s_statusScroll, sy + P(8),
                    textW + P(8), P(20), Draw.L);
                g.ResetClip();
            }
            else
            {
                Draw.Text(g, s_status, F.R(8.5f), T.SubText, sx, sy + P(8), sw, P(20), Draw.R);
            }
        }

        void PaintThemeBtn(Graphics g, Rectangle r)
        {
            Point mp = PointToClient(Cursor.Position);
            bool hover = r.Contains(mp) && ClientRectangle.Contains(mp);
            if (hover) Draw.FillRound(g, r, P(6), T.RowHover);

            int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, rad = P(7);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush b = new SolidBrush(hover ? T.Text : T.SubText))
                g.FillEllipse(b, cx - rad, cy - rad, rad * 2, rad * 2);
            using (SolidBrush b = new SolidBrush(T.TitleBar))
            {
                g.SetClip(new Rectangle(cx, cy - rad - 1, rad + 1, rad * 2 + 2), CombineMode.Replace);
                g.FillEllipse(b, cx - rad, cy - rad, rad * 2, rad * 2);
                g.ResetClip();
            }
            using (Pen pen = new Pen(hover ? T.Text : T.Muted, 1f))
                g.DrawEllipse(pen, cx - rad, cy - rad, rad * 2, rad * 2);
        }

        // 最大化/还原图标改成手绘方框：字体里的 ❐ / □ 在不同 DPI 下渲染出来大小不一，看着不像图标
        void DrawMaxGlyph(Graphics g, Rectangle r, bool maximized, bool hover)
        {
            if (hover) Draw.FillRound(g, r, P(6), T.RowHover);
            int s = P(9);
            int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            int x = cx - s / 2, y = cy - s / 2;
            g.SmoothingMode = SmoothingMode.None;
            using (Pen pen = new Pen(hover ? T.Text : T.SubText, 1f))
            {
                if (!maximized)
                {
                    g.DrawRectangle(pen, x, y, s, s);
                }
                else
                {
                    // 还原：前面一个完整框，后面只露右下两条边（与 Windows 原生图标一致）
                    int k = P(3);
                    int w = s - k;
                    g.DrawRectangle(pen, x, y, w, w);
                    g.DrawLine(pen, x + k, y + s, x + s, y + s);
                    g.DrawLine(pen, x + s, y + k, x + s, y + s);
                }
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
        }

        void PaintMaxBtn(Graphics g, Rectangle r)
        {
            Point mp = PointToClient(Cursor.Position);
            bool hover = r.Contains(mp) && ClientRectangle.Contains(mp);
            DrawMaxGlyph(g, r, WindowState == FormWindowState.Maximized, hover);
        }

        // 自测用：把三种标题栏图标状态画成一张小图，便于核对几何
        public Bitmap TestCaptionIcons()
        {
            int w = P(180), h = P(62);
            Bitmap bmp = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(T.Bg);
                Rectangle r1 = new Rectangle(P(10), P(6), P(34), P(28));
                Rectangle r2 = new Rectangle(P(58), P(6), P(34), P(28));
                Rectangle r3 = new Rectangle(P(106), P(6), P(34), P(28));
                DrawMaxGlyph(g, r1, false, false);
                DrawMaxGlyph(g, r2, false, true);
                DrawMaxGlyph(g, r3, true, false);
                Draw.Text(g, "最大化", F.R(8f), T.Muted, P(4), P(36), P(46), P(16), Draw.C);
                Draw.Text(g, "悬停", F.R(8f), T.Muted, P(52), P(36), P(46), P(16), Draw.C);
                Draw.Text(g, "还原态", F.R(8f), T.Muted, P(100), P(36), P(46), P(16), Draw.C);
                Rectangle r4 = new Rectangle(P(148), P(6), P(34), P(28));
                Draw.Text(g, "✕", F.R(9.5f), T.SubText, r4, Draw.C);
                Draw.Text(g, "关闭", F.R(8f), T.Muted, P(142), P(36), P(46), P(16), Draw.C);
            }
            return bmp;
        }

        void PaintCaptionBtn(Graphics g, Rectangle r, string glyph, Color normal)
        {
            Point mp = PointToClient(Cursor.Position);
            bool hover = r.Contains(mp) && ClientRectangle.Contains(mp);
            if (hover)
            {
                bool isClose = (glyph == "✕");
                Draw.FillRound(g, r, P(6), isClose ? T.Bad : T.RowHover);
                Draw.Text(g, glyph, F.R(9.5f), isClose ? Color.White : T.Text, r, Draw.C);
            }
            else Draw.Text(g, glyph, F.R(9.5f), normal, r, Draw.C);
        }

        void PaintCardMem(object sender, PaintEventArgs e)
        {
            Card c = (Card)sender;
            Draw.Text(e.Graphics, "内存与缓存治理", F.B(10f), T.Text, P(16), P(12), c.Width - 32, P(20), Draw.L);
            Draw.Text(e.Graphics, "降低系统内存压力，缓解内存回收导致的卡顿", F.R(8.5f), T.Muted, P(16), P(29), c.Width - 32, P(16), Draw.L);
        }

        void PaintCardProc(object sender, PaintEventArgs e)
        {
            Card c = (Card)sender;
            Draw.Text(e.Graphics, "进程处置", F.B(10f), T.Text, P(16), P(12), c.Width - 32, P(20), Draw.L);
            string sub = s_selPid > 0 ? ("当前选中 PID " + s_selPid) : "先在右侧列表选中一个进程";
            Draw.Text(e.Graphics, sub, F.R(8.5f), s_selPid > 0 ? T.SubText : T.Muted, P(16), P(29), c.Width - 32, P(16), Draw.L);

            int ty = P(200);
            if (c.Height < ty + P(40)) return;
            using (Pen pen = new Pen(T.BorderSoft, 1f))
                e.Graphics.DrawLine(pen, P(16), ty - P(12), c.Width - P(16), ty - P(12));
            Draw.Text(e.Graphics, "处置建议", F.B(8.75f), T.SubText, P(16), ty, c.Width - 32, P(16), Draw.L);
            Draw.Text(e.Graphics, "句柄或线程持续增长的进程，优先用「重启」而不是「强制结束」——重启会重建句柄表，是消除泄漏最彻底的办法。",
                F.R(8.25f), T.Muted, P(16), ty + P(20), c.Width - P(32), P(76),
                TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }

        void PaintSearch(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(T.Muted, 1.4f))
            {
                g.DrawEllipse(pen, P(12), P(10), P(9), P(9));
                g.DrawLine(pen, P(20), P(19), P(25), P(24));
            }
        }

        Rectangle BrandRect() { return new Rectangle(P(12), P(6), P(238), P(44)); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool overCap = rcClose.Contains(e.Location) || rcMax.Contains(e.Location) ||
                           rcMin.Contains(e.Location) || rcTheme.Contains(e.Location);
            bool overBrand = BrandRect().Contains(e.Location);
            if (overBrand != s_brandHover)
            {
                s_brandHover = overBrand;
                Invalidate(new Rectangle(P(8), P(2), P(246), P(52)));
            }
            Cursor = (overCap || overBrand) ? Cursors.Hand : Cursors.Default;
            if (overCap) Invalidate(new Rectangle(rcTheme.Left - P(4), P(10), ClientSize.Width - rcTheme.Left + P(4), P(36)));
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (s_brandHover)
            {
                s_brandHover = false;
                Invalidate(new Rectangle(P(8), P(2), P(246), P(52)));
            }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (rcClose.Contains(e.Location)) { Close(); return; }
            if (rcMax.Contains(e.Location)) { ToggleMaximize(); return; }
            if (rcMin.Contains(e.Location)) { WindowState = FormWindowState.Minimized; return; }
            if (rcTheme.Contains(e.Location)) { ToggleTheme(); return; }
            if (e.Clicks == 1 && BrandRect().Contains(e.Location)) { ShowAbout(); return; }
            base.OnMouseDown(e);
        }

        void ToggleMaximize()
        {
            if (WindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Normal;
                if (s_restoreBounds.Width > 0 && s_restoreBounds.Height > 0) Bounds = s_restoreBounds;
            }
            else
            {
                s_restoreBounds = Bounds;
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
                WindowState = FormWindowState.Maximized;
            }
            Invalidate();
        }

        void ShowAbout()
        {
            AboutDlg.Show(this, T, s_isAdmin);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                Point p = PointToClient(Cursor.Position);
                int edge = P(6);
                int hit = 0;
                // 最大化后不再需要边缘改尺寸（拖到边缘也别让人改成半屏）
                if (WindowState == FormWindowState.Normal)
                {
                    bool L = p.X <= edge, R = p.X >= ClientSize.Width - edge;
                    bool Tp = p.Y <= edge, B = p.Y >= ClientSize.Height - edge;
                    if (Tp && L) hit = 13;
                    else if (Tp && R) hit = 14;
                    else if (B && L) hit = 16;
                    else if (B && R) hit = 17;
                    else if (Tp) hit = 12;
                    else if (B) hit = 15;
                    else if (L) hit = 10;
                    else if (R) hit = 11;
                }
                // 标题栏：按钮与名称区走客户区（自己处理点击），其余给 HTCAPTION（可拖动、可双击）
                if (hit == 0 && p.Y >= 0 && p.Y < P(56) && p.X >= 0 && p.X < ClientSize.Width)
                {
                    if (rcClose.Contains(p) || rcMax.Contains(p) || rcMin.Contains(p) || rcTheme.Contains(p) ||
                        BrandRect().Contains(p))
                        hit = HTCLIENT;
                    else
                        hit = HTCAPTION;
                }
                if (hit != 0) { m.Result = (IntPtr)hit; return; }
            }
            else if (m.Msg == WM_NCLBUTTONDBLCLK)
            {
                // 系统默认的双击标题栏最大化只对标准边框窗口生效，无边框窗口必须自己处理，
                // 而且命中要覆盖最大化态，否则"双击最大化后再双击还原"永远走不到
                Point p = PointFromLParam(m.LParam);
                if (p.Y >= 0 && p.Y < P(56) && p.X >= 0 && p.X < ClientSize.Width &&
                    !rcClose.Contains(p) && !rcMax.Contains(p) && !rcMin.Contains(p) && !rcTheme.Contains(p) &&
                    !BrandRect().Contains(p))
                {
                    ToggleMaximize();
                    return;
                }
            }
            base.WndProc(ref m);
        }

        Point PointFromLParam(IntPtr lp)
        {
            long v = lp.ToInt64();
            int x = (int)(short)(v & 0xFFFF);
            int y = (int)(short)((v >> 16) & 0xFFFF);
            return PointToClient(new Point(x, y));
        }

        void ToggleTheme()
        {
            T = (T == Theme.Dark) ? Theme.Light : Theme.Dark;
            ApplyThemeToControls(this);
            BackColor = T.Bg;
            txtSearch.BackColor = T.PanelAlt;
            txtSearch.ForeColor = T.Text;
            SyncButtonBackgrounds(this, T);
            try { if (s_themeCacheFile.Length > 0) File.WriteAllText(s_themeCacheFile, T.Name); }
            catch { }
            stMem.ValueColor = Color.Empty;
            stCpu.ValueColor = Color.Empty;
            if (s_samples.Count > 0) ApplySample(s_sys, s_samples);
            LayoutUi();
            Invalidate(true);
        }

        void ApplyThemeToControls(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is StatCard) { StatCard s = (StatCard)c; s.T = T; s.BackColor = T.PanelAlt; }
                else if (c is Card) { Card k = (Card)c; k.T = T; k.BackColor = (k == searchBox) ? T.PanelAlt : T.Panel; }
                else if (c is FlatButton) { FlatButton b = (FlatButton)c; b.T = T; }
                else if (c is SegTabs) { SegTabs s = (SegTabs)c; s.T = T; s.BackColor = T.PanelAlt; }
                else if (c is DataGrid) { DataGrid d = (DataGrid)c; d.T = T; d.BackColor = T.Panel; }
                ApplyThemeToControls(c);
                c.Invalidate();
            }
        }

        // ------------------------------------------------------------------
        // 采样
        // ------------------------------------------------------------------
        void StartSample()
        {
            if (s_sampling) return;
            s_sampling = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                SystemSnapshot sys = null;
                List<ProcSample> list = null;
                string err = null;
                try
                {
                    sys = Engine.GetSystemSnapshot();
                    list = Engine.SampleProcesses(s_baseline);
                }
                catch (Exception ex) { err = ex.Message; }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        s_sampling = false;
                        if (err != null) { SetStatus("采样失败：" + err); return; }
                        ApplySample(sys, list);
                    }));
                }
                catch { s_sampling = false; }
            });
        }

        void ApplySample(SystemSnapshot sys, List<ProcSample> list)
        {
            s_sys = sys;
            s_samples = list;
            s_round++;

            long sumH = 0, sumT = 0;
            foreach (ProcSample p in list) { sumH += p.Handles; sumT += p.Threads; }
            sys.HandleCount = sumH;
            sys.ThreadCount = sumT;
            sys.ProcessCount = list.Count;

            long tot = sys.PhysicalTotal, avail = sys.PhysicalAvailable;
            double usedPct = tot > 0 ? (double)(tot - avail) / tot * 100.0 : 0;

            stHandles.Value = FormatNum(sys.HandleCount);
            stHandles.Hint = sys.ProcessCount + " 个可访问进程合计";
            stHandles.Invalidate();

            stThreads.Value = FormatNum(sys.ThreadCount);
            stThreads.Hint = "均值 " + (sys.ProcessCount > 0 ? (sys.ThreadCount / Math.Max(1, sys.ProcessCount)) : 0) + " 个 / 进程";
            stThreads.Invalidate();

            stMem.Value = usedPct.ToString("0.0") + "%";
            stMem.Hint = FormatBytes(tot - avail) + " / " + FormatBytes(tot);
            stMem.Ratio = usedPct / 100.0;
            stMem.ValueColor = usedPct > 88 ? T.Bad : (usedPct > 75 ? T.Warn : Color.Empty);
            stMem.Invalidate();

            stCpu.Value = sys.CpuUsage.ToString("0.0") + "%";
            stCpu.Hint = Environment.ProcessorCount + " 个逻辑处理器";
            stCpu.Ratio = sys.CpuUsage / 100.0;
            stCpu.ValueColor = sys.CpuUsage > 85 ? T.Bad : (sys.CpuUsage > 65 ? T.Warn : Color.Empty);
            stCpu.Invalidate();

            if (s_baseline.Count == 0 && s_round >= 2)
            {
                foreach (ProcSample p in list) s_baseline[p.Pid] = new int[] { p.Handles, p.Threads };
                for (int i = 0; i < list.Count; i++) { list[i].HandlesDelta = 0; list[i].ThreadsDelta = 0; }
            }

            ApplyFilter();
            s_metric = "系统句柄 " + FormatNum(sys.HandleCount) + " · 线程 " + FormatNum(sys.ThreadCount)
                + " · CPU " + sys.CpuUsage.ToString("0.0") + "%";
            Invalidate(new Rectangle(0, ClientSize.Height - P(38), ClientSize.Width, P(38)));
        }

        void CycleTempo()
        {
            s_tempo = (s_tempo + 1) % s_tempos.Length;
            int iv = s_tempos[s_tempo];
            s_autoRefresh = iv > 0;
            btnTempo.Text = s_tempoNames[s_tempo];
            btnTempo.Invalidate();
            if (iv > 0) { s_timer.Interval = iv; s_timer.Start(); }
            else s_timer.Stop();
            SetStatus(s_autoRefresh ? ("已切换为每 " + (iv / 1000) + " 秒自动刷新") : "已改为手动刷新，点「立即刷新」才更新");
            StartSample();
        }

        void OnHeaderSort(int col)
        {
            if (col == s_colSort) s_colDesc = !s_colDesc;
            else { s_colSort = col; s_colDesc = (col >= 2); }   // 数值列默认从大到小，名字 / PID 默认升序
            gridProcs.SortColumn = s_colSort;
            gridProcs.SortDescending = s_colDesc;
            ApplyFilter();
            gridProcs.Invalidate();
        }

        int CmpCol(ProcSample a, ProcSample b, int col)
        {
            switch (col)
            {
                case 0: return a.Pid.CompareTo(b.Pid);
                case 1: return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                case 2: return a.Handles.CompareTo(b.Handles);
                case 3: return a.Threads.CompareTo(b.Threads);
                case 4: return a.WorkingSet.CompareTo(b.WorkingSet);
                case 5: return a.Cpu.CompareTo(b.Cpu);
                case 6: return a.HandlesDelta.CompareTo(b.HandlesDelta);
                default: return a.ThreadsDelta.CompareTo(b.ThreadsDelta);
            }
        }

        void ApplyFilter()
        {
            string q = txtSearch.Text.Trim().ToLower();
            s_filtered = new List<ProcSample>();
            foreach (ProcSample p in s_samples)
            {
                if (q.Length > 0)
                {
                    bool match = p.Name.ToLower().Contains(q) || p.Pid.ToString() == q;
                    if (!match) continue;
                }
                s_filtered.Add(p);
            }

            List<ProcSample> sorted = new List<ProcSample>(s_filtered);
            if (s_colSort >= 0)
            {
                int col = s_colSort;
                bool desc = s_colDesc;
                sorted.Sort(delegate (ProcSample a, ProcSample b)
                {
                    int r = CmpCol(a, b, col);
                    return desc ? -r : r;
                });
            }
            else
            {
                sorted.Sort(delegate (ProcSample a, ProcSample b)
                {
                    switch (s_sortMode)
                    {
                        case 0: return b.Handles.CompareTo(a.Handles);
                        case 2: return b.ThreadsDelta != a.ThreadsDelta ? b.ThreadsDelta.CompareTo(a.ThreadsDelta) : b.Threads.CompareTo(a.Threads);
                        case 3: return b.Cpu.CompareTo(a.Cpu);
                        case 4: return b.WorkingSet.CompareTo(a.WorkingSet);
                        default:
                            int sa = a.HandlesDelta + a.ThreadsDelta * 4;
                            int sb = b.HandlesDelta + b.ThreadsDelta * 4;
                            return sb.CompareTo(sa);
                    }
                });
            }
            s_filtered = sorted;

            // 刷新间隔变长时，同样"每分钟涨 20 个句柄"会被算成大数字，阈值必须按间隔等比放大
            double f = Math.Max(1.0, s_tempos[s_tempo] / 1400.0);
            double warnH = 12 * f, badH = 40 * f, warnT = 6 * f, badT = 16 * f;

            List<string[]> rows = new List<string[]>(sorted.Count);
            List<Color[]> colors = new List<Color[]>(sorted.Count);
            foreach (ProcSample p in sorted)
            {
                string dh = p.HandlesDelta == 0 ? "—" : (p.HandlesDelta > 0 ? "+" : "") + p.HandlesDelta;
                string dt = p.ThreadsDelta == 0 ? "—" : (p.ThreadsDelta > 0 ? "+" : "") + p.ThreadsDelta;
                string pname = p.Name + (p.Pid == s_selfPid ? "（本工具）" : "");
                rows.Add(new string[] {
                    p.Pid.ToString(),
                    pname,
                    FormatNum(p.Handles),
                    FormatNum(p.Threads),
                    FormatBytes(p.WorkingSet),
                    p.Cpu.ToString("0.0") + "%",
                    dh, dt
                });
                Color[] cc = new Color[8];
                for (int i = 0; i < cc.Length; i++) cc[i] = T.Text;
                cc[0] = T.Muted;
                if (p.HandlesDelta >= badH || p.ThreadsDelta >= badT)
                {
                    cc[2] = T.Bad; cc[3] = T.Bad; cc[6] = T.Bad; cc[7] = T.Bad;
                }
                else if (p.HandlesDelta >= warnH || p.ThreadsDelta >= warnT)
                {
                    cc[2] = T.Warn; cc[3] = T.Warn; cc[6] = T.Warn; cc[7] = T.Warn;
                }
                if (p.Cpu > 25) cc[5] = T.Warn;
                colors.Add(cc);
            }
            gridProcs.RowColors = colors;
            gridProcs.SetRows(rows);

            // 保持选中
            int sel = -1;
            for (int i = 0; i < s_filtered.Count; i++) if (s_filtered[i].Pid == s_selPid) { sel = i; break; }
            if (sel >= 0 && gridProcs.SelectedIndex != sel)
            {
                gridProcs.SelectedIndex = sel;
                gridProcs.Invalidate();
            }
            else if (sel < 0) gridProcs.SelectedIndex = -1;
        }

        void SetStatus(string s)
        {
            s_status = s;
            UpdateStatusScroll();
            Invalidate(new Rectangle(0, ClientSize.Height - P(38), ClientSize.Width, P(38)));
        }

        // 文本超出状态栏可用宽度时启动跑马灯，否则停掉并复位
        void UpdateStatusScroll()
        {
            int avail = P(620);
            int w = string.IsNullOrEmpty(s_status) ? 0 : TextRenderer.MeasureText(s_status, F.R(8.5f)).Width;
            s_statusMax = w > avail ? w - avail + P(24) : 0;
            s_statusScroll = 0;
            if (s_statusMax > 0)
            {
                if (s_scrollTimer == null)
                {
                    s_scrollTimer = new System.Windows.Forms.Timer();
                    s_scrollTimer.Interval = 45;
                    s_scrollTimer.Tick += delegate
                    {
                        if (s_statusMax <= 0) { s_scrollTimer.Stop(); return; }
                        s_statusScroll += 1;
                        if (s_statusScroll > s_statusMax) s_statusScroll = 0;
                        Invalidate(new Rectangle(0, ClientSize.Height - P(38), ClientSize.Width, P(38)));
                    };
                }
                if (!s_scrollTimer.Enabled) s_scrollTimer.Start();
            }
            else if (s_scrollTimer != null) s_scrollTimer.Stop();
        }

        // ------------------------------------------------------------------
        // 选中与明细
        // ------------------------------------------------------------------
        void OnProcSelected()
        {
            int idx = gridProcs.SelectedIndex;
            if (idx < 0 || idx >= s_filtered.Count) return;
            int pid = s_filtered[idx].Pid;
            if (pid == s_selPid) return;
            s_selPid = pid;
            cardProc.Invalidate();
            LoadDetail(pid, true);
        }

        void LoadDetail(int pid, bool force)
        {
            if (pid <= 0) return;
            int mode = segDetail.Selected;
            bool profileMode = (mode == 0 || mode == 1);
            bool profileCached = s_profile != null && s_profilePid == pid;
            if (!force)
            {
                if (profileMode && profileCached) return;
                if (mode == 2 && pid == s_handlesPid && s_handles.Count > 0) { ShowHandles(-1); return; }
                if (mode == 3 && pid == s_threadsPid && s_threads.Count > 0) { ShowThreads(); return; }
            }
            s_detailPid = pid;
            s_loadingDetail = true;
            SetStatus(profileMode
                ? "正在读取 PID " + pid + " 的进程档案（路径 / 命令行 / 父子关系 / 文件 / 网络）…"
                : (mode == 2 ? "正在扫描 PID " + pid + " 的句柄表…" : "正在扫描 PID " + pid + " 的线程…"));

            ThreadPool.QueueUserWorkItem(delegate
            {
                ProcProfile pf = null;
                List<HandleEntry> hs = null;
                List<ThreadEntry> ts = null;
                string err = null;
                int dup = 0;
                try
                {
                    if (profileMode)
                    {
                        pf = Inspector.Load(pid);
                    }
                    else if (mode == 2)
                    {
                        hs = Engine.EnumerateAllHandles(out err);
                        List<HandleEntry> mine = new List<HandleEntry>();
                        foreach (HandleEntry h in hs) if (h.Pid == pid) mine.Add(h);
                        hs = mine;
                        Engine.EnrichHandles(hs, 4000);
                        dup = Engine.MarkDuplicates(hs);
                    }
                    else
                    {
                        ts = Engine.EnumerateThreads(pid);
                    }
                }
                catch (Exception ex) { err = ex.Message; }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        s_loadingDetail = false;
                        if (err != null) { SetStatus("读取失败：" + err); return; }
                        int now = segDetail.Selected;
                        if (profileMode)
                        {
                            s_profile = pf; s_profilePid = pid;
                            if (now == 0) ShowProfile();
                            else if (now == 1) ShowFiles();
                        }
                        else if (mode == 2)
                        {
                            s_handles = hs; s_handlesPid = pid;
                            if (now == 2) ShowHandles(dup);
                        }
                        else
                        {
                            s_threads = ts; s_threadsPid = pid;
                            if (now == 3) ShowThreads();
                        }
                    }));
                }
                catch { s_loadingDetail = false; }
            });
        }

        // 未选中进程时各页统一的空状态提示（不带数据时不再显示"正在读取…"这种误导文案）
        const string HintSelect = "在上方进程列表中选择一个进程后，这里会显示";
        const string EmptyProfile = HintSelect + "它的完整档案（路径 / 命令行 / 父子进程 / 打开的文件 / 网络）";
        const string EmptyFiles = HintSelect + "它加载的模块、打开的文件和网络连接";
        const string EmptyHandles = HintSelect + "它的句柄明细";
        const string EmptyThreads = HintSelect + "它的线程明细";

        void ShowDetailPage()
        {
            gridDetail.Columns.Clear();
            gridDetail.SelectedIndex = -1;
            int mode = segDetail.Selected;

            // 没选进程时状态栏也别留着上一个进程的旧消息
            if (s_selPid <= 0) SetStatus("未选中进程 · 在上方进程列表里点一个进程，这里会显示它的全部信息");

            if (mode == 0)
            {
                btnDetailAction.Text = "复制整份档案";
                btnDetailAction.Tone = BtnTone.Subtle;
                gridDetail.Columns.Add(new ColumnDef("k", "项目", P(136), Draw.L));
                gridDetail.Columns.Add(new ColumnDef("v", "内容", -1, Draw.L));
                gridDetail.EmptyText = EmptyProfile;
                if (s_selPid > 0 && s_profilePid != s_selPid) LoadDetail(s_selPid, true);
                else ShowProfile();
            }
            else if (mode == 1)
            {
                btnDetailAction.Text = "复制选中项";
                btnDetailAction.Tone = BtnTone.Subtle;
                gridDetail.Columns.Add(new ColumnDef("c", "类别", P(76), Draw.L));
                gridDetail.Columns.Add(new ColumnDef("t", "目标", -1, Draw.L));
                gridDetail.Columns.Add(new ColumnDef("r", "备注 / 路径", P(330), Draw.L));
                gridDetail.EmptyText = EmptyFiles;
                if (s_selPid > 0 && s_profilePid != s_selPid) LoadDetail(s_selPid, true);
                else ShowFiles();
            }
            else if (mode == 2)
            {
                btnDetailAction.Text = "关闭选中句柄";
                btnDetailAction.Tone = BtnTone.Danger;
                gridDetail.Columns.Add(new ColumnDef("h", "句柄值", P(96), Draw.L));
                gridDetail.Columns.Add(new ColumnDef("t", "类型", P(132), Draw.L));
                gridDetail.Columns.Add(new ColumnDef("n", "对象名称", -1, Draw.L));
                gridDetail.Columns.Add(new ColumnDef("a", "访问掩码", P(96), Draw.L));
                gridDetail.Columns.Add(new ColumnDef("d", "同对象句柄", P(96), Draw.L));
                gridDetail.EmptyText = EmptyHandles;
                if (s_selPid > 0 && s_handlesPid != s_selPid) LoadDetail(s_selPid, true);
                else ShowHandles(-1);
            }
            else
            {
                btnDetailAction.Text = "挂起选中线程";
                btnDetailAction.Tone = BtnTone.Subtle;
                gridDetail.Columns.Add(new ColumnDef("tid", "TID", P(80), Draw.L));
                gridDetail.Columns.Add(new ColumnDef("cpu", "CPU时间", P(90), Draw.R));
                gridDetail.Columns.Add(new ColumnDef("pri", "优先级", P(70), Draw.R));
                gridDetail.Columns.Add(new ColumnDef("addr", "起始地址", P(180), Draw.L));
                gridDetail.Columns.Add(new ColumnDef("desc", "线程描述", -1, Draw.L));
                gridDetail.EmptyText = EmptyThreads;
                if (s_selPid > 0 && s_threadsPid != s_selPid) LoadDetail(s_selPid, true);
                else ShowThreads();
            }
            gridDetail.Invalidate();
        }

        // ------------------------------------------------------------------
        // 进程档案页
        // ------------------------------------------------------------------
        void AddKV(List<string[]> rows, List<Color[]> colors, string k, string v, Color vc)
        {
            string shown = (v == null || v.Length == 0) ? "—" : v;
            rows.Add(new string[] { k, shown });
            Color[] cc = new Color[2];
            cc[0] = T.SubText;
            cc[1] = (v == null || v.Length == 0) ? T.Muted : vc;
            colors.Add(cc);
        }

        void ShowProfile()
        {
            if (s_selPid <= 0)
            {
                s_profile = null;
                s_profilePid = -1;
                gridDetail.RowColors = new List<Color[]>();
                gridDetail.SetRows(new List<string[]>());
                gridDetail.EmptyText = EmptyProfile;
                gridDetail.Invalidate();
                return;
            }
            if (s_profile == null || s_profilePid != s_selPid)
            {
                gridDetail.RowColors = new List<Color[]>();
                gridDetail.SetRows(new List<string[]>());
                gridDetail.EmptyText = "正在读取进程档案…";
                gridDetail.Invalidate();
                return;
            }
            ProcProfile pf = s_profile;
            List<string[]> rows = new List<string[]>();
            List<Color[]> colors = new List<Color[]>();

            AddKV(rows, colors, "进程名称", pf.Name + "   (PID " + pf.Pid + ")", T.Text);
            AddKV(rows, colors, "映像路径", pf.Path, T.Text);
            AddKV(rows, colors, "命令行", pf.CommandLine, T.AccentText);

            string parent = "—（无父进程，或该进程属受保护的顶层进程）";
            if (pf.ParentPid > 0)
            {
                parent = (pf.ParentName.Length > 0 ? pf.ParentName : "已退出或不可访问的进程")
                    + "   (PID " + pf.ParentPid + ")";
            }
            AddKV(rows, colors, "父进程", parent, T.Text);
            if (pf.ParentPid > 0) AddKV(rows, colors, "父进程路径", pf.ParentPath, T.SubText);
            AddKV(rows, colors, "下级进程", pf.Children.Length > 0 ? pf.Children : "未拉起其他进程", T.Text);

            AddKV(rows, colors, "启动时间", pf.Started.Length > 0 ? pf.Started + "（已运行 " + pf.Uptime + "）" : "", T.SubText);
            AddKV(rows, colors, "运行账户", pf.User, T.SubText);
            AddKV(rows, colors, "架构", pf.Arch, T.SubText);
            AddKV(rows, colors, "文件说明", pf.FileDesc, T.SubText);
            AddKV(rows, colors, "产品名称", pf.Product, T.SubText);
            AddKV(rows, colors, "公司", pf.Company, T.SubText);
            AddKV(rows, colors, "文件版本", pf.FileVersion, T.SubText);

            AddKV(rows, colors, "句柄 / 线程", FormatNum(pf.Handles) + " 句柄 / " + FormatNum(pf.Threads) + " 线程", T.Text);
            AddKV(rows, colors, "工作集 / 私有字节",
                FormatBytes(pf.WorkingSet) + " / " + FormatBytes(pf.PrivateBytes), T.Text);
            AddKV(rows, colors, "加载模块", pf.ModuleCount + " 个", T.SubText);
            AddKV(rows, colors, "打开的文件", pf.FileCount + " 个", T.SubText);
            AddKV(rows, colors, "网络连接", pf.NetCount + " 条", T.SubText);

            if (pf.Note.Length > 0) AddKV(rows, colors, "提示", pf.Note, T.Warn);

            gridDetail.EmptyText = EmptyProfile;
            gridDetail.RowColors = colors;
            gridDetail.SetRows(rows);
            SetStatus("PID " + pf.Pid + " · 进程档案已读取"
                + (pf.CommandLine.Length == 0 ? " · 命令行不可读（需管理员，或受保护进程）" : ""));
        }

        void ShowFiles()
        {
            if (s_selPid <= 0)
            {
                s_profile = null;
                s_profilePid = -1;
                gridDetail.RowColors = new List<Color[]>();
                gridDetail.SetRows(new List<string[]>());
                gridDetail.EmptyText = EmptyFiles;
                gridDetail.Invalidate();
                return;
            }
            if (s_profile == null || s_profilePid != s_selPid)
            {
                gridDetail.RowColors = new List<Color[]>();
                gridDetail.SetRows(new List<string[]>());
                gridDetail.EmptyText = "正在读取进程档案…";
                gridDetail.Invalidate();
                return;
            }
            ProcProfile pf = s_profile;
            List<string[]> rows = new List<string[]>();
            List<Color[]> colors = new List<Color[]>();
            foreach (string[] it in pf.Items)
            {
                rows.Add(it);
                Color[] cc = new Color[3];
                if (it[0] == "网络") cc[0] = T.AccentText;
                else if (it[0] == "文件") cc[0] = T.Warn;
                else cc[0] = T.SubText;
                cc[1] = T.Text;
                cc[2] = T.SubText;
                colors.Add(cc);
            }
            gridDetail.RowColors = colors;
            gridDetail.SetRows(rows);
            gridDetail.EmptyText = EmptyFiles;
            if (rows.Count == 0) gridDetail.EmptyText = "该进程没有可读取的模块 / 文件 / 连接（可能权限不足）";
            SetStatus("PID " + pf.Pid + " · 模块 " + pf.ModuleCount + " · 磁盘文件 " + pf.FileCount + " · 网络 " + pf.NetCount);
        }

        void ShowHandles(int dupCount)
        {
            if (s_selPid <= 0)
            {
                s_handles.Clear();
                s_handlesPid = -1;
                gridDetail.RowColors = new List<Color[]>();
                gridDetail.SetRows(new List<string[]>());
                gridDetail.EmptyText = EmptyHandles;
                gridDetail.Invalidate();
                return;
            }
            List<string[]> rows = new List<string[]>();
            List<Color[]> colors = new List<Color[]>();
            int dupTotal = 0;
            int unknownType = 0;
            foreach (HandleEntry h in s_handles)
            {
                if (h.IsDuplicate) dupTotal++;
                if (h.TypeName == "—") unknownType++;
                string acc = "0x" + h.GrantedAccess.ToString("X3");
                string group = h.GroupCount > 1 ? ("×" + h.GroupCount) : "";
                rows.Add(new string[] {
                    "0x" + h.HandleValue.ToInt64().ToString("X"),
                    h.TypeName,
                    h.ObjectName,
                    acc,
                    group
                });
                Color[] cc = new Color[5];
                for (int i = 0; i < cc.Length; i++) cc[i] = T.Text;
                cc[0] = T.Muted;
                cc[1] = T.AccentText;
                if (h.IsDuplicate) { cc[4] = T.Warn; cc[2] = T.Warn; }
                if (h.ObjectName.Length == 0) cc[2] = T.Muted;
                colors.Add(cc);
            }
            gridDetail.RowColors = colors;
            gridDetail.SetRows(rows);
            string msg = "PID " + s_handlesPid + " · 句柄 " + s_handles.Count + " 条";
            if (dupTotal > 0) msg += " · 同对象重复 " + dupTotal + " 条";
            if (unknownType == s_handles.Count && s_handles.Count > 0)
                msg += " · 无法读取句柄类型，该进程属其他用户或受保护，请以管理员身份运行";
            SetStatus(msg);
        }

        void ShowThreads()
        {
            if (s_selPid <= 0)
            {
                s_threads.Clear();
                s_threadsPid = -1;
                gridDetail.RowColors = new List<Color[]>();
                gridDetail.SetRows(new List<string[]>());
                gridDetail.EmptyText = EmptyThreads;
                gridDetail.Invalidate();
                return;
            }
            List<string[]> rows = new List<string[]>();
            List<Color[]> colors = new List<Color[]>();
            foreach (ThreadEntry t in s_threads)
            {
                string addr = t.StartAddress == IntPtr.Zero ? "—" : "0x" + t.StartAddress.ToInt64().ToString("X");
                rows.Add(new string[] {
                    t.Tid.ToString(),
                    FormatMs(t.CpuMs),
                    t.Priority.ToString(),
                    addr,
                    t.Description
                });
                Color[] cc = new Color[5];
                for (int i = 0; i < cc.Length; i++) cc[i] = T.Text;
                cc[0] = T.Muted; cc[3] = T.SubText; cc[4] = T.SubText;
                if (t.CpuMs > 20000) cc[1] = T.Warn;
                colors.Add(cc);
            }
            gridDetail.RowColors = colors;
            gridDetail.SetRows(rows);
            string msg = "PID " + s_threadsPid + " · 线程 " + s_threads.Count + " 个";
            bool allBlind = s_threads.Count > 0;
            foreach (ThreadEntry t in s_threads)
                if (t.StartAddress != IntPtr.Zero || t.Description.Length > 0 || t.CpuMs > 0) { allBlind = false; break; }
            if (allBlind) msg += " · 无法读取线程详情，该进程属其他用户或受保护，请以管理员身份运行";
            SetStatus(msg);
        }

        // ------------------------------------------------------------------
        // 处置动作
        // ------------------------------------------------------------------
        ProcSample SelectedProc()
        {
            if (s_selPid <= 0) return null;
            foreach (ProcSample p in s_samples) if (p.Pid == s_selPid) return p;
            return null;
        }

        // 把失败原因说清楚：管理员模式下也可能因为特权被禁用/被策略阻止而失败，不能一律说"需要管理员权限"
        string CleanReason(int code)
        {
            if (code == 0) return "原因未知";
            if (code == 5) return "系统拒绝访问（错误码 5）";
            if (code == 1314) return "缺少所需特权（1314）";
            if (code == 1300) return "当前账户未被分配该特权（1300）";
            if (code == unchecked((int)0xC0000061)) return "缺少所需特权（STATUS_PRIVILEGE_NOT_HELD）";
            return "系统错误码 " + code;
        }

        string PrivHint(bool enabled, string priv)
        {
            return enabled ? "" : "（" + priv + " 未能启用，可能被组策略限制）";
        }

        void DoRelease()
        {
            SetStatus("正在释放内存…");
            ThreadPool.QueueUserWorkItem(delegate
            {
                CleanReport rep = Engine.ReleaseMemory(true, true, true);
                BeginInvoke(new Action(delegate
                {
                    SetStatus(rep.Detail + " 估算释放 " + FormatBytes(rep.BytesReleased));
                    StartSample();
                    string msg = "";
                    if (rep.ProcessesTrimmed > 0) msg += "已修剪 " + rep.ProcessesTrimmed + " 个进程的工作集\n";
                    if (rep.CacheCleared) msg += "系统文件缓存已清空\n";
                    else msg += "文件缓存清理失败：" + CleanReason(rep.CacheError) + PrivHint(rep.QuotaPriv, "提高配额特权") + "\n";
                    if (rep.StandbyPurged) msg += "待机内存列表已清空\n";
                    else msg += "待机列表清理失败：" + CleanReason(rep.StandbyError) + PrivHint(rep.ProfilePriv, "性能诊断特权") + "\n";
                    msg += "\n约 " + FormatBytes(rep.BytesReleased) + " 工作集已归还系统，可用内存随即上升。";
                    ConfirmDlg.Info(this, T, "内存与缓存治理完成", msg);
                }));
            });
        }

        void DoCache()
        {
            SetStatus("正在清空文件缓存…");
            ThreadPool.QueueUserWorkItem(delegate
            {
                CleanReport rep = Engine.ReleaseMemory(false, true, false);
                BeginInvoke(new Action(delegate
                {
                    SetStatus(rep.CacheCleared ? "系统文件缓存已清空" : "文件缓存清理失败");
                    StartSample();
                    ConfirmDlg.Info(this, T, "文件缓存清理" + (rep.CacheCleared ? "完成" : "失败"),
                        rep.CacheCleared
                            ? "系统文件缓存已全部释放。\n常用程序下次读取这些文件时需要重新读盘，首屏可能略慢一点，属正常现象。"
                            : "清理失败：" + CleanReason(rep.CacheError) + "\n" + PrivHint(rep.QuotaPriv, "提高配额特权") +
                              "\n\n该操作需要「提高配额」特权；管理员模式下若被组策略禁用，也会失败。");
                }));
            });
        }

        void DoStandby()
        {
            SetStatus("正在清空待机内存列表…");
            ThreadPool.QueueUserWorkItem(delegate
            {
                CleanReport rep = Engine.ReleaseMemory(false, false, true);
                BeginInvoke(new Action(delegate
                {
                    SetStatus(rep.StandbyPurged ? "待机内存列表已清空" : "待机列表清理失败");
                    StartSample();
                    ConfirmDlg.Info(this, T, "待机内存清理" + (rep.StandbyPurged ? "完成" : "失败"),
                        rep.StandbyPurged
                            ? "待机内存列表已清空，这部分内存已转为可用内存。\n原本缓存在内存里的数据需要重新从磁盘读取，短时间内打开旧程序可能稍慢。"
                            : "清理失败：" + CleanReason(rep.StandbyError) + "\n" + PrivHint(rep.ProfilePriv, "性能诊断特权") +
                              "\n\n该操作需要「性能诊断」特权；管理员模式下若被组策略禁用，也会失败。");
                }));
            });
        }

        void DoKill()
        {
            ProcSample p = SelectedProc();
            if (p == null) { SetStatus("请先在进程列表中选择一个进程"); return; }
            string msg = "将强制结束进程 " + p.Name + " (PID " + p.Pid + ")。\r\n未保存的工作会丢失，继续吗？";
            if (!ConfirmDlg.Show(this, T, "结束进程", msg, "结束进程", true)) return;
            string r = Engine.KillProcess(p.Pid);
            SetStatus(p.Name + "：" + r);
            if (r == "已结束") { s_selPid = -1; StartSample(); }
        }

        void DoRestart()
        {
            ProcSample p = SelectedProc();
            if (p == null) { SetStatus("请先在进程列表中选择一个进程"); return; }
            if (string.IsNullOrEmpty(p.Path) || !File.Exists(p.Path))
            {
                SetStatus("无法获取 " + p.Name + " 的可执行文件路径，无法重启");
                return;
            }
            string msg = "将结束 " + p.Name + " 并重新启动：\r\n" + p.Path;
            if (!ConfirmDlg.Show(this, T, "重启进程", msg, "重启", false)) return;
            string r = Engine.KillProcess(p.Pid);
            if (r != "已结束") { SetStatus("重启失败：" + r); return; }
            Thread.Sleep(400);
            try
            {
                Process.Start(p.Path);
                SetStatus("已重新启动 " + p.Name);
            }
            catch (Exception ex) { SetStatus("启动失败：" + ex.Message); }
            StartSample();
        }

        void DoDetailAction()
        {
            int mode = segDetail.Selected;

            if (mode == 0)
            {
                if (s_profile == null || s_profilePid != s_selPid) { SetStatus("请先选中一个进程"); return; }
                StringBuilder sb = new StringBuilder();
                foreach (string[] r in gridDetail.Rows) sb.AppendLine(r[0] + "：" + r[1]);
                try
                {
                    Clipboard.SetText(sb.ToString());
                    SetStatus("整份档案已复制到剪贴板（" + gridDetail.Rows.Count + " 项）");
                }
                catch { SetStatus("复制失败：剪贴板被其他程序占用"); }
                return;
            }

            if (mode == 1)
            {
                int ci = gridDetail.SelectedIndex;
                if (ci < 0 || ci >= gridDetail.Rows.Count) { SetStatus("请先在列表里选中一行"); return; }
                string[] row = gridDetail.Rows[ci];
                string txt = row[0] == "模块" ? row[2] : row[1];
                try
                {
                    Clipboard.SetText(txt);
                    SetStatus("已复制：" + txt);
                }
                catch { SetStatus("复制失败：剪贴板被其他程序占用"); }
                return;
            }

            if (mode == 2)
            {
                int idx = gridDetail.SelectedIndex;
                if (s_detailPid <= 0 || idx < 0 || idx >= s_handles.Count) { SetStatus("请先选中一条句柄"); return; }
                HandleEntry h = s_handles[idx];
                if (!h.IsDuplicate)
                {
                    SetStatus("该句柄不是重复句柄，为保证程序稳定不予关闭");
                    return;
                }
                ProcSample p = null;
                foreach (ProcSample q in s_samples) if (q.Pid == s_detailPid) { p = q; break; }
                string who = p != null ? p.Name : ("PID " + s_detailPid);
                string msg = "将向 " + who + " 注入一次 CloseHandle 调用，关闭句柄 0x" +
                             h.HandleValue.ToInt64().ToString("X") + "（" + h.TypeName + "）。\r\n" +
                             "该进程可能随后崩溃或丢失数据，建议先保存工作。继续吗？";
                if (!ConfirmDlg.Show(this, T, "关闭远端句柄", msg, "关闭句柄", true)) return;
                string r = Engine.CloseHandleInProcess(s_detailPid, h.HandleValue);
                SetStatus("关闭句柄 0x" + h.HandleValue.ToInt64().ToString("X") + "：" + r);
                if (r.StartsWith("句柄已关闭")) { h.DuplicateClosed = true; LoadDetail(s_detailPid, true); }
                return;
            }

            int ti = gridDetail.SelectedIndex;
            if (s_detailPid <= 0 || ti < 0 || ti >= s_threads.Count) { SetStatus("请先选中一条线程"); return; }
            ThreadEntry t = s_threads[ti];
            string res = Engine.SuspendThreadByTid(t.Tid);
            SetStatus("挂起线程 " + t.Tid + "：" + res);
            if (btnDetailAction.Text.StartsWith("挂起") && res.StartsWith("已挂起"))
            {
                btnDetailAction.Text = "恢复选中线程";
                btnDetailAction.Invalidate();
            }
            else if (btnDetailAction.Text.StartsWith("恢复"))
            {
                string r2 = Engine.ResumeThreadByTid(t.Tid);
                SetStatus("恢复线程 " + t.Tid + "：" + r2);
            }
        }

        void DoExport()
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Markdown 报告 (*.md)|*.md|文本文件 (*.txt)|*.txt";
            dlg.FileName = "HandleGuard-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".md";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# HandleGuard 诊断报告");
                sb.AppendLine();
                sb.AppendLine("- 导出时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("- 计算机：" + Environment.MachineName + " / " + Environment.OSVersion.VersionString);
                sb.AppendLine("- 管理员权限：" + (s_isAdmin ? "是" : "否"));
                sb.AppendLine();
                sb.AppendLine("## 系统总览");
                sb.AppendLine();
                sb.AppendLine("| 指标 | 数值 |");
                sb.AppendLine("| --- | --- |");
                sb.AppendLine("| 系统句柄总数 | " + FormatNum(s_sys.HandleCount) + " |");
                sb.AppendLine("| 系统线程总数 | " + FormatNum(s_sys.ThreadCount) + " |");
                sb.AppendLine("| 进程数 | " + FormatNum(s_sys.ProcessCount) + " |");
                sb.AppendLine("| 物理内存 | " + FormatBytes(s_sys.PhysicalTotal - s_sys.PhysicalAvailable) + " / " + FormatBytes(s_sys.PhysicalTotal) + " |");
                sb.AppendLine("| CPU 占用 | " + s_sys.CpuUsage.ToString("0.0") + "% |");
                sb.AppendLine();
                sb.AppendLine("## 句柄 / 线程增长排行（相对本次运行基线）");
                sb.AppendLine();
                sb.AppendLine("| PID | 进程 | 句柄 | Δ句柄 | 线程 | Δ线程 | 工作集 | CPU | 路径 |");
                sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");
                List<ProcSample> sorted = new List<ProcSample>(s_samples);
                sorted.Sort(delegate (ProcSample a, ProcSample b)
                {
                    int sa = a.HandlesDelta + a.ThreadsDelta * 4;
                    int sb2 = b.HandlesDelta + b.ThreadsDelta * 4;
                    return sb2.CompareTo(sa);
                });
                int n = 0;
                foreach (ProcSample p in sorted)
                {
                    if (n++ > 40) break;
                    sb.AppendLine("| " + p.Pid + " | " + p.Name + " | " + p.Handles + " | " + p.HandlesDelta +
                                  " | " + p.Threads + " | " + p.ThreadsDelta + " | " + FormatBytes(p.WorkingSet) +
                                  " | " + p.Cpu.ToString("0.0") + "% | " + p.Path + " |");
                }
                if (s_handles.Count > 0 && s_detailPid > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("## 进程 " + s_detailPid + " 句柄明细（前 60 条）");
                    sb.AppendLine();
                    sb.AppendLine("| 句柄 | 类型 | 对象名 | 访问掩码 | 重复 |");
                    sb.AppendLine("| --- | --- | --- | --- | --- |");
                    int k = 0;
                    foreach (HandleEntry h in s_handles)
                    {
                        if (k++ > 60) break;
                        sb.AppendLine("| 0x" + h.HandleValue.ToInt64().ToString("X") + " | " + h.TypeName + " | " +
                                      h.ObjectName + " | 0x" + h.GrantedAccess.ToString("X3") + " | " + (h.IsDuplicate ? "是" : "") + " |");
                    }
                }
                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                SetStatus("报告已保存：" + dlg.FileName);
            }
            catch (Exception ex) { SetStatus("导出失败：" + ex.Message); }
        }

        // ------------------------------------------------------------------
        // 自测入口
        // ------------------------------------------------------------------
        public void TestSelectFirst()
        {
            if (s_filtered.Count == 0) return;
            // 自测固定选本工具自身：档案字段一定可读，样本可复现
            int pick = -1;
            for (int i = 0; i < s_filtered.Count; i++)
                if (s_filtered[i].Pid == s_selfPid) { pick = i; break; }
            if (pick < 0)
                for (int i = 0; i < s_filtered.Count; i++)
                    if (s_filtered[i].Pid > 100) { pick = i; break; }
            if (pick < 0) pick = 0;
            gridProcs.SelectedIndex = pick;
            gridProcs.Invalidate();
            OnProcSelected();
        }

        // 自测：取消选中，用来核对"未选中进程"时空状态文案
        public void TestClearSelection()
        {
            gridProcs.SelectedIndex = -1;
            s_selPid = -1;
            s_profile = null;
            s_profilePid = -1;
            s_handles.Clear();
            s_handlesPid = -1;
            s_threads.Clear();
            s_threadsPid = -1;
            gridProcs.Invalidate();
            cardProc.Invalidate();
            ShowDetailPage();
        }

        // 自测：模拟点表头排序
        public void TestSortColumn(int c) { OnHeaderSort(c); }

        public void TestToggleMaximize() { ToggleMaximize(); }
        public bool TestMaximized { get { return WindowState == FormWindowState.Maximized; } }

        public void TestSetDetailPage(int i)
        {            if (!s_uiReady) return;
            segDetail.Selected = i;
            segDetail.Invalidate();
            ShowDetailPage();
        }

        public void TestDetailScrollBottom()
        {
            gridDetail.ScrollToBottom();
        }

        public void TestToggleTheme() { ToggleTheme(); }

        public Theme CurrentTheme { get { return T; } }

        public string TestDiag()
        {
            return "client=" + ClientSize.Width + "x" + ClientSize.Height +
                   " dpi=" + DeviceDpi + " scale=" + S.ToString("0.00") +
                   " procs=" + s_samples.Count +
                   " sysHandles=" + s_sys.HandleCount + " sysThreads=" + s_sys.ThreadCount +
                   " cpu=" + s_sys.CpuUsage.ToString("0.0") +
                   " admin=" + s_isAdmin +
                   " tempo=" + s_tempos[s_tempo] + " colSort=" + s_colSort + " desc=" + s_colDesc +
                   " selPid=" + s_selPid + " handles=" + s_handles.Count + " threads=" + s_threads.Count +
                   " status=" + s_status;
        }

        // ------------------------------------------------------------------
        // 工具
        // ------------------------------------------------------------------
        public static string FormatNum(long v)
        {
            if (v >= 1000000) return (v / 1000000.0).ToString("0.0") + "M";
            if (v >= 10000) return (v / 1000.0).ToString("0.0") + "K";
            return v.ToString("#,##0");
        }

        public static string FormatBytes(long b)
        {
            if (b >= 1024L * 1024 * 1024) return (b / 1024.0 / 1024 / 1024).ToString("0.0") + " GB";
            if (b >= 1024L * 1024) return (b / 1024.0 / 1024).ToString("0.0") + " MB";
            if (b >= 1024) return (b / 1024.0).ToString("0") + " KB";
            return b + " B";
        }

        static string FormatMs(long ms)
        {
            if (ms >= 3600000) return (ms / 3600000.0).ToString("0.0") + " h";
            if (ms >= 60000) return (ms / 60000.0).ToString("0.0") + " min";
            if (ms >= 1000) return (ms / 1000.0).ToString("0.00") + " s";
            return ms + " ms";
        }
    }
}
