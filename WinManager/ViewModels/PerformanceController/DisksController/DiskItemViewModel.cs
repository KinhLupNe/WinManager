using CommunityToolkit.Mvvm.ComponentModel;
using WinManager.Models;      

namespace WinManager.ViewModels.DisksController
{
    public partial class DiskItemViewModel : ObservableObject
    {
        public DiskInfo OriginalDiskInfo { get; }

        public string DeviceID => OriginalDiskInfo.DeviceID;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActiveTimeDisplay))]
        private double _activeTime;

        public string Model => OriginalDiskInfo.Model;

        public string DiskType => OriginalDiskInfo.DiskType;

        public string FormattedTotalSize
        {
            get
            {
                double sizeInGB = OriginalDiskInfo.FormattedCapacity / (1024.0 * 1024 * 1024);
                if (sizeInGB > 1000)
                {
                    return $"{sizeInGB / 1024.0:F1} TB";
                }
                return $"{sizeInGB:F0} GB";
            }
        }

        public List<VolumeItemViewModel> Volumes { get; }

        public string ActiveTimeDisplay => $"{ActiveTime:F0}%";

        public DiskItemViewModel(DiskInfo diskInfo)
        {
            OriginalDiskInfo = diskInfo;

            ActiveTime = diskInfo.ActiveTime;

            if (diskInfo.Volumes != null)
            {
                Volumes = diskInfo.Volumes.Select(v => new VolumeItemViewModel(v)).ToList();
            }
            else
            {
                Volumes = new List<VolumeItemViewModel>();
            }
        }
    }

    public class VolumeItemViewModel
    {
        public string DriveLetter { get; }

        public string Label { get; }

        public VolumeItemViewModel(VolumeInfo volume)
        {
            DriveLetter = volume.DriveLetter;
            Label = volume.Label;
        }
    }
}