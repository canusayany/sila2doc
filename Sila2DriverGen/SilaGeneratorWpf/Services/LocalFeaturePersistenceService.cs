using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SilaGeneratorWpf.Models;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// 本地特性持久化服务
    /// </summary>
    public class LocalFeaturePersistenceService
    {
        private readonly ILogger _logger;
        private readonly string _persistencePath;

        /// <summary>
        /// 持久化数据模型（用于JSON序列化）
        /// </summary>
        private class PersistenceData
        {
            public List<NodeData> Nodes { get; set; } = new();
        }

        private class NodeData
        {
            public string NodeName { get; set; } = string.Empty;
            public string NodePath { get; set; } = string.Empty;
            public List<FileData> Files { get; set; } = new();
        }

        private class FileData
        {
            public string FileName { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;
            public string Identifier { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
        }

        public LocalFeaturePersistenceService()
        {
            _logger = LoggerService.GetLogger<LocalFeaturePersistenceService>();
            _persistencePath = GetPersistencePath();
        }

        /// <summary>
        /// 获取持久化文件路径
        /// </summary>
        private string GetPersistencePath()
        {
            try
            {
                // 优先使用CommonApplicationData（通常是C:\ProgramData）
                var commonDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var targetDir = Path.Combine(commonDataPath, "Sila2D3Gen");
                
                // 测试是否有写入权限
                Directory.CreateDirectory(targetDir);
                var testFile = Path.Combine(targetDir, ".test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                
                var path = Path.Combine(targetDir, "local_features.json");
                _logger.LogInformation($"使用持久化路径: {path}");
                return path;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "无法使用CommonApplicationData，使用程序目录作为备用");
                
                // Fallback: 使用程序目录/Data/
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var dataDir = Path.Combine(appDir, "Data");
                Directory.CreateDirectory(dataDir);
                
                var path = Path.Combine(dataDir, "local_features.json");
                _logger.LogInformation($"使用备用持久化路径: {path}");
                return path;
            }
        }

        /// <summary>
        /// 保存本地特性节点
        /// </summary>
        public bool Save(ObservableCollection<LocalFeatureNodeViewModel> nodes)
        {
            try
            {
                var data = new PersistenceData
                {
                    Nodes = nodes.Select(n => new NodeData
                    {
                        NodeName = n.NodeName,
                        NodePath = n.NodePath,
                        Files = n.Files.Select(f => new FileData
                        {
                            FileName = f.FileName,
                            FilePath = f.FilePath,
                            Identifier = f.Identifier,
                            DisplayName = f.DisplayName
                        }).ToList()
                    }).ToList()
                };

                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                
                // 确保目录存在
                var directory = Path.GetDirectoryName(_persistencePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                File.WriteAllText(_persistencePath, json);
                
                _logger.LogInformation($"成功保存 {nodes.Count} 个本地特性节点到 {_persistencePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存本地特性节点失败");
                return false;
            }
        }

        /// <summary>
        /// 加载本地特性节点
        /// </summary>
        public ObservableCollection<LocalFeatureNodeViewModel> Load()
        {
            var result = new ObservableCollection<LocalFeatureNodeViewModel>();

            try
            {
                if (!File.Exists(_persistencePath))
                {
                    _logger.LogInformation("持久化文件不存在，返回空集合");
                    return result;
                }

                var json = File.ReadAllText(_persistencePath);
                var data = JsonConvert.DeserializeObject<PersistenceData>(json);

                if (data == null || data.Nodes == null)
                {
                    _logger.LogWarning("持久化数据为空");
                    return result;
                }

                foreach (var nodeData in data.Nodes)
                {
                    var node = new LocalFeatureNodeViewModel
                    {
                        NodeName = nodeData.NodeName,
                        NodePath = nodeData.NodePath
                    };

                    foreach (var fileData in nodeData.Files)
                    {
                        // 验证文件是否仍然存在
                        if (File.Exists(fileData.FilePath))
                        {
                            var file = new LocalFeatureFileViewModel
                            {
                                FileName = fileData.FileName,
                                FilePath = fileData.FilePath,
                                Identifier = fileData.Identifier,
                                DisplayName = fileData.DisplayName,
                                ParentNode = node
                            };
                            node.Files.Add(file);
                        }
                        else
                        {
                            _logger.LogWarning($"文件不存在，跳过: {fileData.FilePath}");
                        }
                    }

                    // 只添加包含有效文件的节点
                    if (node.Files.Count > 0)
                    {
                        result.Add(node);
                    }
                    else
                    {
                        _logger.LogWarning($"节点 {nodeData.NodeName} 无有效文件，跳过");
                    }
                }

                _logger.LogInformation($"成功加载 {result.Count} 个本地特性节点");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载本地特性节点失败");
            }

            return result;
        }

        /// <summary>
        /// 清除所有持久化数据
        /// </summary>
        public bool Clear()
        {
            try
            {
                if (File.Exists(_persistencePath))
                {
                    File.Delete(_persistencePath);
                    _logger.LogInformation("已清除持久化数据");
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清除持久化数据失败");
                return false;
            }
        }
    }
}


