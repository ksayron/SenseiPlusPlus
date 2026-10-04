using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Sensei.LocalAgent.Core;

namespace Sensei.LocalAgent.Windows;

/// <summary>Windows DPAPI, CurrentUser scope, with UI forbidden. No machine-wide flag.</summary>
public sealed partial class WindowsContentProtector : IContentProtector
{
    public byte[] Protect(byte[] plaintext) => Transform(plaintext, protect: true);
    public byte[] Unprotect(byte[] ciphertext) => Transform(ciphertext, protect: false);

    private static byte[] Transform(byte[] input, bool protect)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The installed storage adapter requires Windows.");
        var native = new DataBlob { Length = input.Length, Data = Marshal.AllocHGlobal(input.Length) };
        DataBlob output = default;
        try
        {
            Marshal.Copy(input, 0, native.Data, input.Length);
            var success = protect
                ? CryptProtectData(ref native, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref native, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (success == 0) throw new CryptographicException("DPAPI failed.", new Win32Exception(Marshal.GetLastPInvokeError()));
            var result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, result.Length);
            return result;
        }
        finally
        {
            Marshal.Copy(new byte[input.Length], 0, native.Data, input.Length);
            Marshal.FreeHGlobal(native.Data);
            if (output.Data != IntPtr.Zero)
            {
                Marshal.Copy(new byte[output.Length], 0, output.Data, output.Length);
                _ = LocalFree(output.Data);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob { public int Length; public IntPtr Data; }

    [LibraryImport("crypt32.dll", SetLastError = true)]
    private static partial int CryptProtectData(ref DataBlob input, IntPtr description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);
    [LibraryImport("crypt32.dll", SetLastError = true)]
    private static partial int CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);
    [LibraryImport("kernel32.dll")]
    private static partial IntPtr LocalFree(IntPtr memory);
}
