using System.Windows;
using SilaGeneratorWpf.ViewModels;

namespace SilaGeneratorWpf.Views
{
    /// <summary>
    /// 设备信息输入对话框
    /// </summary>
    public partial class DeviceInfoDialog : Window
    {
        public DeviceInfoDialog()
        {
            InitializeComponent();
            
            // 如果DataContext是DeviceInfoDialogViewModel，订阅其事件
            Loaded += (s, e) =>
            {
                if (DataContext is DeviceInfoDialogViewModel viewModel)
                {
                    viewModel.OnConfirmed += () =>
                    {
                        DialogResult = true;
                        Close();
                    };

                    viewModel.OnCancelled += () =>
                    {
                        DialogResult = false;
                        Close();
                    };
                }
            };
        }
    }
}

