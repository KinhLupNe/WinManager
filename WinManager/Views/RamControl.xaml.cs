using System.Windows.Controls;
using WinManager.ViewModels;


namespace WinManager.Views
{
    public partial class RamControl : UserControl
    {
        public RamControl()
        {
            InitializeComponent();
            DataContext = new RamPresenter();   // giống cách bạn làm với CpuControl
        }
    }
}
