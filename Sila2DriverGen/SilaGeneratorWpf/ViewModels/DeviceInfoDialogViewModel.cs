using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SilaGeneratorWpf.ViewModels
{
    /// <summary>
    /// 设备信息输入对话框的ViewModel
    /// </summary>
    public partial class DeviceInfoDialogViewModel : ObservableObject
    {
        /// <summary>
        /// 设备品牌
        /// </summary>
        [ObservableProperty]
        private string _brand = string.Empty;

        /// <summary>
        /// 设备型号
        /// </summary>
        [ObservableProperty]
        private string _model = string.Empty;

        /// <summary>
        /// 设备类型
        /// </summary>
        [ObservableProperty]
        private string _deviceType = string.Empty;

        /// <summary>
        /// 开发者
        /// </summary>
        [ObservableProperty]
        private string _developer = "Bioyond";

        /// <summary>
        /// 确认事件
        /// </summary>
        public event Action? OnConfirmed;

        /// <summary>
        /// 取消事件
        /// </summary>
        public event Action? OnCancelled;

        /// <summary>
        /// 确认命令
        /// </summary>
        [RelayCommand]
        private void Confirm()
        {
            // 验证必填字段
            if (string.IsNullOrWhiteSpace(Brand))
            {
                System.Windows.MessageBox.Show("请输入设备品牌", "验证失败", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(Model))
            {
                System.Windows.MessageBox.Show("请输入设备型号", "验证失败", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(DeviceType))
            {
                System.Windows.MessageBox.Show("请输入设备类型", "验证失败", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // 触发确认事件
            OnConfirmed?.Invoke();
        }

        /// <summary>
        /// 取消命令
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            // 触发取消事件
            OnCancelled?.Invoke();
        }
    }
}

