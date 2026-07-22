// MSI Hardware Console - MSI laptop hardware access.
// GPL-3.0-or-later. See LICENSE.md.

using System;
using System.Management;

namespace MsiHardwareConsole
{
    internal sealed class MsiWmiController : IDisposable
    {
        private static readonly byte[] TemperatureMap = { 0, 3, 4, 5, 6, 7, 1 };
        private readonly object sync = new object();
        private ManagementObject instance;
        private ManagementBaseObject parameters;

        public string Version { get; private set; }

        public void Connect()
        {
            lock (sync)
            {
                instance = new ManagementObject(
                    @"root\WMI",
                    "MSI_ACPI.InstanceName='ACPI\\PNP0C14\\0_0'",
                    null);
                parameters = instance.InvokeMethod("Get_WMI", null, null);
                var bytes = (byte[])((ManagementBaseObject)parameters["Data"])["Bytes"];
                if (bytes == null || bytes.Length < 3 || bytes[0] != 1)
                    throw new InvalidOperationException("MSI WMI 接口没有返回成功状态。");
                Version = bytes[1] + "." + bytes[2];
            }
        }

        private byte[] Read(string method, byte subValue)
        {
            if (instance == null || parameters == null)
                throw new InvalidOperationException("MSI WMI 尚未连接。");

            var data = (ManagementBaseObject)parameters["Data"];
            var bytes = new byte[32];
            bytes[0] = subValue;
            data.SetPropertyValue("Bytes", bytes);
            parameters.SetPropertyValue("Data", data);
            var output = instance.InvokeMethod(method, parameters, null);
            var result = (byte[])((ManagementBaseObject)output["Data"])["Bytes"];
            if (result == null || result.Length == 0 || result[0] != 1)
                throw new InvalidOperationException(method + " 读取失败。");
            return result;
        }

        private void Write(string method, byte[] bytes)
        {
            if (instance == null || parameters == null)
                throw new InvalidOperationException("MSI WMI 尚未连接。");

            var data = (ManagementBaseObject)parameters["Data"];
            data.SetPropertyValue("Bytes", bytes);
            parameters.SetPropertyValue("Data", data);
            var output = instance.InvokeMethod(method, parameters, null);
            var result = (byte[])((ManagementBaseObject)output["Data"])["Bytes"];
            if (result == null || result.Length == 0 || result[0] != 1)
                throw new InvalidOperationException(method + " 写入失败。");
        }

        public HardwareSnapshot GetSnapshot()
        {
            lock (sync)
            {
                var temperatures = Read("Get_Temperature", 0);
                var fan = Read("Get_Fan", 0);
                var fanMode = Read("Get_AP", 1);
                var performanceMode = Read("Get_AP", 0);
                var thermal = Read("Get_Thermal", 3);
                int rawRpm = (fan[1] << 8) | fan[2];
                return new HardwareSnapshot
                {
                    CpuTemperature = temperatures[1],
                    GpuTemperature = temperatures[2],
                    FanRpm = rawRpm > 0 ? 478000 / rawRpm : 0,
                    FanMode = fanMode[1],
                    PerformanceMode = performanceMode[3],
                    FullBlast = (thermal[1] & 0x80) == 0x80
                };
            }
        }

        public void SetAutomatic()
        {
            lock (sync)
            {
                SetFullBlastInternal(false);
                SetFanModeInternal(0x0D);
            }
        }

        public void SetCurve(byte[] speeds)
        {
            if (speeds == null || speeds.Length != 7)
                throw new ArgumentException("风扇曲线必须包含七个速度点。", "speeds");

            lock (sync)
            {
                var fan = Read("Get_Fan", 1);
                fan[0] = 1;
                for (int i = 0; i < speeds.Length; i++) fan[i + 2] = speeds[i];
                Write("Set_Fan", fan);
                SetFullBlastInternal(false);
                SetFanModeInternal(0x8D);
            }
        }

        public FanCurve GetFanCurve()
        {
            lock (sync)
            {
                var up = Read("Get_Temperature", 1);
                var fan = Read("Get_Fan", 1);
                var temperatures = new int[7];
                var speeds = new int[7];
                temperatures[0] = 40; // Visual anchor: the first hardware stage means "below point 2".
                speeds[0] = fan[2];
                for (int i = 1; i < 7; i++)
                {
                    temperatures[i] = up[TemperatureMap[i] + 1];
                    speeds[i] = fan[i + 2];
                }
                return new FanCurve(temperatures, speeds);
            }
        }

        public void SetFanCurve(FanCurve curve)
        {
            if (curve == null || curve.Temperatures == null || curve.Speeds == null ||
                curve.Temperatures.Length != 7 || curve.Speeds.Length != 7)
                throw new ArgumentException("自定义曲线必须包含七个温度和转速点。", "curve");

            lock (sync)
            {
                var up = Read("Get_Temperature", 1);
                var fan = Read("Get_Fan", 1);
                var down = Read("Get_Thermal", 1);
                up[0] = 1;
                fan[0] = 1;
                down[0] = 1;
                for (int i = 0; i < 7; i++)
                {
                    int speed = Math.Max(0, Math.Min(100, curve.Speeds[i]));
                    if (speed > 0 && speed < 30) speed = 30;
                    if (speed > 60 && speed < 100) speed = 60;
                    fan[i + 2] = (byte)speed;
                    if (i == 0) continue;
                    int temperature = Math.Max(42, Math.Min(90, curve.Temperatures[i]));
                    up[TemperatureMap[i] + 1] = (byte)temperature;
                    down[i + 1] = 3; // MSI WMI2 stores downward hysteresis as an offset by default.
                }
                Write("Set_Temperature", up);
                Write("Set_Fan", fan);
                Write("Set_Thermal", down);
                SetFullBlastInternal(false);
                SetFanModeInternal(0x8D);
            }
        }

        public void SetFullBlast(bool enabled)
        {
            lock (sync) SetFullBlastInternal(enabled);
        }

        public void SetFanMode(byte mode)
        {
            if (mode != 0x0D && mode != 0x1D && mode != 0x4D && mode != 0x8D)
                throw new ArgumentOutOfRangeException("mode", "未知的 MSI 风扇模式。");
            lock (sync) SetFanModeInternal(mode);
        }

        private void SetFanModeInternal(byte mode)
        {
            var ap = Read("Get_AP", 1);
            ap[0] = 1;
            ap[1] = mode;
            Write("Set_AP", ap);
        }

        private void SetFullBlastInternal(bool enabled)
        {
            var thermal = Read("Get_Thermal", 3);
            thermal[0] = 3;
            thermal[1] = enabled ? (byte)(thermal[1] | 0x80) : (byte)(thermal[1] & 0x7F);
            Write("Set_Thermal", thermal);
        }

        public void Dispose()
        {
            if (parameters != null) parameters.Dispose();
            if (instance != null) instance.Dispose();
            parameters = null;
            instance = null;
        }
    }

    internal sealed class HardwareSnapshot
    {
        public int CpuTemperature;
        public int GpuTemperature;
        public int FanRpm;
        public byte FanMode;
        public byte PerformanceMode;
        public bool FullBlast;
    }

    public sealed class FanCurve
    {
        public int[] Temperatures { get; set; }
        public int[] Speeds { get; set; }

        public FanCurve()
        {
            Temperatures = new int[7];
            Speeds = new int[7];
        }

        public FanCurve(int[] temperatures, int[] speeds)
        {
            Temperatures = (int[])temperatures.Clone();
            Speeds = (int[])speeds.Clone();
        }

        public FanCurve Clone()
        {
            return new FanCurve(Temperatures, Speeds);
        }
    }
}
