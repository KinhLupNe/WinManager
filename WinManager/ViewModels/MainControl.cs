
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WinManager.ViewModels
{
    public partial class MainControl : ObservableObject
    {
        private readonly PerformanceControl _performanceControl = new PerformanceControl();
        private readonly ServicesControl _servicesControl = new ServicesControl();
        private readonly SettingControl _settingControl = new SettingControl();

        [ObservableProperty]
        private object? _currentView;

        public MainControl()
        {
            CurrentView = _performanceControl;
        }

        [RelayCommand]
        public void NavigateToPerformance()
        {
            CurrentView = _performanceControl;
        }

        [RelayCommand]
        public void NavigateToServices()
        {
            CurrentView = _servicesControl;
        }

        [RelayCommand]
        public void NavigateToSetting()
        {
            CurrentView = _settingControl;
        }
    }
}
