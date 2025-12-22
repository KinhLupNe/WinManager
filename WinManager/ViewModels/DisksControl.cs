using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows; // Cần thiết cho Dispatcher
using WinManager.Models;

namespace WinManager.ViewModels
{
    public partial class DisksControl : ObservableObject, IDisposable
    {
        private readonly DisksModel _disksModel;
        private readonly CancellationTokenSource _cts; // Token để hủy luồng an toàn

        [ObservableProperty]
        private ObservableCollection<DiskInfo> _diskList;



        public DisksControl()
        {
            _disksModel = new DisksModel();
            _diskList = new ObservableCollection<DiskInfo>(_disksModel.GetAllDisksInfo());
            _cts = new CancellationTokenSource();

            // Chạy loop trên luồng phụ để không đơ UI
            Task.Run(() => MonitorLoop(_cts.Token));
        }

        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Lấy dữ liệu mới nhất từ Model (xử lý trên luồng phụ)
                    // Lưu ý: Hàm này trong Model nên cập nhật thông số vào chính các object DiskInfo
                    var updatedDisks = _disksModel.GetAllDisksInfo();

                    // Cập nhật lên UI (bắt buộc dùng Dispatcher)
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Thay vì thay thế toàn bộ list (gây mất focus), ta cập nhật từng phần tử
                        // Giả sử thứ tự ổ đĩa không đổi. Nếu cắm rút USB thì cần logic phức tạp hơn chút.
                        for (int i = 0; i < DiskList.Count; i++)
                        {
                            if (i < updatedDisks.Count)
                            {
                                // Vì DiskInfo là class (reference type), việc update thông số bên trong
                                // sẽ tự động reflect lên UI nếu DiskInfo có INotifyPropertyChanged
                                _disksModel.UpdateDiskMetrics(DiskList[i]);
                            }
                        }
                    });

                    await Task.Delay(1000, token); // Đợi 1 giây
                }
                catch (TaskCanceledException) { break; }
                catch { await Task.Delay(1000, token); }
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