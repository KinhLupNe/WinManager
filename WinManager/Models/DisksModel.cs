using System.Diagnostics;
using System.IO;
using System.Management;

namespace WinManager.Models
{
    internal class DisksModel : IDisposable
    {
        private readonly Dictionary<string, PerformanceCounter> _diskReadCounters;
        private readonly Dictionary<string, PerformanceCounter> _diskWriteCounters;
        private readonly Dictionary<string, PerformanceCounter> _diskActiveTimeCounters;
        private readonly Dictionary<string, PerformanceCounter> _diskResponseTimeCounters;
        private readonly Dictionary<string, PerformanceCounter> _diskTransferRateCounters;
        private readonly List<DiskInfo> _disks;

        // For tracking transfer rate history (for graphing)
        private readonly Dictionary<string, List<float>> _transferRateHistory;

        public DisksModel()
        {
            _diskReadCounters = new Dictionary<string, PerformanceCounter>();
            _diskWriteCounters = new Dictionary<string, PerformanceCounter>();
            _diskActiveTimeCounters = new Dictionary<string, PerformanceCounter>();
            _diskResponseTimeCounters = new Dictionary<string, PerformanceCounter>();
            _diskTransferRateCounters = new Dictionary<string, PerformanceCounter>();
            _disks = new List<DiskInfo>();
            _transferRateHistory = new Dictionary<string, List<float>>();

            InitializeDisks();
            InitializePerformanceCounters();

            // Give counters time to initialize
            System.Threading.Thread.Sleep(100);
        }

