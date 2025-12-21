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
    public partial class MemoryControl : ObservableObject, IDisposable
    {
        private readonly MemoryModel _memoryModel;
        private readonly CancellationTokenSource _cts;

        private ObservableCollection<double> _memoryValues;

        public ISeries[] Series { get; set; }
        public Axis[] XAxes { get; set; }
        public Axis[] YAxes { get; set; }

        [ObservableProperty] private double _totalMemoryGB;
        [ObservableProperty] private string? _formFactor;
        [ObservableProperty] private double _memoryUsagePercent;
        [ObservableProperty] private double _inUseGB;
        [ObservableProperty] private double _inUseMB;
        [ObservableProperty] private double _availableGB;
        [ObservableProperty] private double _committedGB;
        [ObservableProperty] private double _committedLimitGB;
        [ObservableProperty] private double _pagedPoolMB;
        [ObservableProperty] private double _nonPagedPoolMB;
        [ObservableProperty] private double _cachedGB;
        [ObservableProperty] private double _hardwareReservedMB;
        [ObservableProperty] private long _memorySpeed;
        [ObservableProperty] private int _slotsUsed;
        [ObservableProperty] private int _totalSlots;

        public MemoryControl()
        {
            _memoryModel = new MemoryModel();
            _memoryValues = new ObservableCollection<double>(new double[60]);
            _cts = new CancellationTokenSource();

            // Cấu hình biểu đồ (Màu Cam)
            var orangeColor = SKColor.Parse("#f97316");
            Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _memoryValues,
                    Stroke = new SolidColorPaint(orangeColor) { StrokeThickness = 2 },
                    Fill = new SolidColorPaint(orangeColor.WithAlpha(50)),
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

            try
            {
                var initInfo = _memoryModel.GetMemoryInfo();
                TotalMemoryGB = initInfo.TotalMemoryGB;
                FormFactor = initInfo.FormFactor;
                TotalSlots = initInfo.TotalSlots;
            }
            catch { }

            // Chạy luồng nền
            Task.Run(() => MonitorLoop(_cts.Token));
        }

        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    
                    var info = _memoryModel.GetMemoryInfo();

                    double usage = Math.Round(info.MemoryUsagePercent, 0);

                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MemoryUsagePercent = usage;
                        InUseGB = info.InUseGB;
                        InUseMB = info.InUseMB;
                        AvailableGB = info.AvailableGB;
                        CommittedGB = info.CommittedGB;
                        CommittedLimitGB = info.CommittedLimitGB;
                        PagedPoolMB = info.PagedPoolMB;
                        NonPagedPoolMB = info.NonPagedPoolMB;
                        CachedGB = info.CachedGB;
                        HardwareReservedMB = info.HardwareReservedMB;
                        MemorySpeed = info.MemorySpeed;
                        SlotsUsed = info.SlotsUsed;

                        _memoryValues.Add(usage);
                        if (_memoryValues.Count > 60) _memoryValues.RemoveAt(0);
                    });

                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException) { break; }
                catch { await Task.Delay(1000, token); }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _memoryModel?.Dispose();
        }
    }
}