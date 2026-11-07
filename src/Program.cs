using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("ZZ游戏管理器")]
[assembly: AssemblyDescription("本地游戏管理器")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace GameShelf
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, eventArgs) => MessageBox.Show(
                "操作未完成：\n" + eventArgs.Exception.Message, "ZZ游戏管理器", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            try
            {
                string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                if (args.Length == 2 && args[0] == "--data-dir")
                {
                    root = Path.GetFullPath(args[1]);
                }
                else if (args.Length != 0)
                {
                    throw new ArgumentException("可选用法：ZZGameManager.exe --data-dir \"游戏库目录\"");
                }
                using (var store = new LibraryStore(root))
                {
                    Application.Run(new MainForm(store));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开ZZ游戏管理器：\n\n" + ex.Message +
                    "\n\n请确认软件位于可写目录（如 D:\\工具\\ZZ游戏管理器），并且同一游戏库没有重复打开。",
                    "ZZ游戏管理器", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
