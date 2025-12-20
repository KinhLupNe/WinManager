using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace WinManager.ViewModels
{
    public partial class PerformanceControl : ObservableObject
    {
        public CPUControl CpuControl { get; } = new CPUControl();
        private readonly MemoryControl _memoryControl = new MemoryControl();

        [ObservableProperty]
        private object? _currentView;

        public PerformanceControl()
        {
            CurrentView = CpuControl;
        }

        [RelayCommand]
        public void NavigateToCPU() => CurrentView = CpuControl;

        [RelayCommand]
        public void NavigateToMemory() => CurrentView = _memoryControl;
    }
}
