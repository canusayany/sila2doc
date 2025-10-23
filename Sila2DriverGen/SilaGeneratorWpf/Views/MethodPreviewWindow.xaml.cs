using System.Windows;
using SilaGeneratorWpf.ViewModels;

namespace SilaGeneratorWpf.Views
{
    /// <summary>
    /// 方法预览与特性调整窗口
    /// </summary>
    public partial class MethodPreviewWindow : Window
    {
        public MethodPreviewWindow(MethodPreviewViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            // 订阅确认和取消事件
            viewModel.OnConfirmed += () => DialogResult = true;
            viewModel.OnCancelled += () => DialogResult = false;
        }
    }
}

