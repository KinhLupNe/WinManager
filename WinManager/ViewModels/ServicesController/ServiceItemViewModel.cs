using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using WinManager.Models;
using WinManager.Views;

namespace WinManager.ViewModels
{
    public partial class ServiceItemViewModel : ObservableObject
    {
        private readonly ServiceInfo _info;
        private readonly ServicesModel _modelReference;

        public ServiceItemViewModel(ServiceInfo info, ServicesModel modelRef)
        {
            _info = info;
            _modelReference = modelRef;
        }

        public string DisplayName => _info.DisplayName;

        public string ServiceName => _info.Name;

        public int Pid => _info.ProcessId;

        public string Status => _info.StatusDisplay;

        public string StartupType => _info.StartModeDisplay;

        public string Description => _info.Description;

        public void RefreshFromModel()
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(Pid));
        }

        [RelayCommand]
        public async Task ToggleService()
        {
            if (_modelReference == null) return;

            string error = "";
            bool success = false;

            await Task.Run(() =>
            {
                if (_info.Status == ServiceStatus.Running)
                {
                    success = _modelReference.StopService(_info.Name, out error);
                }
                else if (_info.Status == ServiceStatus.Stopped)
                {
                    success = _modelReference.StartService(_info.Name, out error);
                }
            });

            if (success)
            {
                _modelReference.RefreshService(_info.Name);
                RefreshFromModel();
            }
            else
            {
                MessageBox.Show(error, "Service Control Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void ShowDetails()
        {
            var detailWindow = new ServiceDetailView(this);

            detailWindow.ShowDialog();
        }
    }
}