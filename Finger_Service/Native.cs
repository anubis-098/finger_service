using System.Runtime.InteropServices;
using System.Text;
using System.Drawing;
namespace CJFingerService;
internal static class Native {
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint type; public InputUnion data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KeyboardInput keyboard; [FieldOffset(0)] public MouseInput mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort key,scan; public uint flags,time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int x,y; public uint data,flags,time; public UIntPtr extra; }
    [DllImport("user32.dll",SetLastError=true)] private static extern uint SendInput(uint count,Input[] inputs,int size);
    internal static void ReplaceFocusedText(string value,Action verifyFocus) {
        void Send(ushort key,ushort scan,uint flags) {
            var inputs=new[]{new Input {type=1,data=new InputUnion {keyboard=new KeyboardInput {key=key,scan=scan,flags=flags}}}};
            if(SendInput(1,inputs,Marshal.SizeOf<Input>())!=1) throw new InvalidOperationException("Keyboard input blocked. Run the exporter and Finger Service at the same privilege level.");
        }
        AssertDesktop(); verifyFocus();
        try { Send(0x11,0,0); Send(0x41,0,0); Send(0x41,0,2); }
        finally { Send(0x11,0,2); }
        verifyFocus(); Send(0x08,0,0); Send(0x08,0,2);
        foreach(var character in value) { verifyFocus(); Send(0,character,4); Send(0,character,6); Thread.Sleep(12); }
    }
    private delegate bool EnumWindowsCallback(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);
    internal static IntPtr FindWindow(int processId,string title) {
        IntPtr found=IntPtr.Zero;
        EnumWindows((handle,_)=> { GetWindowThreadProcessId(handle,out var id); if(id!=(uint)processId) return true; var text=new StringBuilder(512); GetWindowText(handle,text,512); if(!text.ToString().Contains(title,StringComparison.OrdinalIgnoreCase)) return true; found=handle; return false; },IntPtr.Zero);
        return found;
    }
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr handle,uint message,IntPtr wparam,IntPtr lparam);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] internal static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] internal static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] internal static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] internal static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern bool GetUserObjectInformation(IntPtr obj, int index, StringBuilder text, int length, out int needed);
    [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint message,IntPtr wparam,IntPtr lparam,uint flags,uint timeout,out IntPtr result);
    [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; }
    internal static void AssertDesktop() {
        var desktop = OpenInputDesktop(0, false, 1);
        if (desktop == IntPtr.Zero) throw new InvalidOperationException("DESKTOP_LOCKED: unlock the interactive Windows session.");
        try { var name = new StringBuilder(256); if (!GetUserObjectInformation(desktop, 2, name, 512, out _) || name.ToString() != "Default") throw new InvalidOperationException("DESKTOP_LOCKED: interactive desktop is unavailable."); } finally { CloseDesktop(desktop); }
    }
    internal static void Click(IntPtr window, double x, double y) {
        AssertDesktop(); if (GetForegroundWindow() != window) throw new InvalidOperationException("FOCUS_LOST: export window is no longer in front.");
        GetWindowRect(window, out var rect);
        if (x < rect.Left || x >= rect.Right || y < rect.Top || y >= rect.Bottom) throw new InvalidOperationException("Click target outside verified window.");
        SetCursorPos((int)x, (int)y); mouse_event(2,0,0,0,UIntPtr.Zero); mouse_event(4,0,0,0,UIntPtr.Zero);
    }
    internal static void EnterDate(IntPtr handle,DateTime date) {
        GetClientRect(handle,out var rect);
        var point=(IntPtr)(((rect.Bottom/2)<<16)|10);
        SendMessageTimeout(handle,0x0201,(IntPtr)1,point,2,3000,out _);
        SendMessageTimeout(handle,0x0202,IntPtr.Zero,point,2,3000,out _);
        foreach(var part in new[]{date.ToString("dd"),date.ToString("MM"),date.ToString("yyyy")}) {
            foreach(var digit in part) { SendMessageTimeout(handle,0x0102,(IntPtr)digit,(IntPtr)1,2,3000,out _); Thread.Sleep(50); }
            SendMessageTimeout(handle,0x0100,(IntPtr)0x27,(IntPtr)1,2,3000,out _);
            SendMessageTimeout(handle,0x0101,(IntPtr)0x27,(IntPtr)1,2,3000,out _);
        }
    }
    internal static void Screenshot(IntPtr window, string path) {
        AssertDesktop(); GetWindowRect(window, out var r);
        using var bitmap = new Bitmap(r.Right-r.Left, r.Bottom-r.Top);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(r.Left,r.Top,0,0,bitmap.Size);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
}
