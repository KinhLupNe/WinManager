using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinManager.Models;
using System.Collections.Generic;
using System.Linq; // Dùng để xử lý danh sách

namespace WinManager.ViewModels
{
    public partial class PerformanceControl : ObservableObject
    {
        // Các View tĩnh
        public CPUControl CpuControl { get; } = new CPUControl();
        public MemoryControl MemControl { get; } = new MemoryControl();

        // Đây là ViewModel chứa danh sách các ổ đĩa (Menu bên trái)
        public DisksControl DiskControl { get; } = new DisksControl();

        // Kho chứa các màn hình chi tiết đã tạo sẵn
        private readonly Dictionary<string, DiskDetailControl> _diskDetailCache = new Dictionary<string, DiskDetailControl>();

        [ObservableProperty]
        private object? _currentView;

        public PerformanceControl()
        {
            // Mặc định hiển thị CPU
            CurrentView = CpuControl;

            // --- THAY ĐỔI QUAN TRỌNG Ở ĐÂY ---
            // Ngay khi khởi động, duyệt qua toàn bộ danh sách ổ đĩa đang có
            // và tạo sẵn các Instance (để chúng chạy song song luôn)

            // Giả sử: DiskControl có một property tên là "Disks" chứa danh sách DiskInfo
            // Bạn hãy đổi 'Disks' thành tên biến thực tế trong class DisksControl của bạn
            foreach (var disk in DiskControl.DiskList)
            {
                // Tạo mới instance
                var detailVM = new DiskDetailControl(disk);

                // Lưu vào cache
                if (!_diskDetailCache.ContainsKey(disk.DeviceID))
                {
                    _diskDetailCache.Add(disk.DeviceID, detailVM);
                }
            }
        }

        [RelayCommand]
        public void NavigateToCPU() => CurrentView = CpuControl;

        [RelayCommand]
        public void NavigateToMemory() => CurrentView = MemControl;

        [RelayCommand]
        public void NavigateToDisk(DiskInfo selectedDisk)
        {
            if (selectedDisk == null) return;

            // Bây giờ khi bấm nút, ta KHÔNG tạo mới nữa
            // Chỉ việc lấy từ kho ra (vì đã tạo sẵn ở Constructor rồi)

            if (_diskDetailCache.TryGetValue(selectedDisk.DeviceID, out var existingVM))
            {
                CurrentView = existingVM;
            }
            else
            {
                // Phòng hờ trường hợp ổ đĩa mới cắm vào (USB) sau khi app đã chạy
                // thì lúc này mới tạo
                var newVM = new DiskDetailControl(selectedDisk);
                _diskDetailCache[selectedDisk.DeviceID] = newVM;
                CurrentView = newVM;
            }
        }
    }
}