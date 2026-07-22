using System;
using System.Management;

namespace MsiHardwareConsole
{
    internal sealed class HardwareCompatibility
    {
        public string Manufacturer { get; private set; }
        public string Model { get; private set; }
        public string WmiVersion { get; private set; }
        public bool FanControlVerified { get; private set; }

        public static HardwareCompatibility Detect(string wmiVersion)
        {
            var result = new HardwareCompatibility
            {
                Manufacturer = "Unknown",
                Model = "Unknown",
                WmiVersion = string.IsNullOrEmpty(wmiVersion) ? "Unknown" : wmiVersion
            };
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\cimv2", "SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    foreach (ManagementObject row in rows)
                    {
                        result.Manufacturer = Convert.ToString(row["Manufacturer"]);
                        result.Model = Convert.ToString(row["Model"]);
                        break;
                    }
                }
            }
            catch { }

            result.FanControlVerified = IsVerified(result.Manufacturer, result.Model, result.WmiVersion);
            return result;
        }

        internal static bool IsVerified(string manufacturer, string model, string wmiVersion)
        {
            manufacturer = manufacturer ?? string.Empty;
            model = model ?? string.Empty;
            bool isMsi = manufacturer.IndexOf("Micro-Star", StringComparison.OrdinalIgnoreCase) >= 0
                || manufacturer.IndexOf("MSI", StringComparison.OrdinalIgnoreCase) >= 0;
            bool verifiedModel = model.IndexOf("Cyborg 15 A13VE", StringComparison.OrdinalIgnoreCase) >= 0;
            return isMsi && verifiedModel && string.Equals(wmiVersion, "2.8", StringComparison.OrdinalIgnoreCase);
        }
    }
}
