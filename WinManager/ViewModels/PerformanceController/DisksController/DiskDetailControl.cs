using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Windows;
using WinManager.Models;

namespace WinManager.ViewModels
{
    public partial class VolumeDisplayItem : ObservableObject
    {
        [ObservableProperty] private string? _driveLetter;
        [ObservableProperty] private string? _label;
        [ObservableProperty] private string? _fileSystem;

        [ObservableProperty] private double _usedPercentage;

        [ObservableProperty] private ulong _totalSize;
        [ObservableProperty] private ulong _usedSpace;

        [ObservableProperty] private string? _totalSizeDisplay;      

        [ObservableProperty] private string? _usedSpaceDisplay;      
        [ObservableProperty] private string? _freeSpaceDisplay;      
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

        [ObservableProperty] private string _readSpeedDisplay;

        [ObservableProperty] private string _writeSpeedDisplay;
        [ObservableProperty] private string _averageResponseTimeDisplay;
        [ObservableProperty] private string _formattedCapacityDisplay;
        [ObservableProperty] private string _sizeDisplay;

        [ObservableProperty] private ObservableCollection<VolumeDisplayItem> _volumes;

        [ObservableProperty] private bool _isSystemDisk;
        [ObservableProperty] private bool _hasPageFile;

        public DiskDetailControl(DiskInfo selectDisk)
        {
            _currenDiskInfo = selectDisk;
            _diskValues = new ObservableCollection<double>(new double[60]);
            _volumes = new ObservableCollection<VolumeDisplayItem>();
            _cts = new CancellationTokenSource();

            _readSpeedDisplay = string.Empty;
            _writeSpeedDisplay = string.Empty;
            _averageResponseTimeDisplay = string.Empty;
            _formattedCapacityDisplay = string.Empty;
            _sizeDisplay = string.Empty;

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

            XAxes = new Axis[] { new Axis { IsVisible = false } };      
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
            // chạy luồng phụ tránh chậm
            Task.Run(() => MonitorLoopD(_cts.Token));
        }

        private void Init()
        {
            if (_currenDiskInfo != null)
            {
                PhysicalDriveNumber = CurrenDiskInfo.PhysicalDriveNumber;
                Disktype = CurrenDiskInfo.DiskType;
                Model = CurrenDiskInfo.Model;
                IsSystemDisk = CurrenDiskInfo.IsSystemDisk;
                HasPageFile = CurrenDiskInfo.HasPageFile;

                SizeDisplay = FormatBytes(CurrenDiskInfo.Size);
                FormattedCapacityDisplay = FormatBytes(CurrenDiskInfo.FormattedCapacity);

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
                        ActiveTime = CurrenDiskInfo.ActiveTime;

                        ReadSpeedDisplay = FormatSpeed(CurrenDiskInfo.ReadSpeed);
                        WriteSpeedDisplay = FormatSpeed(CurrenDiskInfo.WriteSpeed);

                        AverageResponseTimeDisplay = $"{CurrenDiskInfo.AverageResponseTime:F1} ms";

                        UpdateVolumesList();

                        double chartValue = CurrenDiskInfo.ActiveTime;
                        _diskValues.Add(chartValue);
                        if (_diskValues.Count > 60) _diskValues.RemoveAt(0);
                    });

                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException) { break; }
                catch { await Task.Delay(1000, token); }
            }
        }

        private void UpdateVolumesList()
        {
            var sourceVolumes = CurrenDiskInfo.Volumes;
            if (sourceVolumes == null) return;

            if (Volumes.Count != sourceVolumes.Count)
            {
                Volumes.Clear();
                foreach (var vol in sourceVolumes)
                {
                    Volumes.Add(new VolumeDisplayItem
                    {
                        DriveLetter = vol.DriveLetter,
                        Label = vol.Label,
                        FileSystem = vol.FileSystem,
                        TotalSize = vol.TotalSize,
                        UsedSpace = vol.UsedSpace,
                        UsedPercentage = vol.UsedPercentage,
                        TotalSizeDisplay = FormatBytes(vol.TotalSize),
                        UsedSpaceDisplay = FormatBytes(vol.UsedSpace),
                        FreeSpaceDisplay = FormatBytes(vol.FreeSpace)
                    });
                }
            }
            else
            {
                for (int i = 0; i < sourceVolumes.Count; i++)
                {
                    var src = sourceVolumes[i];
                    var dest = Volumes[i];

                    dest.UsedSpace = src.UsedSpace;
                    dest.UsedPercentage = src.UsedPercentage;

                    dest.UsedSpaceDisplay = FormatBytes(src.UsedSpace);
                    dest.FreeSpaceDisplay = FormatBytes(src.FreeSpace);
                }
            }
        }

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
