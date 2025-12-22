using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows; // Cần thiết cho Dispatcher
using WinManager.Models;

namespace WinManager.ViewModels
{
    public partial class DiskDetailControl : ObservableObject, IDisposable
    {
        private readonly CancellationTokenSource _cts; // Token hủy luồng

        [ObservableProperty]
        private DiskInfo _currenDiskInfo;

        private ObservableCollection<double> _diskValues;

        public ISeries[] Series { get; set; }
        public Axis[] XAxes { get; set; }
        public Axis[] YAxes { get; set; }

        [ObservableProperty] private string? _physicalDriveNumber;
        [ObservableProperty] private string? _disktype;
        [ObservableProperty] private string? _model;
        [ObservableProperty] private float? _activeTime;
        [ObservableProperty] private string? _activeTimeColor;
        [ObservableProperty] private float _readSpeedDisplay;
        [ObservableProperty] private float _writeSpeedDisplay;
        [ObservableProperty] private float _averageResponseTime;
        [ObservableProperty] private ulong _formattedCapacityDisplay;
        [ObservableProperty] private List<VolumeInfo>? _volumes;
        [ObservableProperty] private bool _isSystemDisk;
        [ObservableProperty] private bool _hasPageFile;
        [ObservableProperty] private ulong _sizeDisplay;

        public DiskDetailControl(DiskInfo selectDisk)
        {
            _currenDiskInfo = selectDisk;
            _diskValues = new ObservableCollection<double>(new double[60]);
            _cts = new CancellationTokenSource();

            Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _diskValues,
                    Stroke = new SolidColorPaint(SKColor.Parse("#06b6d4")) { StrokeThickness = 2 },
                    Fill = new LinearGradientPaint(
                        new [] { SKColor.Parse("#06b6d4").WithAlpha(80), SKColors.Empty },
                        new SKPoint(0.5f, 0),
                        new SKPoint(0.5f, 1)
                    ),
                    GeometrySize = 0,
                    LineSmoothness = 0.0,
                    AnimationsSpeed = TimeSpan.Zero
                }
            };

            XAxes = new Axis[] { new Axis { IsVisible = true } };
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

            Init();

            // Chạy loop trên luồng phụ
            Task.Run(() => MonitorLoopD(_cts.Token));
        }

        private void Init()
        {
            // Gán giá trị tĩnh ban đầu để hiển thị ngay lập tức
            if (_currenDiskInfo != null)
            {
                PhysicalDriveNumber = _currenDiskInfo.PhysicalDriveNumber;
                Disktype = _currenDiskInfo.DiskType;
                Model = _currenDiskInfo.Model;
                Volumes = _currenDiskInfo.Volumes;
                IsSystemDisk = _currenDiskInfo.IsSystemDisk;
                HasPageFile = _currenDiskInfo.HasPageFile;
                SizeDisplay = _currenDiskInfo.Size;
                FormattedCapacityDisplay = _currenDiskInfo.FormattedCapacity;
            }
        }

        private async Task MonitorLoopD(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Update UI trên Dispatcher
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Lưu ý: Phải gán vào Property (Viết Hoa) thì View mới nhận được thông báo thay đổi
                        // Không gán vào biến private (có dấu gạch dưới)

                        ActiveTime = _currenDiskInfo.ActiveTime;
                        Debug.WriteLine(ActiveTime
                            );
                        ReadSpeedDisplay = _currenDiskInfo.ReadSpeed;
                        WriteSpeedDisplay = _currenDiskInfo.WriteSpeed;
                        AverageResponseTime = _currenDiskInfo.AverageResponseTime;

                        // Cập nhật biểu đồ (Vẽ phần trăm hoạt động ActiveTime)
                        // Nếu ActiveTime null thì coi như 0
                        double chartValue = _currenDiskInfo.ActiveTime;
                        _diskValues.Add(chartValue);

                        // Giữ biểu đồ ở mức 60 điểm dữ liệu
                        if (_diskValues.Count > 60) _diskValues.RemoveAt(0);
                    });

                    await Task.Delay(1000, token); // Quan trọng: Đợi 1s để không treo máy
                }
                catch (TaskCanceledException) { break; }
                catch { await Task.Delay(1000, token); }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}