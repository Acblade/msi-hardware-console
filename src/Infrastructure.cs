// MSI Hardware Console - settings and elevated logon task.
// GPL-3.0-or-later. See LICENSE.md.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Xml.Serialization;

namespace MsiHardwareConsole
{
    public sealed class AppSettings
    {
        public bool StartWithWindowsToTray { get; set; }
        public int FixedFanSpeed { get; set; }
        public int FixedRunningFanSpeed { get; set; }
        public string SelectedFanMode { get; set; }
        public int[] CustomTemperatures { get; set; }
        public int[] CustomSpeeds { get; set; }
        public string Language { get; set; }

        public AppSettings()
        {
            StartWithWindowsToTray = false;
            FixedFanSpeed = 50;
            FixedRunningFanSpeed = 50;
            SelectedFanMode = "Automatic";
            Language = "en-US";
            CustomTemperatures = new[] { 40, 50, 57, 64, 71, 78, 85 };
            CustomSpeeds = new[] { 25, 35, 43, 52, 62, 76, 100 };
        }
    }

    internal static class SettingsStore
    {
        private static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MSI Hardware Console");
        private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.xml");

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new AppSettings();
                using (var stream = File.OpenRead(FilePath))
                    return (AppSettings)new XmlSerializer(typeof(AppSettings)).Deserialize(stream);
            }
            catch { return new AppSettings(); }
        }

        public static void Save(AppSettings settings)
        {
            Directory.CreateDirectory(DirectoryPath);
            using (var stream = File.Create(FilePath))
                new XmlSerializer(typeof(AppSettings)).Serialize(stream, settings);
        }
    }

    internal static class AutoStartManager
    {
        public const string TaskName = "MSI Hardware Console";

        public static bool IsInstalled()
        {
            object service = null;
            object root = null;
            object task = null;
            try
            {
                service = CreateService();
                root = ((dynamic)service).GetFolder("\\");
                task = ((dynamic)root).GetTask(TaskName);
                return task != null;
            }
            catch (COMException) { return false; }
            finally
            {
                Release(task);
                Release(root);
                Release(service);
            }
        }

        public static void EnsureInstalled()
        {
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            string sid = WindowsIdentity.GetCurrent().User.Value;
            object service = null;
            object root = null;
            object definition = null;
            object trigger = null;
            object action = null;
            object registered = null;
            try
            {
                service = CreateService();
                root = ((dynamic)service).GetFolder("\\");
                definition = ((dynamic)service).NewTask(0);
                dynamic task = definition;
                task.RegistrationInfo.Description = "Start MSI Hardware Console elevated in the notification area.";
                task.Principal.UserId = sid;
                task.Principal.LogonType = 3; // TASK_LOGON_INTERACTIVE_TOKEN
                task.Principal.RunLevel = 1;  // TASK_RUNLEVEL_HIGHEST
                task.Settings.Enabled = true;
                task.Settings.StartWhenAvailable = true;
                task.Settings.DisallowStartIfOnBatteries = false;
                task.Settings.StopIfGoingOnBatteries = false;
                task.Settings.MultipleInstances = 2; // TASK_INSTANCES_IGNORE_NEW
                task.Settings.ExecutionTimeLimit = "PT0S";

                trigger = task.Triggers.Create(9); // TASK_TRIGGER_LOGON
                ((dynamic)trigger).UserId = sid;
                ((dynamic)trigger).Enabled = true;
                action = task.Actions.Create(0); // TASK_ACTION_EXEC
                ((dynamic)action).Path = exe;
                ((dynamic)action).Arguments = "--background";

                registered = ((dynamic)root).RegisterTaskDefinition(
                    TaskName, definition, 6, null, null, 3, null); // CREATE_OR_UPDATE, INTERACTIVE_TOKEN
            }
            finally
            {
                Release(registered);
                Release(action);
                Release(trigger);
                Release(definition);
                Release(root);
                Release(service);
            }
        }

        public static void Remove()
        {
            object service = null;
            object root = null;
            try
            {
                service = CreateService();
                root = ((dynamic)service).GetFolder("\\");
                try { ((dynamic)root).DeleteTask(TaskName, 0); }
                catch (COMException) { }
            }
            finally
            {
                Release(root);
                Release(service);
            }
        }

        private static object CreateService()
        {
            Type type = Type.GetTypeFromProgID("Schedule.Service");
            if (type == null) throw new InvalidOperationException("Windows Task Scheduler service is unavailable.");
            object service = Activator.CreateInstance(type);
            ((dynamic)service).Connect();
            return service;
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value))
                Marshal.FinalReleaseComObject(value);
        }
    }

    internal static class SecurityContext
    {
        public static bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
