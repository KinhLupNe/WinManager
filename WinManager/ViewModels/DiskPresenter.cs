using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Windows.Threading;
using WinManager.Models;

namespace WinManager.ViewModels
{
    public partial class DiskPresenter : ObservableObject
    {
        private readonly DiskModel _model = new DiskModel();
        private readonly DispatcherTimer _timer;

        [ObservableProperty]
        private string diskUsageText = "Đang đọc Disk...";

        public DiskPresenter()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _timer.Tick += (s, e) => UpdateDisk();
            _timer.Start();

            UpdateDisk();
        }

        private void UpdateDisk()
        {
            var info = _model.GetMainDiskInfo();
            double totalGb = BytesToGb(info.TotalBytes);
            double usedGb = BytesToGb(info.UsedBytes);

            DiskUsageText = $"{info.Name} dùng {info.PercentUsed:0.0}% ({usedGb:0.0} / {totalGb:0.0} GB)";
        }

        private static double BytesToGb(long bytes)
            => bytes / 1024.0 / 1024.0 / 1024.0;
    }
}
