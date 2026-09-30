using System.Windows.Forms;
namespace CJFingerService;
internal static class DummyProgram {
    [STAThread]
    static void Main() {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new DemoTarget());
    }
}
