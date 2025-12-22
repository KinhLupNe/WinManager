using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;
using WinManager.Models;
// Đảm bảo namespace này đúng với nơi bạn để DiskItemViewModel
using WinManager.ViewModels;
using WinManager.ViewModels.DisksController;

namespace WinManager.ViewModels
{
    public partial class PerformanceControl : ObservableObject
    {
        // Các View tĩnh
        public CPUControl CpuControl { get; } = new CPUControl();
        public MemoryControl MemControl { get; } = new MemoryControl();

        // ViewModel chứa danh sách ổ đĩa (Menu bên trái - chứa các Wrapper)
        public DisksControl DiskControl { get; } = new DisksControl();

        // Kho chứa các màn hình chi tiết đã tạo sẵn
        private readonly Dictionary<string, DiskDetailControl> _diskDetailCache = new Dictionary<string, DiskDetailControl>();

        [ObservableProperty]
        private object? _currentView;

        public PerformanceControl()
        {
            // Mặc định hiển thị CPU
            CurrentView = CpuControl;

            // --- SỬA PHẦN NÀY: BÓC VỎ WRAPPER ---
            // DiskControl.DiskList bây giờ là danh sách DiskItemViewModel
            foreach (var wrapper in DiskControl.DiskList)
            {
                // 1. Lấy thông tin gốc từ bên trong Wrapper ra
                var info = wrapper.OriginalDiskInfo;

                // 2. Tạo màn hình chi tiết (DiskDetailControl cần DiskInfo gốc)
                var detailVM = new DiskDetailControl(info);

                // 3. Lưu vào cache với Key là DeviceID (như code cũ của bạn)
                if (!_diskDetailCache.ContainsKey(info.DeviceID))
                {
                    _diskDetailCache.Add(info.DeviceID, detailVM);
                }
            }
        }

        [RelayCommand]
        public void NavigateToCPU() => CurrentView = CpuControl;

        [RelayCommand]
        public void NavigateToMemory() => CurrentView = MemControl;

        // --- SỬA PHẦN NÀY: NHẬN WRAPPER TỪ VIEW ---
        [RelayCommand]
        public void NavigateToDisk(object parameter) // Đổi tham số thành object
        {
            DiskInfo selectedDisk = null;

            // 1. Kiểm tra xem View gửi xuống cái gì?
            if (parameter is DiskItemViewModel wrapper)
            {
                // Nếu là Wrapper (do Binding gửi) -> Bóc vỏ lấy lõi
                selectedDisk = wrapper.OriginalDiskInfo;
            }
            else if (parameter is DiskInfo info)
            {
                // Phòng hờ trường hợp code cũ gọi trực tiếp
                selectedDisk = info;
            }

            if (selectedDisk == null) return;

            // 2. Logic Cache (Giữ nguyên như cũ)
            string key = selectedDisk.DeviceID; // Dùng DeviceID làm key

            if (_diskDetailCache.TryGetValue(key, out var existingVM))
            {
                CurrentView = existingVM;
            }
            else
            {
                // Phòng trường hợp ổ đĩa mới cắm vào sau khi App đã chạy
                var newVM = new DiskDetailControl(selectedDisk);
                _diskDetailCache[key] = newVM;
                CurrentView = newVM;
            }
        }
    }
}