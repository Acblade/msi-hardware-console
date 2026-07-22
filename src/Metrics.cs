using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MsiHardwareConsole
{
    internal sealed class SystemUsageSnapshot
    {
        public int CpuPercent;
        public int DiscreteGpuPercent;
        public int IntegratedGpuPercent;
    }

    // Persistent PDH counters use the same Windows performance-counter source
    // as Task Manager without recreating a WMI query for every refresh.
    internal sealed class SystemUsageReader : IDisposable
    {
        private const uint PdhFormatDouble = 0x00000200;
        private const uint PdhMoreData = 0x800007D2;
        private readonly object sync = new object();
        private readonly Dictionary<string, GpuKind> adapters;
        private IntPtr query;
        private IntPtr cpuCounter;
        private IntPtr gpuCounter;
        private bool available;

        public SystemUsageReader()
        {
            adapters = DxgiAdapters.Read();
            try
            {
                available = PdhOpenQuery(null, IntPtr.Zero, out query) == 0 &&
                    PdhAddEnglishCounter(query, @"\Processor Information(_Total)\% Processor Utility", IntPtr.Zero, out cpuCounter) == 0 &&
                    PdhAddEnglishCounter(query, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out gpuCounter) == 0 &&
                    PdhCollectQueryData(query) == 0;
            }
            catch { available = false; }
        }

        public SystemUsageSnapshot Read()
        {
            var result = new SystemUsageSnapshot();
            lock (sync)
            {
                if (!available || PdhCollectQueryData(query) != 0) return result;
                result.CpuPercent = ReadCpu();
                ReadGpu(result);
            }
            return result;
        }

        private int ReadCpu()
        {
            uint type;
            PdhCounterValue value;
            if (PdhGetFormattedCounterValue(cpuCounter, PdhFormatDouble, out type, out value) != 0 || value.CStatus > 1)
                return 0;
            return Clamp((int)Math.Round(value.DoubleValue));
        }

        private void ReadGpu(SystemUsageSnapshot snapshot)
        {
            uint bytes = 0;
            uint count = 0;
            uint status = PdhGetFormattedCounterArray(gpuCounter, PdhFormatDouble, ref bytes, ref count, IntPtr.Zero);
            if (status != PdhMoreData || bytes == 0 || count == 0) return;

            IntPtr buffer = Marshal.AllocHGlobal((int)bytes);
            try
            {
                if (PdhGetFormattedCounterArray(gpuCounter, PdhFormatDouble, ref bytes, ref count, buffer) != 0) return;
                var engineTotals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                int itemSize = Marshal.SizeOf(typeof(PdhCounterValueItem));
                for (int i = 0; i < count; i++)
                {
                    var item = (PdhCounterValueItem)Marshal.PtrToStructure(
                        IntPtr.Add(buffer, i * itemSize), typeof(PdhCounterValueItem));
                    if (item.Value.CStatus > 1 || item.Name == IntPtr.Zero) continue;
                    string name = Marshal.PtrToStringUni(item.Name);
                    string luid;
                    string engine;
                    if (!TryParseEngine(name, out luid, out engine) || !adapters.ContainsKey(luid)) continue;
                    string key = luid + "|" + engine;
                    double total;
                    engineTotals.TryGetValue(key, out total);
                    engineTotals[key] = total + Math.Max(0, item.Value.DoubleValue);
                }

                foreach (var pair in engineTotals)
                {
                    int separator = pair.Key.IndexOf('|');
                    string luid = pair.Key.Substring(0, separator);
                    int value = Clamp((int)Math.Round(pair.Value));
                    if (adapters[luid] == GpuKind.Integrated)
                        snapshot.IntegratedGpuPercent = Math.Max(snapshot.IntegratedGpuPercent, value);
                    else if (adapters[luid] == GpuKind.Discrete)
                        snapshot.DiscreteGpuPercent = Math.Max(snapshot.DiscreteGpuPercent, value);
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        private static bool TryParseEngine(string name, out string luid, out string engine)
        {
            luid = null;
            engine = null;
            int luidStart = name.IndexOf("luid_", StringComparison.OrdinalIgnoreCase);
            int physStart = name.IndexOf("_phys_", StringComparison.OrdinalIgnoreCase);
            int typeStart = name.IndexOf("_engtype_", StringComparison.OrdinalIgnoreCase);
            if (luidStart < 0 || physStart <= luidStart || typeStart <= physStart) return false;
            luid = name.Substring(luidStart + 5, physStart - luidStart - 5).ToUpperInvariant();
            engine = name.Substring(physStart + 1, typeStart - physStart - 1);
            return true;
        }

        private static int Clamp(int value) { return Math.Max(0, Math.Min(100, value)); }

        public void Dispose()
        {
            lock (sync)
            {
                if (query != IntPtr.Zero) PdhCloseQuery(query);
                query = cpuCounter = gpuCounter = IntPtr.Zero;
                available = false;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct PdhCounterValue
        {
            [FieldOffset(0)] public uint CStatus;
            [FieldOffset(8)] public double DoubleValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PdhCounterValueItem
        {
            public IntPtr Name;
            public PdhCounterValue Value;
        }

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhOpenQuery(string source, IntPtr userData, out IntPtr query);
        [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhAddEnglishCounterW")]
        private static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);
        [DllImport("pdh.dll")]
        private static extern uint PdhCollectQueryData(IntPtr query);
        [DllImport("pdh.dll")]
        private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out PdhCounterValue value);
        [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhGetFormattedCounterArrayW")]
        private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, ref uint itemCount, IntPtr itemBuffer);
        [DllImport("pdh.dll")]
        private static extern uint PdhCloseQuery(IntPtr query);

        private enum GpuKind { Other, Integrated, Discrete }

        private static class DxgiAdapters
        {
            [StructLayout(LayoutKind.Sequential)]
            private struct Luid { public uint LowPart; public int HighPart; }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            private struct AdapterDesc1
            {
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
                public uint VendorId, DeviceId, SubSysId, Revision;
                public UIntPtr DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
                public Luid AdapterLuid;
                public uint Flags;
            }

            [ComImport, Guid("770aae78-f26f-4dba-a829-253c83d1b387"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IDXGIFactory1
            {
                void SetPrivateData(); void SetPrivateDataInterface(); void GetPrivateData(); void GetParent();
                void EnumAdapters(); void MakeWindowAssociation(); void GetWindowAssociation(); void CreateSwapChain();
                void CreateSoftwareAdapter();
                [PreserveSig] int EnumAdapters1(uint index, out IDXGIAdapter1 adapter);
                [PreserveSig] bool IsCurrent();
            }

            [ComImport, Guid("29038f61-3839-4626-91fd-086879011a05"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IDXGIAdapter1
            {
                void SetPrivateData(); void SetPrivateDataInterface(); void GetPrivateData(); void GetParent();
                void EnumOutputs(); void GetDesc(); void CheckInterfaceSupport();
                void GetDesc1(out AdapterDesc1 desc);
            }

            [DllImport("dxgi.dll")]
            private static extern int CreateDXGIFactory1(ref Guid riid, out IDXGIFactory1 factory);

            public static Dictionary<string, GpuKind> Read()
            {
                var result = new Dictionary<string, GpuKind>(StringComparer.OrdinalIgnoreCase);
                IDXGIFactory1 factory = null;
                try
                {
                    Guid id = typeof(IDXGIFactory1).GUID;
                    Marshal.ThrowExceptionForHR(CreateDXGIFactory1(ref id, out factory));
                    for (uint index = 0; ; index++)
                    {
                        IDXGIAdapter1 adapter;
                        if (factory.EnumAdapters1(index, out adapter) != 0) break;
                        try
                        {
                            AdapterDesc1 desc;
                            adapter.GetDesc1(out desc);
                            string luid = string.Format("0X{0:X8}_0X{1:X8}", desc.AdapterLuid.HighPart, desc.AdapterLuid.LowPart);
                            result[luid] = desc.VendorId == 0x8086 ? GpuKind.Integrated
                                : desc.VendorId == 0x10DE ? GpuKind.Discrete : GpuKind.Other;
                        }
                        finally { Marshal.ReleaseComObject(adapter); }
                    }
                }
                catch { }
                finally { if (factory != null) Marshal.ReleaseComObject(factory); }
                return result;
            }
        }
    }
}
