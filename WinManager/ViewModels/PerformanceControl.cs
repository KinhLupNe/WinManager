using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignColors.Recommended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinManager.ViewModels
{
    public partial class PerformanceControl : ObservableObject
    {
        private readonly CPUControl _cpuControl = new CPUControl();
        private readonly MemoryControl _memoryControl = new MemoryControl();

        [ObservableProperty]
        private object? _currentView;

        public PerformanceControl()
        {
            CurrentView = _cpuControl;
        }

        [RelayCommand]
        public void NavigateToCPU() => CurrentView = _cpuControl;

        [RelayCommand]
        public void NavigateToMemory() => CurrentView = _memoryControl;
    }
}
