// dotnet add package LibreHardwareMonitorLib
using LibreHardwareMonitor.Hardware;
using System.Diagnostics;
using System.Management;

namespace WinManager.Models
{
    internal class CpuModel : IDisposable
    {
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _cpuFrequencyCounter;
        private readonly Computer _computer;
        private int _coreCount;
        private int _logicalProcessors;
        private float _baseSpeed;
        private float _maxSpeed;
        private string _cpuName = string.Empty;
        private int _socketCount;

        public CpuModel()
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue();

            // Thêm counter để đọc tần số thực tế
            _cpuFrequencyCounter = new PerformanceCounter(
                "Processor Information",
                "% Processor Performance",
                "_Total"
            );
            _cpuFrequencyCounter.NextValue();

            // Initialize LibreHardwareMonitor
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = false,
                IsMemoryEnabled = false,
                IsMotherboardEnabled = true, // Enable motherboard for additional sensors
                IsControllerEnabled = false,
                IsNetworkEnabled = false,
                IsStorageEnabled = false
            };
            _computer.Open();

            // Update all hardware to get initial readings
            foreach (IHardware hardware in _computer.Hardware)
            {
                hardware.Update();
            }

            InitializeCpuInfo();

            // Give CPU counter time to initialize
            System.Threading.Thread.Sleep(100);
        }

        private void InitializeCpuInfo()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_Processor"))
                {
                    var processors = searcher.Get().Cast<ManagementObject>().ToList();
                    _socketCount = processors.Count;

                    foreach (ManagementObject obj in processors)
                    {
                        _cpuName = obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
                        _coreCount = Convert.ToInt32(obj["NumberOfCores"]);
                        _logicalProcessors = Convert.ToInt32(obj["NumberOfLogicalProcessors"]);

                        // Base speed (current speed) in MHz
                        var currentSpeed = obj["CurrentClockSpeed"];
                        if (currentSpeed != null)
                        {
                            _baseSpeed = Convert.ToSingle(currentSpeed) / 1000f; // Convert to GHz
                        }

                        // Max speed in MHz
                        var maxSpeed = obj["MaxClockSpeed"];
                        if (maxSpeed != null)
                        {
                            _maxSpeed = Convert.ToSingle(maxSpeed) / 1000f; // Convert to GHz
                        }

                        break; // Get info from first processor (if multiple sockets, they're usually identical)
                    }
                }

                // Get total logical processors from Environment
                if (_logicalProcessors == 0)
                {
                    _logicalProcessors = Environment.ProcessorCount;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing CPU info: {ex.Message}");
                // Fallback values
                _cpuName = "Unknown CPU";
                _logicalProcessors = Environment.ProcessorCount;
                _coreCount = _logicalProcessors;
                _socketCount = 1;
            }
        }

        // CPU Usage(%) - float - Tải 1 thời điểm
        public float GetCpuUsage()
        {
            return _cpuCounter.NextValue();
        }

        // Thời gian bắt đầu chạy - hh:mm:ss
        public string GetUptime()
        {
            TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            return uptime.ToString(@"hh\:mm\:ss");
        }

        // Số core, số sockets - int
        public int GetCoreCount()
        {
            return _coreCount;
        }

        public int GetSocketCount()
        {
            return _socketCount;
        }

        public string GetCoreSocketInfo()
        {
            return $"{_coreCount} Cores, {_socketCount} Sockets";
        }

        // Xung nhịp(GHz) - float
        public float GetCurrentSpeed()
        {
            try
            {
                if (_cpuFrequencyCounter != null)
                {
                    // "% Processor Performance" trả về phần trăm so với base frequency
                    // Ví dụ: 100% = base speed, 150% = 1.5x base speed
                    float performancePercent = _cpuFrequencyCounter.NextValue();

                    if (performancePercent > 0)
                    {
                        float currentSpeed = _baseSpeed * (performancePercent / 100f);

                        return currentSpeed;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting current speed from counter: {ex.Message}");
            }

            // Fallback: Try WMI real-time query
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT CurrentClockSpeed FROM Win32_Processor");

                foreach (ManagementObject obj in searcher.Get())
                {
                    float speed = Convert.ToSingle(obj["CurrentClockSpeed"]) / 1000f;

                    return speed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting current speed from WMI: {ex.Message}");
            }

            return _baseSpeed;
        }

        // Nhiệt độ CPU, Công suất tiêu thụ, điện áp - float
        public float GetTemperature()
        {
            try
            {
                // Try CPU sensors first
                foreach (IHardware hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        hardware.Update();

                        foreach (ISensor sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Value.Value > 0)
                            {
                                return sensor.Value.Value;
                            }
                        }
                    }
                }

                // Try motherboard sensors if CPU doesn't have temperature
                foreach (IHardware hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.Motherboard)
                    {
                        hardware.Update();

                        // Look for CPU-related temperature on motherboard
                        foreach (ISensor sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature &&
                                (sensor.Name.Contains("CPU") || sensor.Name.Contains("Processor")))
                            {
                                if (sensor.Value.HasValue && sensor.Value.Value > 0)
                                    return sensor.Value.Value;
                            }
                        }

                        // Check sub-hardware (like Super I/O chips)
                        foreach (IHardware subHardware in hardware.SubHardware)
                        {
                            subHardware.Update();
                            foreach (ISensor sensor in subHardware.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Temperature &&
                                    (sensor.Name.Contains("CPU") || sensor.Name.Contains("Processor")))
                                {
                                    if (sensor.Value.HasValue && sensor.Value.Value > 0)
                                        return sensor.Value.Value;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting temperature: {ex.Message}");
            }
            return 0f;
        }

        public float GetPowerConsumption()
        {
            try
            {
                foreach (IHardware hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        hardware.Update();

                        // Debug: print all power sensors
                        foreach (ISensor sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Power)
                            {
                                //Debug.WriteLine($"Power Sensor: {sensor.Name} = {sensor.Value}");
                            }
                        }

                        foreach (ISensor sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Power)
                            {
                                if (sensor.Name.Contains("Package") ||
                                    sensor.Name.Contains("CPU Package") ||
                                    sensor.Name.Contains("CPU Cores"))
                                {
                                    var value = sensor.Value.GetValueOrDefault();
                                    if (value > 0)
                                        return value;
                                }
                            }
                        }

                        // Fallback: get first available power reading
                        foreach (ISensor sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue && sensor.Value.Value > 0)
                            {
                                return sensor.Value.Value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting power consumption: {ex.Message}");
            }
            return 0f;
        }

        public float GetVoltage()
        {
            try
            {
                foreach (IHardware hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        hardware.Update();

                        // Debug: print all voltage sensors
                        foreach (ISensor sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Voltage)
                            {
                                //Debug.WriteLine($"Voltage Sensor: {sensor.Name} = {sensor.Value}");
                            }
                        }

                        foreach (ISensor sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Voltage)
                            {
                                if (sensor.Name.Contains("Core") ||
                                    sensor.Name.Contains("CPU") ||
                                    sensor.Name.Contains("VID"))
                                {
                                    var value = sensor.Value.GetValueOrDefault();
                                    if (value > 0)
                                        return value;
                                }
                            }
                        }

                        // Fallback: get first available voltage reading
                        foreach (ISensor sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Voltage && sensor.Value.HasValue && sensor.Value.Value > 0)
                            {
                                return sensor.Value.Value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting voltage: {ex.Message}");
            }
            return 0f;
        }

        // Số tiến trình, số Threads, số handles - int
        public int GetProcessCount()
        {
            return Process.GetProcesses().Length;
        }

        public int GetThreadCount()
        {
            int totalThreads = 0;
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    totalThreads += process.Threads.Count;
                }
                catch
                {
                    // Skip processes we can't access
                }
            }
            return totalThreads;
        }

        public int GetHandleCount()
        {
            int totalHandles = 0;
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    totalHandles += process.HandleCount;
                }
                catch
                {
                    // Skip processes we can't access
                }
            }
            return totalHandles;
        }

        public string GetThreadHandleInfo()
        {
            return $"{GetProcessCount()} Processes, {GetThreadCount()} Threads, {GetHandleCount()} Handles";
        }

        // Additional utility methods
        public string GetCpuName()
        {
            return _cpuName;
        }

        public int GetLogicalProcessors()
        {
            return _logicalProcessors;
        }

        public float GetMaxSpeed()
        {
            return _maxSpeed;
        }

        // Get all CPU information at once
        public CpuInfo GetCpuInfo()
        {
            return new CpuInfo
            {
                CpuUsage = GetCpuUsage(),
                Uptime = GetUptime(),
                CoreCount = GetCoreCount(),
                SocketCount = GetSocketCount(),
                LogicalProcessors = GetLogicalProcessors(),
                CurrentSpeed = GetCurrentSpeed(),
                MaxSpeed = GetMaxSpeed(),
                Temperature = GetTemperature(),
                PowerConsumption = GetPowerConsumption(),
                Voltage = GetVoltage(),
                ProcessCount = GetProcessCount(),
                ThreadCount = GetThreadCount(),
                HandleCount = GetHandleCount(),
                CpuName = GetCpuName()
            };
        }

        // Debug method to print all CPU information
        public void PrintCpuInfo()
        {
            var info = GetCpuInfo();

            Debug.WriteLine("=== CPU Information ===");
            Debug.WriteLine($"CPU Name: {info.CpuName}");
            Debug.WriteLine($"CPU Usage: {info.CpuUsage:F2}%");
            Debug.WriteLine($"Uptime: {info.Uptime}");
            Debug.WriteLine($"Cores: {info.CoreCount} | Sockets: {info.SocketCount} | Logical Processors: {info.LogicalProcessors}");
            Debug.WriteLine($"Current Speed: {info.CurrentSpeed:F2} GHz | Max Speed: {info.MaxSpeed:F2} GHz");
            Debug.WriteLine($"Temperature: {info.Temperature:F1}°C");
            Debug.WriteLine($"Power Consumption: {info.PowerConsumption:F2}W");
            Debug.WriteLine($"Voltage: {info.Voltage:F3}V");
            Debug.WriteLine($"Processes: {info.ProcessCount} | Threads: {info.ThreadCount} | Handles: {info.HandleCount}");
            Debug.WriteLine("======================");

            // Additional debug: Check ALL sensors with values
            Debug.WriteLine("\n=== All Available Sensors ===");
            foreach (IHardware hardware in _computer.Hardware)
            {
                Debug.WriteLine($"\n[{hardware.HardwareType}] {hardware.Name}");

                if (hardware.HardwareType == HardwareType.Cpu || hardware.HardwareType == HardwareType.Motherboard)
                {
                    int validSensors = 0;
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.Value.HasValue && sensor.Value.Value > 0)
                        {
                            validSensors++;
                            Debug.WriteLine($"  [{sensor.SensorType}] {sensor.Name} = {sensor.Value.Value:F2} {GetUnit(sensor.SensorType)}");
                        }
                    }

                    // Check sub-hardware (like Super I/O)
                    foreach (IHardware subHardware in hardware.SubHardware)
                    {
                        Debug.WriteLine($"  Sub-Hardware: {subHardware.Name}");
                        foreach (ISensor sensor in subHardware.Sensors)
                        {
                            if (sensor.Value.HasValue && sensor.Value.Value > 0)
                            {
                                validSensors++;
                                Debug.WriteLine($"    [{sensor.SensorType}] {sensor.Name} = {sensor.Value.Value:F2} {GetUnit(sensor.SensorType)}");
                            }
                        }
                    }

                    Debug.WriteLine($"  Valid Sensors: {validSensors}");
                }
            }

            Debug.WriteLine("\n=== Sensor Summary ===");
            Debug.WriteLine($"  Has Temperature: {HasTemperature()}");
            Debug.WriteLine($"  Has Power: {HasPower()}");
            Debug.WriteLine($"  Has Voltage: {HasVoltage()}");

            if (!HasTemperature() || !HasPower() || !HasVoltage())
            {
                Debug.WriteLine("\nNOTE: Missing sensors detected.");
                Debug.WriteLine("This is normal for some laptop CPUs (especially Intel Gen 11).");
                Debug.WriteLine("These sensors may not be exposed by the hardware.");
            }
            Debug.WriteLine("=============================\n");
        }

        private string GetUnit(SensorType type)
        {
            return type switch
            {
                SensorType.Temperature => "°C",
                SensorType.Power => "W",
                SensorType.Voltage => "V",
                SensorType.Clock => "MHz",
                SensorType.Load => "%",
                SensorType.Fan => "RPM",
                _ => ""
            };
        }

        private bool HasTemperature()
        {
            foreach (IHardware hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Value.Value > 0)
                            return true;
                    }
                }
            }
            return false;
        }

        private bool HasPower()
        {
            foreach (IHardware hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue && sensor.Value.Value > 0)
                            return true;
                    }
                }
            }
            return false;
        }

        private bool HasVoltage()
        {
            foreach (IHardware hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Voltage && sensor.Value.HasValue && sensor.Value.Value > 0)
                            return true;
                    }
                }
            }
            return false;
        }

        // Cleanup
        public void Dispose()
        {
            _cpuFrequencyCounter?.Dispose();
            _cpuCounter?.Dispose();
            _computer?.Close();
        }
    }

    // Data class to hold all CPU information
    public class CpuInfo
    {
        public float CpuUsage { get; set; }
        public string Uptime { get; set; } = string.Empty;
        public int CoreCount { get; set; }
        public int SocketCount { get; set; }
        public int LogicalProcessors { get; set; }
        public float CurrentSpeed { get; set; }
        public float MaxSpeed { get; set; }
        public float Temperature { get; set; }
        public float PowerConsumption { get; set; }
        public float Voltage { get; set; }
        public int ProcessCount { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public string CpuName { get; set; } = string.Empty;
    }
}