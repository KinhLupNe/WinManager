using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using WinManager.Models;

namespace WinManager.ViewModels
{
    public partial class RamPresenter : ObservableObject
    {
        private readonly RamModel _model = new RamModel();
        private readonly DispatcherTimer _timer;

        [ObservableProperty]
        private string ramUsageText = "Đang đọc RAM...";

        public RamPresenter()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (s, e) => UpdateRam();
            _timer.Start();

            UpdateRam();
        }

        private void UpdateRam()
        {
            var info = _model.GetRamInfo();
            double totalGb = BytesToGb(info.TotalBytes);
            double usedGb = BytesToGb(info.UsedBytes);

            RamUsageText = $"Đang dùng {info.PercentUsed:0.0}% ({usedGb:0.0} / {totalGb:0.0} GB)";
        }

        private static double BytesToGb(ulong bytes)
            => bytes / 1024.0 / 1024.0 / 1024.0;
    }
}
