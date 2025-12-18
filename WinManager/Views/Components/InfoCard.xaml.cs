using System.Windows;
using System.Windows.Controls;

namespace WinManager.Views.Components
{
    public partial class InfoCard : UserControl
    {
        public InfoCard() { InitializeComponent(); }

        // Dependency Properties để có thể truyền dữ liệu từ bên ngoài vào (Label="Speed" Value="4GHz")
        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(string), typeof(InfoCard), new PropertyMetadata(string.Empty));

        public string Value
        {
            get { return (string)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(string), typeof(InfoCard), new PropertyMetadata(string.Empty));
    }
}