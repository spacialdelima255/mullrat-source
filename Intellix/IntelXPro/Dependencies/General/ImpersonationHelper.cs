using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.General
{
    public static class ImpersonationHelper
    {
        private class ImpersonationContext : IDisposable
        {
            public void Dispose()
            {
                NativeMethods.RevertToSelf();
            }
        }

        private const uint TOKEN_DUPLICATE = 2u;
        private const uint TOKEN_IMPERSONATE = 4u;
        private const uint TOKEN_QUERY = 8u;
        private const uint TOKEN_ADJUST_PRIVILEGES = 32u;
        private const uint SecurityImpersonation = 2u;
        private const uint TokenImpersonation = 2u;
        private const uint SE_PRIVILEGE_ENABLED = 2u;

        public static IDisposable ImpersonateWinlogon()
        {
            IntPtr TokenHandle = IntPtr.Zero;
            IntPtr phNewToken = IntPtr.Zero;
            try
            {
                EnableDebugPrivilege();
                if (!NativeMethods.OpenProcessToken((Process.GetProcessesByName("winlogon").FirstOrDefault() ?? throw new Exception("winlogon.exe process not found")).Handle, 14u, out TokenHandle))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcessToken error");
                }
                if (!NativeMethods.DuplicateTokenEx(TokenHandle, 12u, IntPtr.Zero, 2u, 2u, out phNewToken))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "DuplicateTokenEx error");
                }
                if (!NativeMethods.ImpersonateLoggedOnUser(phNewToken))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "ImpersonateLoggedOnUser error");
                }
                return new ImpersonationContext();
            }
            catch
            {
                if (phNewToken != IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(phNewToken);
                }
                if (TokenHandle != IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(TokenHandle);
                }
                NativeMethods.RevertToSelf();
                throw;
            }
        }

        private static void EnableDebugPrivilege()
        {
            IntPtr TokenHandle = IntPtr.Zero;
            try
            {
                if (!NativeMethods.OpenProcessToken(NativeMethods.GetCurrentProcess(), 40u, out TokenHandle))
                {
                    int lastWin32Error = Marshal.GetLastWin32Error();
                    throw new Win32Exception(lastWin32Error, $"OpenProcessToken error: Error code {lastWin32Error}");
                }
                NativeMethods.LUID lpLuid = default(NativeMethods.LUID);
                if (!NativeMethods.LookupPrivilegeValue(null, "SeDebugPrivilege", ref lpLuid))
                {
                    int lastWin32Error2 = Marshal.GetLastWin32Error();
                    throw new Win32Exception(lastWin32Error2, $"LookupPrivilegeValue error: Error code {lastWin32Error2}");
                }
                NativeMethods.TOKEN_PRIVILEGES NewState = new NativeMethods.TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1u,
                    Luid = lpLuid,
                    Attributes = 2u
                };
                if (!NativeMethods.AdjustTokenPrivileges(TokenHandle, DisableAllPrivileges: false, ref NewState, (uint)Marshal.SizeOf(typeof(NativeMethods.TOKEN_PRIVILEGES)), IntPtr.Zero, IntPtr.Zero))
                {
                    int lastWin32Error3 = Marshal.GetLastWin32Error();
                    throw new Win32Exception(lastWin32Error3, $"AdjustTokenPrivileges error: Error code {lastWin32Error3}");
                }
                int lastWin32Error4 = Marshal.GetLastWin32Error();
                if (lastWin32Error4 != 0)
                {
                    throw new Win32Exception(lastWin32Error4, $"AdjustTokenPrivileges returned success but set error code {lastWin32Error4}");
                }
            }
            finally
            {
                if (TokenHandle != IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(TokenHandle);
                }
            }
        }
    }
}
