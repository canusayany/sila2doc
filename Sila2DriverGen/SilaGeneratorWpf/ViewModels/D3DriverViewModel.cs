using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SilaGeneratorWpf.Models;
using SilaGeneratorWpf.Services;

namespace SilaGeneratorWpf.ViewModels
{
    /// <summary>
    /// D3驱动生成功能的ViewModel
    /// </summary>
    public partial class D3DriverViewModel : ObservableObject
    {
        private readonly ILogger _logger;
        private D3DriverGeneratorService? _generatorService;

        #region Observable Properties

        [ObservableProperty]
        private string _clientCodePath = string.Empty;

        [ObservableProperty]
        private string _detectedFeaturesText = "检测到的特性: (空)";

        [ObservableProperty]
        private string _deviceBrand = string.Empty;

        [ObservableProperty]
        private string _deviceModel = string.Empty;

        [ObservableProperty]
        private string _deviceType = string.Empty;

        [ObservableProperty]
        private string _developerName = string.Empty;

        [ObservableProperty]
        private string _d3OutputPath = string.Empty;

        [ObservableProperty]
        private string _d3Namespace = "BR.ECS.DeviceDriver.Generated";

        [ObservableProperty]
        private bool _generateTestConsole = true;

        [ObservableProperty]
        private bool _autoCompile = true;

        [ObservableProperty]
        private ObservableCollection<MethodPreviewData> _methodPreviewData = new();

        [ObservableProperty]
        private string _statusMessage = "就绪";

        [ObservableProperty]
        private string _statusColor = "#27ae60";

        [ObservableProperty]
        private bool _isGenerating;

        [ObservableProperty]
        private bool _canGenerate;

        #endregion

        private List<ClientFeatureInfo> _detectedFeatures = new();

        public D3DriverViewModel()
        {
            _logger = LoggerService.GetLogger<D3DriverViewModel>();
            _logger.LogInformation("D3驱动生成 ViewModel 初始化");
        }

        /// <summary>
        /// 浏览客户端代码目录
        /// </summary>
        [RelayCommand]
        private void BrowseClientCode()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择客户端代码目录",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ClientCodePath = dialog.SelectedPath;
                _logger.LogInformation($"选择了客户端代码目录: {ClientCodePath}");
                
