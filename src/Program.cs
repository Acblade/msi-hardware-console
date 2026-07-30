using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

[assembly: AssemblyTitle("MSI Hardware Console")]
[assembly: AssemblyProduct("MSI Hardware Console")]
[assembly: AssemblyDescription("Unofficial MSI laptop hardware monitor and fan controller")]
[assembly: AssemblyCompany("Acblade")]
[assembly: AssemblyVersion("0.1.8.0")]
[assembly: AssemblyFileVersion("0.1.8.0")]

namespace MsiHardwareConsole
{
    internal static class Program
    {
        private const string MutexName = "Local\\MSIHardwareConsole.SingleInstance";
        private const string ShowEventName = "Local\\MSIHardwareConsole.ShowWindow";
        private static Mutex mutex;
        private static EventWaitHandle showEvent;

        [STAThread]
        private static void Main(string[] args)
        {
            Log("START " + string.Join(" ", args));
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { Log("UNHANDLED " + e.ExceptionObject); };
            bool qaSecondary = HasArgument(args, "--qa-secondary");
            string mutexName = qaSecondary ? MutexName + ".QA" : MutexName;
            string showEventName = qaSecondary ? ShowEventName + ".QA" : ShowEventName;
            bool created;
            mutex = new Mutex(true, mutexName, out created);
            if (!created)
            {
                try { EventWaitHandle.OpenExisting(showEventName).Set(); } catch { }
                Log("Existing instance signaled; exiting.");
                return;
            }

            bool background = HasArgument(args, "--background");
            bool renderPreview = HasArgument(args, "--render-preview");
            bool renderOverlayPreview = HasArgument(args, "--render-overlay-preview");
            bool renderCurvePreview = HasArgument(args, "--render-curve-preview");
            bool skipAutoStartSetup = HasArgument(args, "--no-autostart-setup");
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e) { Log("DISPATCHER_UNHANDLED " + e.Exception); };
            string forcedLanguage = HasArgument(args, "--lang-zh") ? "zh-CN" : HasArgument(args, "--lang-en") ? "en-US" : null;
            var window = new MainWindow(skipAutoStartSetup, forcedLanguage);
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, showEventName);
            ThreadPool.RegisterWaitForSingleObject(showEvent, delegate
            {
                app.Dispatcher.BeginInvoke(new Action(window.ShowFromTray));
            }, null, Timeout.Infinite, false);

            app.Exit += delegate
            {
                window.DisposeResources();
                if (showEvent != null) showEvent.Dispose();
                if (mutex != null) mutex.Dispose();
            };

            window.InitializeRuntime();
            if (renderPreview || renderOverlayPreview || renderCurvePreview)
            {
                window.Show();
                var previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                previewTimer.Tick += delegate
                {
                    previewTimer.Stop();
                    if (renderCurvePreview)
                    {
                        window.ShowCurvePreviewForQa();
                        window.SaveViewportPreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "curve-preview.png"));
                    }
                    else if (renderOverlayPreview)
                    {
                        window.ShowPerformancePreviewForQa();
                        window.SaveViewportPreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "overlay-preview.png"));
                    }
                    else
                    {
                        window.SaveViewportPreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "viewport-preview.png"));
                        window.SavePreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-preview.png"));
                    }
                    app.Shutdown();
                };
                previewTimer.Start();
            }
            else if (!background) window.Show();
            else
            {
                // Create a native WPF window handle without flashing a taskbar button.
                window.Opacity = 0;
                window.ShowActivated = false;
                window.ShowInTaskbar = false;
                window.Show();
                window.Hide();
                window.Opacity = 1;
                window.ShowActivated = true;
                window.ShowInTaskbar = true;
            }
            Log("Entering Application.Run; background=" + background);
            app.Run();
            Log("Application.Run returned.");
        }

        private static bool HasArgument(string[] args, string expected)
        {
            foreach (string arg in args)
                if (string.Equals(arg, expected, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void Log(string message)
        {
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MSI Hardware Console");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "runtime.log"), DateTime.Now.ToString("o") + "  " + message + Environment.NewLine);
            }
            catch { }
        }
    }
}
