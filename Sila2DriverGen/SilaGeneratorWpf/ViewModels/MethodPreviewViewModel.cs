using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SilaGeneratorWpf.Models;

namespace SilaGeneratorWpf.ViewModels
{
    /// <summary>
    /// 方法预览与特性调整窗口的ViewModel
    /// </summary>
    public partial class MethodPreviewViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<MethodPreviewData> _methodPreviewData;

        /// <summary>
        /// 确认事件
        /// </summary>
        public event Action? OnConfirmed;

        /// <summary>
        /// 取消事件
        /// </summary>
        public event Action? OnCancelled;

        public MethodPreviewViewModel(ObservableCollection<MethodPreviewData> methodPreviewData)
        {
            _methodPreviewData = methodPreviewData;
        }

        [RelayCommand]
        private void ToggleAllIncluded()
        {
            // 如果全部包含，则全部不包含；否则全部包含
            bool allIncluded = MethodPreviewData.All(m => m.IsIncluded);
            foreach (var method in MethodPreviewData)
            {
                method.IsIncluded = !allIncluded;
            }
        }

        [RelayCommand]
        private void SetAllMaintenance()
        {
            foreach (var method in MethodPreviewData)
            {
                method.IsMaintenance = true;
            }
        }

        [RelayCommand]
        private void SetAllOperations()
        {
            foreach (var method in MethodPreviewData)
            {
                method.IsOperations = true;
            }
        }

        [RelayCommand]
        private void ClearAllAttributes()
        {
            foreach (var method in MethodPreviewData)
            {
                method.IsOperations = false;
                method.IsMaintenance = false;
            }
        }

        [RelayCommand]
        private void Confirm()
        {
            OnConfirmed?.Invoke();
        }

        [RelayCommand]
        private void Cancel()
        {
            OnCancelled?.Invoke();
        }
    }
}

