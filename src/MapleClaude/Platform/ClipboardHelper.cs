using System.Runtime.InteropServices;

namespace MapleClaude.Platform;

/// <summary>
/// Direct Win32 clipboard access via <c>user32</c> P/Invoke. Used by
/// <see cref="UI.TextField"/> for Ctrl-C / Ctrl-V / Ctrl-X. We avoid
/// <c>System.Windows.Forms.Clipboard</c> here because the MonoGame main
/// thread runs in MTA and the WinForms Clipboard requires STA — calling
/// it from the wrong apartment throws <c>ThreadStateException</c>.
/// </summary>
public static class ClipboardHelper
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetClipboardData(uint uFormat, nint hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalLock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalFree(nint hMem);

    /// <summary>Read the current clipboard contents as Unicode text. Returns null on failure.</summary>
    public static string? GetText()
    {
        if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
        {
            return null;
        }
        if (!OpenClipboard(nint.Zero))
        {
            return null;
        }
        try
        {
            var hData = GetClipboardData(CF_UNICODETEXT);
            if (hData == nint.Zero)
            {
                return null;
            }
            var pData = GlobalLock(hData);
            if (pData == nint.Zero)
            {
                return null;
            }
            try
            {
                return Marshal.PtrToStringUni(pData);
            }
            finally
            {
                GlobalUnlock(hData);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>Replace the clipboard with the given Unicode text. Returns true on success.</summary>
    public static bool SetText(string text)
    {
        if (text is null)
        {
            return false;
        }
        if (!OpenClipboard(nint.Zero))
        {
            return false;
        }
        var hGlobal = nint.Zero;
        try
        {
            EmptyClipboard();
            // Two bytes per char + trailing NUL (2 bytes).
            var bytes = (nuint)((text.Length + 1) * 2);
            hGlobal = GlobalAlloc(GMEM_MOVEABLE, bytes);
            if (hGlobal == nint.Zero)
            {
                return false;
            }
            var target = GlobalLock(hGlobal);
            if (target == nint.Zero)
            {
                return false;
            }
            try
            {
                // Marshal will zero-terminate for us.
                var ansiBytes = System.Text.Encoding.Unicode.GetBytes(text + '\0');
                Marshal.Copy(ansiBytes, 0, target, ansiBytes.Length);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }
            if (SetClipboardData(CF_UNICODETEXT, hGlobal) == nint.Zero)
            {
                return false;
            }
            // Ownership transferred to the clipboard.
            hGlobal = nint.Zero;
            return true;
        }
        finally
        {
            if (hGlobal != nint.Zero)
            {
                GlobalFree(hGlobal);
            }
            CloseClipboard();
        }
    }
}
