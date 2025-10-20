using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SilaGeneratorWpf.Services;

namespace SilaGeneratorWpf.ViewModels
{
    /// <summary>
    /// 文件生成功能的ViewModel
    /// </summary>
    public partial class FileGenerationViewModel : ObservableObject
    {
        private readonly ClientCodeGenerator _codeGenerator;

        [ObservableProperty]
        private ObservableCollection<string> _featureFiles = new();

        [ObservableProperty]
        private string _outputDirectory = string.Empty;

        [ObservableProperty]
        private string _namespace = "Sila2Client";

        [ObservableProperty]
        private string _statusMessage = "就绪";

        [ObservableProperty]
        private string _statusColor = "#27ae60";

        [ObservableProperty]
        private bool _isGenerating;

        public FileGenerationViewModel()
        {
            _codeGenerator = new ClientCodeGenerator();
            InitializeOutputDirectory();
        }

        private void InitializeOutputDirectory()
        {
            var tempPath = Path.GetTempPath();
            OutputDirectory = Path.Combine(tempPath, "SilaGeneratedClients", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            
            try
            {
                if (!Directory.Exists(OutputDirectory))
                {
                    Directory.CreateDirectory(OutputDirectory);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建输出目录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void AddFiles(string[] files)
        {
            var addedCount = 0;

            foreach (var file in files)
            {
                // 验证文件扩展名
                var extension = Path.GetExtension(file).ToLower();
                if (extension != ".xml")
                {
                    continue;
                }

                // 避免重复添加
                if (!FeatureFiles.Contains(file))
                {
                    FeatureFiles.Add(file);
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                UpdateStatus($"已添加 {addedCount} 个文件", StatusType.Success);
            }
            else if (files.Length > 0)
            {
                UpdateStatus("未添加新文件（可能已存在或格式不正确）", StatusType.Warning);
            }
        }

        [RelayCommand]
        private void RemoveFile(string filePath)
        {
            FeatureFiles.Remove(filePath);
            UpdateStatus("已移除文件", StatusType.Info);
        }

        [RelayCommand]
        private void ClearFiles()
        {
            if (FeatureFiles.Any())
            {
                var result = MessageBox.Show(
                    $"确定要清空列表中的 {FeatureFiles.Count} 个文件吗？",
                    "确认清空",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    FeatureFiles.Clear();
                    UpdateStatus("列表已清空", StatusType.Info);
                }
            }
        }

        [RelayCommand]
        private void BrowseOutputDirectory()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择输出目录",
                UseDescriptionForTitle = true,
                SelectedPath = OutputDirectory,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OutputDirectory = dialog.SelectedPath;
            }
        }

        [RelayCommand]
        private async Task GenerateClientAsync()
        {
            if (!FeatureFiles.Any())
            {
                MessageBox.Show("请先添加 Feature 文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 创建新的输出目录
            InitializeOutputDirectory();

            IsGenerating = true;
            UpdateStatus("正在生成代码...", StatusType.Info);

            try
            {
                var customNamespace = string.IsNullOrWhiteSpace(Namespace) ? "Sila2Client" : Namespace;

                var result = await Task.Run(() =>
                {
                    return _codeGenerator.GenerateClientCode(
                        FeatureFiles.ToList(),
                        OutputDirectory,
                        customNamespace,
                        message => Application.Current.Dispatcher.Invoke(() => UpdateStatus(message, StatusType.Info)));
                });

                if (result.Success)
                {
                    UpdateStatus($"✓ {result.Message}", StatusType.Success);

                    var detailMessage = $"生成完成！\n\n" +
                                      $"输出目录: {OutputDirectory}\n" +
                                      $"生成文件数: {result.GeneratedFiles.Count}\n";

                    if (result.Warnings.Any())
                    {
                        detailMessage += $"\n警告信息:\n{string.Join("\n", result.Warnings)}";
                    }

                    var msgResult = MessageBox.Show(
                        detailMessage + "\n\n是否打开输出文件夹？",
                        "生成成功",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (msgResult == MessageBoxResult.Yes)
                    {
                        OpenOutputFolder();
                    }
                }
                else
                {
                    UpdateStatus("✗ 生成失败", StatusType.Error);
                    
                    var errorMessage = $"生成失败！\n\n{result.Message}";
                    if (!string.IsNullOrEmpty(result.ErrorDetails))
                    {
                        errorMessage += $"\n\n详细信息:\n{result.ErrorDetails}";
                    }

                    MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("✗ 生成过程中发生错误", StatusType.Error);
                MessageBox.Show($"发生未预期的错误:\n\n{ex.Message}\n\n{ex.StackTrace}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsGenerating = false;
            }
        }

        [RelayCommand]
        private void OpenOutputFolder()
        {
            if (!string.IsNullOrEmpty(OutputDirectory) && Directory.Exists(OutputDirectory))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = OutputDirectory,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法打开文件夹:\n{ex.Message}", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("输出目录不存在，请先生成代码", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void UpdateStatus(string message, StatusType type)
        {
            StatusMessage = message;
            StatusColor = type switch
            {
                StatusType.Success => "#27ae60",
                StatusType.Warning => "#f39c12",
                StatusType.Error => "#e74c3c",
                _ => "#34495e"
            };
        }

        public enum StatusType
        {
            Info,
            Success,
            Warning,
            Error
        }
    }
}

