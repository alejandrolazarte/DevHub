using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DevHub.Services;

internal sealed class WindowsConPty : IDisposable
{
    private IntPtr _hPseudoConsole;
    private IntPtr _hProcess;
    private bool _disposed;

    public Stream OutputStream { get; }
    public Stream InputStream { get; }

    private WindowsConPty(
        IntPtr hPseudoConsole,
        IntPtr hProcess,
        SafeFileHandle stdoutRead,
        SafeFileHandle stdinWrite)
    {
        _hPseudoConsole = hPseudoConsole;
        _hProcess = hProcess;
        OutputStream = new FileStream(stdoutRead, FileAccess.Read, 4096, isAsync: true);
        InputStream = new FileStream(stdinWrite, FileAccess.Write, 4096, isAsync: true);
    }

    public static WindowsConPty Create(string cwd, string exe, string arguments, short cols = 220, short rows = 50)
    {
        if (!NativeMethods.CreatePipe(out var stdinRead, out var stdinWrite, IntPtr.Zero, 0))
        {
            throw new InvalidOperationException("CreatePipe (stdin) failed");
        }

        if (!NativeMethods.CreatePipe(out var stdoutRead, out var stdoutWrite, IntPtr.Zero, 0))
        {
            NativeMethods.CloseHandle(stdinRead);
            NativeMethods.CloseHandle(stdinWrite);
            throw new InvalidOperationException("CreatePipe (stdout) failed");
        }

        var size = new NativeMethods.COORD { X = cols, Y = rows };
        var hr = NativeMethods.CreatePseudoConsole(size, stdinRead, stdoutWrite, 0, out var hPC);

        NativeMethods.CloseHandle(stdinRead);
        NativeMethods.CloseHandle(stdoutWrite);

        if (hr != 0)
        {
            NativeMethods.CloseHandle(stdinWrite);
            NativeMethods.CloseHandle(stdoutRead);
            throw new InvalidOperationException($"CreatePseudoConsole failed: 0x{hr:X}");
        }

        IntPtr attrListSize = IntPtr.Zero;
        NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrListSize);

        var attrList = Marshal.AllocHGlobal(attrListSize);
        try
        {
            if (!NativeMethods.InitializeProcThreadAttributeList(attrList, 1, 0, ref attrListSize))
            {
                throw new InvalidOperationException("InitializeProcThreadAttributeList failed");
            }

            if (!NativeMethods.UpdateProcThreadAttribute(
                    attrList, 0,
                    NativeMethods.ProcThreadAttributePseudoConsole,
                    hPC, (IntPtr)IntPtr.Size,
                    IntPtr.Zero, IntPtr.Zero))
            {
                throw new InvalidOperationException("UpdateProcThreadAttribute failed");
            }

            var si = new NativeMethods.STARTUPINFOEX();
            si.StartupInfo.cb = Marshal.SizeOf<NativeMethods.STARTUPINFOEX>();
            si.lpAttributeList = attrList;

            var commandLine = $"\"{exe}\" {arguments}";
            var workDir = Directory.Exists(cwd) ? cwd : Environment.CurrentDirectory;

            if (!NativeMethods.CreateProcess(
                    null, commandLine,
                    IntPtr.Zero, IntPtr.Zero,
                    false,
                    NativeMethods.ExtendedStartupinfoPresent,
                    IntPtr.Zero, workDir,
                    ref si, out var pi))
            {
                throw new InvalidOperationException($"CreateProcess failed: {Marshal.GetLastWin32Error()}");
            }

            NativeMethods.CloseHandle(pi.hThread);

            return new WindowsConPty(
                hPC, pi.hProcess,
                new SafeFileHandle(stdoutRead, ownsHandle: true),
                new SafeFileHandle(stdinWrite, ownsHandle: true));
        }
        finally
        {
            NativeMethods.DeleteProcThreadAttributeList(attrList);
            Marshal.FreeHGlobal(attrList);
        }
    }

    public void Kill()
    {
        if (_hProcess != IntPtr.Zero)
        {
            NativeMethods.TerminateProcess(_hProcess, 0);
            NativeMethods.CloseHandle(_hProcess);
            _hProcess = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Kill();
        try { OutputStream.Dispose(); } catch { }
        try { InputStream.Dispose(); } catch { }

        if (_hPseudoConsole != IntPtr.Zero)
        {
            NativeMethods.ClosePseudoConsole(_hPseudoConsole);
            _hPseudoConsole = IntPtr.Zero;
        }
    }

    private static class NativeMethods
    {
        public const uint ExtendedStartupinfoPresent = 0x00080000;
        public static readonly IntPtr ProcThreadAttributePseudoConsole = new(0x00020016);

        [StructLayout(LayoutKind.Sequential)]
        public struct COORD
        {
            public short X, Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public int dwX, dwY, dwXSize, dwYSize;
            public int dwXCountChars, dwYCountChars;
            public int dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess, hThread;
            public int dwProcessId, dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CreatePipe(
            out IntPtr hReadPipe, out IntPtr hWritePipe,
            IntPtr lpPipeAttributes, uint nSize);

        [DllImport("kernel32.dll", SetLastError = false)]
        public static extern int CreatePseudoConsole(
            COORD size, IntPtr hInput, IntPtr hOutput,
            uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList, int dwAttributeCount,
            int dwFlags, ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList, uint dwFlags,
            IntPtr Attribute, IntPtr lpValue, IntPtr cbSize,
            IntPtr lpPreviousValue, IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcess(
            string? lpApplicationName, string lpCommandLine,
            IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
            bool bInheritHandles, uint dwCreationFlags,
            IntPtr lpEnvironment, string? lpCurrentDirectory,
            ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);
    }
}
