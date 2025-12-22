using System.Windows;

using System.Windows.Input;

namespace WinManager.Views
{
    /// <summary>
    /// Interaction logic for SeviceDetailView.xaml
    /// </summary>
    public partial class ServiceDetailView : Window
    {
        public ServiceDetailView(object viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}