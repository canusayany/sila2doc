using System.Windows.Controls;
using System.Windows.Input;
using SilaGeneratorWpf.ViewModels;

namespace SilaGeneratorWpf.Views
{
    /// <summary>
    /// D3DriverView.xaml 的交互逻辑
    /// </summary>
    public partial class D3DriverView : UserControl
    {
        public D3DriverView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// GridSplitter双击事件：切换侧边栏
        /// </summary>
        private void GridSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is D3DriverViewModel viewModel)
            {
                viewModel.ToggleSidebarCommand.Execute(null);
            }
        }
    }
}


