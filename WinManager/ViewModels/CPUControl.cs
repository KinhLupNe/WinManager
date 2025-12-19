using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinManager.ViewModels
{
    public class CPUControl  // Giả sử bạn kế thừa BaseViewModel
    {
        // 1. Dữ liệu thực tế để vẽ (ObservableCollection tự động báo cho View khi thay đổi)
        private ObservableCollection<double> _cpuValues;

        // 2. Các thuộc tính Binding ra View
        public ISeries[] Series { get; set; }
        public Axis[] XAxes { get; set; }
        public Axis[] YAxes { get; set; }

        public CPUControl()
        {
            // Khởi tạo danh sách giá trị rỗng (hoặc vài giá trị ban đầu)
            _cpuValues = new ObservableCollection<double> { 0, 10, 25, 15, 40, 30, 50 };

            // Cấu hình biểu đồ đường (Line Chart)
            Series = new ISeries[]
            {
            new LineSeries<double>
            {
                Values = _cpuValues,
                // Màu đường kẻ (Cyan - giống màu nút bấm của bạn)
                Stroke = new SolidColorPaint(SKColor.Parse("#06b6d4")) { StrokeThickness = 2 },
                // Màu nền (Gradient mờ dần xuống dưới)
                Fill = new LinearGradientPaint(
                    new [] { SKColor.Parse("#06b6d4").WithAlpha(80), SKColors.Empty }, // Từ Cyan mờ -> Trong suốt
                    new SKPoint(0.5f, 0), // Bắt đầu từ trên
                    new SKPoint(0.5f, 1)  // Kết thúc ở dưới
                ),
                // Hiệu ứng cong mềm mại (Process Explore style)
                GeometrySize = 0, // Không hiện chấm tròn tại các điểm
                LineSmoothness = 1 // 0 là thẳng tuột, 1 là cong mềm
            }
            };

            // Cấu hình trục X (Ẩn nhãn để giống Task Manager)
            XAxes = new Axis[]
            {
            new Axis
            {
                IsVisible = false, // Ẩn trục X
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
            SimulateData();
        }

        private async void SimulateData()
        {
            while (true)
            {
                await Task.Delay(1000); // Cập nhật mỗi 1 giây

                // Giả lập giá trị CPU ngẫu nhiên
                var randomValue = new Random().Next(10, 80);

                // Thêm giá trị mới
                _cpuValues.Add(randomValue);

                // Xóa giá trị cũ để biểu đồ luôn trôi (giữ lại 50 điểm gần nhất)
                if (_cpuValues.Count > 50)
                {
                    _cpuValues.RemoveAt(0);
                }
            }
        }
    }
}
