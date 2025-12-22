using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace WinManager.ViewModels
{

    public partial class SettingControl : ObservableObject
    {

        [ObservableProperty]
        private bool _isAlwaysOnTop;

        [ObservableProperty]
        private bool _runAtStartup;

        [ObservableProperty]
        private bool _startMinimized;


        public SettingControl()
        {

        }

        partial void OnIsAlwaysOnTopChanged(bool value)
        {
            if (Application.Current != null && Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Topmost = value;
            }
        }

    }
}