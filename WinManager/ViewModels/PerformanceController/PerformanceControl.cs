using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinManager.Models;

using WinManager.ViewModels.DisksController;

namespace WinManager.ViewModels
{
    public partial class PerformanceControl : ObservableObject
    {
        public CPUControl CpuControl { get; } = new CPUControl();

        public MemoryControl MemControl { get; } = new MemoryControl();

        public DisksControl DiskControl { get; } = new DisksControl();

        private readonly Dictionary<string, DiskDetailControl> _diskDetailCache = new Dictionary<string, DiskDetailControl>();

        [ObservableProperty]
        private object? _currentView;

        public PerformanceControl()
        {
            CurrentView = CpuControl;

            foreach (var wrapper in DiskControl.DiskList)
            {
                var info = wrapper.OriginalDiskInfo;

                var detailVM = new DiskDetailControl(info);

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

        [RelayCommand]
        public void NavigateToDisk(object parameter)      
        {
            DiskInfo selectedDisk = null;

            if (parameter is DiskItemViewModel wrapper)
            {
                selectedDisk = wrapper.OriginalDiskInfo;
            }
            else if (parameter is DiskInfo info)
            {
                selectedDisk = info;
            }

            if (selectedDisk == null) return;

            string key = selectedDisk.DeviceID;     

            if (_diskDetailCache.TryGetValue(key, out var existingVM))
            {
                CurrentView = existingVM;
            }
            else
            {
                var newVM = new DiskDetailControl(selectedDisk);
                _diskDetailCache[key] = newVM;
                CurrentView = newVM;
            }
        }
    }
}