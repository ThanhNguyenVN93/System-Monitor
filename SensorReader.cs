using System;
using LibreHardwareMonitor.Hardware;

namespace frm_sys_monitor
{
    // One hardware sample. Built on a worker thread, then applied to the UI in one go.
    internal sealed class SensorSnapshot
    {
        public int?  CpuLoad;
        public int?  GpuLoad;
        public int?  CpuTemp;
        public float GpuTemp;           // 0 = not reported
        public float CpuWatts, GpuWatts;
        public float CpuClockSumMHz;  public int CpuClockCount;
        public float VramUsedMB, VramTotalMB;
        public float GpuHotspotTemp, SsdTemp;
        public float RamUsedGB, RamAvailableGB;

        public float CpuClockAvgMHz => CpuClockCount > 0 ? CpuClockSumMHz / CpuClockCount : 0;
    }

    // Reads LHM sensors. No UI access: safe to call from a background thread.
    internal static class SensorReader
    {
        private sealed class UpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer) { computer.Traverse(this); }
            public void VisitHardware(IHardware hardware) { hardware.Update(); foreach (IHardware sub in hardware.SubHardware) sub.Accept(this); }
            public void VisitSensor(ISensor sensor) { }
            public void VisitParameter(IParameter parameter) { }
        }

        private static readonly UpdateVisitor Visitor = new UpdateVisitor();

        public static SensorSnapshot Read(IComputer computer)
        {
            computer.Accept(Visitor);
            var snap   = new SensorSnapshot();
            var picker = new CpuTempPicker();
            foreach (IHardware hw in computer.Hardware) ReadRecursive(hw, snap, picker);
            snap.CpuTemp = picker.Value;
            return snap;
        }

        private static void ReadRecursive(IHardware hw, SensorSnapshot snap, CpuTempPicker picker)
        {
            ReadHardware(hw, snap, picker);
            foreach (IHardware sub in hw.SubHardware) ReadRecursive(sub, snap, picker);
        }

        private static void ReadHardware(IHardware hw, SensorSnapshot snap, CpuTempPicker picker)
        {
            bool isCpu = hw.HardwareType == HardwareType.Cpu;
            bool isGpu = hw.HardwareType == HardwareType.GpuNvidia
                      || hw.HardwareType == HardwareType.GpuAmd
                      || hw.HardwareType == HardwareType.GpuIntel;
            bool isMb  = hw.HardwareType == HardwareType.Motherboard
                      || hw.HardwareType == HardwareType.SuperIO;
            bool isRam = hw.HardwareType == HardwareType.Memory
                      && hw.Name.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) < 0;
            bool isStorage = hw.HardwareType == HardwareType.Storage;

            foreach (ISensor s in hw.Sensors)
            {
                if (s.Value == null) continue;
                float val = s.Value.Value;

                if (isCpu)
                {
                    if (s.SensorType == SensorType.Load && s.Name == "CPU Total")
                        snap.CpuLoad = Clamp(val);
                    if (s.SensorType == SensorType.Temperature)
                        picker.Offer(s.Name, (int)val);
                    if (s.SensorType == SensorType.Power && val > 0)
                    {
                        if (s.Name == "CPU Package")  snap.CpuWatts = val;
                        else if (snap.CpuWatts <= 0)  snap.CpuWatts = val;
                    }
                    if (s.SensorType == SensorType.Clock && val > 0
                        && s.Name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        snap.CpuClockSumMHz += val;
                        snap.CpuClockCount++;
                    }
                }

                // Super I/O fallback: "CPU Core" etc. from the motherboard chip.
                if (isMb && s.SensorType == SensorType.Temperature)
                {
                    string n = s.Name;
                    if (n.IndexOf("CPU",  StringComparison.OrdinalIgnoreCase) >= 0
                     || n.IndexOf("Tdie", StringComparison.OrdinalIgnoreCase) >= 0
                     || n.IndexOf("Tctl", StringComparison.OrdinalIgnoreCase) >= 0)
                        picker.Offer(n, (int)val);
                }

                if (isGpu)
                {
                    if (s.SensorType == SensorType.Load && s.Name == "GPU Core")
                        snap.GpuLoad = Clamp(val);
                    if (s.SensorType == SensorType.Temperature)
                    {
                        if (s.Name == "GPU Core") snap.GpuTemp = val;
                        if ((s.Name == "GPU Hot Spot" || s.Name == "GPU Memory Junction")
                            && val > snap.GpuHotspotTemp)
                            snap.GpuHotspotTemp = val;
                    }
                    if (s.SensorType == SensorType.Power && val > 0)
                    {
                        if (s.Name == "GPU Power" || s.Name == "GPU Package") snap.GpuWatts = val;
                        else if (snap.GpuWatts <= 0)                          snap.GpuWatts = val;
                    }
                    if (s.SensorType == SensorType.SmallData)
                    {
                        if (s.Name == "GPU Memory Used")  snap.VramUsedMB  = val;
                        if (s.Name == "GPU Memory Total") snap.VramTotalMB = val;
                    }
                }

                if (isStorage && s.SensorType == SensorType.Temperature
                    && s.Name.IndexOf("Warning",  StringComparison.OrdinalIgnoreCase) < 0   // NVMe threshold sensors,
                    && s.Name.IndexOf("Critical", StringComparison.OrdinalIgnoreCase) < 0   // not live temperatures
                    && val > snap.SsdTemp)
                    snap.SsdTemp = val;

                if (isRam && s.SensorType == SensorType.Data)
                {
                    if (s.Name == "Memory Used")           snap.RamUsedGB      = val;
                    else if (s.Name == "Memory Available") snap.RamAvailableGB = val;
                }
            }
        }

        private static int Clamp(float v) => Math.Max(0, Math.Min((int)v, 100));
    }
}
