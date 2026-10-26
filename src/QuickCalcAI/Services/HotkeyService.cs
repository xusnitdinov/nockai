using System.Runtime.InteropServices;

namespace QuickCalcAI.Services;

/// <summary>
/// Message-only HWND that receives WM_HOTKEY without touching the WinUI window proc.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    public const int HotkeyId = 0x5143; // "QC"
    private const uint ModControl = 0x0002;
    private const uint ModNorepeat = 0x4000;
    private const uint VkSpace = 0x20;
    private const int WmHotkey = 0x0312;
    private const int WmDestroy = 0x0002;

    private readonly IntPtr _hwnd;
    private readonly WndProc _wndProc;
    private readonly GCHandle _wndProcHandle;
    private bool _registered;

    public event Action? HotkeyPressed;

    public HotkeyService()
    {
        _wndProc = WndProcImpl;
        _wndProcHandle = GCHandle.Alloc(_wndProc);

        var wc = new WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = "QuickCalcAI_HotkeyMsg"
        };

        var atom = RegisterClassEx(ref wc);
        if (atom == 0)
            throw new InvalidOperationException("RegisterClassEx failed for hotkey window.");

        _hwnd = CreateWindowEx(
            0, (IntPtr)atom, "QuickCalcAI Hotkey",
            0, 0, 0, 0, 0,
            new IntPtr(-3), // HWND_MESSAGE
            IntPtr.Zero, wc.hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException("CreateWindowEx failed for hotkey window.");

        // Ctrl+Space
        _registered = RegisterHotKey(_hwnd, HotkeyId, ModControl | ModNorepeat, VkSpace);
        if (!_registered)
            throw new InvalidOperationException("Could not register Ctrl+Space. Is another app using it?");
    }

    private IntPtr WndProcImpl(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            return IntPtr.Zero;
        }

        if (msg == WmDestroy)
            return IntPtr.Zero;

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }

        if (_hwnd != IntPtr.Zero)
            DestroyWindow(_hwnd);

        if (_wndProcHandle.IsAllocated)
            _wndProcHandle.Free();
    }

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle, IntPtr lpClassName, string lpWindowName, int dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
