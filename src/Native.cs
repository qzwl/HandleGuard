using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace HandleGuard
{
    // ------------------------------------------------------------------
    // 底层 Win32 / NT 调用
    // ------------------------------------------------------------------
    internal static class Win
    {
        public const int PROCESS_QUERY_INFORMATION = 0x0400;
        public const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const int PROCESS_SET_QUOTA = 0x0100;
        public const int PROCESS_DUP_HANDLE = 0x0040;
        public const int PROCESS_CREATE_THREAD = 0x0002;
        public const int PROCESS_VM_OPERATION = 0x0008;
        public const int PROCESS_VM_WRITE = 0x0020;
        public const int PROCESS_TERMINATE = 0x0001;

        public const int THREAD_QUERY_INFORMATION = 0x0040;
        public const int THREAD_QUERY_LIMITED_INFORMATION = 0x0800;
        public const int THREAD_SUSPEND_RESUME = 0x0002;
        public const int THREAD_TERMINATE = 0x0001;

        public const int DUPLICATE_SAME_ACCESS = 0x00000002;
        public const uint TH32CS_SNAPTHREAD = 0x00000004;

        // ---- process ----
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(int access, bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr h);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DuplicateHandle(IntPtr srcProc, IntPtr srcHandle,
            IntPtr dstProc, out IntPtr dstHandle, int access, bool inherit, int options);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string name);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr mod, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr attr, int stackSize,
            IntPtr startAddr, IntPtr param, uint flags, out int threadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr h, uint ms);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetExitCodeThread(IntPtr h, out int code);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool TerminateProcess(IntPtr h, uint code);

        // ---- memory ----
        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX st);

        [StructLayout(LayoutKind.Sequential)]
        public struct PERFORMANCE_INFORMATION
        {
            public IntPtr CommitTotal;
            public IntPtr CommitLimit;
            public IntPtr CommitPeak;
            public IntPtr PhysicalTotal;
            public IntPtr PhysicalAvailable;
            public IntPtr SystemCache;
            public IntPtr KernelTotal;
            public IntPtr KernelPaged;
            public IntPtr KernelNonpaged;
            public IntPtr PageSize;
            public uint HandleCount;
            public uint ProcessCount;
            public uint ThreadCount;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION info, int size);

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr min, IntPtr max);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetSystemFileCacheSize(IntPtr min, IntPtr max, uint flags);

        [DllImport("ntdll.dll")]
        public static extern int NtSetSystemInformation(int infoClass, ref int info, int len);

        [DllImport("ntdll.dll")]
        public static extern int NtQuerySystemInformation(int infoClass, IntPtr info, int len, out int retLen);

        [DllImport("ntdll.dll")]
        public static extern int NtQueryObject(IntPtr handle, int infoClass, IntPtr info, int len, out int retLen);

        [DllImport("ntdll.dll")]
        public static extern int NtQueryInformationThread(IntPtr handle, int infoClass, ref IntPtr info,
            int len, out int retLen);

        // ---- 令牌特权 ----
        // 管理员令牌里 SeIncreaseQuotaPrivilege / SeProfileSingleProcessPrivilege 默认是“已禁用”状态，
        // 不显式启用的话，下面两个清理调用必然失败，看起来就像“没有管理员权限”。
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LookupPrivilegeValue(string system, string name, out long luid);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TOKEN_PRIVILEGES
        {
            public int Count;
            public long Luid;
            public int Attributes;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll,
            ref TOKEN_PRIVILEGES newState, int len, IntPtr prev, IntPtr retLen);

        public static bool EnablePrivilege(string name)
        {
            IntPtr token = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), 0x0028, out token)) return false; // ADJUST+QUERY
                long luid;
                if (!LookupPrivilegeValue(null, name, out luid)) return false;
                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                tp.Count = 1;
                tp.Luid = luid;
                tp.Attributes = 2; // SE_PRIVILEGE_ENABLED
                if (!AdjustTokenPrivileges(token, false, ref tp, 16, IntPtr.Zero, IntPtr.Zero)) return false;
                return Marshal.GetLastWin32Error() == 0; // 1300 = ERROR_NOT_ALL_ASSIGNED
            }
            catch { return false; }
            finally { if (token != IntPtr.Zero) CloseHandle(token); }
        }

        // ---- cpu ----
        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public uint Low;
            public uint High;
            public long Value { get { return ((long)High << 32) | Low; } }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

        // ---- threads ----
        [StructLayout(LayoutKind.Sequential)]
        public struct THREADENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ThreadID;
            public uint th32OwnerProcessID;
            public int tpBasePri;
            public int tpDeltaPri;
            public uint dwFlags;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateToolhelp32Snapshot(uint flags, int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool Thread32First(IntPtr snap, IntPtr entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool Thread32Next(IntPtr snap, IntPtr entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenThread(int access, bool inherit, int tid);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetThreadTimes(IntPtr hThread, out FILETIME create,
            out FILETIME exit, out FILETIME kernel, out FILETIME user);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int GetThreadDescription(IntPtr hThread, out IntPtr pDesc);

        // ---- window / process list helpers ----
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool IsWow64Process(IntPtr hProcess, out bool wow64);

        [DllImport("kernel32.dll")]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool QueryFullProcessImageName(IntPtr hProcess, uint flags,
            StringBuilder name, ref uint size);
    }

    // ------------------------------------------------------------------
    // 数据模型
    // ------------------------------------------------------------------
    public class HandleEntry
    {
        public int Pid;
        public IntPtr HandleValue;
        public IntPtr ObjectAddress;
        public ushort TypeIndex;
        public uint GrantedAccess;
        public string TypeName = "—";
        public string ObjectName = "";
        public bool Enriched;
        public bool IsDuplicate;
        public int GroupCount;
        public bool DuplicateClosed;
    }

    public class ThreadEntry
    {
        public int Tid;
        public int Pid;
        public int Priority;
        public long CpuMs;
        public IntPtr StartAddress;
        public string Description = "";
        public bool QueryLimited;
    }

    public class ProcSample
    {
        public int Pid;
        public string Name = "";
        public string Path = "";
        public int Handles;
        public int Threads;
        public long WorkingSet;
        public double Cpu;
        public int HandlesDelta;
        public int ThreadsDelta;
        public bool HandlesGrowing { get { return HandlesDelta > 0; } }
    }

    public class SystemSnapshot
    {
        public long HandleCount;
        public long ThreadCount;
        public long ProcessCount;
        public long PhysicalTotal;
        public long PhysicalAvailable;
        public long CommitTotal;
        public long CommitLimit;
        public long SystemCache;
        public double CpuUsage;
        public long SampleTick;
        public string Error;
    }

    public class CleanReport
    {
        public int ProcessesTrimmed;
        public long BytesReleased;
        public bool CacheCleared;
        public bool StandbyPurged;
        public int CacheError;      // 0 表示成功；否则是 Win32 错误码
        public int StandbyError;    // 0 表示成功；否则是 NTSTATUS
        public bool QuotaPriv;      // SeIncreaseQuotaPrivilege 是否启用成功
        public bool ProfilePriv;    // SeProfileSingleProcessPrivilege 是否启用成功
        public string Detail = "";
    }

    // ------------------------------------------------------------------
    // 引擎：采样 / 枚举 / 处置
    // ------------------------------------------------------------------
    public static class Engine
    {
        static Dictionary<int, TimeSpan> s_prevProcCpu = new Dictionary<int, TimeSpan>();
        static long s_prevIdle, s_prevKernel, s_prevUser;
        static long s_prevSampleTime;
        static bool s_cpuInit;

        // ---------- 系统总览 ----------
        public static SystemSnapshot GetSystemSnapshot()
        {
            SystemSnapshot s = new SystemSnapshot();
            s.SampleTick = Environment.TickCount;
            try
            {
                Win.PERFORMANCE_INFORMATION pi;
                if (Win.GetPerformanceInfo(out pi, Marshal.SizeOf(typeof(Win.PERFORMANCE_INFORMATION))))
                {
                    s.HandleCount = (long)pi.HandleCount;
                    s.ThreadCount = (long)pi.ThreadCount;
                    s.ProcessCount = (long)pi.ProcessCount;
                    s.SystemCache = (long)pi.SystemCache * (long)pi.PageSize;
                }
                Win.MEMORYSTATUSEX ms = new Win.MEMORYSTATUSEX();
                ms.dwLength = (uint)Marshal.SizeOf(typeof(Win.MEMORYSTATUSEX));
                if (Win.GlobalMemoryStatusEx(ref ms))
                {
                    s.PhysicalTotal = (long)ms.ullTotalPhys;
                    s.PhysicalAvailable = (long)ms.ullAvailPhys;
                    s.CommitTotal = (long)(ms.ullTotalPageFile - ms.ullAvailPageFile);
                    s.CommitLimit = (long)ms.ullTotalPageFile;
                }
            }
            catch (Exception ex)
            {
                s.Error = ex.Message;
            }
            s.CpuUsage = ReadCpuUsage();
            return s;
        }

        static double ReadCpuUsage()
        {
            Win.FILETIME idle, kernel, user;
            if (!Win.GetSystemTimes(out idle, out kernel, out user)) return 0;
            long i = idle.Value, k = kernel.Value, u = user.Value;
            if (!s_cpuInit)
            {
                s_prevIdle = i; s_prevKernel = k; s_prevUser = u; s_cpuInit = true;
                return 0;
            }
            long di = i - s_prevIdle, dk = k - s_prevKernel, du = u - s_prevUser;
            s_prevIdle = i; s_prevKernel = k; s_prevUser = u;
            double total = (double)(dk + du);
            if (total <= 0) return 0;
            double busy = total - (double)di;
            double pct = busy / total * 100.0;
            if (pct < 0) pct = 0;
            if (pct > 100) pct = 100;
            return pct;
        }

        // ---------- 进程采样 ----------
        public static List<ProcSample> SampleProcesses(Dictionary<int, int[]> baseline)
        {
            List<ProcSample> list = new List<ProcSample>(256);
            Process[] procs = Process.GetProcesses();
            long nowTicks = Stopwatch.GetTimestamp();
            double elapsedMs = 0;
            if (s_prevSampleTime > 0)
                elapsedMs = (nowTicks - s_prevSampleTime) * 1000.0 / (double)Stopwatch.Frequency;
            s_prevSampleTime = nowTicks;
            int cores = Environment.ProcessorCount;
            foreach (Process p in procs)
            {
                ProcSample ps = new ProcSample();
                try
                {
                    ps.Pid = p.Id;
                    ps.Name = SafeName(p);
                    ps.Handles = p.HandleCount;
                    ps.Threads = p.Threads.Count;
                    ps.WorkingSet = p.WorkingSet64;
                    TimeSpan cpu = TimeSpan.Zero;
                    try { cpu = p.TotalProcessorTime; }
                    catch { }
                    TimeSpan prev = TimeSpan.Zero;
                    if (s_prevProcCpu.ContainsKey(ps.Pid)) prev = s_prevProcCpu[ps.Pid];
                    TimeSpan delta = cpu - prev;
                    ps.Cpu = 0;
                    if (elapsedMs > 80 && delta.TotalMilliseconds >= 0)
                    {
                        double pct = delta.TotalMilliseconds / elapsedMs * 100.0 / cores;
                        ps.Cpu = pct < 0 ? 0 : (pct > 100 ? 100 : pct);
                    }
                    s_prevProcCpu[ps.Pid] = cpu;
                    ps.Path = SafePath(p);
                    if (baseline != null && baseline.ContainsKey(ps.Pid))
                    {
                        int[] b = baseline[ps.Pid];
                        ps.HandlesDelta = ps.Handles - b[0];
                        ps.ThreadsDelta = ps.Threads - b[1];
                    }
                }
                catch { }
                finally { p.Dispose(); }
                if (ps.Pid != 0) list.Add(ps);
            }
            return list;
        }

        static string SafeName(Process p)
        {
            try { return p.ProcessName; }
            catch { return "pid" + p.Id; }
        }

        static string SafePath(Process p)
        {
            try
            {
                IntPtr h = Win.OpenProcess(Win.PROCESS_QUERY_LIMITED_INFORMATION, false, p.Id);
                if (h == IntPtr.Zero) return "";
                try
                {
                    StringBuilder sb = new StringBuilder(1024);
                    uint size = 1024;
                    if (Win.QueryFullProcessImageName(h, 0, sb, ref size)) return sb.ToString();
                }
                finally { Win.CloseHandle(h); }
            }
            catch { }
            return "";
        }

        // ---------- 句柄枚举 ----------
        public static bool HasDebugPrivilege()
        {
            IntPtr token = IntPtr.Zero;
            try
            {
                return true;
            }
            catch { return false; }
        }

        public static List<HandleEntry> EnumerateAllHandles(out string error)
        {
            error = null;
            List<HandleEntry> result = new List<HandleEntry>(4096);
            int len = 2 << 20;
            IntPtr buf = Marshal.AllocHGlobal(len);
            int retLen = 0;
            int status = -1;
            try
            {
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    status = Win.NtQuerySystemInformation(64, buf, len, out retLen);
                    if (status == 0) break;
                    if (status == unchecked((int)0xC0000004) || retLen > len)
                    {
                        Marshal.FreeHGlobal(buf);
                        len = retLen + (1 << 20);
                        buf = Marshal.AllocHGlobal(len);
                        continue;
                    }
                    break;
                }
                if (status != 0)
                {
                    error = status == unchecked((int)0xC0000022)
                        ? "缺少管理员权限，无法读取系统句柄表"
                        : "NtQuerySystemInformation 失败 (0x" + status.ToString("X8") + ")";
                    return result;
                }
                long count = Marshal.ReadIntPtr(buf, 0).ToInt64();
                if (count <= 0) return result;
                long entriesStart = (long)buf + IntPtr.Size * 2;
                int size = IntPtr.Size == 8 ? 40 : 24;
                int oObj = 0;
                int oPid = IntPtr.Size;
                int oHandle = IntPtr.Size * 2;
                int oAccess = IntPtr.Size * 3;
                int oType = IntPtr.Size == 8 ? 30 : 14;

                for (long i = 0; i < count; i++)
                {
                    long b = entriesStart + i * size;
                    long pid = Marshal.ReadIntPtr(buf, (int)(b - (long)buf) + oPid).ToInt64();
                    if (pid <= 0) continue;
                    HandleEntry he = new HandleEntry();
                    he.Pid = (int)pid;
                    he.ObjectAddress = Marshal.ReadIntPtr(buf, (int)(b - (long)buf) + oObj);
                    he.HandleValue = Marshal.ReadIntPtr(buf, (int)(b - (long)buf) + oHandle);
                    he.GrantedAccess = (uint)Marshal.ReadInt32(buf, (int)(b - (long)buf) + oAccess);
                    he.TypeIndex = (ushort)Marshal.ReadInt16(buf, (int)(b - (long)buf) + oType);
                    result.Add(he);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
            return result;
        }

        static Dictionary<ushort, string> s_typeNames = new Dictionary<ushort, string>();

        static bool QueryTypeName(IntPtr objHandle, out string name)
        {
            name = null;
            IntPtr buf = Marshal.AllocHGlobal(4096);
            int ret = 0;
            try
            {
                int st = Win.NtQueryObject(objHandle, 2, buf, 4096, out ret);
                if (st != 0) return false;
                ushort len = (ushort)Marshal.ReadInt16(buf, 0);
                IntPtr strBuf = Marshal.ReadIntPtr(buf, IntPtr.Size == 8 ? 8 : 4);
                if (len == 0 || strBuf == IntPtr.Zero) return false;
                name = Marshal.PtrToStringUni(strBuf, len / 2);
                return true;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        static bool QueryObjectName(IntPtr objHandle, out string name)
        {
            name = null;
            IntPtr buf = Marshal.AllocHGlobal(8192);
            int ret = 0;
            try
            {
                int st = Win.NtQueryObject(objHandle, 1, buf, 8192, out ret);
                if (st != 0) return false;
                ushort len = (ushort)Marshal.ReadInt16(buf, 0);
                IntPtr strBuf = Marshal.ReadIntPtr(buf, IntPtr.Size == 8 ? 8 : 4);
                if (len == 0 || strBuf == IntPtr.Zero) return false;
                name = Marshal.PtrToStringUni(strBuf, len / 2);
                return true;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        static bool NameQuerySafe(string type)
        {
            switch (type)
            {
                case "Event":
                case "Mutant":
                case "Semaphore":
                case "Section":
                case "Timer":
                case "Key":
                case "Token":
                case "Directory":
                case "WindowStation":
                case "Desktop":
                case "Job":
                case "IoCompletion":
                case "ALPC Port":
                case "Process":
                case "Thread":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>查询一个已复制到本进程的句柄的类型名（档案页枚举 File 句柄时用）。</summary>
        public static bool TryTypeName(IntPtr dup, out string name)
        {
            return QueryTypeName(dup, out name);
        }

        /// <summary>为给定句柄补充类型名 / 对象名。budgetMs 为时间预算，超时后剩余项保持未填充。</summary>
        public static void EnrichHandles(List<HandleEntry> entries, int budgetMs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Dictionary<int, IntPtr> procHandles = new Dictionary<int, IntPtr>();
            try
            {
                foreach (HandleEntry e in entries)
                {
                    if (sw.ElapsedMilliseconds > budgetMs) break;
                    e.Enriched = true;
                    IntPtr hProc;
                    if (!procHandles.ContainsKey(e.Pid))
                    {
                        hProc = Win.OpenProcess(Win.PROCESS_DUP_HANDLE, false, e.Pid);
                        procHandles[e.Pid] = hProc;
                    }
                    else hProc = procHandles[e.Pid];
                    if (hProc == IntPtr.Zero) continue;

                    IntPtr dup = IntPtr.Zero;
                    try
                    {
                        bool ok = Win.DuplicateHandle(hProc, e.HandleValue, Win.GetCurrentProcess(),
                            out dup, 0, false, Win.DUPLICATE_SAME_ACCESS);
                        if (!ok || dup == IntPtr.Zero) continue;

                        string typeName = null;
                        if (s_typeNames.ContainsKey(e.TypeIndex)) typeName = s_typeNames[e.TypeIndex];
                        else if (QueryTypeName(dup, out typeName)) s_typeNames[e.TypeIndex] = typeName;
                        if (typeName != null) e.TypeName = typeName;

                        if (typeName != null && NameQuerySafe(typeName))
                        {
                            string objName;
                            if (QueryObjectName(dup, out objName)) e.ObjectName = objName;
                        }
                    }
                    finally
                    {
                        if (dup != IntPtr.Zero) Win.CloseHandle(dup);
                    }
                }
            }
            finally
            {
                foreach (KeyValuePair<int, IntPtr> kv in procHandles)
                    if (kv.Value != IntPtr.Zero) Win.CloseHandle(kv.Value);
            }
        }

        /// <summary>
        /// 标记同一进程内重复持有的句柄。
        /// 管理员下 NtQuerySystemInformation 会给出真实内核对象地址，可直接按地址分组；
        /// 非管理员时该字段被系统清零，此时退化为「类型 + 对象名」分组（仅统计有名字的对象）。
        /// </summary>
        public static int MarkDuplicates(List<HandleEntry> entries)
        {
            bool useAddress = false;
            foreach (HandleEntry e in entries)
            {
                if (e.ObjectAddress != IntPtr.Zero) { useAddress = true; break; }
            }

            Dictionary<string, List<HandleEntry>> groups = new Dictionary<string, List<HandleEntry>>();
            foreach (HandleEntry e in entries)
            {
                string key;
                if (useAddress)
                {
                    if (e.ObjectAddress == IntPtr.Zero) continue;
                    key = "A:" + e.ObjectAddress.ToInt64().ToString("X");
                }
                else
                {
                    if (string.IsNullOrEmpty(e.ObjectName)) continue;
                    key = "N:" + e.TypeIndex + ":" + e.ObjectName;
                }
                if (!groups.ContainsKey(key)) groups[key] = new List<HandleEntry>();
                groups[key].Add(e);
            }

            int dup = 0;
            foreach (KeyValuePair<string, List<HandleEntry>> kv in groups)
            {
                int n = kv.Value.Count;
                foreach (HandleEntry e in kv.Value) e.GroupCount = n;
                if (n > 1)
                {
                    for (int i = 1; i < n; i++)
                    {
                        kv.Value[i].IsDuplicate = true;
                        dup++;
                    }
                }
            }
            return dup;
        }

        // ---------- 线程枚举 ----------
        public static List<ThreadEntry> EnumerateThreads(int pid)
        {
            List<ThreadEntry> list = new List<ThreadEntry>();
            IntPtr snap = Win.CreateToolhelp32Snapshot(Win.TH32CS_SNAPTHREAD, 0);
            if (snap == IntPtr.Zero) return list;
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Win.THREADENTRY32)));
            try
            {
                Win.THREADENTRY32 te = new Win.THREADENTRY32();
                te.dwSize = (uint)Marshal.SizeOf(typeof(Win.THREADENTRY32));
                Marshal.StructureToPtr(te, ptr, false);
                bool ok = Win.Thread32First(snap, ptr);
                while (ok)
                {
                    te = (Win.THREADENTRY32)Marshal.PtrToStructure(ptr, typeof(Win.THREADENTRY32));
                    if ((int)te.th32OwnerProcessID == pid)
                    {
                        ThreadEntry ent = new ThreadEntry();
                        ent.Tid = (int)te.th32ThreadID;
                        ent.Pid = pid;
                        ent.Priority = te.tpBasePri;
                        FillThreadDetail(ent);
                        list.Add(ent);
                    }
                    te.dwSize = (uint)Marshal.SizeOf(typeof(Win.THREADENTRY32));
                    Marshal.StructureToPtr(te, ptr, false);
                    ok = Win.Thread32Next(snap, ptr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
                Win.CloseHandle(snap);
            }
            return list;
        }

        static void FillThreadDetail(ThreadEntry ent)
        {
            IntPtr h = Win.OpenThread(Win.THREAD_QUERY_INFORMATION, false, ent.Tid);
            if (h == IntPtr.Zero) h = Win.OpenThread(Win.THREAD_QUERY_LIMITED_INFORMATION, false, ent.Tid);
            if (h == IntPtr.Zero) return;
            try
            {
                Win.FILETIME c, e, k, u;
                if (Win.GetThreadTimes(h, out c, out e, out k, out u))
                    ent.CpuMs = (k.Value + u.Value) / 10000L;
                IntPtr addr = IntPtr.Zero;
                int ret = 0;
                int st = Win.NtQueryInformationThread(h, 9, ref addr, IntPtr.Size, out ret);
                if (st == 0) ent.StartAddress = addr;
                IntPtr desc = IntPtr.Zero;
                if (Win.GetThreadDescription(h, out desc) >= 0 && desc != IntPtr.Zero)
                {
                    ent.Description = Marshal.PtrToStringUni(desc);
                    try { Marshal.FreeHGlobal(desc); } catch { }
                }
            }
            catch { }
            finally { Win.CloseHandle(h); }
        }

        // ---------- 处置动作 ----------
        public static string KillProcess(int pid)
        {
            IntPtr h = Win.OpenProcess(Win.PROCESS_TERMINATE | Win.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return "无法打开进程 (错误 " + Marshal.GetLastWin32Error() + ")";
            try
            {
                if (!Win.TerminateProcess(h, 1)) return "结束失败 (错误 " + Marshal.GetLastWin32Error() + ")";
                return "已结束";
            }
            finally { Win.CloseHandle(h); }
        }

        public static string SuspendThreadByTid(int tid)
        {
            IntPtr h = Win.OpenThread(Win.THREAD_SUSPEND_RESUME, false, tid);
            if (h == IntPtr.Zero) return "无法打开线程 (错误 " + Marshal.GetLastWin32Error() + ")";
            try
            {
                int prev = Win.SuspendThread(h);
                if (prev < 0) return "挂起失败 (错误 " + Marshal.GetLastWin32Error() + ")";
                return "已挂起 (原挂起计数 " + prev + ")";
            }
            finally { Win.CloseHandle(h); }
        }

        public static string ResumeThreadByTid(int tid)
        {
            IntPtr h = Win.OpenThread(Win.THREAD_SUSPEND_RESUME, false, tid);
            if (h == IntPtr.Zero) return "无法打开线程 (错误 " + Marshal.GetLastWin32Error() + ")";
            try
            {
                int prev = Win.ResumeThread(h);
                if (prev < 0) return "恢复失败 (错误 " + Marshal.GetLastWin32Error() + ")";
                return "已恢复 (原挂起计数 " + prev + ")";
            }
            finally { Win.CloseHandle(h); }
        }

        /// <summary>
        /// 通过向目标进程注入一次 CloseHandle 调用来关闭它持有的句柄。
        /// 仅用于关闭确认无害的重复句柄，可能使目标程序崩溃。
        /// </summary>
        public static string CloseHandleInProcess(int pid, IntPtr handle)
        {
            IntPtr hp = Win.OpenProcess(Win.PROCESS_CREATE_THREAD | Win.PROCESS_QUERY_INFORMATION |
                                        Win.PROCESS_VM_OPERATION | Win.PROCESS_VM_WRITE, false, pid);
            if (hp == IntPtr.Zero) return "无法打开进程 (错误 " + Marshal.GetLastWin32Error() + ")";
            try
            {
                bool wow = false;
                if (Win.IsWow64Process(hp, out wow) && wow && IntPtr.Size == 8)
                    return "目标为 32 位进程，跨架构关闭句柄不受支持";

                IntPtr k32 = Win.GetModuleHandle("kernel32.dll");
                IntPtr addr = Win.GetProcAddress(k32, "CloseHandle");
                if (addr == IntPtr.Zero) return "找不到 CloseHandle 地址";

                int tid = 0;
                IntPtr th = Win.CreateRemoteThread(hp, IntPtr.Zero, 0, addr, handle, 0, out tid);
                if (th == IntPtr.Zero) return "注入失败 (错误 " + Marshal.GetLastWin32Error() + ")";
                try
                {
                    Win.WaitForSingleObject(th, 3000);
                    int code = 0;
                    Win.GetExitCodeThread(th, out code);
                    return code != 0 ? "句柄已关闭" : "目标进程返回失败（句柄可能已失效）";
                }
                finally { Win.CloseHandle(th); }
            }
            finally { Win.CloseHandle(hp); }
        }

        // ---------- 系统级内存整理 ----------
        public static CleanReport ReleaseMemory(bool trimWorkingSets, bool clearCache, bool purgeStandby)
        {
            CleanReport rep = new CleanReport();
            StringBuilder sb = new StringBuilder();
            if (trimWorkingSets)
            {
                Process[] procs = Process.GetProcesses();
                foreach (Process p in procs)
                {
                    if (p.Id == Process.GetCurrentProcess().Id) { p.Dispose(); continue; }
                    try
                    {
                        long ws = p.WorkingSet64;
                        if (ws > 40L * 1024 * 1024)
                        {
                            IntPtr h = Win.OpenProcess(Win.PROCESS_QUERY_INFORMATION | Win.PROCESS_SET_QUOTA,
                                false, p.Id);
                            if (h != IntPtr.Zero)
                            {
                                try
                                {
                                    if (Win.EmptyWorkingSet(h))
                                    {
                                        rep.ProcessesTrimmed++;
                                        rep.BytesReleased += ws;
                                    }
                                }
                                finally { Win.CloseHandle(h); }
                            }
                        }
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
                sb.Append("已修剪 " + rep.ProcessesTrimmed + " 个进程的工作集；");
            }
            if (clearCache)
            {
                // 管理员令牌默认不启用这两个特权，不启用就会一直失败
                rep.QuotaPriv = Win.EnablePrivilege("SeIncreaseQuotaPrivilege");
                rep.CacheCleared = Win.SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0);
                if (!rep.CacheCleared) rep.CacheError = Marshal.GetLastWin32Error();
                sb.Append(rep.CacheCleared ? "系统文件缓存已清空；" : "文件缓存清理失败；");
            }
            if (purgeStandby)
            {
                rep.ProfilePriv = Win.EnablePrivilege("SeProfileSingleProcessPrivilege");
                int cmd = 4; // MemoryPurgeStandbyList
                int st = Win.NtSetSystemInformation(0x50, ref cmd, 4);
                rep.StandbyPurged = st == 0;
                rep.StandbyError = st;
                sb.Append(rep.StandbyPurged ? "待机内存列表已清空；" : "待机列表清理失败；");
            }
            rep.Detail = sb.ToString();
            return rep;
        }
    }
}
