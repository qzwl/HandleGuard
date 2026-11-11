using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace HandleGuard
{
    public static class Program
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        public static void Main(string[] args)
        {
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); }
            catch
            {
                try { SetProcessDPIAware(); } catch { }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e)
            {
                MessageBox.Show("程序遇到异常：\r\n" + e.Exception.Message + "\r\n\r\n" + e.Exception.StackTrace,
                    "HandleGuard", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                if (ex != null)
                    MessageBox.Show("程序遇到异常：\r\n" + ex.Message, "HandleGuard", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            if (args.Length >= 1 && args[0] == "--snapshot")
            {
                RunSnapshot(args);
                return;
            }

            Application.Run(new MainForm());
        }

        // 自测：把界面离屏渲染成 PNG，并输出诊断日志（不影响用户桌面）
        static void RunSnapshot(string[] args)
        {
            string dir = args.Length >= 2 ? args[1] : ".";
            Directory.CreateDirectory(dir);
            StringBuilder log = new StringBuilder();

            // 自测固定从深色主题开始，避免受上次运行保存的主题影响
            try
            {
                // 设 HG_KEEP_THEME=1 时保留上一次的主题，用来复现“上次用浅色主题”的首次启动画面
                if (Environment.GetEnvironmentVariable("HG_KEEP_THEME") != "1")
                    File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "HandleGuard", "theme.txt"));
            }
            catch { }

            MainForm f = new MainForm();
            ConfirmDlg dlg = null;
            AboutDlg about = null;
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-5000, -5000);
            f.ShowInTaskbar = false;
            f.Show();

            int step = 0;
            Timer tk = new Timer();
            tk.Interval = 1000;
            tk.Tick += delegate
            {
                step++;
                try
                {
                    if (step == 3)
                    {
                        Shot(f, Path.Combine(dir, "ui-1-main-dark.png"));
                        Log(f, log, "main");
                    }
                    else if (step == 4) f.TestSelectFirst();
                    else if (step == 9)
                    {
                        Shot(f, Path.Combine(dir, "ui-2-profile.png"));
                        Log(f, log, "profile");
                    }
                    else if (step == 10) f.TestSetDetailPage(1);
                    else if (step == 12)
                    {
                        Shot(f, Path.Combine(dir, "ui-3-files.png"));
                        Log(f, log, "files");
                    }
                    else if (step == 13) f.TestDetailScrollBottom();
                    else if (step == 14)
                    {
                        Shot(f, Path.Combine(dir, "ui-4-files-bottom.png"));
                        Log(f, log, "files-bottom");
                    }
                    else if (step == 15) f.TestSetDetailPage(2);
                    else if (step == 18)
                    {
                        Shot(f, Path.Combine(dir, "ui-5-handles.png"));
                        Log(f, log, "handles");
                    }
                    else if (step == 19) f.TestSetDetailPage(3);
                    else if (step == 21)
                    {
                        Shot(f, Path.Combine(dir, "ui-6-threads.png"));
                        Log(f, log, "threads");
                    }
                    else if (step == 22) f.TestToggleTheme();
                    else if (step == 23)
                    {
                        Shot(f, Path.Combine(dir, "ui-7-light.png"));
                        Log(f, log, "light");
                    }
                    else if (step == 24)
                    {
                        f.TestSetDetailPage(2);
                        dlg = ConfirmDlg.CreateInstance(f.CurrentTheme, "结束进程",
                            "将强制结束进程 DisplayFusion.ui (PID 5472)。\r\n未保存的工作会丢失，继续吗？", "结束进程", true);
                        dlg.StartPosition = FormStartPosition.Manual;
                        dlg.Location = new Point(-5000, -5000);
                        dlg.ShowInTaskbar = false;
                        dlg.Show();
                    }
                    else if (step == 25)
                    {
                        Bitmap d = new Bitmap(dlg.Width, dlg.Height);
                        dlg.DrawToBitmap(d, new Rectangle(0, 0, dlg.Width, dlg.Height));
                        d.Save(Path.Combine(dir, "ui-8-confirm.png"), ImageFormat.Png);
                        d.Dispose();
                        Bitmap cap = f.TestCaptionIcons();
                        cap.Save(Path.Combine(dir, "ui-9-caption.png"), ImageFormat.Png);
                        cap.Dispose();

                        dlg.Close();
                        dlg = ConfirmDlg.CreateInfo(f.CurrentTheme, "内存与缓存治理完成",
                            "已修剪 128 个进程的工作集\r\n系统文件缓存已清空\r\n待机内存列表已清空\r\n\r\n约 3.1 GB 工作集已归还系统，可用内存随即上升。");
                        dlg.StartPosition = FormStartPosition.Manual;
                        dlg.Location = new Point(-5000, -5000);
                        dlg.ShowInTaskbar = false;
                        dlg.Show();
                    }
                    else if (step == 26)
                    {
                        Bitmap d = new Bitmap(dlg.Width, dlg.Height);
                        dlg.DrawToBitmap(d, new Rectangle(0, 0, dlg.Width, dlg.Height));
                        d.Save(Path.Combine(dir, "ui-10-info.png"), ImageFormat.Png);
                        d.Dispose();

                        about = AboutDlg.CreateInstance(f.CurrentTheme, false);
                        about.StartPosition = FormStartPosition.Manual;
                        about.Location = new Point(-5000, -5000);
                        about.ShowInTaskbar = false;
                        about.Show();
                    }
                    else if (step == 27)
                    {
                        Bitmap d = new Bitmap(about.Width, about.Height);
                        about.DrawToBitmap(d, new Rectangle(0, 0, about.Width, about.Height));
                        d.Save(Path.Combine(dir, "ui-11-about.png"), ImageFormat.Png);
                        d.Dispose();
                        Log(f, log, "about");
                    }
                    else if (step == 28)
                    {
                        about.Close();
                        dlg.Close();
                        // 取消选中：核对"未选中进程"时档案页的空状态文案
                        f.TestSetDetailPage(0);
                        f.TestClearSelection();
                    }
                    else if (step == 29)
                    {
                        Shot(f, Path.Combine(dir, "ui-12-empty-profile.png"));
                        Log(f, log, "empty-profile");
                    }
                    else if (step == 30) f.TestSortColumn(2);
                    else if (step == 31)
                    {
                        Shot(f, Path.Combine(dir, "ui-13-sort.png"));
                        Log(f, log, "sort");
                    }
                    else if (step == 32)
                    {
                        tk.Stop();
                        File.WriteAllText(Path.Combine(dir, "selftest.log"), log.ToString(), new UTF8Encoding(true));
                        f.Close();
                        Application.Exit();
                    }
                }
                catch (Exception ex)
                {
                    log.AppendLine("SNAPSHOT-ERR " + ex.Message);
                }
            };
            tk.Start();
            Application.Run(f);
        }

        static void Shot(Form f, string path)
        {
            Bitmap bmp = new Bitmap(f.Width, f.Height);
            using (Graphics g = Graphics.FromImage(bmp)) g.Clear(Color.Black);
            f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
            bmp.Save(path, ImageFormat.Png);
            bmp.Dispose();
        }

        static void Log(MainForm f, StringBuilder log, string tag)
        {
            log.AppendLine("=== " + tag + " === " + f.TestDiag());
        }
    }
}