        private void InitializeDisks()
        {
            try
            {
                // Get physical disks from WMI
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject disk in searcher.Get())
                    {
                        var diskInfo = new DiskInfo
                        {
                            DeviceID = disk["DeviceID"]?.ToString() ?? "",
                            Model = disk["Model"]?.ToString() ?? "Unknown",
                            InterfaceType = disk["InterfaceType"]?.ToString() ?? "Unknown",
                            MediaType = disk["MediaType"]?.ToString() ?? "Unknown",
                            Size = Convert.ToUInt64(disk["Size"] ?? 0),
                            Partitions = Convert.ToInt32(disk["Partitions"] ?? 0)
                        };

                        // Get disk index for performance counters
                        var deviceId = diskInfo.DeviceID;
                        if (deviceId.Contains("PHYSICALDRIVE"))
                        {
                            var parts = deviceId.Split('\\');
                            diskInfo.PhysicalDriveNumber = parts[parts.Length - 1].Replace("PHYSICALDRIVE", "");
                        }

                        // Determine disk type (SSD/HDD)
                        diskInfo.DiskType = DetectDiskType(diskInfo);

                        // Get partitions/volumes for this disk
                        diskInfo.Volumes = GetVolumesForDisk(diskInfo.DeviceID);

                        // Calculate formatted capacity (sum of all volumes)
                        diskInfo.FormattedCapacity = diskInfo.Volumes.Aggregate(0UL, (sum, vol) => sum + vol.TotalSize);

                        // Check if system disk (contains C: drive)
                        diskInfo.IsSystemDisk = diskInfo.Volumes.Any(v => v.DriveLetter.Equals("C:", StringComparison.OrdinalIgnoreCase));

                        // Check for page file
                        diskInfo.HasPageFile = CheckPageFile(diskInfo.Volumes);

                        _disks.Add(diskInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing disks: {ex.Message}");
            }
        }

        private string DetectDiskType(DiskInfo diskInfo)
        {
            try
            {
                // Try to detect SSD using MSFT_PhysicalDisk (Windows 8+)
                using (var searcher = new ManagementObjectSearcher(
                    "root\\Microsoft\\Windows\\Storage",
                    $"SELECT MediaType FROM MSFT_PhysicalDisk WHERE DeviceId = '{diskInfo.PhysicalDriveNumber}'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var mediaType = Convert.ToUInt16(obj["MediaType"]);
                        return mediaType switch
                        {
                            3 => "HDD",
                            4 => "SSD",
                            5 => "SCM", // Storage Class Memory
                            _ => "Unknown"
                        };
                    }
                }
            }
            catch
            {
                // Fallback: Use media type or interface type
                if (diskInfo.MediaType.Contains("SSD") ||
                    diskInfo.Model.Contains("SSD") ||
                    diskInfo.InterfaceType.Contains("NVMe"))
                {
                    return "SSD";
                }

                if (diskInfo.MediaType.Contains("Fixed hard disk"))
                {
                    return "HDD";
                }
            }

            return "Unknown";
        }

        private string FindInstanceName(string physicalDriveNumber, string[] availableInstances)
        {
            // Try exact match first: "0", "1", etc.
            if (availableInstances.Contains(physicalDriveNumber))
            {
                return physicalDriveNumber;
            }

            // Try with different formats
            var formats = new[]
            {
                physicalDriveNumber,                           // "0"
                $"{physicalDriveNumber} ",                     // "0 " (with space)
                $"PhysicalDrive{physicalDriveNumber}",        // "PhysicalDrive0"
                $"{physicalDriveNumber} C:",                   // "0 C:" (if system disk)
            };

            foreach (var format in formats)
            {
                if (availableInstances.Contains(format))
                {
                    return format;
                }
            }

            // Try partial match (instance contains the drive number)
            foreach (var instance in availableInstances)
            {
                // Skip "_Total" instance
                if (instance == "_Total") continue;

                // Check if instance starts with the drive number
                if (instance.StartsWith(physicalDriveNumber + " "))
                {
                    return instance;
                }
            }

            // Last resort: try to parse instance name
            // Some systems use format like "0 C: D:" for multi-partition disks
            foreach (var instance in availableInstances)
            {
                if (instance == "_Total") continue;

                var parts = instance.Split(' ');
                if (parts.Length > 0 && parts[0] == physicalDriveNumber)
                {
                    return instance;
                }
            }

            return null;
        }

        private bool CheckPageFile(List<VolumeInfo> volumes)
        {
            try
            {
                // Check if any volume on this disk has a page file
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PageFileUsage"))
                {
                    foreach (ManagementObject pageFile in searcher.Get())
                    {
                        var pageFilePath = pageFile["Name"]?.ToString();
                        if (!string.IsNullOrEmpty(pageFilePath))
                        {
                            var pageFileDrive = Path.GetPathRoot(pageFilePath);
                            if (volumes.Any(v => v.DriveLetter.Equals(pageFileDrive.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking page file: {ex.Message}");
            }
            return false;
        }

        private List<VolumeInfo> GetVolumesForDisk(string diskDeviceID)
        {
            var volumes = new List<VolumeInfo>();

            try
            {
                // Get partitions for this disk
                using (var partitionSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{diskDeviceID}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition"))
                {
                    foreach (ManagementObject partition in partitionSearcher.Get())
                    {
                        // Get logical disks for this partition
                        var partitionDeviceID = partition["DeviceID"]?.ToString();

                        using (var logicalSearcher = new ManagementObjectSearcher(
                            $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionDeviceID}'}} WHERE AssocClass = Win32_LogicalDiskToPartition"))
                        {
                            foreach (ManagementObject logical in logicalSearcher.Get())
                            {
                                var volumeInfo = new VolumeInfo
                                {
                                    DriveLetter = logical["DeviceID"]?.ToString() ?? "",
                                    Label = logical["VolumeName"]?.ToString() ?? "",
                                    FileSystem = logical["FileSystem"]?.ToString() ?? "",
                                    TotalSize = Convert.ToUInt64(logical["Size"] ?? 0),
                                    FreeSpace = Convert.ToUInt64(logical["FreeSpace"] ?? 0)
                                };

                                volumeInfo.UsedSpace = volumeInfo.TotalSize - volumeInfo.FreeSpace;
                                volumes.Add(volumeInfo);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting volumes for disk {diskDeviceID}: {ex.Message}");
            }

            return volumes;
        }

        private void InitializePerformanceCounters()
        {
            try
            {
                // First, get all available PhysicalDisk instances
                var category = new PerformanceCounterCategory("PhysicalDisk");
                var instanceNames = category.GetInstanceNames();

                Debug.WriteLine($"Available PhysicalDisk instances: {string.Join(", ", instanceNames)}");

                foreach (var disk in _disks)
                {
                    if (!string.IsNullOrEmpty(disk.PhysicalDriveNumber))
                    {
                        try
                        {
                            // Try to find the correct instance name
                            var instanceName = FindInstanceName(disk.PhysicalDriveNumber, instanceNames);

                            if (string.IsNullOrEmpty(instanceName))
                            {
                                Debug.WriteLine($"Could not find instance for {disk.Model}");
                                continue;
                            }

                            Debug.WriteLine($"Using instance '{instanceName}' for {disk.Model}");

                            // Read bytes/sec
                            var readCounter = new PerformanceCounter(
                                "PhysicalDisk",
                                "Disk Read Bytes/sec",
                                instanceName);
                            readCounter.NextValue();
                            _diskReadCounters[disk.DeviceID] = readCounter;

                            // Write bytes/sec
                            var writeCounter = new PerformanceCounter(
                                "PhysicalDisk",
                                "Disk Write Bytes/sec",
                                instanceName);
                            writeCounter.NextValue();
                            _diskWriteCounters[disk.DeviceID] = writeCounter;

                            // % Disk Time (Active Time)
                            var activeCounter = new PerformanceCounter(
                                "PhysicalDisk",
                                "% Idle Time",
                                instanceName);
                            activeCounter.NextValue();
                            _diskActiveTimeCounters[disk.DeviceID] = activeCounter;

                            // Avg. Disk sec/Transfer (Response Time) - converted to ms
                            var responseCounter = new PerformanceCounter(
                                "PhysicalDisk",
                                "Avg. Disk sec/Transfer",
                                instanceName);
                            responseCounter.NextValue();
                            _diskResponseTimeCounters[disk.DeviceID] = responseCounter;

                            // Disk Bytes/sec (Transfer Rate) - total read + write
                            var transferCounter = new PerformanceCounter(
                                "PhysicalDisk",
                                "Disk Bytes/sec",
                                instanceName);
                            transferCounter.NextValue();
                            _diskTransferRateCounters[disk.DeviceID] = transferCounter;

                            // Initialize transfer rate history
                            _transferRateHistory[disk.DeviceID] = new List<float>();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error initializing counters for {disk.Model}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing performance counters: {ex.Message}");
            }
        }

        // Get all disks
        public List<DiskInfo> GetAllDisks()
        {
            return _disks;
        }

        // Get specific disk by index
        public DiskInfo GetDisk(int index)
        {
            if (index >= 0 && index < _disks.Count)
            {
                return _disks[index];
            }
            return null;
        }

        // Get transfer rate history for graphing
        public List<float> GetTransferRateHistory(DiskInfo disk)
        {
            if (disk != null && _transferRateHistory.ContainsKey(disk.DeviceID))
            {
                return new List<float>(_transferRateHistory[disk.DeviceID]);
            }
            return new List<float>();
        }

        // Get max transfer rate from history (for graph scaling)
        public float GetMaxTransferRate(DiskInfo disk)
        {
            var history = GetTransferRateHistory(disk);
            if (history.Count > 0)
            {
                return history.Max();
            }
            return 0f;
        }

        // Clear transfer rate history
        public void ClearTransferRateHistory(DiskInfo disk)
        {
            if (disk != null && _transferRateHistory.ContainsKey(disk.DeviceID))
            {
                _transferRateHistory[disk.DeviceID].Clear();
            }
        }

        // Update disk performance metrics
        public void UpdateDiskMetrics(DiskInfo disk)
        {
            if (disk == null) return;

            try
            {
                // Read speed
                if (_diskReadCounters.ContainsKey(disk.DeviceID))
                {
                    disk.ReadSpeed = _diskReadCounters[disk.DeviceID].NextValue();
                }

                // Write speed
                if (_diskWriteCounters.ContainsKey(disk.DeviceID))
                {
                    disk.WriteSpeed = _diskWriteCounters[disk.DeviceID].NextValue();
                }

                // Active time
                if (_diskActiveTimeCounters.ContainsKey(disk.DeviceID))
                {
                    float idleTime = _diskActiveTimeCounters[disk.DeviceID].NextValue();

                    float activeTime = 100f - idleTime;
                    if (activeTime < 0) activeTime = 0;
                    if (activeTime > 100) activeTime = 100;

                    disk.ActiveTime = activeTime;
                }

                // Average response time (convert seconds to milliseconds)
                if (_diskResponseTimeCounters.ContainsKey(disk.DeviceID))
                {
                    disk.AverageResponseTime = _diskResponseTimeCounters[disk.DeviceID].NextValue() * 1000f;
                }

                // Transfer rate (total disk activity)
                if (_diskTransferRateCounters.ContainsKey(disk.DeviceID))
                {
                    disk.TransferRate = _diskTransferRateCounters[disk.DeviceID].NextValue();

                    // Update transfer rate history (keep last 60 samples for 60 seconds graph)
                    if (_transferRateHistory.ContainsKey(disk.DeviceID))
                    {
                        var history = _transferRateHistory[disk.DeviceID];
                        history.Add(disk.TransferRate);

                        // Keep only last 60 samples
                        if (history.Count > 60)
                        {
                            history.RemoveAt(0);
                        }
                    }
                }

                // Update volumes
                foreach (var volume in disk.Volumes)
                {
                    UpdateVolumeInfo(volume);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating disk metrics: {ex.Message}");
            }
        }

        private void UpdateVolumeInfo(VolumeInfo volume)
        {
            try
            {
                if (!string.IsNullOrEmpty(volume.DriveLetter) && Directory.Exists(volume.DriveLetter))
                {
                    var driveInfo = new DriveInfo(volume.DriveLetter);
                    if (driveInfo.IsReady)
                    {
                        volume.FreeSpace = (ulong)driveInfo.AvailableFreeSpace;
                        volume.TotalSize = (ulong)driveInfo.TotalSize;
                        volume.UsedSpace = volume.TotalSize - volume.FreeSpace;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating volume info for {volume.DriveLetter}: {ex.Message}");
            }
        }

        // Get disk by drive letter
        public DiskInfo GetDiskByDriveLetter(string driveLetter)
        {
            foreach (var disk in _disks)
            {
                if (disk.Volumes.Any(v => v.DriveLetter.Equals(driveLetter, StringComparison.OrdinalIgnoreCase)))
                {
                    return disk;
                }
            }
            return null;
        }

        // Get total disk space (all disks)
        public ulong GetTotalDiskSpace()
        {
            return _disks.Aggregate(0UL, (sum, disk) => sum + disk.Size);
        }

        // Get total used space (all volumes)
        public ulong GetTotalUsedSpace()
        {
            ulong total = 0;
            foreach (var disk in _disks)
            {
                total += disk.Volumes.Aggregate(0UL, (sum, vol) => sum + vol.UsedSpace);
            }
            return total;
        }

        // Get total free space (all volumes)
        public ulong GetTotalFreeSpace()
        {
            ulong total = 0;
            foreach (var disk in _disks)
            {
                total += disk.Volumes.Aggregate(0UL, (sum, vol) => sum + vol.FreeSpace);
            }
            return total;
        }

        // Get disk health status (basic check)
        public string GetDiskHealth(DiskInfo disk)
        {
            try
            {
                // Check SMART status using WMI
                using (var searcher = new ManagementObjectSearcher(
                    "root\\WMI",
                    $"SELECT * FROM MSStorageDriver_FailurePredictStatus WHERE InstanceName LIKE '%{disk.PhysicalDriveNumber}%'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var predictFailure = Convert.ToBoolean(obj["PredictFailure"]);
                        return predictFailure ? "Warning" : "Healthy";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking disk health: {ex.Message}");
            }

            return "Unknown";
        }

        // Get disk temperature (if available)
        public float GetDiskTemperature(DiskInfo disk)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "root\\WMI",
                    $"SELECT * FROM MSStorageDriver_ATAPISmartData WHERE InstanceName LIKE '%{disk.PhysicalDriveNumber}%'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        // Temperature is usually in attribute 194 (0xC2)
                        var vendorSpecific = (byte[])obj["VendorSpecific"];
                        if (vendorSpecific != null && vendorSpecific.Length >= 362)
                        {
                            // SMART attribute 194 is typically at offset 194*12 + 2
                            int offset = 194 * 12 + 5;
                            if (offset < vendorSpecific.Length)
                            {
                                return vendorSpecific[offset];
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting disk temperature: {ex.Message}");
            }

            return 0f;
        }

        // Format bytes to human readable
        public static string FormatBytes(ulong bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int order = 0;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:F2} {sizes[order]}";
        }

        // Format speed (bytes/sec to MB/s)
        public static string FormatSpeed(float bytesPerSec)
        {
            double mbps = bytesPerSec / (1024.0 * 1024.0);
            if (mbps >= 1000)
            {
                return $"{mbps / 1024.0:F2} GB/s";
            }
            return $"{mbps:F2} MB/s";
        }

        // Get complete disk info with updated metrics
        public DiskInfo GetDiskInfo(int index)
        {
            var disk = GetDisk(index);
            if (disk != null)
            {
                UpdateDiskMetrics(disk);
            }
            return disk;
        }

        // Get all disks info with updated metrics
        public List<DiskInfo> GetAllDisksInfo()
        {
            foreach (var disk in _disks)
            {
                UpdateDiskMetrics(disk);
            }
            return _disks;
        }

        // Debug: Print all disk information
        public void PrintAllDisksInfo()
        {
            Debug.WriteLine("=== Disk Information ===");
            Debug.WriteLine($"\nDetecting performance counter instances...");

            try
            {
                var category = new PerformanceCounterCategory("PhysicalDisk");
                var instances = category.GetInstanceNames();
                Debug.WriteLine($"Available instances: {string.Join(", ", instances)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting instances: {ex.Message}");
            }

            for (int i = 0; i < _disks.Count; i++)
            {
                var disk = _disks[i];

                Debug.WriteLine($"\nDisk {i}: {disk.Model}");
                Debug.WriteLine($"  Type: {disk.DiskType}");
                Debug.WriteLine($"  Interface: {disk.InterfaceType}");
                Debug.WriteLine($"  Capacity: {FormatBytes(disk.Size)}");
                Debug.WriteLine($"  Partitions: {disk.Partitions}");
                Debug.WriteLine($"  Health: {GetDiskHealth(disk)}");

                UpdateDiskMetrics(disk);
                Debug.WriteLine($"  Read Speed: {FormatSpeed(disk.ReadSpeed)}");
                Debug.WriteLine($"  Write Speed: {FormatSpeed(disk.WriteSpeed)}");
                Debug.WriteLine($"  Transfer Rate: {FormatSpeed(disk.TransferRate)}");
                Debug.WriteLine($"  Active Time: {disk.ActiveTime:F1}%");
                Debug.WriteLine($"  Avg Response Time: {disk.AverageResponseTime:F1} ms");
                Debug.WriteLine($"  Formatted Capacity: {FormatBytes(disk.FormattedCapacity)}");
                Debug.WriteLine($"  System Disk: {(disk.IsSystemDisk ? "Yes" : "No")}");
                Debug.WriteLine($"  Page File: {(disk.HasPageFile ? "Yes" : "No")}");
                Debug.WriteLine($"  Type: {disk.DiskType} ({disk.InterfaceType})");

                var temp = GetDiskTemperature(disk);
                if (temp > 0)
                {
                    Debug.WriteLine($"  Temperature: {temp:F1}°C");
                }

                Debug.WriteLine($"  Volumes:");
                foreach (var volume in disk.Volumes)
                {
                    var usedPercent = volume.TotalSize > 0
                        ? (volume.UsedSpace * 100.0 / volume.TotalSize)
                        : 0;

                    Debug.WriteLine($"    {volume.DriveLetter} ({volume.Label})");
                    Debug.WriteLine($"      File System: {volume.FileSystem}");
                    Debug.WriteLine($"      Total: {FormatBytes(volume.TotalSize)}");
                    Debug.WriteLine($"      Used: {FormatBytes(volume.UsedSpace)} ({usedPercent:F1}%)");
                    Debug.WriteLine($"      Free: {FormatBytes(volume.FreeSpace)}");
                }
            }

            Debug.WriteLine($"\nTotal Disk Space: {FormatBytes(GetTotalDiskSpace())}");
            Debug.WriteLine($"Total Used Space: {FormatBytes(GetTotalUsedSpace())}");
            Debug.WriteLine($"Total Free Space: {FormatBytes(GetTotalFreeSpace())}");
            Debug.WriteLine("========================");
        }

        // Cleanup
        public void Dispose()
        {
            foreach (var counter in _diskReadCounters.Values)
            {
                counter?.Dispose();
            }
            _diskReadCounters.Clear();

            foreach (var counter in _diskWriteCounters.Values)
            {
                counter?.Dispose();
            }
            _diskWriteCounters.Clear();

            foreach (var counter in _diskActiveTimeCounters.Values)
            {
                counter?.Dispose();
            }
            _diskActiveTimeCounters.Clear();

            foreach (var counter in _diskResponseTimeCounters.Values)
            {
                counter?.Dispose();
            }
            _diskResponseTimeCounters.Clear();

            foreach (var counter in _diskTransferRateCounters.Values)
            {
                counter?.Dispose();
            }
            _diskTransferRateCounters.Clear();

            _transferRateHistory.Clear();
        }
    }

    // Disk information class
    public class DiskInfo
    {
        public string DeviceID { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string InterfaceType { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public string DiskType { get; set; } = string.Empty; // SSD/HDD
        public string PhysicalDriveNumber { get; set; } = string.Empty;
        public ulong Size { get; set; }
        public int Partitions { get; set; }
        public List<VolumeInfo> Volumes { get; set; } = new List<VolumeInfo>();

        // Performance metrics
        public float ReadSpeed { get; set; } // bytes/sec

        public float WriteSpeed { get; set; } // bytes/sec
        public float TransferRate { get; set; } // bytes/sec (read + write)
        public float ActiveTime { get; set; } // percentage
        public float AverageResponseTime { get; set; } // ms

        // Disk properties
        public ulong FormattedCapacity { get; set; }

        public bool IsSystemDisk { get; set; }
        public bool HasPageFile { get; set; }
    }

    // Volume/Partition information class
    public class VolumeInfo
    {
        public string DriveLetter { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string FileSystem { get; set; } = string.Empty;
        public ulong TotalSize { get; set; }
        public ulong UsedSpace { get; set; }
        public ulong FreeSpace { get; set; }

        public double UsedPercentage => TotalSize > 0 ? (UsedSpace * 100.0 / TotalSize) : 0;
    }
}