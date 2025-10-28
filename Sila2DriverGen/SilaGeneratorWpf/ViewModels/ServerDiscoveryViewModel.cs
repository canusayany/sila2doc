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
        private readonly Sila2RealTimeDiscoveryService _realTimeDiscoveryService;
        private readonly ServerInteractionService _interactionService;
        private readonly ClientCodeGenerator _codeGenerator;
        private readonly ILogger _logger;

        [ObservableProperty]
        private ObservableCollection<ServerInfoViewModel> _servers = new();

        [ObservableProperty]
        private string _discoveryStatus = "实时监控已启动";

        [ObservableProperty]
        private string _discoveryStatusColor = "#27ae60";

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
            _realTimeDiscoveryService = new Sila2RealTimeDiscoveryService();
            _interactionService = new ServerInteractionService();
            _codeGenerator = new ClientCodeGenerator();
            _logger = LoggerService.GetLogger<ServerDiscoveryViewModel>();
            
            InitializeOutputDirectory();

            // 订阅实时监控事件
            _realTimeDiscoveryService.ServerOnline += OnServerOnline;
            _realTimeDiscoveryService.ServerOffline += OnServerOffline;
            _realTimeDiscoveryService.ServerUpdated += OnServerUpdated;

            // 启动实时监控
            try
            {
                _realTimeDiscoveryService.StartRealTimeMonitoring();
                _logger.LogInformation("ServerDiscoveryViewModel: 已启动SiLA2服务器实时监控");
                UpdateStatus("实时监控已启动，等待服务器连接...", StatusType.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动实时监控失败");
                UpdateStatus("启动实时监控失败", StatusType.Error);
            }
        }

        private void InitializeOutputDirectory()
        {
            var tempPath = Path.GetTempPath();
            OutputDirectory = Path.Combine(tempPath, "SilaDiscoveredServers", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        }

        /// <summary>
        /// 服务器上线事件处理
        /// </summary>
        private void OnServerOnline(ServerInfoViewModel server)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 检查是否已存在
                var existing = Servers.FirstOrDefault(s => s.Uuid == server.Uuid);
                if (existing == null)
                {
                    Servers.Add(server);
                    
                    _logger.LogInformation($"服务器上线: {server.ServerName}");
                    UpdateStatus($"服务器上线: {server.ServerName} ({Servers.Count} 个在线)", StatusType.Success);
                }
            });
        }

        /// <summary>
        /// 服务器下线事件处理
        /// </summary>
        private void OnServerOffline(ServerInfoViewModel server)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var existing = Servers.FirstOrDefault(s => s.Uuid == server.Uuid);
                if (existing != null)
                {
                    Servers.Remove(existing);
                    
                    _logger.LogInformation($"服务器下线: {server.ServerName}");
                    UpdateStatus($"服务器下线: {server.ServerName} ({Servers.Count} 个在线)", StatusType.Warning);
                }
            });
        }

        /// <summary>
        /// 服务器更新事件处理
        /// </summary>
        private void OnServerUpdated(ServerInfoViewModel server)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var existing = Servers.FirstOrDefault(s => s.Uuid == server.Uuid);
                if (existing != null)
                {
                    // 更新服务器信息
                    existing.ServerName = server.ServerName;
                    existing.IPAddress = server.IPAddress;
                    existing.Port = server.Port;
                    existing.ServerType = server.ServerType;
                    existing.Description = server.Description;
                    existing.LastSeen = server.LastSeen;
                    
                    _logger.LogInformation($"服务器更新: {server.ServerName}");
                }
            });
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

        // 已移除 SaveFeatures 和 DirectGenerateClients 命令（功能已移至D3驱动生成Tab）

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

        /// <summary>
        /// 获取 ServerData（已弃用，建议直接使用 ServerInfoViewModel.ServerDataCache）
        /// </summary>
        [Obsolete("建议直接使用 ServerInfoViewModel.ServerDataCache 属性")]
        public ServerData? GetServerData(Guid uuid)
        {
#pragma warning disable CS0618 // 类型或成员已过时
            return _discoveryService.GetServerData(uuid);
#pragma warning restore CS0618 // 类型或成员已过时
        }
        
        /// <summary>
        /// 根据 ServerInfoViewModel 获取 ServerData
        /// </summary>
        public ServerData? GetServerData(ServerInfoViewModel server)
        {
            return _discoveryService.GetServerData(server);
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

