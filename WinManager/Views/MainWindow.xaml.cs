using System.Windows;
using System.Windows.Media.Imaging;

namespace WinManager.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            try
            {
                Uri icon = new Uri("../../../Views/Image/icon1.ico",UriKind.Relative);
                this.Icon = new BitmapImage(icon);
            }
            catch (Exception ex)
            {
                MessageBox.Show( "Loi icon :"+ex.Message);
            }
            SetWindowSize();
        }

        private void SetWindowSize()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            this.Width = screenWidth * 0.85;
            this.Height = screenHeight * 0.9;
        }
    }
}