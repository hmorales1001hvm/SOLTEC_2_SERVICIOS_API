using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using static BLL.ProcessHandler;

public static class InteractiveProcessLauncher
{
    public static Process LaunchInActiveSession(string exePath, string args)
    {
        IntPtr userToken = IntPtr.Zero;
        IntPtr duplicatedToken = IntPtr.Zero;
        PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
        STARTUPINFO si = new STARTUPINFO
        {
            cb = Marshal.SizeOf<STARTUPINFO>(),
            lpDesktop = @"winsta0\default"
        };

        SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
        sa.Length = Marshal.SizeOf<SECURITY_ATTRIBUTES>();

        try
        {
            int sessionId = GetActiveSessionId();

            if (!NativeMethods.WTSQueryUserToken(sessionId, out userToken))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "WTSQueryUserToken failed");

            if (!NativeMethods.DuplicateTokenEx(
                userToken,
                NativeMethods.GENERIC_ALL_ACCESS,
                ref sa,
                (int)SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                (int)TOKEN_TYPE.TokenPrimary,
                out duplicatedToken))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "DuplicateTokenEx failed");
            }

            string commandLine = $"\"{exePath}\" {args}";
            string workingDir = Path.GetDirectoryName(exePath);

            if (!NativeMethods.CreateProcessAsUser(
                duplicatedToken,
                null,
                commandLine,
                ref sa,
                ref sa,
                false,
                0,
                IntPtr.Zero,
                workingDir,
                ref si,
                out pi))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcessAsUser failed");
            }

            return Process.GetProcessById(pi.dwProcessID);
        }
        finally
        {
            if (pi.hProcess != IntPtr.Zero) NativeMethods.CloseHandle(pi.hProcess);
            if (pi.hThread != IntPtr.Zero) NativeMethods.CloseHandle(pi.hThread);
            if (userToken != IntPtr.Zero) NativeMethods.CloseHandle(userToken);
            if (duplicatedToken != IntPtr.Zero) NativeMethods.CloseHandle(duplicatedToken);
        }
    }

    private static int GetActiveSessionId()
    {
        int sessionCount;
        IntPtr sessionInfoPtr;

        if (!NativeMethods.WTSEnumerateSessions(
            IntPtr.Zero,
            0,
            1,
            out sessionInfoPtr,
            out sessionCount))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "WTSEnumerateSessions failed");
        }

        int dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
        for (int i = 0; i < sessionCount; i++)
        {
            var current = Marshal.PtrToStructure<WTS_SESSION_INFO>(sessionInfoPtr + i * dataSize);
            if (current.State == WTS_CONNECTSTATE_CLASS.WTSActive)
            {
                NativeMethods.WTSFreeMemory(sessionInfoPtr);
                return current.SessionID;
            }
        }

        NativeMethods.WTSFreeMemory(sessionInfoPtr);
        throw new Exception("No active user session found");
    }
}



internal static class NativeMethods
{
    public const int GENERIC_ALL_ACCESS = 0x10000000;

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool DuplicateTokenEx(
        IntPtr existingToken,
        int desiredAccess,
        ref SECURITY_ATTRIBUTES tokenAttributes,
        int impersonationLevel,
        int tokenType,
        out IntPtr newToken);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcessAsUser(
        IntPtr token,
        string appName,
        string cmdLine,
        ref SECURITY_ATTRIBUTES processAttr,
        ref SECURITY_ATTRIBUTES threadAttr,
        bool inheritHandles,
        int flags,
        IntPtr env,
        string currentDir,
        ref STARTUPINFO startupInfo,
        out PROCESS_INFORMATION processInfo);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern bool WTSQueryUserToken(int sessionId, out IntPtr Token);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern bool WTSEnumerateSessions(
        IntPtr hServer,
        int Reserved,
        int Version,
        out IntPtr ppSessionInfo,
        out int pCount);

    [DllImport("wtsapi32.dll")]
    public static extern void WTSFreeMemory(IntPtr pMemory);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);
}

internal enum TOKEN_TYPE
{
    TokenPrimary = 1,
    TokenImpersonation
}

internal enum SECURITY_IMPERSONATION_LEVEL
{
    SecurityAnonymous,
    SecurityIdentification,
    SecurityImpersonation,
    SecurityDelegation
}

[StructLayout(LayoutKind.Sequential)]
internal struct SECURITY_ATTRIBUTES
{
    public int Length;
    public IntPtr lpSecurityDescriptor;
    public bool bInheritHandle;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct STARTUPINFO
{
    public int cb;
    public string lpReserved;
    public string lpDesktop;
    public string lpTitle;
    public int dwX, dwY, dwXSize, dwYSize;
    public int dwXCountChars, dwYCountChars;
    public int dwFillAttribute;
    public int dwFlags;
    public short wShowWindow;
    public short cbReserved2;
    public IntPtr lpReserved2;
    public IntPtr hStdInput, hStdOutput, hStdError;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PROCESS_INFORMATION
{
    public IntPtr hProcess;
    public IntPtr hThread;
    public int dwProcessID;
    public int dwThreadID;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WTS_SESSION_INFO
{
    public int SessionID;
    [MarshalAs(UnmanagedType.LPStr)]
    public string pWinStationName;
    public WTS_CONNECTSTATE_CLASS State;
}

internal enum WTS_CONNECTSTATE_CLASS
{
    WTSActive,
    WTSConnected,
    WTSConnectQuery,
    WTSShadow,
    WTSDisconnected,
    WTSIdle,
    WTSListen,
    WTSReset,
    WTSDown,
    WTSInit
}




public static class PrivilegeHelper
{
    public static void EnablePrivilege(string privilegeName)
    {
        if (!NativeMethods.OpenProcessToken(Process.GetCurrentProcess().Handle,
            TokenAccess.TOKEN_ADJUST_PRIVILEGES | TokenAccess.TOKEN_QUERY,
            out IntPtr tokenHandle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcessToken failed.");
        }

        try
        {
            TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new LUID_AND_ATTRIBUTES[1]
            };

            if (!NativeMethods.LookupPrivilegeValue(null, privilegeName, out tp.Privileges[0].Luid))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "LookupPrivilegeValue failed.");
            }

            tp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;

            if (!NativeMethods.AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "AdjustTokenPrivileges failed.");
            }
        }
        finally
        {
            NativeMethods.CloseHandle(tokenHandle);
        }
    }

    private const int SE_PRIVILEGE_ENABLED = 0x00000002;

    private static class TokenAccess
    {
        public const int TOKEN_ADJUST_PRIVILEGES = 0x0020;
        public const int TOKEN_QUERY = 0x0008;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public int Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public int PrivilegeCount;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public LUID_AND_ATTRIBUTES[] Privileges;
    }

    private static class NativeMethods
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(
            IntPtr processHandle,
            int desiredAccess,
            out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool LookupPrivilegeValue(
            string lpSystemName,
            string lpName,
            out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool AdjustTokenPrivileges(
            IntPtr tokenHandle,
            bool disableAllPrivileges,
            ref TOKEN_PRIVILEGES newState,
            int bufferLength,
            IntPtr previousState,
            IntPtr returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);
    }
}
