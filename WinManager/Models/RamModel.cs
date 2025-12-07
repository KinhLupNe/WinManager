using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WinManager.Models
{
    public class RamInfo
    {
        public ulong TotalBytes { get; set; }
        public ulong UsedBytes { get; set; }
        public double PercentUsed { get; set; }
    }
    internal class RamModel
    {
        // struct dùng cho GlobalMemoryStatusEx
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
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

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        public RamInfo GetRamInfo()
        {
            var status = new MEMORYSTATUSEX();
            if (!GlobalMemoryStatusEx(status))
            {
                // nếu gọi WinAPI lỗi thì trả 0 hết
                return new RamInfo();
            }

            ulong total = status.ullTotalPhys;
            ulong available = status.ullAvailPhys;
            ulong used = total - available;
            double percent = total == 0 ? 0 : used * 100.0 / total;

            return new RamInfo
            {
                TotalBytes = total,
                UsedBytes = used,
                PercentUsed = percent
            };
        }
    }
}
