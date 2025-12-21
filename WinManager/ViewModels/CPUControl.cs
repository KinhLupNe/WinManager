using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Windows; // Để dùng Application.Current.Dispatcher
using WinManager.Models;

namespace WinManager.ViewModels
{
    public partial class CPUControl : ObservableObject, IDisposable
    {
        private readonly CpuModel _cpuModel;
        private readonly CancellationTokenSource _cts; // Token để hủy luồng an toàn

        private ObservableCollection<double> _cpuValues;

        // Binding Properties
        public ISeries[] Series { get; set; }
        public Axis[] XAxes { get; set; }
        public Axis[] YAxes { get; set; }

        [ObservableProperty] private double _threads;
        [ObservableProperty] private double _handles;
        [ObservableProperty] private double _processes;
        [ObservableProperty] private double _speed;
        [ObservableProperty] private string? _name;
        [ObservableProperty] private double _usage;
        [ObservableProperty] private string? _upTime;
        [ObservableProperty] private double _core;
        [ObservableProperty] private string? _temperature;
        [ObservableProperty] private string? _power;
        [ObservableProperty] private string? _voltage;
        [ObservableProperty] private double _baseSpeed;

        public CPUControl()
        {
            _cpuModel = new CpuModel();
            _cpuValues = new ObservableCollection<double>(new double[60]);
            _cts = new CancellationTokenSource(); // Khởi tạo token hủy

            // Lấy thông tin tĩnh (Chạy 1 lần)
            try
            {
                var initInfo = _cpuModel.GetCpuInfo();
                BaseSpeed = initInfo.MaxSpeed;
                Core = initInfo.CoreCount;
                Name = initInfo.CpuName;
            }
            catch { }

            // Cấu hình biểu đồ (Giữ nguyên màu Cyan)
            Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _cpuValues,
                    Stroke = new SolidColorPaint(SKColor.Parse("#06b6d4")) { StrokeThickness = 2 },
                    Fill = new LinearGradientPaint(
                        new [] { SKColor.Parse("#06b6d4").WithAlpha(80), SKColors.Empty },
                        new SKPoint(0.5f, 0),
                        new SKPoint(0.5f, 1)
                    ),
                    GeometrySize = 0, // Tắt chấm tròn để vẽ nhanh hơn
                    LineSmoothness = 0.0, // Tăng độ mượt đường cong
                    AnimationsSpeed = TimeSpan.Zero // Hiệu ứng lướt nhẹ
                }
            };

            XAxes = new Axis[] { new Axis { IsVisible = true } }; // Tắt trục X cho đỡ rối
            YAxes = new Axis[]
            {
                new Axis
                {
                    MinLimit = 0, MaxLimit = 100,
                    Labeler = value => $"{value:F0}%",
                    TextSize = 10,
                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#27272a")) { StrokeThickness = 1 }
                }
            };

            // Thread logic
            Task.Run(() => MonitorLoop(_cts.Token));
        }

        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    
                    var info = _cpuModel.GetCpuInfo();

                    double u = Math.Round(info.CpuUsage, 1);
                    double s = Math.Round(info.CurrentSpeed, 2);
                    string uptime = info.Uptime;
                    string vol = $"{info.Voltage:F2} V";
                    string temp = (info.Temperature > 10) ? $"{info.Temperature:F1} °C" : "N/A";
                    string pow = (info.PowerConsumption > 0) ? $"{Math.Round(info.PowerConsumption, 2)}" : "N/A";

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Usage = u;
                        Speed = s;
                        Processes = info.ProcessCount;
                        Threads = info.ThreadCount;
                        Handles = info.HandleCount;
                        UpTime = uptime;
                        Voltage = vol;
                        Temperature = temp;
                        Power = pow;

                        _cpuValues.Add(Usage);
                        if (_cpuValues.Count > 60) _cpuValues.RemoveAt(0);
                    });

                    
                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException) { break; } // Thoát êm đẹp khi hủy
                catch { await Task.Delay(1000, token); } // Lỗi thì đợi chút rồi thử lại
            }
        }

        public void Dispose()
        {
            _cts.Cancel(); // Ra lệnh dừng vòng lặp ngay lập tức
            _cts.Dispose();
            _cpuModel?.Dispose();
        }
    }
}