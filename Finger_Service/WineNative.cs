using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace CJFingerService;

// No UI Automation calls: Wine may implement user32 while UiaFind is E_NOTIMPL.
internal sealed class WineControl(IntPtr handle) {
    internal IntPtr Handle => handle;
    internal WineControl Current => this;
    internal IntPtr NativeWindowHandle => handle;
    internal string ClassName => Native.WindowClass(handle);
    internal bool IsEnabled => Native.Enabled(handle);
    internal bool IsPassword => Native.PasswordEdit(handle);
    internal string Name => IsPassword ? "" : Native.ControlText(handle);
    internal Rectangle BoundingRectangle => Native.Bounds(handle);
}

internal static partial class Native {
    internal static bool IsWine {
        get {
            if(!NativeLibrary.TryLoad("ntdll.dll",out var library))return false;
            try {return NativeLibrary.TryGetExport(library,"wine_get_version",out _);}
            finally {NativeLibrary.Free(library);}
        }
    }
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")] private static extern int GetWindowStyle(IntPtr handle,int index);
    [DllImport("user32.dll")] private static extern bool IsChild(IntPtr parent,IntPtr child);
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread,ref GuiInfo info);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window,ref Point point);
    [StructLayout(LayoutKind.Sequential)] private struct GuiInfo {
        public uint Size,Flags;
        public IntPtr Active,Focus,Capture,Menu,Move,Caret;
        public RECT CaretRect;
    }
    internal static string WindowClass(IntPtr handle) {var text=new StringBuilder(256);GetClassName(handle,text,text.Capacity);return text.ToString();}
    internal static bool Enabled(IntPtr handle)=>IsWindowVisible(handle)&&IsWindowEnabled(handle);
    internal static bool PasswordEdit(IntPtr handle)=>WindowClass(handle).Contains("edit",StringComparison.OrdinalIgnoreCase) && (GetWindowStyle(handle,-16)&0x20)!=0;
    internal static bool ReadOnlyEdit(IntPtr handle)=>(GetWindowStyle(handle,-16)&0x800)!=0;
    internal static Rectangle Bounds(IntPtr handle) {
        if(!GetWindowRect(handle,out var r))throw new InvalidOperationException("Wine control disappeared.");
        return Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom);
    }
    internal static string ControlText(IntPtr handle) {
        if(PasswordEdit(handle))return "";
        var text=new StringBuilder(131072);
        if(ReadTextMessage(handle,0x000D,(IntPtr)text.Capacity,text,2,500,out _)==IntPtr.Zero)
            throw new TimeoutException("Wine control did not respond to text read.");
        return text.ToString();
    }
    internal static WineControl[] ChildControls(IntPtr parent) {
        var result=new List<WineControl>();
        EnumChildWindows(parent,(handle,_)=> {if(IsWindowVisible(handle))result.Add(new(handle));return true;},IntPtr.Zero);
        return result.ToArray();
    }
    internal static bool HasNativeFocus(IntPtr window,IntPtr control) {
        var info=new GuiInfo {Size=(uint)Marshal.SizeOf<GuiInfo>()};
        return GetForegroundWindow()==window && GetGUIThreadInfo(GetWindowThreadProcessId(window,out _),ref info) && info.Focus==control;
    }
    internal static bool HasCaretIn(IntPtr window,Rectangle field) {
        var info=new GuiInfo {Size=(uint)Marshal.SizeOf<GuiInfo>()};
        if(GetForegroundWindow()!=window || !GetGUIThreadInfo(GetWindowThreadProcessId(window,out _),ref info) || info.Caret==IntPtr.Zero || !IsChild(window,info.Focus))return false;
        var point=new Point(info.CaretRect.Left,info.CaretRect.Top);
        return ClientToScreen(info.Caret,ref point) && field.Contains(point);
    }
}
