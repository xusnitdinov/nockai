using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace NockAI.Services;

/// <summary>
/// Lightweight Win32 notify-icon tray.
/// </summary>
public sealed class TrayService : IDisposable
{
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const int WmApp = 0x8000;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonUp = 0x0205;
    public const int CallbackMessage = WmApp + 41;

    private readonly Dispatcher _dispatcher;
    private readonly IntPtr _hwnd;
    private readonly WndProc _wndProc;
    private readonly GCHandle _wndProcHandle;
    private readonly IntPtr _icon;
    private bool _added;
    private bool _disposed;

    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public TrayService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _wndProc = WndProcImpl;
        _wndProcHandle = GCHandle.Alloc(_wndProc);
        _icon = LoadIcon(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION

        var wc = new WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = "NockAI_TrayMsg"
        };

        var atom = RegisterClassEx(ref wc);
        if (atom == 0)
            throw new InvalidOperationException("RegisterClassEx failed for tray window.");

        _hwnd = CreateWindowEx(
            0, (IntPtr)atom, "NockAI Tray",
            0, 0, 0, 0, 0,
            new IntPtr(-3),
            IntPtr.Zero, wc.hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException("CreateWindowEx failed for tray window.");

        var data = BuildNotifyData("NockAI - Ctrl+Space");
        _added = Shell_NotifyIcon(NimAdd, ref data);
    }

    private NotifyIconData BuildNotifyData(string tip)
    {
        return new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = CallbackMessage,
            hIcon = _icon,
            szTip = tip
        };
    }

    private IntPtr WndProcImpl(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == CallbackMessage)
        {
            var mouse = lParam.ToInt32();
            if (mouse is WmLButtonUp or WmRButtonUp)
            {
                _dispatcher.BeginInvoke(() =>
                {
                    if (mouse == WmRButtonUp)
                        ShowContextMenu();
                    else
                        OpenRequested?.Invoke();
                });
            }

            return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        AppendMenu(menu, 0, 1, "Open NockAI");
        AppendMenu(menu, 0, 2, "Exit");

        GetCursorPos(out var pt);
        SetForegroundWindow(_hwnd);
        var cmd = (int)TrackPopupMenu(menu, 0x0100, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);
        DestroyMenu(menu);

        if (cmd == 1) OpenRequested?.Invoke();
        else if (cmd == 2) ExitRequested?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_added)
        {
            var data = BuildNotifyData("");
            Shell_NotifyIcon(NimDelete, ref data);
            _added = false;
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);
}
