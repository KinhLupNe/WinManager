using System.Diagnostics;
using System.Windows;       // Cần thêm
using System.Windows.Controls;

namespace WinManager.Views
{
    public partial class SettingView : UserControl
    {
        public SettingView()
        {
            InitializeComponent();
        }

        // Hàm cũ xử lý Hyperlink (nếu có)
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        // --- THÊM HÀM MỚI NÀY ---
        // Hàm này dùng chung được cho mọi nút Button có chứa link trong Tag
        private void LinkButton_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra xem người gửi có phải là Button và Tag có phải là chuỗi không
            if (sender is Button btn && btn.Tag is string url)
            {
                // Mở trình duyệt
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }
    }
}