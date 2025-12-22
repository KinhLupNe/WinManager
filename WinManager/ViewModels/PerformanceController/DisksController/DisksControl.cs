using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using WinManager.Models;
using WinManager.ViewModels.DisksController;       

namespace WinManager.ViewModels
{
    public partial class DisksControl : ObservableObject, IDisposable
    {
        private readonly DisksModel _disksModel;
        private readonly CancellationTokenSource _cts;

        [ObservableProperty]
        private ObservableCollection<DiskItemViewModel> _diskList;

        public DisksControl()
        {
            _disksModel = new DisksModel();
            _cts = new CancellationTokenSource();

            var rawData = _disksModel.GetAllDisksInfo();

            var wrapperList = rawData.Select(d => new DiskItemViewModel(d));

            _diskList = new ObservableCollection<DiskItemViewModel>(wrapperList);

            Task.Run(() => MonitorLoop(_cts.Token));
        }

        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var latestDataList = _disksModel.GetAllDisksInfo();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var wrapper in DiskList)
                        {
                            var newData = latestDataList.FirstOrDefault(x => x.DeviceID == wrapper.DeviceID);

                            if (newData != null)
                            {
                                wrapper.ActiveTime = newData.ActiveTime;

                            }
                        }
                    });

                    await Task.Delay(1000, token);    
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    await Task.Delay(1000, token);
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _disksModel?.Dispose();
        }
    }
}