using System;

namespace MsiHardwareConsole
{
    internal static class SmokeTests
    {
        private static int failures;

        private static void Assert(bool condition, string name)
        {
            if (condition) Console.WriteLine("PASS " + name);
            else
            {
                Console.WriteLine("FAIL " + name);
                failures++;
            }
        }

        private static int Main()
        {
            Assert(HardwareCompatibility.IsVerified("Micro-Star International Co., Ltd.", "Cyborg 15 A13VE", "2.8"), "verified target");
            Assert(!HardwareCompatibility.IsVerified("Micro-Star International Co., Ltd.", "Cyborg 15 A13VF", "2.8"), "different model locked");
            Assert(!HardwareCompatibility.IsVerified("Micro-Star International Co., Ltd.", "Cyborg 15 A13VE", "2.7"), "different WMI locked");
            Assert(!HardwareCompatibility.IsVerified("Other", "Cyborg 15 A13VE", "2.8"), "non-MSI locked");

            Localization.Chinese = false;
            Assert(Localization.T("English", "中文") == "English", "English default");
            Localization.Chinese = true;
            Assert(Localization.T("English", "中文") == "中文", "Chinese switch");
            return failures == 0 ? 0 : 1;
        }
    }
}
