using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SilaGeneratorWpf.Models;
using SilaGeneratorWpf.Services;
using Tecan.Sila2;

namespace SilaGeneratorWpf.ViewModels
{
    /// <summary>
    /// 服务器发现功能的ViewModel
    /// </summary>
    public partial class ServerDiscoveryViewModel : ObservableObject
    {
        private readonly ServerDiscoveryService _discoveryService;
        private readonly ServerInteractionService _interactionService;
        private readonly ClientCodeGenerator _codeGenerator;
        private readonly ILogger _logger;

        [ObservableProperty]
        private ObservableCollection<ServerInfoViewModel> _servers = new();

        [ObservableProperty]
        private string _discoveryStatus = "就绪";

        [ObservableProperty]
        private string _discoveryStatusColor = "#7f8c8d";

        [ObservableProperty]
        private string _outputDirectory = string.Empty;

        [ObservableProperty]
        private bool _isSidebarVisible = true;

        [ObservableProperty]
        private object? _selectedItem;

        [ObservableProperty]
        private string _detailTitle = "请选择服务器或特性";

        public ServerDiscoveryViewModel()
        {
            _discoveryService = new ServerDiscoveryService();
            _interactionService = new ServerInteractionService();
            _codeGenerator = new ClientCodeGenerator();
            _logger = LoggerService.GetLogger<ServerDiscoveryViewModel>();
            
            InitializeOutputDirectory();
        }

