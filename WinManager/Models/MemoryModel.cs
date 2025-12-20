using System;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace WinManager.Models
{
    internal class MemoryModel : IDisposable
    {
        private readonly PerformanceCounter _availableMemoryCounter;

        // Cache values
        private ulong _totalPhysicalMemory;
        private int _memorySlots;
        private int _slotsUsed;
        private string _formFactor = "Unknown";
        private long _memorySpeed; // MHz

        // P/Invoke for getting memory info
        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        public MemoryModel()
        {
            // Initialize performance counter for available memory
            _availableMemoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            _availableMemoryCounter.NextValue();

            InitializeMemoryInfo();

            // Give counter time to initialize
            System.Threading.Thread.Sleep(100);
        }

        private void InitializeMemoryInfo()
        {
            try
            {
                // Get total physical memory using P/Invoke
                MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));

                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    _totalPhysicalMemory = memStatus.ullTotalPhys;
                }

                // Get memory slot information from WMI
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory"))
                {
                    var memoryModules = searcher.Get();
                    _slotsUsed = memoryModules.Count;

                    foreach (ManagementObject module in memoryModules)
                    {
                        // Get memory speed (first module)
                        if (_memorySpeed == 0)
                        {
                            var speed = module["Speed"];
                            if (speed != null)
                            {
                                _memorySpeed = Convert.ToInt64(speed);
                            }
                        }

                        // Get form factor (first module)
                        if (_formFactor == "Unknown")
                        {
                            var formFactor = module["FormFactor"];
                            if (formFactor != null)
                            {
                                _formFactor = GetFormFactorName(Convert.ToInt32(formFactor));
                            }
                        }
                    }
                }

                // Get total memory slots
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemoryArray"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var slots = obj["MemoryDevices"];
                        if (slots != null)
                        {
                            _memorySlots = Convert.ToInt32(slots);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing memory info: {ex.Message}");

                // Fallback using P/Invoke
                MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    _totalPhysicalMemory = memStatus.ullTotalPhys;
                }

                _memorySlots = 2; // Default assumption
                _slotsUsed = 1;
            }
        }

        private string GetFormFactorName(int formFactor)
        {
            return formFactor switch
            {
                8 => "DIMM",
                12 => "SODIMM",
                13 => "RIMM",
                _ => "Unknown"
            };
        }

        private MEMORYSTATUSEX GetMemoryStatus()
        {
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            GlobalMemoryStatusEx(ref memStatus);
            return memStatus;
        }

        // Memory Usage (%) - float
        public float GetMemoryUsagePercent()
        {
            var memStatus = GetMemoryStatus();
            return memStatus.dwMemoryLoad;
        }

        // In use (Compressed) - GB and MB
        public double GetInUseMemoryGB()
        {
            var memStatus = GetMemoryStatus();
            ulong usedMemory = memStatus.ullTotalPhys - memStatus.ullAvailPhys;
            return usedMemory / (1024.0 * 1024.0 * 1024.0); // Convert to GB
        }

        public double GetInUseMemoryMB()
        {
            return GetInUseMemoryGB() * 1024.0;
        }

        public string GetInUseMemoryFormatted()
        {
            double gb = GetInUseMemoryGB();
            double mb = GetCompressedMemoryMB(); // Approximate compressed size
            return $"{gb:F1} GB ({mb:F1} MB)";
        }

        // Available - GB
        public double GetAvailableMemoryGB()
        {
            var memStatus = GetMemoryStatus();
            return memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
        }

        public string GetAvailableMemoryFormatted()
        {
            return $"{GetAvailableMemoryGB():F1} GB";
        }

        // Committed - GB (current/total)
        public double GetCommittedMemoryGB()
        {
            try
            {
                var memStatus = GetMemoryStatus();
                ulong committed = memStatus.ullTotalPageFile - memStatus.ullAvailPageFile;
                return committed / (1024.0 * 1024.0 * 1024.0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting committed memory: {ex.Message}");
                return 0;
            }
        }

        public double GetCommittedLimitGB()
        {
            try
            {
                var memStatus = GetMemoryStatus();
                return memStatus.ullTotalPageFile / (1024.0 * 1024.0 * 1024.0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting committed limit: {ex.Message}");
                return 0;
            }
        }

        public string GetCommittedMemoryFormatted()
        {
            return $"{GetCommittedMemoryGB():F1}/{GetCommittedLimitGB():F1} GB";
        }

        // Cached - GB
        public double GetCachedMemoryGB()
        {
            try
            {
                using (var counter = new PerformanceCounter("Memory", "Cache Bytes"))
                {
                    counter.NextValue();
                    System.Threading.Thread.Sleep(100);
                    return counter.NextValue() / (1024.0 * 1024.0 * 1024.0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting cached memory: {ex.Message}");
                return 0;
            }
        }

        public string GetCachedMemoryFormatted()
        {
            return $"{GetCachedMemoryGB():F1} GB";
        }

        // Paged pool - MB
        public double GetPagedPoolMB()
        {
            try
            {
                using (var counter = new PerformanceCounter("Memory", "Pool Paged Bytes"))
                {
                    counter.NextValue();
                    System.Threading.Thread.Sleep(100);
                    return counter.NextValue() / (1024.0 * 1024.0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting paged pool: {ex.Message}");
                return 0;
            }
        }

        public string GetPagedPoolFormatted()
        {
            return $"{GetPagedPoolMB():F0} MB";
        }

        // Non-paged pool - GB/MB
        public double GetNonPagedPoolMB()
        {
            try
            {
                using (var counter = new PerformanceCounter("Memory", "Pool Nonpaged Bytes"))
                {
                    counter.NextValue();
                    System.Threading.Thread.Sleep(100);
                    return counter.NextValue() / (1024.0 * 1024.0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting non-paged pool: {ex.Message}");
                return 0;
            }
        }

        public double GetNonPagedPoolGB()
        {
            return GetNonPagedPoolMB() / 1024.0;
        }

        public string GetNonPagedPoolFormatted()
        {
            double mb = GetNonPagedPoolMB();
            if (mb >= 1024)
            {
                return $"{mb / 1024.0:F1} GB";
            }
            return $"{mb:F0} MB";
        }

        // Hardware specifications
        public long GetMemorySpeed()
        {
            return _memorySpeed;
        }

        public string GetMemorySpeedFormatted()
        {
            return $"{_memorySpeed} MT/s";
        }

        public int GetSlotsUsed()
        {
            return _slotsUsed;
        }

        public int GetTotalSlots()
        {
            return _memorySlots;
        }

        public string GetSlotsInfo()
        {
            return $"{_slotsUsed} of {_memorySlots}";
        }

        public string GetFormFactor()
        {
            return _formFactor;
        }

        public double GetTotalMemoryGB()
        {
            return _totalPhysicalMemory / (1024.0 * 1024.0 * 1024.0);
        }

        public string GetTotalMemoryFormatted()
        {
            return $"{GetTotalMemoryGB():F1} GB";
        }

        // Hardware reserved - MB (approximate)
        public double GetHardwareReservedMB()
        {
            try
            {
                // Get installed physical memory from WMI
                ulong installedMemory = 0;
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory"))
                {
                    foreach (ManagementObject module in searcher.Get())
                    {
                        var capacity = module["Capacity"];
                        if (capacity != null)
                        {
                            installedMemory += Convert.ToUInt64(capacity);
                        }
                    }
                }

                // Hardware reserved = Installed - Available to OS
                long reserved = (long)(installedMemory - _totalPhysicalMemory);
                if (reserved > 0)
                {
                    return reserved / (1024.0 * 1024.0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error calculating hardware reserved: {ex.Message}");
            }
            return 0;
        }

        public string GetHardwareReservedFormatted()
        {
            return $"{GetHardwareReservedMB():F0} MB";
        }

        // Compressed memory estimate (Windows 10+)
        private double GetCompressedMemoryMB()
        {
            try
            {
                // Approximate: compressed memory is usually 10-30% of in-use
                return GetInUseMemoryMB() * 0.01; // Very rough estimate
            }
            catch
            {
                return 0;
            }
        }

        // Get all memory information at once
        public MemoryInfo GetMemoryInfo()
        {
            return new MemoryInfo
            {
                MemoryUsagePercent = GetMemoryUsagePercent(),
                InUseGB = GetInUseMemoryGB(),
                InUseMB = GetInUseMemoryMB(),
                AvailableGB = GetAvailableMemoryGB(),
                CommittedGB = GetCommittedMemoryGB(),
                CommittedLimitGB = GetCommittedLimitGB(),
                CachedGB = GetCachedMemoryGB(),
                PagedPoolMB = GetPagedPoolMB(),
                NonPagedPoolMB = GetNonPagedPoolMB(),
                TotalMemoryGB = GetTotalMemoryGB(),
                MemorySpeed = _memorySpeed,
                SlotsUsed = _slotsUsed,
                TotalSlots = _memorySlots,
                FormFactor = _formFactor,
                HardwareReservedMB = GetHardwareReservedMB()
            };
        }

        // Debug method to print all memory information
        public void PrintMemoryInfo()
        {
            var info = GetMemoryInfo();

            Debug.WriteLine("=== Memory Information ===");
            Debug.WriteLine($"Memory Usage: {info.MemoryUsagePercent:F2}%");
            Debug.WriteLine($"In use (Compressed): {info.InUseGB:F1} GB ({info.InUseMB * 0.01:F1} MB)");
            Debug.WriteLine($"Available: {info.AvailableGB:F1} GB");
            Debug.WriteLine($"Committed: {info.CommittedGB:F1}/{info.CommittedLimitGB:F1} GB");
            Debug.WriteLine($"Cached: {info.CachedGB:F1} GB");
            Debug.WriteLine($"Paged pool: {info.PagedPoolMB:F0} MB");
            Debug.WriteLine($"Non-paged pool: {info.NonPagedPoolMB:F0} MB");
            Debug.WriteLine("");
            Debug.WriteLine($"Speed: {info.MemorySpeed} MT/s");
            Debug.WriteLine($"Slots used: {info.SlotsUsed} of {info.TotalSlots}");
            Debug.WriteLine($"Form factor: {info.FormFactor}");
            Debug.WriteLine($"Hardware reserved: {info.HardwareReservedMB:F0} MB");
            Debug.WriteLine($"Total: {info.TotalMemoryGB:F1} GB");
            Debug.WriteLine("==========================");
        }

        // Cleanup
        public void Dispose()
        {
            _availableMemoryCounter?.Dispose();
        }
    }

    // Data class to hold all memory information
    public class MemoryInfo
    {
        public float MemoryUsagePercent { get; set; }
        public double InUseGB { get; set; }
        public double InUseMB { get; set; }
        public double AvailableGB { get; set; }
        public double CommittedGB { get; set; }
        public double CommittedLimitGB { get; set; }
        public double CachedGB { get; set; }
        public double PagedPoolMB { get; set; }
        public double NonPagedPoolMB { get; set; }
        public double TotalMemoryGB { get; set; }
        public long MemorySpeed { get; set; }
        public int SlotsUsed { get; set; }
        public int TotalSlots { get; set; }
        public string FormFactor { get; set; } = string.Empty;
        public double HardwareReservedMB { get; set; }
    }
}