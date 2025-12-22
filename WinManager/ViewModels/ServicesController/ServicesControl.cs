using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using WinManager.Models;

namespace WinManager.ViewModels
{
    public partial class ServicesControl : ObservableObject, IDisposable
    {
        private readonly ServicesModel _servicesModel;
        private readonly CancellationTokenSource _cts;

 
        private List<ServiceItemViewModel> _allServicesCache;

        [ObservableProperty]
        private ObservableCollection<ServiceItemViewModel> _servicesList;


        [ObservableProperty]
        private string _searchText;

        public ServicesControl()
        {
            _servicesModel = new ServicesModel();
            _servicesList = new ObservableCollection<ServiceItemViewModel>();
            _allServicesCache = new List<ServiceItemViewModel>();
            _cts = new CancellationTokenSource();

            LoadData();

            Task.Run(() => MonitorLoop(_cts.Token));
        }

        private void LoadData()
        {
            try
            {

                var rawServices = _servicesModel.GetAllServices();


                var viewModels = rawServices.Select(s => new ServiceItemViewModel(s, _servicesModel)).ToList();


                _allServicesCache = viewModels;


                ApplyFilter();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading services: {ex.Message}");
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_allServicesCache == null) return;

            IEnumerable<ServiceItemViewModel> filtered;

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = _allServicesCache;
            }
            else
            {
                var keyword = SearchText.ToLower();
                filtered = _allServicesCache.Where(s =>
                    s.DisplayName.ToLower().Contains(keyword) ||
                    s.ServiceName.ToLower().Contains(keyword) ||
                    s.Description.ToLower().Contains(keyword)
                );
            }

            // Cập nhật ObservableCollection trên UI Thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                ServicesList = new ObservableCollection<ServiceItemViewModel>(filtered);
            });
        }

        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
 
                    if (ServicesList != null && ServicesList.Count > 0)
                    {

                        var currentViewItems = ServicesList.ToList();

                        foreach (var item in currentViewItems)
                        {
                            if (token.IsCancellationRequested) break;


                            _servicesModel.RefreshService(item.ServiceName);

                            item.RefreshFromModel();
                        }
                    }

                    await Task.Delay(3000, token);
                }
                catch {  }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _servicesModel?.Dispose();
        }
    }


}