                // 自动分析客户端代码
                AnalyzeClientCode();
            }
        }

        /// <summary>
        /// 浏览输出目录
        /// </summary>
        [RelayCommand]
        private void BrowseD3Output()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择D3驱动输出目录",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                D3OutputPath = dialog.SelectedPath;
                _logger.LogInformation($"选择了输出目录: {D3OutputPath}");
            }
        }

        /// <summary>
        /// 分析客户端代码
        /// </summary>
        private void AnalyzeClientCode()
        {
            try
            {
                UpdateStatus("正在分析客户端代码...", StatusType.Info);
                _logger.LogInformation("开始分析客户端代码");

                var analyzer = new ClientCodeAnalyzer();
                var analysisResult = analyzer.Analyze(ClientCodePath);
                
                _detectedFeatures = analysisResult.Features;
                
                // 更新检测到的特性文本
                if (_detectedFeatures.Any())
                {
                    var featureNames = string.Join(", ", _detectedFeatures.Select(f => f.FeatureName));
                    DetectedFeaturesText = $"检测到的特性: {featureNames} ({_detectedFeatures.Count}个)";
                    
                    // 更新预览表格
                    var previewData = analysisResult.GetMethodPreviewData();
                    MethodPreviewData = new ObservableCollection<MethodPreviewData>(previewData);
                    
                    UpdateStatus($"成功分析 {_detectedFeatures.Count} 个特性", StatusType.Success);
                    CanGenerate = true;
                }
                else
                {
                    DetectedFeaturesText = "检测到的特性: (未检测到有效特性)";
                    MethodPreviewData.Clear();
                    UpdateStatus("未检测到有效的特性", StatusType.Warning);
                    CanGenerate = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分析客户端代码失败");
                UpdateStatus("分析失败", StatusType.Error);
                MessageBox.Show($"分析客户端代码失败：\n\n{ex.Message}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                CanGenerate = false;
            }
        }

        /// <summary>
        /// 生成D3驱动
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExecuteGenerate))]
        private async System.Threading.Tasks.Task GenerateD3DriverAsync()
        {
            if (!_detectedFeatures.Any())
            {
                MessageBox.Show("请先选择客户端代码目录", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 验证设备信息
            if (string.IsNullOrWhiteSpace(DeviceBrand) || string.IsNullOrWhiteSpace(DeviceModel))
            {
                MessageBox.Show("请填写设备品牌和型号", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 确保输出目录
            if (string.IsNullOrWhiteSpace(D3OutputPath))
            {
                D3OutputPath = Path.Combine(
                    Path.GetTempPath(),
                    "SiLA2_D3Driver",
                    $"{DeviceBrand}_{DeviceModel}_{DateTime.Now:yyyyMMdd_HHmmss}");
            }

            IsGenerating = true;
            UpdateStatus("正在生成D3驱动...", StatusType.Info);
            _logger.LogInformation("开始生成D3驱动");

            try
            {
                var config = new D3DriverGenerationConfig
                {
                    Brand = DeviceBrand.Trim(),
                    Model = DeviceModel.Trim(),
                    DeviceType = DeviceType.Trim(),
                    Developer = DeveloperName.Trim(),
                    Namespace = D3Namespace.Trim(),
                    OutputPath = D3OutputPath,
                    ClientCodePath = ClientCodePath,
                    Features = _detectedFeatures,
                    GenerateTestConsole = GenerateTestConsole,
                    AutoCompile = AutoCompile
                };

                _generatorService = new D3DriverGeneratorService();

                var result = await System.Threading.Tasks.Task.Run(() => _generatorService.Generate(
                    config,
                    message => Application.Current.Dispatcher.Invoke(() => UpdateStatus(message, StatusType.Info))));

                if (result.Success)
                {
                    var message = result.Message;
                    var compileSuccess = result.CompileSuccess;
                    if (compileSuccess.HasValue)
                    {
                        var compileWarnings = result.CompileWarnings;
                        var compileErrors = result.CompileErrors;
                        message += compileSuccess.Value 
                            ? $"\n编译成功 (警告: {compileWarnings})" 
                            : $"\n编译失败 (错误: {compileErrors}, 警告: {compileWarnings})";
                    }
                    
                    UpdateStatus($"✓ {message}", StatusType.Success);
                    _logger.LogInformation("D3驱动生成成功");

                    var dialogResult = MessageBox.Show(
                        $"D3驱动生成完成！\n\n输出目录: {D3OutputPath}\n\n{message}\n\n是否打开输出文件夹？",
                        "生成成功",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (dialogResult == MessageBoxResult.Yes)
                    {
                        OpenDirectory(D3OutputPath);
                    }
                }
                else
                {
                    UpdateStatus($"✗ 生成失败", StatusType.Error);
                    _logger.LogError($"D3驱动生成失败: {result.Message}");
                    MessageBox.Show($"生成失败！\n\n{result.Message}",
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成D3驱动过程中发生错误");
                UpdateStatus("✗ 生成过程中发生错误", StatusType.Error);
                MessageBox.Show($"发生未预期的错误:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsGenerating = false;
            }
        }

        private bool CanExecuteGenerate() => !IsGenerating && CanGenerate;

        /// <summary>
        /// 打开输出文件夹
        /// </summary>
        [RelayCommand]
        private void OpenD3OutputFolder()
        {
            if (!string.IsNullOrEmpty(D3OutputPath) && Directory.Exists(D3OutputPath))
            {
                OpenDirectory(D3OutputPath);
            }
            else
            {
                MessageBox.Show("输出目录不存在，请先生成驱动代码", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// 打开目录
        /// </summary>
        private void OpenDirectory(string path)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"打开目录失败: {path}");
                MessageBox.Show($"无法打开目录：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 更新状态
        /// </summary>
        private void UpdateStatus(string message, StatusType type)
        {
            StatusMessage = message;
            StatusColor = type switch
            {
                StatusType.Success => "#27ae60",
                StatusType.Warning => "#f39c12",
                StatusType.Error => "#e74c3c",
                _ => "#7f8c8d"
            };
        }
    }

    /// <summary>
    /// 状态类型
    /// </summary>
    public enum StatusType
    {
        Info,
        Success,
        Warning,
        Error
    }
}

