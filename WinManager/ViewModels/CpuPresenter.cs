
using System.Windows.Threading;
using WinManager.Models;

using CommunityToolkit.Mvvm.ComponentModel;


namespace WinManager.ViewModels
{
    public partial class CpuPresenter : ObservableObject
    {
        private readonly CpuModel _model = new CpuModel();
        private readonly DispatcherTimer _timer;

        [ObservableProperty]
        private string usageText = "Đang đọc CPU...";

        public CpuPresenter()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (s, e) => UpdateCpu();
            _timer.Start();

            UpdateCpu();
        }

        private void UpdateCpu()
        {
            float u = _model.GetCpuUsage();
            UsageText = $" Đang sử dụng {u:0.0}%";
        }
    }
}
