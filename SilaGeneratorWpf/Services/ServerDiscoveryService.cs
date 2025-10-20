using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tecan.Sila2;
using Tecan.Sila2.Client;
using Tecan.Sila2.Discovery;
using SilaGeneratorWpf.Models;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// SiLA2 服务器发现服务
    /// </summary>
    public class ServerDiscoveryService
    {
        private readonly IServerDiscovery _discovery;
        private readonly IServerConnector _connector;
        private readonly IClientExecutionManager _executionManager;
        private readonly Dictionary<Guid, ServerData> _serverDataCache = new();
        private readonly ILogger _logger;

        public ServerDiscoveryService()
        {
            _executionManager = new DiscoveryExecutionManager();
            _connector = new ServerConnector(_executionManager);
            _discovery = new ServerDiscovery(_connector);
            _logger = LoggerService.GetLogger<ServerDiscoveryService>();
        }

        /// <summary>
        /// 扫描网络中的SiLA2服务器
        /// </summary>
        public Task<List<ServerInfoViewModel>> ScanServersAsync(TimeSpan? timeout = null)
        {
            return Task.Run(() =>
            {
                timeout ??= TimeSpan.FromSeconds(5);
                var servers = new List<ServerInfoViewModel>();

                try
                {
                    _logger.LogInformation($"开始扫描服务器，超时时间: {timeout.Value.TotalSeconds}秒");
                    var discoveredServers = _discovery.GetServers(timeout.Value);
                    _logger.LogInformation($"发现 {discoveredServers.Count()} 个服务器");

                    foreach (var serverData in discoveredServers)
                    {
                        try
                        {
                            // 缓存ServerData以便后续使用
                            _serverDataCache[serverData.Config.Uuid] = serverData;

                            var serverInfo = ConvertToViewModel(serverData);
                            servers.Add(serverInfo);
                            _logger.LogDebug($"成功加载服务器: {serverData.Config.Name} ({serverData.Config.Uuid})");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"处理服务器时出错: {serverData.Config.Name}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "扫描服务器时出错");
                }

                return servers;
            });
        }

        private ServerInfoViewModel ConvertToViewModel(ServerData serverData)
        {
            // 尝试从 Channel 中提取地址信息
            string ipAddress = "mDNS";
            int port = 0;
            
            try
            {
                // 尝试获取 Channel 的地址信息
                if (serverData.Channel != null)
                {
                    var channelType = serverData.Channel.GetType();
                    
                    // 尝试访问 SilaChannel 的 _channel 字段
                    var channelField = channelType.GetField("_channel", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (channelField != null)
                    {
                        var grpcChannel = channelField.GetValue(serverData.Channel);
                        if (grpcChannel != null)
                        {
                            // 获取 Target 属性
                            var targetProperty = grpcChannel.GetType().GetProperty("Target");
                            if (targetProperty != null)
                            {
                                var target = targetProperty.GetValue(grpcChannel)?.ToString();
                                if (!string.IsNullOrEmpty(target))
                                {
                                    // 解析 target (格式可能是 "http://192.168.1.100:50051" 或 "192.168.1.100:50051")
                                    var parts = target.Replace("http://", "").Replace("https://", "").Split(':');
                                    if (parts.Length >= 2)
                                    {
                                        ipAddress = parts[0];
                                        if (int.TryParse(parts[1], out int parsedPort))
                                        {
                                            port = parsedPort;
                                        }
                                    }
                                    else if (parts.Length == 1)
                                    {
                                        ipAddress = parts[0];
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果无法提取地址信息，使用默认值
                Console.WriteLine($"无法从 Channel 提取地址信息: {ex.Message}");
            }

            var serverInfo = new ServerInfoViewModel
            {
                ServerName = serverData.Config.Name,
                IPAddress = ipAddress,
                Port = port,
                Uuid = serverData.Config.Uuid,
                ServerType = serverData.Info.Type ?? "Unknown",
                Description = serverData.Info.Description ?? "",
                LastSeen = DateTime.Now
            };

            // 直接加载特性
            LoadFeatures(serverInfo, serverData);

            return serverInfo;
        }

        /// <summary>
        /// 刷新服务器特性
        /// </summary>
        public Task<bool> LoadServerFeaturesAsync(ServerInfoViewModel server)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (_serverDataCache.TryGetValue(server.Uuid, out var serverData))
                    {
                        LoadFeatures(server, serverData);
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading features for server {server.ServerName}: {ex.Message}");
                    return false;
                }
            });
        }

        private void LoadFeatures(ServerInfoViewModel server, ServerData serverData)
        {
            server.Features.Clear();

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
                    ParentServer = server
                };

                // 加载命令、属性、元数据（从Items数组中）
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

                server.Features.Add(featureVm);
            }
        }

        /// <summary>
        /// 保存特性到文件
        /// </summary>
        public FeatureSaveResult SaveFeatures(
            List<ServerInfoViewModel> servers, 
            string baseOutputDirectory,
            bool selectedOnly = true)
        {
            var result = new FeatureSaveResult { Success = true };
            var savedFiles = new List<string>();

            try
            {
                foreach (var server in servers)
                {
                    if (selectedOnly && !server.IsSelected && !server.Features.Any(f => f.IsSelected))
                        continue;

                    // 为每个服务器创建文件夹
                    var serverFolder = System.IO.Path.Combine(
                        baseOutputDirectory, 
                        SanitizeFolderName($"{server.ServerName}_{server.Uuid}")
                    );

                    if (!System.IO.Directory.Exists(serverFolder))
                    {
                        System.IO.Directory.CreateDirectory(serverFolder);
                    }

                    var featuresToSave = selectedOnly 
                        ? server.Features.Where(f => f.IsSelected).ToList()
                        : server.Features.ToList();

                    foreach (var feature in featuresToSave)
                    {
                        try
                        {
                            var fileName = $"{feature.Identifier}.sila.xml";
                            var filePath = System.IO.Path.Combine(serverFolder, fileName);
                            System.IO.File.WriteAllText(filePath, feature.FeatureXml);
                            savedFiles.Add(filePath);
                        }
                        catch (Exception ex)
                        {
                            result.Warnings.Add($"保存特性 {feature.Identifier} 失败: {ex.Message}");
                        }
                    }
                }

                result.SavedFiles = savedFiles;
                result.Message = $"成功保存 {savedFiles.Count} 个特性文件";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"保存失败: {ex.Message}";
                result.ErrorDetails = ex.ToString();
            }

            return result;
        }

        private string SanitizeFolderName(string name)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// 获取选中的Feature对象（按服务器分组）
        /// </summary>
        public Dictionary<string, Dictionary<string, Feature>> GetSelectedFeaturesGroupedByServer(
            List<ServerInfoViewModel> servers,
            bool selectedOnly = true)
        {
            var result = new Dictionary<string, Dictionary<string, Feature>>();

            foreach (var server in servers)
            {
                if (selectedOnly && !server.IsSelected && !server.Features.Any(f => f.IsSelected))
                    continue;

                var serverKey = SanitizeFolderName($"{server.ServerName}_{server.Uuid}");
                var serverFeatures = new Dictionary<string, Feature>();

                // 从缓存中获取ServerData
                if (_serverDataCache.TryGetValue(server.Uuid, out var serverData))
                {
                    var featuresToInclude = selectedOnly 
                        ? server.Features.Where(f => f.IsSelected).ToList()
                        : server.Features.ToList();

                    foreach (var featureVm in featuresToInclude)
                    {
                        // 从ServerData.Features中找到对应的Feature对象
                        var feature = serverData.Features.FirstOrDefault(f => f.Identifier == featureVm.Identifier);
                        if (feature != null)
                        {
                            serverFeatures[feature.Identifier] = feature;
                        }
                    }
                }

                if (serverFeatures.Any())
                {
                    result[serverKey] = serverFeatures;
                }
            }

            return result;
        }

        /// <summary>
        /// 根据 UUID 获取 ServerData
        /// </summary>
        public ServerData? GetServerData(Guid uuid)
        {
            return _serverDataCache.TryGetValue(uuid, out var serverData) ? serverData : null;
        }
    }

    /// <summary>
    /// 特性保存结果
    /// </summary>
    public class FeatureSaveResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> SavedFiles { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string? ErrorDetails { get; set; }
    }
}

