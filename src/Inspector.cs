using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace HandleGuard
{
    /// <summary>进程全息档案：路径、命令行、父子关系、账户、模块、打开的文件、网络连接。</summary>
    public class ProcProfile
    {
        public int Pid;
        public string Name = "";
        public string Path = "";
        public string CommandLine = "";
        public int ParentPid;
        public string ParentName = "";
        public string ParentPath = "";
        public string Children = "";
        public string User = "";
        public string Started = "";
        public string Uptime = "";
        public string Arch = "";
        public string FileDesc = "";
        public string Company = "";
        public string Product = "";
        public string FileVersion = "";
        public string Note = "";

        public long WorkingSet;
        public long PrivateBytes;
        public int Handles;
        public int Threads;

        public int ModuleCount;
        public int FileCount;
        public int NetCount;

        /// <summary>[类别, 目标, 备注]</summary>
        public List<string[]> Items = new List<string[]>();
    }

    // ------------------------------------------------------------------
    // Win32 / NT 声明（档案用）
    // ------------------------------------------------------------------
    internal static class Insp
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, IntPtr buf, IntPtr size, out IntPtr read);

        [DllImport("ntdll.dll")]
        public static extern int NtQueryInformationProcess(IntPtr h, int cls, IntPtr buf, int len, out int ret);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetFinalPathNameByHandle(IntPtr h, StringBuilder path, int len, int flags);

        [DllImport("kernel32.dll")]
        public static extern int GetFileType(IntPtr h);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool QueryFullProcessImageName(IntPtr h, uint flags, StringBuilder sb, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateToolhelp32Snapshot(uint flags, int pid);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool Module32First(IntPtr snap, IntPtr entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool Module32Next(IntPtr snap, IntPtr entry);

        [DllImport("kernel32.dll")]
        public static extern bool IsWow64Process(IntPtr h, out bool wow64);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(IntPtr h, int access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool GetTokenInformation(IntPtr token, int cls, IntPtr buf, int len, out int ret);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool LookupAccountSid(string sys, IntPtr sid, StringBuilder name, ref int nameLen,
            StringBuilder domain, ref int domLen, out int usage);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern int GetExtendedTcpTable(IntPtr buf, ref int size, bool order, int af, int cls, int reserved);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern int GetExtendedUdpTable(IntPtr buf, ref int size, bool order, int af, int cls, int reserved);

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int access, bool inherit, int pid);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr h);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MODULEENTRY32W
        {
            public int dwSize;
            public int th32ModuleID;
            public int th32ProcessID;
            public int GlblcntUsage;
            public int ProccntUsage;
            public IntPtr modBaseAddr;
            public int modBaseSize;
            public IntPtr hModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_TCPROW_OWNER_PID
        {
            public uint LocalAddr;
            public uint LocalPort;
            public uint RemoteAddr;
            public uint RemotePort;
            public uint State;
            public uint Pid;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_UDPROW_OWNER_PID
        {
            public uint LocalAddr;
            public uint LocalPort;
            public uint Pid;
        }

        public const int PROCESS_DUP_HANDLE = 0x0040;
        public const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const int PROCESS_QUERY_INFORMATION = 0x0400;
        public const int PROCESS_VM_READ = 0x0010;
        public const int TH32CS_SNAPMODULE = 0x00000008;
        public const int TH32CS_SNAPMODULE32 = 0x00000010;
        public const int TOKEN_QUERY = 0x0008;
        public const int TokenUser = 1;
        public const int FILE_TYPE_DISK = 1;
    }

    // ------------------------------------------------------------------
    // 巡检引擎
    // ------------------------------------------------------------------
    public static class Inspector
    {
        public static ProcProfile Load(int pid)
        {
            ProcProfile p = new ProcProfile();
            p.Pid = pid;

            // ---- 基本信息 ----
            try
            {
                using (Process pr = Process.GetProcessById(pid))
                {
                    p.Name = pr.ProcessName;
                    try { p.Handles = pr.HandleCount; } catch { }
                    try { p.Threads = pr.Threads.Count; } catch { }
                    try { p.WorkingSet = pr.WorkingSet64; } catch { }
                    try { p.PrivateBytes = pr.PrivateMemorySize64; } catch { }
                    try
                    {
                        DateTime st = pr.StartTime;
                        p.Started = st.ToString("yyyy-MM-dd HH:mm:ss");
                        TimeSpan up = DateTime.Now - st;
                        p.Uptime = DescribeUptime(up);
                    }
                    catch { }
                }
            }
            catch { }

            if (p.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == false && p.Name.Length > 0)
                p.Name = p.Name + ".exe";

            // ---- 映像路径 / 架构 / 账户 / 版本 ----
            IntPtr h = Insp.OpenProcess(Insp.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) h = Insp.OpenProcess(Insp.PROCESS_QUERY_INFORMATION, false, pid);
            bool is32 = false;
            if (h != IntPtr.Zero)
            {
                try
                {
                    StringBuilder sb = new StringBuilder(1024);
                    int size = sb.Capacity;
                    if (Insp.QueryFullProcessImageName(h, 0, sb, ref size)) p.Path = sb.ToString();

                    bool wow = false;
                    if (Insp.IsWow64Process(h, out wow)) { is32 = wow; p.Arch = wow ? "x86（WOW64）" : "x64"; }
                    p.User = QueryUser(h);
                }
                finally { Insp.CloseHandle(h); }
            }
            if (p.Path.Length == 0) p.Note = "无权读取映像路径（该进程属其他用户或受保护）";

            if (p.Path.Length > 0)
            {
                try
                {
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(p.Path);
                    p.FileDesc = fvi.FileDescription;
                    p.Company = fvi.CompanyName;
                    p.Product = fvi.ProductName;
                    p.FileVersion = fvi.FileVersion;
                }
                catch { }
            }

            // ---- 命令行 + 父子关系 ----
            p.ParentPid = QueryPpid(pid);
            p.CommandLine = QueryCommandLine(pid);
            if (p.ParentPid > 0)
            {
                p.ParentName = SafeName(p.ParentPid);
                p.ParentPath = GetImagePathQuick(p.ParentPid);
            }
            p.Children = QueryChildren(pid);

            // ---- 模块 / 打开的文件 / 网络 ----
            LoadModules(p);
            LoadFileHandles(p);
            LoadNet(p);
            return p;
        }

        // ==============================================================
        static string DescribeUptime(TimeSpan t)
        {
            if (t.TotalDays >= 1) return String.Format("{0} 天 {1} 小时", (int)t.TotalDays, t.Hours);
            if (t.TotalHours >= 1) return String.Format("{0} 小时 {1} 分", (int)t.TotalHours, t.Minutes);
            return String.Format("{0} 分 {1} 秒", t.Minutes, t.Seconds);
        }

        static string SafeName(int pid)
        {
            try
            {
                using (Process pr = Process.GetProcessById(pid))
                    return pr.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        ? pr.ProcessName : pr.ProcessName + ".exe";
            }
            catch { return ""; }
        }

        static string GetImagePathQuick(int pid)
        {
            IntPtr h = Insp.OpenProcess(Insp.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return "";
            try
            {
                StringBuilder sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (Insp.QueryFullProcessImageName(h, 0, sb, ref size)) return sb.ToString();
                return "";
            }
            finally { Insp.CloseHandle(h); }
        }

        // ---------------- 命令行（读 PEB） ----------------
        static bool Rpm(IntPtr h, IntPtr addr, IntPtr buf, int size)
        {
            IntPtr read;
            return Insp.ReadProcessMemory(h, addr, buf, (IntPtr)size, out read) && read.ToInt64() == (long)size;
        }

        static IntPtr ReadRemotePtr(IntPtr h, IntPtr addr, bool is32Target)
        {
            IntPtr buf = Marshal.AllocHGlobal(8);
            try
            {
                if (is32Target)
                {
                    if (!Rpm(h, addr, buf, 4)) return IntPtr.Zero;
                    return (IntPtr)(uint)Marshal.ReadInt32(buf);
                }
                if (!Rpm(h, addr, buf, 8)) return IntPtr.Zero;
                return Marshal.ReadIntPtr(buf);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        static string QueryCommandLine(int pid)
        {
            IntPtr h = Insp.OpenProcess(Insp.PROCESS_QUERY_INFORMATION | Insp.PROCESS_VM_READ, false, pid);
            if (h == IntPtr.Zero)
                h = Insp.OpenProcess(Insp.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return "";
            try
            {
                bool is32 = false;
                bool wow;
                if (Insp.IsWow64Process(h, out wow)) is32 = wow;

                // PROCESS_BASIC_INFORMATION
                int pbiSize = IntPtr.Size == 8 ? 48 : 24;
                IntPtr pbi = Marshal.AllocHGlobal(pbiSize);
                try
                {
                    for (int i = 0; i < pbiSize; i++) Marshal.WriteByte(pbi, i, 0);
                    int ret = 0;
                    int st = Insp.NtQueryInformationProcess(h, 0, pbi, pbiSize, out ret);
                    if (st != 0) return "";
                    IntPtr peb = Marshal.ReadIntPtr(pbi, IntPtr.Size == 8 ? 8 : 4);
                    if (peb == IntPtr.Zero) return "";

                    // PEB -> ProcessParameters
                    IntPtr pp = ReadRemotePtr(h, Add(peb, is32 ? 0x10 : 0x20), is32);
                    if (pp == IntPtr.Zero) return "";

                    // ProcessParameters -> CommandLine (UNICODE_STRING)
                    IntPtr us = Add(pp, is32 ? 0x40 : 0x70);
                    IntPtr buf = Marshal.AllocHGlobal(16);
                    try
                    {
                        if (!Rpm(h, us, buf, is32 ? 8 : 16)) return "";
                        ushort len = (ushort)Marshal.ReadInt16(buf, 0);
                        IntPtr strBuf = is32
                            ? (IntPtr)(uint)Marshal.ReadInt32(buf, 4)
                            : Marshal.ReadIntPtr(buf, 8);
                        if (len == 0 || strBuf == IntPtr.Zero || len > 0x8000) return "";
                        byte[] raw = new byte[len];
                        IntPtr rawPtr = Marshal.AllocHGlobal(len);
                        try
                        {
                            if (!Rpm(h, strBuf, rawPtr, len)) return "";
                            Marshal.Copy(rawPtr, raw, 0, len);
                        }
                        finally { Marshal.FreeHGlobal(rawPtr); }
                        return Encoding.Unicode.GetString(raw);
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
                finally { Marshal.FreeHGlobal(pbi); }
            }
            catch { return ""; }
            finally { Insp.CloseHandle(h); }
        }

        static IntPtr Add(IntPtr p, int off)
        {
            return (IntPtr)(p.ToInt64() + off);
        }

        // ---------------- 父进程 / 子进程 ----------------
        /// <summary>用 NtQueryInformationProcess 取 PPID（对受保护进程可能失败，返回 0）。</summary>
        public static int QueryPpid(int pid)
        {
            IntPtr h = Insp.OpenProcess(Insp.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) h = Insp.OpenProcess(Insp.PROCESS_QUERY_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return 0;
            try
            {
                int pbiSize = IntPtr.Size == 8 ? 48 : 24;
                IntPtr buf = Marshal.AllocHGlobal(pbiSize);
                try
                {
                    for (int i = 0; i < pbiSize; i++) Marshal.WriteByte(buf, i, 0);
                    int ret = 0;
                    if (Insp.NtQueryInformationProcess(h, 0, buf, pbiSize, out ret) != 0) return 0;
                    long v = IntPtr.Size == 8
                        ? Marshal.ReadIntPtr(buf, 40).ToInt64()
                        : Marshal.ReadInt32(buf, 20);
                    return (int)v;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            catch { return 0; }
            finally { Insp.CloseHandle(h); }
        }

        static string QueryChildren(int pid)
        {
            List<string> kids = new List<string>();
            Process[] all;
            try { all = Process.GetProcesses(); }
            catch { return ""; }
            foreach (Process pr in all)
            {
                try
                {
                    int kp = pr.Id;
                    if (kp == pid) continue;
                    if (QueryPpid(kp) != pid) continue;
                    string nm = pr.ProcessName;
                    if (!nm.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) nm += ".exe";
                    kids.Add(nm + " (" + kp + ")");
                }
                catch { }
                finally { try { pr.Dispose(); } catch { } }
            }
            kids.Sort(StringComparer.OrdinalIgnoreCase);
            if (kids.Count == 0) return "";
            if (kids.Count > 6)
                return String.Join("，", kids.ToArray(), 0, 6) + " … 共 " + kids.Count + " 个";
            return String.Join("，", kids.ToArray());
        }

        // ---------------- 模块 ----------------
        static void LoadModules(ProcProfile p)
        {
            IntPtr snap = Insp.CreateToolhelp32Snapshot(Insp.TH32CS_SNAPMODULE | Insp.TH32CS_SNAPMODULE32, p.Pid);
            if (snap == IntPtr.Zero || snap == (IntPtr)(-1)) return;
            int size = Marshal.SizeOf(typeof(Insp.MODULEENTRY32W));
            IntPtr buf = Marshal.AllocHGlobal(size);
            try
            {
                for (int i = 0; i < size; i++) Marshal.WriteByte(buf, i, 0);
                Marshal.WriteInt32(buf, 0, size);
                List<string[]> mods = new List<string[]>();
                if (Insp.Module32First(snap, buf))
                {
                    do
                    {
                        Insp.MODULEENTRY32W me = (Insp.MODULEENTRY32W)Marshal.PtrToStructure(buf, typeof(Insp.MODULEENTRY32W));
                        if (me.szModule != null && me.szModule.Length > 0)
                            mods.Add(new string[] { "模块", me.szModule, me.szExePath });
                    }
                    while (Insp.Module32Next(snap, buf));
                }
                mods.Sort(delegate(string[] a, string[] b) { return string.Compare(a[1], b[1], StringComparison.OrdinalIgnoreCase); });
                p.ModuleCount = mods.Count;
                foreach (string[] m in mods) p.Items.Add(m);

            }
            finally
            {
                Marshal.FreeHGlobal(buf);
                Insp.CloseHandle(snap);
            }
        }

        // ---------------- 打开的文件（磁盘类句柄） ----------------
        static void LoadFileHandles(ProcProfile p)
        {
            string err;
            List<HandleEntry> all = Engine.EnumerateAllHandles(out err);
            List<HandleEntry> mine = new List<HandleEntry>();
            foreach (HandleEntry e in all) if (e.Pid == p.Pid) mine.Add(e);
            if (mine.Count == 0) return;

            IntPtr hProc = Insp.OpenProcess(Insp.PROCESS_DUP_HANDLE, false, p.Pid);
            if (hProc == IntPtr.Zero) return;
            List<string> paths = new List<string>();
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                foreach (HandleEntry e in mine)
                {
                    if (sw.ElapsedMilliseconds > 3000) break;
                    IntPtr dup = IntPtr.Zero;
                    try
                    {
                        if (!Win.DuplicateHandle(hProc, e.HandleValue, Win.GetCurrentProcess(), out dup,
                                0, false, Win.DUPLICATE_SAME_ACCESS)) continue;
                        if (dup == IntPtr.Zero) continue;
                        string typeName;
                        if (!Engine.TryTypeName(dup, out typeName)) continue;
                        if (typeName != "File") continue;
                        if (Insp.GetFileType(dup) != Insp.FILE_TYPE_DISK) continue;   // 管道/设备跳过，避免查名阻塞

                        StringBuilder sb = new StringBuilder(1024);
                        int n = Insp.GetFinalPathNameByHandle(dup, sb, sb.Capacity, 0);
                        if (n <= 0) continue;
                        string path = sb.ToString();
                        if (path.StartsWith(@"\\?\UNC\")) path = @"\\" + path.Substring(8);
                        else if (path.StartsWith(@"\\?\")) path = path.Substring(4);
                        if (path.Length > 0 && !paths.Contains(path)) paths.Add(path);
                    }
                    catch { }
                    finally { if (dup != IntPtr.Zero) Insp.CloseHandle(dup); }
                }
            }
            finally { Insp.CloseHandle(hProc); }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            p.FileCount = paths.Count;
            foreach (string s in paths)
                p.Items.Add(new string[] { "文件", s, "打开中" });
        }

        // ---------------- 网络 ----------------
        static string Ipv4(uint a)
        {
            return String.Format("{0}.{1}.{2}.{3}",
                a & 0xFF, (a >> 8) & 0xFF, (a >> 16) & 0xFF, (a >> 24) & 0xFF);
        }

        static int Ntohs(uint p)
        {
            return (int)(((p & 0xFF) << 8) | ((p >> 8) & 0xFF));
        }

        static string TcpState(uint s)
        {
            switch (s)
            {
                case 1: return "CLOSED";
                case 2: return "LISTEN";
                case 3: return "SYN_SENT";
                case 4: return "SYN_RCVD";
                case 5: return "ESTABLISHED";
                case 6: return "FIN_WAIT1";
                case 7: return "FIN_WAIT2";
                case 8: return "CLOSE_WAIT";
                case 9: return "CLOSING";
                case 10: return "LAST_ACK";
                case 11: return "TIME_WAIT";
                case 12: return "DELETE_TCB";
                default: return s.ToString();
            }
        }

        static void LoadNet(ProcProfile p)
        {
            int size = 0;
            int r = Insp.GetExtendedTcpTable(IntPtr.Zero, ref size, true, 2, 5, 0);
            if (r == 0 && size > 0)
            {
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (Insp.GetExtendedTcpTable(buf, ref size, true, 2, 5, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf, 0);
                        int rowSize = Marshal.SizeOf(typeof(Insp.MIB_TCPROW_OWNER_PID));
                        int baseOff = 4;
                        int nets = 0;
                        for (int i = 0; i < count; i++)
                        {
                            Insp.MIB_TCPROW_OWNER_PID row =
                                (Insp.MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(
                                    (IntPtr)(buf.ToInt64() + baseOff + i * rowSize),
                                    typeof(Insp.MIB_TCPROW_OWNER_PID));
                            if (row.Pid != (uint)p.Pid) continue;
                            string local = Ipv4(row.LocalAddr) + ":" + Ntohs(row.LocalPort);
                            string remote = Ipv4(row.RemoteAddr) + ":" + Ntohs(row.RemotePort);
                            if (row.State == 2) remote = "0.0.0.0:0（监听）";
                            p.Items.Add(new string[] { "网络", local + "  →  " + remote, "TCP " + TcpState(row.State) });
                            nets++;
                        }
                        p.NetCount = nets;
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }

            size = 0;
            r = Insp.GetExtendedUdpTable(IntPtr.Zero, ref size, true, 2, 1, 0);
            if (r == 0 && size > 0)
            {
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (Insp.GetExtendedUdpTable(buf, ref size, true, 2, 1, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf, 0);
                        int rowSize = Marshal.SizeOf(typeof(Insp.MIB_UDPROW_OWNER_PID));
                        for (int i = 0; i < count; i++)
                        {
                            Insp.MIB_UDPROW_OWNER_PID row =
                                (Insp.MIB_UDPROW_OWNER_PID)Marshal.PtrToStructure(
                                    (IntPtr)(buf.ToInt64() + 4 + i * rowSize),
                                    typeof(Insp.MIB_UDPROW_OWNER_PID));
                            if (row.Pid != (uint)p.Pid) continue;
                            p.Items.Add(new string[] { "网络", Ipv4(row.LocalAddr) + ":" + Ntohs(row.LocalPort), "UDP" });
                            p.NetCount++;
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }

        // ---------------- 账户 ----------------
        static string QueryUser(IntPtr hProc)
        {
            IntPtr token = IntPtr.Zero;
            try
            {
                if (!Insp.OpenProcessToken(hProc, Insp.TOKEN_QUERY, out token)) return "";
                int len = 0;
                int ret = 0;
                Insp.GetTokenInformation(token, Insp.TokenUser, IntPtr.Zero, 0, out len);
                if (len <= 0) return "";
                IntPtr buf = Marshal.AllocHGlobal(len);
                try
                {
                    if (!Insp.GetTokenInformation(token, Insp.TokenUser, buf, len, out ret)) return "";
                    IntPtr sid = Marshal.ReadIntPtr(buf, 0);
                    if (sid == IntPtr.Zero) return "";
                    StringBuilder name = new StringBuilder(256);
                    StringBuilder dom = new StringBuilder(256);
                    int nl = name.Capacity, dl = dom.Capacity;
                    int usage;
                    if (Insp.LookupAccountSid(null, sid, name, ref nl, dom, ref dl, out usage))
                        return dom.Length > 0 ? dom + "\\" + name : name.ToString();
                    return "";
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            catch { return ""; }
            finally { if (token != IntPtr.Zero) Insp.CloseHandle(token); }
        }
    }
}
