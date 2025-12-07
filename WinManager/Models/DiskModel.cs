using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinManager.Models
{

    public class DiskInfo
    {
        public string Name { get; set; } = "";
        public long TotalBytes { get; set; }
        public long UsedBytes { get; set; }
        public double PercentUsed { get; set; }
    }

    internal class DiskModel
    {
        public DiskInfo GetMainDiskInfo()
        {
            var drive = DriveInfo.GetDrives()
                                 .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                                 .OrderBy(d => d.Name)
                                 .FirstOrDefault();

            if (drive == null)
                return new DiskInfo();

            long total = drive.TotalSize;
            long free = drive.AvailableFreeSpace;
            long used = total - free;
            double percent = total == 0 ? 0 : used * 100.0 / total;

            return new DiskInfo
            {
                Name = drive.Name,
                TotalBytes = total,
                UsedBytes = used,
                PercentUsed = percent
            };
        }
    }
}
