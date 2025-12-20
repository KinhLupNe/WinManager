using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;

using WinManager.Models;
using System.Diagnostics;
using System.Windows.Media.Animation;

namespace WinManager.ViewModels
{
    public partial class CPUControl : ObservableObject
    {

        private readonly CpuModel _cpuModel;
        // 1. Dữ liệu thực tế để vẽ (ObservableCollection tự động báo cho View khi thay đổi)
        private ObservableCollection<double> _cpuValues;

        //private bool _isActive = true;


        // 2. Các thuộc tính Binding ra View
        public ISeries[] Series { get; set; }
        public Axis[] XAxes { get; set; }
        public Axis[] YAxes { get; set; }

        [ObservableProperty]
        private double _threads;

        [ObservableProperty]
        private double _handles;

        [ObservableProperty]
        private double _processes;

        [ObservableProperty]
        private double _speed; // Tốc độ hiện tại (GHz)

        [ObservableProperty]
        private string? _name;

        [ObservableProperty]
        private double _usage; // % CPU

        [ObservableProperty]
        private string? _upTime; // Đã đổi sang string để khớp với "hh:mm:ss" của Model

        [ObservableProperty]
        private double _core;

        [ObservableProperty]
        private string? _temperature;
        
        [ObservableProperty]
        private string? _power;

        [ObservableProperty]
        private string? _voltage;

        [ObservableProperty]
        private double _baseSpeed; // Tốc độ cơ bản
        public CPUControl()
        {
        
            _cpuModel = new CpuModel();

            _cpuValues = new ObservableCollection<double>(new double[60]);

            var initInfo = _cpuModel.GetCpuInfo();
            BaseSpeed = initInfo.MaxSpeed;
            Core = initInfo.CoreCount;

            Name = initInfo.CpuName;
            Series = new ISeries[]
            {
            new LineSeries<double>
            {
                Values = _cpuValues,

                Stroke = new SolidColorPaint(SKColor.Parse("#06b6d4")) { StrokeThickness = 2 },
                // Màu nền (Gradient mờ dần xuống dưới)
                Fill = new LinearGradientPaint(
                    new [] { SKColor.Parse("#06b6d4").WithAlpha(80), SKColors.Empty }, // Từ Cyan mờ -> Trong suốt
                    new SKPoint(0.5f, 0), // Bắt đầu từ trên
                    new SKPoint(0.5f, 1)  // Kết thúc ở dưới
                ),
                // Hiệu ứng cong mềm mại (Process Explore style)
                GeometrySize = 0, // Không hiện chấm tròn tại các điểm
                LineSmoothness = 0.3,// 0 là thẳng tuột, 1 là cong mềm
                AnimationsSpeed = TimeSpan.Zero
            }
            };

            // Cấu hình trục X (Ẩn nhãn để giống Task Manager)
            XAxes = new Axis[]
            {
            new Axis
            {
                IsVisible = true, // Ẩn trục X
            }
            };

            // Cấu hình trục Y (Thang đo 0 - 100%)
            YAxes = new Axis[]
            {
            new Axis
            {
                MinLimit = 0,
                MaxLimit = 100,
                Labeler = value => $"{value}%", // Format hiển thị
                TextSize = 10,
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                
                // Lưới kẻ ngang (Grid lines) mờ mờ
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#27272a")) { StrokeThickness = 1 }
            }
            };

            // Bắt đầu giả lập chạy dữ liệu (Sau này bạn thay bằng Timer đọc CPU thật)
            StartMonitoring();
        }

        private async void StartMonitoring()
        {
            while (true)
            {
                try
                {

                    var info = _cpuModel.GetCpuInfo();

                    Usage = Math.Round(info.CpuUsage, 1);
                    Speed = Math.Round(info.CurrentSpeed, 2);
                    Processes = info.ProcessCount;
                    Threads = info.ThreadCount;
                    Handles = info.HandleCount;
                    UpTime = info.Uptime;
                    Power = Math.Round(info.PowerConsumption, 2).ToString();
                    float t = info.Temperature;
                    float p = info.PowerConsumption;

                    Voltage = $"{info.Voltage:F2} V";
                    if (t > 10)
                    {
                        Temperature = $"{info.Temperature:F1} °C";

                    }
                    else
                    {
                        Temperature = "N/A";
                    }
                    if ( p >0)
                    {
                        Power = Math.Round(info.PowerConsumption, 2).ToString();
                    }else
                    {
                        Power = "N/A";
                    }
                    
                   
            

                    // do thi
                    _cpuValues.Add(Usage);
                    if (_cpuValues.Count > 60)
                    {
                        _cpuValues.RemoveAt(0);
                    }
                }
                catch
                {
                }

                // slepp 1 s
                await Task.Delay(1000);
            }
        }

        public void Dispose()
        {
            _cpuModel?.Dispose();
        }
    }
}
