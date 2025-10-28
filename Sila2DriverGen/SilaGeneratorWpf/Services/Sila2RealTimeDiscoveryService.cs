extern alias SilaClientCore;
using BR.PC.Device.Sila2Discovery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SilaGeneratorWpf.Models;
using System;
using System.Collections.Generic;
using SilaClientCore::Tecan.Sila2.Client;
using Tecan.Sila2;
using Tecan.Sila2.Discovery;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// SiLA2 实时发现服务（基于Sila2Discovery）
    /// </summary>
    public class Sila2RealTimeDiscoveryService : IDisposable
    {
        private readonly ILogger _logger;
        private readonly IServerConnector _connector;
        private bool _isMonitoring = false;
        private bool _disposed = false;

        /// <summary>
        /// 服务器上线事件
        /// </summary>
        public event Action<ServerInfoViewModel>? ServerOnline;

        /// <summary>
        /// 服务器下线事件
        /// </summary>
        public event Action<ServerInfoViewModel>? ServerOffline;

        /// <summary>
        /// 服务器更新事件
        /// </summary>
        public event Action<ServerInfoViewModel>? ServerUpdated;

        /// <summary>
        /// 是否正在监控
        /// </summary>
        public bool IsMonitoring => _isMonitoring;

        public Sila2RealTimeDiscoveryService()
        {
            _logger = LoggerService.GetLogger<Sila2RealTimeDiscoveryService>();
            _connector = new ServerConnector(new DiscoveryExecutionManager());
            
            // 设置NullLogger以减少日志输出
            Sila2Discovery.SetLogger(NullLoggerFactory.Instance.CreateLogger("Sila2Discovery"));
        }

        /// <summary>
        /// 启动实时监控
        /// </summary>
        public void StartRealTimeMonitoring()
        {
            if (_isMonitoring)
            {
                _logger.LogWarning("实时监控已经在运行中");
                return;
            }

            try
            {
                _logger.LogInformation("启动SiLA2服务器实时监控");

                // 订阅事件
                Sila2Discovery.OnServerOnline += OnServerOnlineHandler;
                Sila2Discovery.OnServerOffline += OnServerOfflineHandler;
                Sila2Discovery.OnServerUpdated += OnServerUpdatedHandler;

                // 启动监控
                Sila2Discovery.StartRealTimeMonitoring();
                
                _isMonitoring = true;
                _logger.LogInformation("实时监控已启动");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动实时监控失败");
                throw;
            }
        }

        /// <summary>
        /// 停止实时监控
        /// </summary>
        public void StopRealTimeMonitoring()
        {
            if (!_isMonitoring)
            {
                return;
            }

            try
            {
                _logger.LogInformation("停止SiLA2服务器实时监控");

                // 取消订阅事件
                Sila2Discovery.OnServerOnline -= OnServerOnlineHandler;
                Sila2Discovery.OnServerOffline -= OnServerOfflineHandler;
                Sila2Discovery.OnServerUpdated -= OnServerUpdatedHandler;

                // 停止监控（如果有停止方法的话）
                // Sila2Discovery.StopRealTimeMonitoring(); // 如果有这个方法

                _isMonitoring = false;
                _logger.LogInformation("实时监控已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止实时监控失败");
            }
        }

        private void OnServerOnlineHandler(Sila2ServerInfo server)
        {
            try
            {
                _logger.LogInformation($"服务器上线: {server.ServerName} ({server.IPAddress}:{server.Port})");
                
                var serverViewModel = ConvertToViewModel(server);
                ServerOnline?.Invoke(serverViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"处理服务器上线事件失败: {server.ServerName}");
            }
        }

        private void OnServerOfflineHandler(Sila2ServerInfo server)
        {
            try
            {
                _logger.LogInformation($"服务器下线: {server.ServerName} ({server.IPAddress}:{server.Port})");
                
                var serverViewModel = ConvertToViewModel(server);
                ServerOffline?.Invoke(serverViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"处理服务器下线事件失败: {server.ServerName}");
            }
        }

        private void OnServerUpdatedHandler(Sila2ServerInfo server)
        {
            try
            {
                _logger.LogInformation($"服务器更新: {server.ServerName} ({server.IPAddress}:{server.Port})");
                
                var serverViewModel = ConvertToViewModel(server);
                ServerUpdated?.Invoke(serverViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"处理服务器更新事件失败: {server.ServerName}");
            }
        }

        /// <summary>
        /// 将Sila2ServerInfo转换为ServerInfoViewModel
        /// </summary>
        private ServerInfoViewModel ConvertToViewModel(Sila2ServerInfo info)
        {
            var serverInfo = new ServerInfoViewModel
            {
                ServerName = info.ServerName,
                IPAddress = info.IPAddress,
                Port = info.Port,
                Uuid = info.Uuid ?? Guid.Empty,
                ServerType = info.ServerType,
                Description = info.Description,
                LastSeen = info.LastSeen
            };

            // 连接服务器并加载特性
            try
            {
                _logger.LogDebug($"连接服务器: {info.ServerName}");
                var serverData = _connector.Connect(info.IPAddress, info.Port, info.Uuid, info.TxtRecords);
                
                if (serverData != null)
                {
                    // 缓存 ServerData 到 ViewModel
                    serverInfo.ServerDataCache = serverData;
                    LoadFeatures(serverInfo, serverData);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"连接服务器失败: {info.ServerName}");
            }

            return serverInfo;
        }

        /// <summary>
        /// 加载服务器特性
        /// </summary>
        private void LoadFeatures(ServerInfoViewModel serverViewModel, ServerData serverData)
        {
            try
            {
                serverViewModel.Features.Clear();

                foreach (var feature in serverData.Features)
                {
                    var featureVm = new FeatureInfoViewModel
                    {
                        Identifier = feature.Identifier,
                        DisplayName = feature.DisplayName,
                        Description = feature.Description,
                        Version = feature.FeatureVersion ?? "1.0",
                        Namespace = feature.Namespace,
                        FeatureXml = FeatureSerializer.SaveToString(feature),
                        ParentServer = serverViewModel
                    };

                    // 加载命令、属性、元数据
                    if (feature.Items != null)
                    {
                        foreach (var item in feature.Items)
                        {
                            if (item is FeatureCommand command)
                            {
                                featureVm.Commands.Add(new CommandInfoViewModel
                                {
                                    Identifier = command.Identifier,
                                    DisplayName = command.DisplayName,
                                    Description = command.Description,
                                    Observable = command.Observable == FeatureCommandObservable.Yes
                                });
                            }
                            else if (item is FeatureProperty property)
                            {
                                featureVm.Properties.Add(new PropertyInfoViewModel
                                {
                                    Identifier = property.Identifier,
                                    DisplayName = property.DisplayName,
                                    Description = property.Description,
                                    Observable = property.Observable == FeaturePropertyObservable.Yes
                                });
                            }
                            else if (item is FeatureMetadata metadata)
                            {
                                featureVm.Metadata.Add(new MetadataInfoViewModel
                                {
                                    Identifier = metadata.Identifier,
                                    DisplayName = metadata.DisplayName,
                                    Description = metadata.Description
                                });
                            }
                        }
                    }

                    serverViewModel.Features.Add(featureVm);
                }

                _logger.LogDebug($"已加载 {serverViewModel.Features.Count} 个特性: {serverViewModel.ServerName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"加载服务器特性失败: {serverViewModel.ServerName}");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            StopRealTimeMonitoring();
            _disposed = true;
        }
    }
}

