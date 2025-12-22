using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq; // Cần để dùng .Select()
using WinManager.Models; // Namespace chứa DiskInfo và VolumeInfo

namespace WinManager.ViewModels.DisksController
{
    public partial class DiskItemViewModel : ObservableObject
    {
        // 1. Giữ tham chiếu gốc (QUAN TRỌNG)
        // Dùng để lấy lại dữ liệu khi cần truyền sang PerformanceControl
        public DiskInfo OriginalDiskInfo { get; }

        // 2. Định danh (Dùng cho Cache và Navigation)
        public string DeviceID => OriginalDiskInfo.DeviceID;

        // 3. BIẾN ACTIVE TIME (Observable)
        // Model trả về float, nhưng UI thường dùng double cho binding mượt mà
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActiveTimeDisplay))]
        private double _activeTime;

        // ---------------------------------------------------------
        // CÁC THUỘC TÍNH BINDING CHO XAML
        // ---------------------------------------------------------

        // XAML: Text="{Binding Model}"
        // Lấy từ thuộc tính 'Model' trong DiskInfo
        public string Model => OriginalDiskInfo.Model;

        // XAML: Tag="{Binding DiskType}"
        // Lấy từ thuộc tính 'DiskType' (SSD/HDD) trong DiskInfo
        public string DiskType => OriginalDiskInfo.DiskType;

        // XAML: Text="{Binding FormattedTotalSize}"
        // Tính toán từ 'FormattedCapacity' (Tổng dung lượng các volume cộng lại)
        public string FormattedTotalSize
        {
            get
            {
                // Sử dụng FormattedCapacity thay vì Size để hiển thị dung lượng thực tế sử dụng được
                double sizeInGB = OriginalDiskInfo.FormattedCapacity / (1024.0 * 1024 * 1024);
                if (sizeInGB > 1000)
                {
                    return $"{sizeInGB / 1024.0:F1} TB";
                }
                return $"{sizeInGB:F0} GB";
            }
        }

        // XAML: ItemsSource="{Binding Volumes}"
        // Danh sách các phân vùng con (C:, D:)
        public List<VolumeItemViewModel> Volumes { get; }

        // Property phụ cho ActiveTime (Giữ lại để tương thích nếu cần hiển thị chuỗi thủ công)
        public string ActiveTimeDisplay => $"{ActiveTime:F0}%";


        // --- CONSTRUCTOR ---
        public DiskItemViewModel(DiskInfo diskInfo)
        {
            OriginalDiskInfo = diskInfo;

            // Gán giá trị ActiveTime ban đầu
            ActiveTime = diskInfo.ActiveTime;

            // Xử lý danh sách Volumes (Phân vùng)
            // Trong DisksModel.cs, thuộc tính tên là 'Volumes' (List<VolumeInfo>)
            if (diskInfo.Volumes != null)
            {
                // Chuyển đổi từ VolumeInfo (Model) sang VolumeItemViewModel (ViewModel)
                Volumes = diskInfo.Volumes.Select(v => new VolumeItemViewModel(v)).ToList();
            }
            else
            {
                Volumes = new List<VolumeItemViewModel>();
            }
        }
    }

    // --- CLASS CON: DÙNG CHO DANH SÁCH VOLUMES ---
    // Class này ánh xạ dữ liệu từ VolumeInfo để Binding vào XAML
    public class VolumeItemViewModel
    {
        // XAML: Text="{Binding DriveLetter}" -> VD: "C:"
        public string DriveLetter { get; }

        // XAML: Text="{Binding Label}" -> VD: "Windows"
        public string Label { get; }

        // Constructor nhận vào VolumeInfo từ Model
        public VolumeItemViewModel(VolumeInfo volume)
        {
            DriveLetter = volume.DriveLetter;
            Label = volume.Label;
        }
    }
}