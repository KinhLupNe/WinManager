using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinManager.Models
{
    internal class CpuModel
    {
        private readonly PerformanceCounter _cpuCounter;

        public CpuModel()
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); 
        }

        public float GetCpuUsage()
        {
            return _cpuCounter.NextValue();
        }
    }
}
