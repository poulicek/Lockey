using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Lockey.UI;

namespace Lockey
{
    static class Program
    {
        private static readonly Mutex mutex = new Mutex(false, Assembly.GetExecutingAssembly().GetName().Name);

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {            
            if (!mutex.WaitOne(0, false))
                return;

            AppDomain.CurrentDomain.UnhandledException += onUnhandledException;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayIcon());
        }

        private static void onUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
#if DEBUG
            throw e.ExceptionObject as Exception;
#endif
        }
    }
}