        private void InitializeOutputDirectory()
        {
            var tempPath = Path.GetTempPath();
            OutputDirectory = Path.Combine(tempPath, "SilaDiscoveredServers", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        }

        [RelayCommand]
        private async Task ScanServersAsync()
        {
            _logger.LogInformation("用户点击扫描服务器");
            UpdateStatus("正在扫描服务器...", StatusType.Info);
            
            try
            {
                var servers = await _discoveryService.ScanServersAsync(TimeSpan.FromSeconds(3));
                
                Servers.Clear();
                foreach (var server in servers)
                {
                    Servers.Add(server);
                }

                _logger.LogInformation($"扫描完成，发现 {servers.Count} 个服务器");
                UpdateStatus($"发现 {servers.Count} 个服务器", StatusType.Success);

                if (servers.Count == 0)
                {
                    _logger.LogWarning("未发现任何服务器");
                    MessageBox.Show("未发现任何SiLA2服务器\n\n请确保：\n1. 服务器正在运行\n2. 网络连接正常\n3. mDNS服务已启用",
                        "扫描结果", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描服务器时出错");
                UpdateStatus("扫描失败", StatusType.Error);
                MessageBox.Show($"扫描失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RefreshSelectedAsync()
        {
            var selectedServers = Servers.Where(s => s.IsSelected || s.Features.Any(f => f.IsSelected)).ToList();
            
            if (!selectedServers.Any())
            {
                MessageBox.Show("请先选择要刷新的服务器", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            UpdateStatus("正在加载特性...", StatusType.Info);
            var successCount = 0;

            foreach (var server in selectedServers)
            {
                try
                {
                    var success = await _discoveryService.LoadServerFeaturesAsync(server);
                    if (success)
                    {
                        successCount++;
                        server.IsExpanded = true;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载服务器 {server.ServerName} 的特性失败：\n{ex.Message}", 
                        "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            UpdateStatus($"成功加载 {successCount}/{selectedServers.Count} 个服务器的特性", StatusType.Success);
        }

        [RelayCommand]
        private void ToggleSidebar()
        {
            IsSidebarVisible = !IsSidebarVisible;
        }

        [RelayCommand]
        private void SaveFeatures()
        {
            var selectedServers = Servers.Where(s => s.IsSelected || s.Features.Any(f => f.IsSelected)).ToList();
            
            if (!selectedServers.Any())
            {
                MessageBox.Show("请先选择要保存的服务器或特性", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                if (!Directory.Exists(OutputDirectory))
                {
                    Directory.CreateDirectory(OutputDirectory);
                }

                var result = _discoveryService.SaveFeatures(selectedServers, OutputDirectory, selectedOnly: true);

                if (result.Success)
                {
                    var message = $"{result.Message}\n\n输出目录: {OutputDirectory}";
                    if (result.Warnings.Any())
                    {
                        message += $"\n\n警告:\n{string.Join("\n", result.Warnings)}";
                    }

                    var dialogResult = MessageBox.Show(
                        message + "\n\n是否打开输出文件夹？",
                        "保存成功",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (dialogResult == MessageBoxResult.Yes)
                    {
                        OpenDirectory(OutputDirectory);
                    }

                    UpdateStatus(result.Message, StatusType.Success);
                }
                else
                {
                    MessageBox.Show($"保存失败:\n\n{result.Message}", 
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateStatus("保存失败", StatusType.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存特性时发生错误：\n\n{ex.Message}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("保存失败", StatusType.Error);
            }
        }

        [RelayCommand]
        private async Task DirectGenerateClientsAsync()
        {
            var selectedServers = Servers.Where(s => s.IsSelected || s.Features.Any(f => f.IsSelected)).ToList();
            
            if (!selectedServers.Any())
            {
                MessageBox.Show("请先选择要生成的服务器或特性", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            UpdateStatus("正在准备生成代码...", StatusType.Info);

            try
            {
                // 创建输出目录
                if (!Directory.Exists(OutputDirectory))
                {
                    Directory.CreateDirectory(OutputDirectory);
                }

                // 获取自定义命名空间
                var customNamespace = "Sila2Client";

                // 获取按服务器分组的Feature对象
                var serverFeaturesMap = _discoveryService.GetSelectedFeaturesGroupedByServer(selectedServers, selectedOnly: true);

                if (!serverFeaturesMap.Any())
                {
                    MessageBox.Show("未找到要生成的特性", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var totalGenerated = 0;

                // 为每个服务器生成代码
                foreach (var serverEntry in serverFeaturesMap)
                {
                    var serverFolderName = serverEntry.Key;
                    var features = serverEntry.Value;

                    var serverOutputDir = Path.Combine(OutputDirectory, serverFolderName);
                    
                    UpdateStatus($"正在为服务器 {serverFolderName} 生成代码...", StatusType.Info);
                    
                    var result = await Task.Run(() =>
                        _codeGenerator.GenerateClientCodeFromFeatures(
                            features,
                            serverOutputDir,
                            customNamespace,
                            message => Application.Current.Dispatcher.Invoke(() => UpdateStatus(message, StatusType.Info))
                        ));

                    if (result.Success)
                    {
                        totalGenerated += result.GeneratedFiles.Count;
                    }
                    else
                    {
                        MessageBox.Show($"生成服务器 {serverFolderName} 的代码失败：\n{result.Message}", 
                            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                UpdateStatus($"完成！已生成 {totalGenerated} 个文件", StatusType.Success);
                
                var dialogResult = MessageBox.Show(
                    $"客户端代码生成完成！\n\n生成代码文件: {totalGenerated}\n命名空间: {customNamespace}\n\n是否打开输出文件夹？",
                    "生成成功",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (dialogResult == MessageBoxResult.Yes)
                {
                    OpenDirectory(OutputDirectory);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"生成代码时发生错误：\n\n{ex.Message}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("生成失败", StatusType.Error);
            }
        }

        partial void OnSelectedItemChanged(object? value)
        {
            if (value is ServerInfoViewModel server)
            {
                DetailTitle = $"服务器: {server.ServerName}";
            }
            else if (value is FeatureInfoViewModel feature)
            {
                DetailTitle = $"特性: {feature.DisplayName ?? feature.Identifier}";
            }
            else
            {
                DetailTitle = "请选择服务器或特性";
            }
        }

        public ServerData? GetServerData(Guid uuid)
        {
            return _discoveryService.GetServerData(uuid);
        }

        public ServerInteractionService GetInteractionService()
        {
            return _interactionService;
        }

        private void OpenDirectory(string path)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件夹:\n{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateStatus(string message, StatusType type)
        {
            DiscoveryStatus = message;
            DiscoveryStatusColor = type switch
            {
                StatusType.Success => "#27ae60",
                StatusType.Warning => "#f39c12",
                StatusType.Error => "#e74c3c",
                _ => "#7f8c8d"
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

