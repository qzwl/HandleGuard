using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text;
using System.Windows.Forms;

namespace HandleGuard
{
    // 自绘「关于」对话框：作者、版本、技术栈、开源组件与许可
    public class AboutDlg : Form
    {
        public const string AppName = "HandleGuard";
        public const string AppVersion = "1.4.0";
        public const string Author = "qzwl";

        Theme T;
        bool s_admin;
        FlatButton btnCopy, btnClose;

        AboutDlg(Theme t, bool admin)
        {
            T = t;
            s_admin = admin;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = t.Bg;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            ClientSize = new Size(560, 556);
            KeyPreview = true;

            btnClose = new FlatButton(t);
            btnClose.Text = "关闭";
            btnClose.Tone = BtnTone.Primary;
            btnClose.BackColor = t.Panel;
            btnClose.Bounds = new Rectangle(ClientSize.Width - 14 - 96, ClientSize.Height - 14 - 36, 96, 36);
            btnClose.Click += delegate { Close(); };
            Controls.Add(btnClose);

            btnCopy = new FlatButton(t);
            btnCopy.Text = "复制信息";
            btnCopy.Tone = BtnTone.Ghost;
            btnCopy.BackColor = t.Panel;
            btnCopy.Bounds = new Rectangle(ClientSize.Width - 14 - 96 - 8 - 96, ClientSize.Height - 14 - 36, 96, 36);
            btnCopy.Click += delegate
            {
                try
                {
                    Clipboard.SetText(PlainText());
                    btnCopy.Text = "已复制到剪贴板";
                    btnCopy.Invalidate();
                }
                catch { }
            };
            Controls.Add(btnCopy);
        }

        public static void Show(IWin32Window owner, Theme t, bool admin)
        {
            new AboutDlg(t, admin).ShowDialog(owner);
        }

        public static AboutDlg CreateInstance(Theme t, bool admin) { return new AboutDlg(t, admin); }

        static string[] TechLines = new string[]
        {
            "· C# 5 + WinForms 全自绘界面，不依赖任何第三方控件库",
            "· 原生 Win32 / NT API 采集句柄表、线程、模块与网络连接",
            "· 进程内部信息（命令行 / 父进程）经 PEB 读取，x86 与 x64 分别按偏移取值",
            "· 目标系统：Windows 10 / 11，x64（32 位进程亦可运行）",
        };

        static string[] OssLines = new string[]
        {
            "· 运行时：.NET Framework 4.x（Microsoft，随 Windows 提供）",
            "· 界面与图形：System.Windows.Forms、System.Drawing / GDI+（Microsoft）",
            "· 系统接口：NT API（NtQuerySystemInformation 等）、Toolhelp32、IPHLPAPI、PSAPI",
            "· 第三方库：无。本工具不含第三方组件，不联网、不上传任何数据",
            "· 应用图标：作者自绘矢量几何（Brand.cs），非第三方素材",
        };

        static string[] RiskLines = new string[]
        {
            "· 句柄是进程私有的内核引用，第三方没有合法途径“一键清理”别人的句柄",
            "· 关闭远端句柄 / 挂起线程 / 强制结束进程都有风险，操作前请先保存工作",
            "· 修剪工作集后短暂卡顿属正常现象，内存会在一两分钟内回到稳态",
        };

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(T.Bg);
            Rectangle r = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            Draw.FillRound(g, r, 10, T.Panel);
            Draw.StrokeRound(g, r, 10, T.Border);

            // 左侧强调条：裁到圆角内
            GraphicsState gs = g.Save();
            using (GraphicsPath cp = Draw.RoundRect(r, 10)) g.SetClip(cp);
            using (SolidBrush b = new SolidBrush(T.Accent))
                g.FillRectangle(b, 0, 0, 4, ClientSize.Height);
            g.Restore(gs);

            int W = ClientSize.Width;

            // 头部：与 exe 图标同一份几何
            Rectangle logo = new Rectangle(26, 22, 46, 46);
            Brand.Draw(g, logo);
            Draw.Text(g, AppName, F.B(14f), T.Text, 86, 24, W - 200, 24, Draw.L);
            Draw.Text(g, "句柄 / 线程治理台", F.R(9f), T.Muted, 86, 50, W - 200, 18, Draw.L);
            Draw.Text(g, "版本 " + AppVersion, F.R(8.75f), T.SubText, W - 150, 30, 124, 18, Draw.R);

            using (Pen pen = new Pen(T.BorderSoft, 1f))
                g.DrawLine(pen, 26, 92, W - 26, 92);

            Draw.Text(g, "作者", F.R(8.75f), T.Muted, 28, 104, 40, 18, Draw.L);
            Draw.Text(g, Author, F.B(10f), T.Text, 74, 102, 160, 20, Draw.L);
            Draw.Text(g, "单文件 exe · 零第三方依赖 · 中文界面 · 不联网", F.R(8.75f), T.SubText, 28, 126, W - 56, 18, Draw.L);

            int y = 158;
            Draw.Text(g, "技术栈", F.B(9.5f), T.AccentText, 28, y, W - 56, 18, Draw.L);
            y += 22;
            foreach (string line in TechLines) { Draw.Text(g, line, F.R(9f), T.SubText, 28, y, W - 56, 18, Draw.L); y += 19; }

            y += 8;
            Draw.Text(g, "开源组件与许可", F.B(9.5f), T.AccentText, 28, y, W - 56, 18, Draw.L);
            y += 22;
            foreach (string line in OssLines) { Draw.Text(g, line, F.R(9f), T.SubText, 28, y, W - 56, 18, Draw.L); y += 19; }

            y += 8;
            Draw.Text(g, "使用与风险", F.B(9.5f), T.AccentText, 28, y, W - 56, 18, Draw.L);
            y += 22;
            foreach (string line in RiskLines) { Draw.Text(g, line, F.R(9f), T.SubText, 28, y, W - 56, 18, Draw.L); y += 19; }

            y += 6;
            Draw.Text(g, "当前会话：" + (s_admin ? "管理员模式" : "普通模式（受保护进程的详情不可读）"),
                F.R(8.5f), s_admin ? T.Muted : T.Warn, 28, y, W - 56, 18, Draw.L);
        }

        string PlainText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(AppName + " 句柄 / 线程治理台");
            sb.AppendLine("版本 " + AppVersion);
            sb.AppendLine("作者 " + Author);
            sb.AppendLine();
            sb.AppendLine("技术栈");
            foreach (string l in TechLines) sb.AppendLine(l);
            sb.AppendLine();
            sb.AppendLine("开源组件与许可");
            foreach (string l in OssLines) sb.AppendLine(l);
            sb.AppendLine();
            sb.AppendLine("使用与风险");
            foreach (string l in RiskLines) sb.AppendLine(l);
            sb.AppendLine();
            sb.AppendLine("当前会话：" + (s_admin ? "管理员模式" : "普通模式"));
            return sb.ToString();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84)
            {
                m.Result = (IntPtr)2;   // HTCAPTION，可拖动
                return;
            }
            base.WndProc(ref m);
        }
    }
}
