using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HandleGuard
{
    // 自绘确认对话框
    public class ConfirmDlg : Form
    {
        Theme T;
        string s_title, s_msg;
        FlatButton btnOk, btnCancel;
        bool s_danger;

        ConfirmDlg(Theme t, string title, string msg, string okText, bool danger, bool showCancel, bool info)
        {
            T = t;
            s_title = title;
            s_msg = msg;
            s_danger = danger;

            int lines = msg.Replace("\r", "").Split('\n').Length;
            int h = 62 + lines * 21 + 16 + 38 + 12;
            if (h < 200) h = 200;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = t.Bg;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            ClientSize = new Size(460, info ? h : 216);
            KeyPreview = true;

            btnOk = new FlatButton(t);
            btnOk.Text = okText;
            btnOk.Tone = danger ? BtnTone.Danger : BtnTone.Primary;
            btnOk.BackColor = t.Panel;
            btnOk.Bounds = new Rectangle(ClientSize.Width - 12 - 108, ClientSize.Height - 12 - 38, 108, 38);
            btnOk.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(btnOk);

            btnCancel = new FlatButton(t);
            btnCancel.Text = "取消";
            btnCancel.Tone = BtnTone.Ghost;
            btnCancel.BackColor = t.Panel;
            btnCancel.Bounds = new Rectangle(ClientSize.Width - 12 - 108 - 8 - 88, ClientSize.Height - 12 - 38, 88, 38);
            btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);

            if (!showCancel)
            {
                btnCancel.Visible = false;
                btnOk.Bounds = new Rectangle((ClientSize.Width - 108) / 2, ClientSize.Height - 12 - 38, 108, 38);
            }
        }

        public static bool Show(IWin32Window owner, Theme t, string title, string msg, string okText, bool danger)
        {
            ConfirmDlg d = new ConfirmDlg(t, title, msg, okText, danger, true, false);
            return d.ShowDialog(owner) == DialogResult.OK;
        }

        public static ConfirmDlg CreateInstance(Theme t, string title, string msg, string okText, bool danger)
        {
            return new ConfirmDlg(t, title, msg, okText, danger, true, false);
        }

        // 只有“知道了”一个按钮的结果提示框（清理类操作完成后用来明确告知结果）
        public static ConfirmDlg CreateInfo(Theme t, string title, string msg)
        {
            return new ConfirmDlg(t, title, msg, "知道了", false, false, true);
        }

        public static void Info(IWin32Window owner, Theme t, string title, string msg)
        {
            CreateInfo(t, title, msg).ShowDialog(owner);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(T.Bg);
            Rectangle r = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            Draw.FillRound(g, r, 10, T.Panel);
            Draw.StrokeRound(g, r, 10, T.Border);

            // 左侧强调条：裁到圆角内，否则会在圆角外侧露出一个方形色块（看起来像“漏底”）
            GraphicsState gs = g.Save();
            using (GraphicsPath cp = Draw.RoundRect(r, 10)) g.SetClip(cp);
            using (SolidBrush b = new SolidBrush(s_danger ? T.Bad : T.Accent))
                g.FillRectangle(b, 0, 0, 4, ClientSize.Height);
            g.Restore(gs);

            Draw.Text(g, s_title, F.B(11.5f), T.Text, 22, 18, ClientSize.Width - 44, 24, Draw.L);

            int y = 50;
            string[] lines = s_msg.Replace("\r", "").Split('\n');
            foreach (string line in lines)
            {
                Draw.Text(g, line, F.R(9.25f), T.SubText, 22, y, ClientSize.Width - 44, 20, Draw.L);
                y += 20;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            base.OnKeyDown(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84)
            {
                m.Result = (IntPtr)2;
                return;
            }
            base.WndProc(ref m);
        }
    }
}
