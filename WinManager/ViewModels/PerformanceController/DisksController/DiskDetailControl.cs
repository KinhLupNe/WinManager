using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using WinManager.Models;

namespace WinManager.ViewModels
{
    // --- LỚP WRAPPER MỚI: Dùng để hiển thị Volume đẹp hơn mà không cần sửa Model gốc ---
    public partial class VolumeDisplayItem : ObservableObject
    {
        [ObservableProperty] private string _driveLetter;
        [ObservableProperty] private string _label;
        [ObservableProperty] private string _fileSystem;

        // Giữ lại số thực để bind vào ProgressBar (nếu cần)
        [ObservableProperty] private double _usedPercentage;
        [ObservableProperty] private ulong _totalSize;
        [ObservableProperty] private ulong _usedSpace;

        // Các thuộc tính chuỗi (String) đã format để bind vào TextBlock
        [ObservableProperty] private string _totalSizeDisplay;  // Ví dụ: "500 GB"
        [ObservableProperty] private string _usedSpaceDisplay;  // Ví dụ: "120 GB"
        [ObservableProperty] private string _freeSpaceDisplay;  // Ví dụ: "380 GB"
    }

    public partial class DiskDetailControl : ObservableObject, IDisposable
    {
        private readonly CancellationTokenSource _cts;

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

        // --- SỬA: Đổi sang string để hiển thị đơn vị (KB/s, MB/s) ---
        [ObservableProperty] private string _readSpeedDisplay;
        [ObservableProperty] private string _writeSpeedDisplay;
        [ObservableProperty] private string _averageResponseTimeDisplay;
        [ObservableProperty] private string _formattedCapacityDisplay;
        [ObservableProperty] private string _sizeDisplay;

        // --- SỬA: Dùng ObservableCollection chứa Wrapper thay vì List gốc ---
        [ObservableProperty] private ObservableCollection<VolumeDisplayItem> _volumes;

        [ObservableProperty] private bool _isSystemDisk;
        [ObservableProperty] private bool _hasPageFile;

        public DiskDetailControl(DiskInfo selectDisk)
        {
            _currenDiskInfo = selectDisk;
            _diskValues = new ObservableCollection<double>(new double[60]);
            _volumes = new ObservableCollection<VolumeDisplayItem>(); // Khởi tạo list hiển thị
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

            XAxes = new Axis[] { new Axis { IsVisible = false } }; // Ẩn trục X cho gọn
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

            Task.Run(() => MonitorLoopD(_cts.Token));
        }

        private void Init()
        {
            if (_currenDiskInfo != null)
            {
                PhysicalDriveNumber = _currenDiskInfo.PhysicalDriveNumber;
                Disktype = _currenDiskInfo.DiskType;
                Model = _currenDiskInfo.Model;
                IsSystemDisk = _currenDiskInfo.IsSystemDisk;
                HasPageFile = _currenDiskInfo.HasPageFile;

                // Format các thông số tĩnh
                SizeDisplay = FormatBytes(_currenDiskInfo.Size);
                FormattedCapacityDisplay = FormatBytes(_currenDiskInfo.FormattedCapacity);

                // Khởi tạo danh sách Volume hiển thị lần đầu
                UpdateVolumesList();
            }
        }

        private async Task MonitorLoopD(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ActiveTime = _currenDiskInfo.ActiveTime;

                        // --- FORMAT DỮ LIỆU ĐỘNG ---
                        // Tự động chuyển đổi đơn vị tốc độ (B/s -> KB/s -> MB/s)
                        ReadSpeedDisplay = FormatSpeed(_currenDiskInfo.ReadSpeed);
                        WriteSpeedDisplay = FormatSpeed(_currenDiskInfo.WriteSpeed);

                        // Response time thêm đơn vị ms
                        AverageResponseTimeDisplay = $"{_currenDiskInfo.AverageResponseTime:F1} ms";

                        // Cập nhật thông tin các phân vùng (Volumes)
                        UpdateVolumesList();

                        // Cập nhật biểu đồ
                        double chartValue = _currenDiskInfo.ActiveTime;
                        _diskValues.Add(chartValue);
                        if (_diskValues.Count > 60) _diskValues.RemoveAt(0);
                    });

                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException) { break; }
                catch { await Task.Delay(1000, token); }
            }
        }

        // Hàm đồng bộ dữ liệu từ Model gốc sang Model hiển thị (Wrapper)
        private void UpdateVolumesList()
        {
            var sourceVolumes = _currenDiskInfo.Volumes;
            if (sourceVolumes == null) return;

            // Nếu số lượng volume thay đổi (lần đầu chạy hoặc cắm thêm USB), clear và tạo lại
            if (_volumes.Count != sourceVolumes.Count)
            {
                _volumes.Clear();
                foreach (var vol in sourceVolumes)
                {
                    _volumes.Add(new VolumeDisplayItem
                    {
                        DriveLetter = vol.DriveLetter,
                        Label = vol.Label,
                        FileSystem = vol.FileSystem,
                        TotalSize = vol.TotalSize,
                        UsedSpace = vol.UsedSpace,
                        UsedPercentage = vol.UsedPercentage,
                        // Format sẵn sang String
                        TotalSizeDisplay = FormatBytes(vol.TotalSize),
                        UsedSpaceDisplay = FormatBytes(vol.UsedSpace),
                        FreeSpaceDisplay = FormatBytes(vol.FreeSpace)
                    });
                }
            }
            else
            {
                // Nếu số lượng volume không đổi, chỉ cập nhật giá trị (để UI mượt hơn)
                for (int i = 0; i < sourceVolumes.Count; i++)
                {
                    var src = sourceVolumes[i];
                    var dest = _volumes[i];

                    // Cập nhật số liệu
                    dest.UsedSpace = src.UsedSpace;
                    dest.UsedPercentage = src.UsedPercentage;

                    // Cập nhật String hiển thị
                    dest.UsedSpaceDisplay = FormatBytes(src.UsedSpace);
                    dest.FreeSpaceDisplay = FormatBytes(src.FreeSpace);
                }
            }
        }

        // --- HÀM HỖ TRỢ FORMAT ---

        // Format dung lượng (Size)
        private string FormatBytes(ulong bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }

        // Format tốc độ (Speed) - Đầu vào là float
        private string FormatSpeed(float bytesPerSec)
        {
            string[] suffixes = { "B/s", "KB/s", "MB/s", "GB/s" };
            int counter = 0;
            float number = bytesPerSec;
            while (number >= 1024 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:F1} {suffixes[counter]}";
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}