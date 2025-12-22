using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using System.Linq; // Cần thiết để dùng FirstOrDefault
using WinManager.Models;
using WinManager.ViewModels.DisksController; // Hoặc namespace chứa DiskItemViewModel của bạn

namespace WinManager.ViewModels
{
    public partial class DisksControl : ObservableObject, IDisposable
    {
        private readonly DisksModel _disksModel;
        private readonly CancellationTokenSource _cts;

        // List chứa Wrapper (Vỏ bọc)
        [ObservableProperty]
        private ObservableCollection<DiskItemViewModel> _diskList;

        public DisksControl()
        {
            _disksModel = new DisksModel();
            _cts = new CancellationTokenSource();

            // 1. Lấy dữ liệu gốc ban đầu
            var rawData = _disksModel.GetAllDisksInfo();

            // 2. Đóng gói vào Wrapper (DiskInfo -> DiskItemViewModel)
            var wrapperList = rawData.Select(d => new DiskItemViewModel(d));

            // 3. Khởi tạo danh sách hiển thị
            _diskList = new ObservableCollection<DiskItemViewModel>(wrapperList);

            // Chạy loop
            Task.Run(() => MonitorLoop(_cts.Token));
        }

        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 1. Lấy danh sách dữ liệu MỚI NHẤT từ Model (chứa ActiveTime, Speed mới...)
                    // Hàm này phải trả về List<DiskInfo> đã được cập nhật số liệu
                    var latestDataList = _disksModel.GetAllDisksInfo();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 2. Duyệt qua từng Wrapper đang hiển thị trên giao diện
                        foreach (var wrapper in DiskList)
                        {
                            // 3. Tìm cục dữ liệu mới tương ứng với ổ đĩa này (So khớp bằng Tên ổ đĩa)
                            // wrapper.DiskName lấy từ wrapper
                            // x.DiskName lấy từ dữ liệu mới
                            var newData = latestDataList.FirstOrDefault(x => x.DeviceID == wrapper.DeviceID);

                            if (newData != null)
                            {
                                // --- KHẮC PHỤC LỖI CỦA BẠN Ở ĐÂY ---

                                // Thay vì gọi hàm UpdateDiskMetrics sai kiểu, ta GÁN TRỰC TIẾP giá trị mới vào Wrapper.
                                // Wrapper có [ObservableProperty] nên UI sẽ tự nhảy số.

                                wrapper.ActiveTime = newData.ActiveTime;

                                // Cập nhật thêm các thông số khác nếu cần (ReadSpeed, FreeSpace...)
                                // Ví dụ:
                                // wrapper.ReadSpeed = newData.ReadSpeed;
                                // wrapper.UsedSpaceGB = ...;
                            }
                        }
                    });

                    await Task.Delay(1000, token); // Đợi 1 giây
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    // Log lỗi nếu cần thiết
                    await Task.Delay(1000, token);
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _disksModel?.Dispose();
        }
    }
}