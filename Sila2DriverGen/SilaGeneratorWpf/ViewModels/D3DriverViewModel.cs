using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SilaGeneratorWpf.Models;
using SilaGeneratorWpf.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SilaGeneratorWpf.ViewModels
{
    /// <summary>
    /// D3驱动生成功能的ViewModel（重构版：支持侧边栏、在线服务器和本地特性）
    /// </summary>
    public partial class D3DriverViewModel : ObservableObject
    {
        private readonly ILogger _logger;
        private readonly ServerDiscoveryService _discoveryService;
        private readonly ClientCodeGenerator _codeGenerator;
        private readonly LocalFeaturePersistenceService _persistenceService;
        private D3DriverGeneratorService? _generatorService;
        private List<ClientFeatureInfo> _detectedFeatures = new();

        #region 侧边栏管理

        [ObservableProperty]
        private bool _isSidebarVisible = true;

        /// <summary>
        /// 侧边栏宽度（GridLength类型）
        /// </summary>
        public GridLength SidebarWidthValue => IsSidebarVisible ? new GridLength(300) : new GridLength(0);

        [ObservableProperty]
        private ObservableCollection<ServerInfoViewModel> _onlineServers = new();

        [ObservableProperty]
        private ObservableCollection<LocalFeatureNodeViewModel> _localNodes = new();

        partial void OnIsSidebarVisibleChanged(bool value)
        {
            OnPropertyChanged(nameof(SidebarWidthValue));
        }

        #endregion

        #region 生成状态

        [ObservableProperty]
        private string _currentProjectPath = string.Empty;

        [ObservableProperty]
        private string _currentDllPath = string.Empty;

        [ObservableProperty]
        private bool _isGenerating = false;

        [ObservableProperty]
        private bool _canAdjustMethods = false;

        [ObservableProperty]
        private bool _canCompile = false;

        /// <summary>
        /// 项目信息文本
        /// </summary>
        [ObservableProperty]
        private string _projectInfoText = "尚未生成项目";

        /// <summary>
        /// 当前分析结果（用于调整方法特性）
        /// </summary>
        private ClientAnalysisResult? _currentAnalysisResult;

        /// <summary>
        /// 当前生成配置（用于重新生成）
        /// </summary>
        private D3DriverGenerationConfig? _currentConfig;

        #endregion

        #region UI绑定

        [ObservableProperty]
        private ObservableCollection<MethodPreviewData> _methodPreviewData = new();

        [ObservableProperty]
        private string _statusText = "就绪";

        [ObservableProperty]
        private string _statusColor = "#27ae60";

        /// <summary>
        /// 过程日志
        /// </summary>
        [ObservableProperty]
        private string _processLog = string.Empty;

        /// <summary>
        /// 过程日志颜色（用于错误高亮）
        /// </summary>
        [ObservableProperty]
        private System.Windows.Media.Brush _processLogColor = System.Windows.Media.Brushes.Black;

        private System.Text.StringBuilder _processLogBuilder = new();

        #endregion

        public D3DriverViewModel()
        {
            _logger = LoggerService.GetLogger<D3DriverViewModel>();
            _discoveryService = new ServerDiscoveryService();
            _codeGenerator = new ClientCodeGenerator();
            _persistenceService = new LocalFeaturePersistenceService();

            _logger.LogInformation("D3驱动生成 ViewModel 初始化（重构版）");

            // 加载持久化的本地特性
            LoadLocalFeatures();
        }

        #region 侧边栏命令

        /// <summary>
        /// 切换侧边栏显示/隐藏
        /// </summary>
        [RelayCommand]
        private void ToggleSidebar()
        {
            IsSidebarVisible = !IsSidebarVisible;
        }

        /// <summary>
        /// 扫描在线服务器
        /// </summary>
        [RelayCommand]
        private async Task ScanServersAsync()
        {
            _logger.LogInformation("开始扫描在线服务器");
            UpdateStatus("正在扫描服务器...", StatusType.Info);

            try
            {
                var servers = await _discoveryService.ScanServersAsync(TimeSpan.FromSeconds(3));

                OnlineServers.Clear();
                foreach (var server in servers)
                {
                    // 设置特性选择变化的回调
                    server.OnFeatureSelectionChanged = OnFeatureSelectionChanged;
                    OnlineServers.Add(server);
                }

                _logger.LogInformation($"扫描完成，发现 {servers.Count} 个服务器");
                UpdateStatus($"发现 {servers.Count} 个服务器", StatusType.Success);

                if (servers.Count == 0)
                {
                    MessageBox.Show("未发现任何SiLA2服务器\n\n请确保：\n1. 服务器正在运行\n2. 网络连接正常\n3. mDNS服务已启用",
                        "扫描结果", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描服务器失败");
                UpdateStatus("扫描失败", StatusType.Error);
                MessageBox.Show($"扫描失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 添加本地特性文件
        /// </summary>
        [RelayCommand]
        private void AddLocalFeatures()
        {
            _logger.LogInformation("用户点击添加本地特性");

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择SiLA2特性文件（.sila.xml）",
                Filter = "SiLA2特性文件|*.sila.xml|所有文件|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var files = dialog.FileNames;
                    if (!files.Any())
                        return;

                    // 按父文件夹分组
                    var groupedFiles = files.GroupBy(f => Path.GetDirectoryName(f));

                    foreach (var group in groupedFiles)
                    {
                        var parentPath = group.Key ?? string.Empty;
                        var nodeName = Path.GetFileName(parentPath);

                        // 检查节点是否已存在
                        var existingNode = LocalNodes.FirstOrDefault(n => n.NodePath == parentPath);
                        if (existingNode == null)
                        {
                            existingNode = new LocalFeatureNodeViewModel
                            {
                                NodeName = nodeName,
                                NodePath = parentPath
                            };
                            LocalNodes.Add(existingNode);
                        }

                        // 添加文件到节点
                        foreach (var filePath in group)
                        {
                            var fileName = Path.GetFileName(filePath);

                            // 避免重复添加
                            if (existingNode.Files.Any(f => f.FilePath == filePath))
                                continue;

                            // TODO: 解析XML文件获取Identifier和DisplayName
                            var file = new LocalFeatureFileViewModel
                            {
                                FileName = fileName,
                                FilePath = filePath,
                                Identifier = Path.GetFileNameWithoutExtension(fileName),
                                DisplayName = Path.GetFileNameWithoutExtension(fileName),
                                ParentNode = existingNode
                            };
                            existingNode.Files.Add(file);
                        }
                    }

                    // 持久化
                    _persistenceService.Save(LocalNodes);

                    UpdateStatus($"成功添加 {files.Length} 个特性文件", StatusType.Success);
                    _logger.LogInformation($"添加了 {files.Length} 个本地特性文件");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "添加本地特性失败");
                    MessageBox.Show($"添加失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 删除本地节点
        /// </summary>
        [RelayCommand]
        private void DeleteLocalNode()
        {
            // 查找选中的节点
            var selectedNode = LocalNodes.FirstOrDefault(n => n.IsSelected);
            if (selectedNode == null)
            {
                MessageBox.Show("请先选择要删除的节点", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"确定要删除节点 \"{selectedNode.NodeName}\" 及其所有文件吗？",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                LocalNodes.Remove(selectedNode);
                _persistenceService.Save(LocalNodes);

                UpdateStatus($"已删除节点: {selectedNode.NodeName}", StatusType.Success);
                _logger.LogInformation($"删除了本地节点: {selectedNode.NodeName}");
            }
        }

        /// <summary>
        /// 加载持久化的本地特性
        /// </summary>
        private void LoadLocalFeatures()
        {
            try
            {
                var nodes = _persistenceService.Load();
                foreach (var node in nodes)
                {
                    LocalNodes.Add(node);
                }

                if (nodes.Count > 0)
                {
                    _logger.LogInformation($"加载了 {nodes.Count} 个本地特性节点");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载本地特性失败");
            }
        }

        #endregion

        #region 生成D3驱动

        /// <summary>
        /// 生成D3项目（不编译）- 主入口函数
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExecuteGenerate))]
        private async Task GenerateD3ProjectAsync()
        {
            ClearProcessLog();
            AppendProcessLog("========== 开始生成D3驱动 ==========");
            _logger.LogInformation("开始生成D3驱动");

            IsGenerating = true;

            try
            {
                // 步骤1：验证特性选择
                var validationResult = ValidateAndGetSelection();
                if (!validationResult.IsValid)
                {
                    AppendProcessLog($"❌ 验证失败: {validationResult.ErrorMessage}", isError: true);
                    MessageBox.Show(validationResult.ErrorMessage, "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 步骤2：获取设备信息
                var deviceInfo = GetDeviceInfoFromUser(validationResult.IsOnline, validationResult.SelectedFeatures);
                if (deviceInfo == null)
                {
                    AppendProcessLog("用户取消了设备信息输入");
                    return;
                }

                // 步骤3：生成项目基本信息（命名空间、输出路径）
                var projectInfo = GenerateProjectInfo(deviceInfo);
                CurrentProjectPath = projectInfo.OutputPath;

                // 步骤4：生成客户端代码
                var clientCodeResult = await GenerateClientCodeAsync(
                    validationResult.IsOnline,
                    validationResult.SelectedFeatures,
                    projectInfo);

                if (!clientCodeResult.Success)
                {
                    AppendProcessLog($"❌ 客户端代码生成失败: {clientCodeResult.ErrorMessage}", isError: true);
                    UpdateStatus("客户端代码生成失败", StatusType.Error);
                    return;
                }

                // 步骤5：分析客户端代码并获取方法信息
                var analysisResult = AnalyzeClientCode(clientCodeResult.ClientCodePath);
                if (analysisResult == null)
                {
                    return;
                }

                // 步骤6：显示方法预览窗口，让用户调整方法特性
                if (!ShowMethodPreviewAndSync(analysisResult))
                {
                    AppendProcessLog("用户取消了生成");
                    return;
                }

                // 步骤7：生成D3驱动代码
                await GenerateD3DriverCodeAsync(
                    deviceInfo,
                    projectInfo,
                    clientCodeResult,
                    analysisResult,
                    validationResult);

                // 步骤8：完成提示
                ShowCompletionMessage(projectInfo.OutputPath);
            }
            catch (Exception ex)
            {
                HandleGenerationError(ex);
            }
            finally
            {
                IsGenerating = false;
            }
        }

        /// <summary>
        /// 验证选择并获取选择结果
        /// </summary>
        private ValidationResult ValidateAndGetSelection()
        {
            AppendProcessLog("▶ 验证特性选择...");
            var (isValid, errorMessage, isOnline, selectedFeatures) = ValidateSelection();
            return new ValidationResult
            {
                IsValid = isValid,
                ErrorMessage = errorMessage,
                IsOnline = isOnline,
                SelectedFeatures = selectedFeatures
            };
        }

        /// <summary>
        /// 从用户获取设备信息
        /// </summary>
        private DeviceInfo? GetDeviceInfoFromUser(bool isOnline, List<FeatureInfoViewModel>? selectedFeatures)
        {
            AppendProcessLog("▶ 等待用户输入设备信息...");

            var deviceInfoDialog = new Views.DeviceInfoDialog();
            var deviceInfoViewModel = new DeviceInfoDialogViewModel();

            // 如果是在线服务器，尝试自动填充DeviceType
            if (isOnline && selectedFeatures?.FirstOrDefault()?.ParentServer != null)
            {
                var parentServer = selectedFeatures.First().ParentServer;
                deviceInfoViewModel.DeviceType = parentServer?.ServerType ?? "";
            }

            deviceInfoDialog.DataContext = deviceInfoViewModel;
            deviceInfoDialog.Owner = Application.Current.MainWindow;

            if (deviceInfoDialog.ShowDialog() != true)
            {
                return null;
            }

            var deviceInfo = new DeviceInfo
            {
                Brand = deviceInfoViewModel.Brand.Trim(),
                Model = deviceInfoViewModel.Model.Trim(),
                DeviceType = deviceInfoViewModel.DeviceType.Trim(),
                Developer = deviceInfoViewModel.Developer.Trim()
            };

            AppendProcessLog($"✓ 设备信息：{deviceInfo.Brand} {deviceInfo.Model} ({deviceInfo.DeviceType}) - 开发者：{deviceInfo.Developer}");
            return deviceInfo;
        }

        /// <summary>
        /// 生成项目信息（命名空间、输出路径等）
        /// </summary>
        private ProjectInfo GenerateProjectInfo(DeviceInfo deviceInfo)
        {
            AppendProcessLog("▶ 生成项目信息...");

            var namespaceName = $"BR.ECS.DeviceDrivers.{deviceInfo.DeviceType}.{deviceInfo.Brand}_{deviceInfo.Model}";
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outputPath = Path.Combine(
                Path.GetTempPath(),
                "Sila2D3Gen",
                $"{deviceInfo.Brand}_{deviceInfo.Model}_{timestamp}");

            AppendProcessLog($"  命名空间: {namespaceName}");
            AppendProcessLog($"  输出目录: {outputPath}");
            _logger.LogInformation($"命名空间: {namespaceName}, 输出目录: {outputPath}");

            return new ProjectInfo
            {
                Namespace = namespaceName,
                OutputPath = outputPath,
                Timestamp = timestamp
            };
        }

        /// <summary>
        /// 生成客户端代码（统一入口）
        /// 本地特性与服务器特性的不同在于本地特性是直接从本地文件生成，而服务器特性是先从服务器获取特性，然后生成客户端代码.还有一点设备类型在线服务器可以直接获取到,但是从文件生成就无法获取.需要用户手动
        /// 获取特性后的流程两者应该完全一致,所以代码上也要共用
        /// </summary>
        private async Task<ClientCodeGenerationResult> GenerateClientCodeAsync(
            bool isOnline,
            List<FeatureInfoViewModel>? selectedFeatures,
            ProjectInfo projectInfo)
        {
            AppendProcessLog("▶ 生成客户端代码...");
            UpdateStatus("正在生成客户端代码...", StatusType.Info);

            try
            {
                string? clientCodePath;
                string? serverIp = null;
                int? serverPort = null;

                if (isOnline)
                {
                    // 步骤1：从在线服务器获取特性并生成代码
                    AppendProcessLog("  来源：在线服务器");
                    var result = await GenerateClientCodeFromOnlineServerAsync(
                        projectInfo.OutputPath,
                        projectInfo.Namespace,
                        selectedFeatures!);

                    if (result == null)
                    {
                        return new ClientCodeGenerationResult { Success = false, ErrorMessage = "在线服务器代码生成失败" };
                    }

                    clientCodePath = result.Value.ClientCodePath;
                    serverIp = result.Value.ServerIp;
                    serverPort = result.Value.ServerPort;
                }
                else
                {
                    // 步骤1：从本地特性文件获取特性并生成代码
                    AppendProcessLog("  来源：本地特性文件");
                    var localFeatures = LocalNodes
                        .SelectMany(n => n.Files.Where(f => f.IsSelected))
                        .ToList();

                    clientCodePath = await GenerateClientCodeFromLocalFeaturesAsync(
                        projectInfo.OutputPath,
                        projectInfo.Namespace,
                        localFeatures);

                    if (clientCodePath == null)
                    {
                        return new ClientCodeGenerationResult { Success = false, ErrorMessage = "本地特性文件代码生成失败" };
                    }

                    // 本地特性没有服务器信息
                    serverIp = null;
                    serverPort = null;
                }

                // 步骤2：客户端代码生成完成（统一处理）
                AppendProcessLog("✓ 客户端代码生成完成");
                return new ClientCodeGenerationResult
                {
                    Success = true,
                    ClientCodePath = clientCodePath,
                    ServerIp = serverIp,
                    ServerPort = serverPort
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成客户端代码失败");
                return new ClientCodeGenerationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 分析客户端代码
        /// </summary>
        private ClientAnalysisResult? AnalyzeClientCode(string clientCodePath)
        {
            AppendProcessLog("▶ 分析客户端代码...");
            UpdateStatus("正在分析客户端代码...", StatusType.Info);

            try
            {
                var analyzer = new ClientCodeAnalyzer();
                var analysisResult = analyzer.Analyze(clientCodePath);
                _currentAnalysisResult = analysisResult;

                AppendProcessLog($"✓ 检测到 {analysisResult.Features.Count} 个特性");

                // 统计方法数量
                var totalMethods = analysisResult.Features.Sum(f => f.Methods.Count);
                AppendProcessLog($"  共 {totalMethods} 个方法");

                return analysisResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分析客户端代码失败");
                AppendProcessLog($"❌ 分析失败: {ex.Message}", isError: true);
                MessageBox.Show($"分析客户端代码失败：\n{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// 显示方法预览窗口并同步方法分类
        /// </summary>
        private bool ShowMethodPreviewAndSync(ClientAnalysisResult analysisResult)
        {
            AppendProcessLog("▶ 等待用户调整方法特性...");

            // 更新预览数据
            var previewData = analysisResult.GetMethodPreviewData();
            MethodPreviewData = new ObservableCollection<MethodPreviewData>(previewData);

            // 显示方法预览窗口
            var result = ShowMethodPreviewWindow();
            if (!result)
            {
                return false;
            }

            // 同步方法分类信息
            AppendProcessLog("▶ 同步方法分类信息...");
            SyncMethodClassification(analysisResult.Features);
            AppendProcessLog("✓ 方法分类同步完成");

            return true;
        }

        /// <summary>
        /// 生成D3驱动代码
        /// </summary>
        private async Task GenerateD3DriverCodeAsync(
            DeviceInfo deviceInfo,
            ProjectInfo projectInfo,
            ClientCodeGenerationResult clientCodeResult,
            ClientAnalysisResult analysisResult,
            ValidationResult validationResult)
        {
            AppendProcessLog("▶ 生成D3驱动代码...");
            UpdateStatus("正在生成D3驱动代码...", StatusType.Info);

            // 构建配置
            var config = new D3DriverGenerationConfig
            {
                Brand = deviceInfo.Brand,
                Model = deviceInfo.Model,
                DeviceType = deviceInfo.DeviceType,
                Developer = deviceInfo.Developer,
                Namespace = projectInfo.Namespace,
                OutputPath = projectInfo.OutputPath,
                ClientCodePath = clientCodeResult.ClientCodePath,
                Features = analysisResult.Features,
                IsOnlineSource = validationResult.IsOnline,
                ServerUuid = validationResult.IsOnline
                    ? validationResult.SelectedFeatures?.FirstOrDefault()?.ParentServer?.Uuid.ToString()
                    : null,
                LocalFeatureXmlPaths = validationResult.IsOnline
                    ? null
                    : LocalNodes.SelectMany(n => n.Files.Where(f => f.IsSelected)).Select(f => f.FilePath).ToList(),
                ServerIp = clientCodeResult.ServerIp,
                ServerPort = clientCodeResult.ServerPort
            };

            _currentConfig = config;

            // 执行生成
            _generatorService = new D3DriverGeneratorService();
            var result = await Task.Run(() => _generatorService.Generate(
                config,
                message => Application.Current.Dispatcher.Invoke(() => AppendProcessLog($"  {message}"))));

            if (!result.Success)
            {
                AppendProcessLog($"❌ 生成失败: {result.Message}", isError: true);
                UpdateStatus($"生成失败", StatusType.Error);
                _logger.LogError($"D3驱动生成失败: {result.Message}");
                MessageBox.Show($"生成失败！\n\n{result.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new Exception(result.Message);
            }

            AppendProcessLog("✓ D3驱动代码生成完成");
            UpdateStatus("✓ D3项目生成完成", StatusType.Success);
            _logger.LogInformation("D3驱动项目生成成功");

            // 更新状态
            CanAdjustMethods = true;
            CanCompile = true;
            ProjectInfoText = $"项目：{deviceInfo.Brand}_{deviceInfo.Model}\n" +
                              $"路径：{projectInfo.OutputPath}\n" +
                              $"状态：已生成，待编译";
        }

        /// <summary>
        /// 显示完成消息
        /// </summary>
        private void ShowCompletionMessage(string outputPath)
        {
            AppendProcessLog("========== D3驱动生成完成 ==========");

            var dialogResult = MessageBox.Show(
                $"D3驱动项目生成完成！\n\n项目目录: {outputPath}\n\n是否打开项目目录？",
                "生成成功",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (dialogResult == MessageBoxResult.Yes)
            {
                OpenDirectory(outputPath);
            }
        }

        /// <summary>
        /// 处理生成错误
        /// </summary>
        private void HandleGenerationError(Exception ex)
        {
            _logger.LogError(ex, "生成D3驱动过程中发生错误");
            AppendProcessLog($"❌ 发生异常: {ex.Message}", isError: true);
            UpdateStatus("✗ 生成过程中发生错误", StatusType.Error);
            MessageBox.Show($"发生未预期的错误:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// 从在线服务器生成客户端代码（优化版：添加性能统计）
        /// </summary>
        private async Task<(string ClientCodePath, string ServerIp, int ServerPort)?> GenerateClientCodeFromOnlineServerAsync(
            string outputPath,
            string namespaceName,
            List<FeatureInfoViewModel> selectedFeatures)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // 验证输入
                if (!selectedFeatures.Any())
                {
                    AppendProcessLog("  ❌ 没有选中的特性", isError: true);
                    MessageBox.Show("没有选中的特性", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                // 获取父服务器
                var parentServer = selectedFeatures.FirstOrDefault()?.ParentServer;
                if (parentServer == null)
                {
                    AppendProcessLog("  ❌ 无法获取服务器信息", isError: true);
                    MessageBox.Show("无法获取服务器信息", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                AppendProcessLog($"  服务器: {parentServer.ServerName}");
                AppendProcessLog($"  地址: {parentServer.IPAddress}:{parentServer.Port}");
                AppendProcessLog($"  特性数量: {selectedFeatures.Count}");

                // 创建输出目录
                var projectDir = Path.Combine(outputPath, namespaceName);
                var clientCodeDir = Path.Combine(projectDir, "Sila2Client");
                Directory.CreateDirectory(clientCodeDir);
                AppendProcessLog($"  输出目录: {clientCodeDir}");

                // 获取特性数据
                AppendProcessLog("  正在获取特性数据...");
                var fetchStopwatch = System.Diagnostics.Stopwatch.StartNew();

                var serverFeaturesMap = _discoveryService.GetSelectedFeaturesGroupedByServer(
                    new List<ServerInfoViewModel> { parentServer },
                    selectedOnly: true);

                fetchStopwatch.Stop();

                if (!serverFeaturesMap.Any())
                {
                    AppendProcessLog("  ❌ 无法获取特性数据", isError: true);
                    MessageBox.Show("无法获取特性数据", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                var featuresDict = serverFeaturesMap.Values.First();
                AppendProcessLog($"  ✓ 获取到 {featuresDict.Count} 个特性定义（{fetchStopwatch.ElapsedMilliseconds}ms）");

                // 生成客户端代码
                AppendProcessLog("  正在生成客户端代码文件（并行模式）...");
                var codeGenStopwatch = System.Diagnostics.Stopwatch.StartNew();

                var result = await Task.Run(() => _codeGenerator.GenerateClientCodeFromFeatures(
                    featuresDict,
                    clientCodeDir,
                    "Sila2Client",
                    message => Application.Current.Dispatcher.Invoke(() => AppendProcessLog($"    {message}"))));

                codeGenStopwatch.Stop();

                if (!result.Success)
                {
                    AppendProcessLog($"  ❌ 生成失败: {result.Message}", isError: true);
                    MessageBox.Show($"生成客户端代码失败: {result.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                stopwatch.Stop();
                AppendProcessLog($"  ✓ 生成成功，共 {result.GeneratedFiles.Count} 个文件");
                AppendProcessLog($"  ⏱ 总耗时: {stopwatch.ElapsedMilliseconds:N0}ms (获取特性:{fetchStopwatch.ElapsedMilliseconds}ms + 生成:{codeGenStopwatch.ElapsedMilliseconds}ms)");

                return (clientCodeDir, parentServer.IPAddress, parentServer.Port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从在线服务器生成客户端代码失败");
                AppendProcessLog($"  ❌ 异常: {ex.Message}", isError: true);
                MessageBox.Show($"生成客户端代码失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// 从本地特性文件生成客户端代码（优化版：并行验证文件）
        /// </summary>
        private async Task<string?> GenerateClientCodeFromLocalFeaturesAsync(
            string outputPath,
            string namespaceName,
            List<LocalFeatureFileViewModel> localFeatures)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // 验证输入
                if (!localFeatures.Any())
                {
                    AppendProcessLog("  ❌ 没有选中的本地特性文件", isError: true);
                    MessageBox.Show("没有选中的本地特性文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                AppendProcessLog($"  特性文件数量: {localFeatures.Count}");

                // 创建输出目录
                var projectDir = Path.Combine(outputPath, namespaceName);
                var clientCodeDir = Path.Combine(projectDir, "Sila2Client");
                Directory.CreateDirectory(clientCodeDir);
                AppendProcessLog($"  输出目录: {clientCodeDir}");

                // 收集所有特性文件路径
                var featureFilePaths = localFeatures.Select(f => f.FilePath).ToList();

                // 并行验证文件存在性（性能优化）
                AppendProcessLog($"  正在验证 {featureFilePaths.Count} 个文件...");
                var validationStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var validationResults = new System.Collections.Concurrent.ConcurrentBag<(string path, bool exists)>();

                await Task.Run(() =>
                {
                    System.Threading.Tasks.Parallel.ForEach(featureFilePaths, filePath =>
                    {
                        validationResults.Add((filePath, File.Exists(filePath)));
                    });
                });

                validationStopwatch.Stop();

                // 检查验证结果
                var missingFiles = validationResults.Where(r => !r.exists).ToList();
                if (missingFiles.Any())
                {
                    foreach (var (path, _) in missingFiles)
                    {
                        AppendProcessLog($"  ❌ 文件不存在: {path}", isError: true);
                    }
                    MessageBox.Show($"以下文件不存在:\n{string.Join("\n", missingFiles.Select(f => f.path))}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                // 批量记录验证成功的文件（避免频繁UI更新）
                var validFileNames = featureFilePaths.Select(Path.GetFileName).ToList();
                AppendProcessLog($"  ✓ 所有文件验证通过（{validationStopwatch.ElapsedMilliseconds}ms）: {string.Join(", ", validFileNames)}");

                // 从本地XML文件生成客户端代码
                AppendProcessLog("  正在从本地特性文件生成客户端代码（并行模式）...");
                var codeGenStopwatch = System.Diagnostics.Stopwatch.StartNew();

                var result = await Task.Run(() => _codeGenerator.GenerateClientCode(
                    featureFilePaths,
                    clientCodeDir,
                    "Sila2Client",
                    message => Application.Current.Dispatcher.Invoke(() => AppendProcessLog($"    {message}"))));

                codeGenStopwatch.Stop();

                if (!result.Success)
                {
                    AppendProcessLog($"  ❌ 生成失败: {result.Message}", isError: true);
                    MessageBox.Show($"生成客户端代码失败: {result.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                stopwatch.Stop();
                AppendProcessLog($"  ✓ 生成成功，共 {result.GeneratedFiles.Count} 个文件");
                AppendProcessLog($"  ⏱ 总耗时: {stopwatch.ElapsedMilliseconds:N0}ms (验证:{validationStopwatch.ElapsedMilliseconds}ms + 生成:{codeGenStopwatch.ElapsedMilliseconds}ms)");

                return clientCodeDir;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从本地特性文件生成客户端代码失败");
                AppendProcessLog($"  ❌ 异常: {ex.Message}", isError: true);
                MessageBox.Show($"生成客户端代码失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// 验证选择
        /// </summary>
        private (bool isValid, string errorMessage, bool isOnline, List<FeatureInfoViewModel>? selectedFeatures) ValidateSelection()
        {
            // 统计选中的特性
            var selectedOnlineFeatures = OnlineServers
                .SelectMany(s => s.Features.Where(f => f.IsSelected))
                .ToList();

            var selectedLocalFeatures = LocalNodes
                .SelectMany(n => n.Files.Where(f => f.IsSelected))
                .ToList();

            // 规则1：至少选择一个特性
            if (!selectedOnlineFeatures.Any() && !selectedLocalFeatures.Any())
            {
                return (false, "请至少选择一个特性", false, null);
            }

            // 规则2：不能同时选择在线和本地特性
            if (selectedOnlineFeatures.Any() && selectedLocalFeatures.Any())
            {
                return (false, "不能同时选择在线服务器特性和本地特性文件", false, null);
            }

            // 规则3：在线特性必须来自同一个服务器
            if (selectedOnlineFeatures.Any())
            {
                var parentServers = selectedOnlineFeatures.Select(f => f.ParentServer).Distinct().ToList();
                if (parentServers.Count > 1)
                {
                    // 显示详细的设备信息
                    var errorMessage = "只能选择来自同一个服务器的特性！\n\n已选择的特性来自以下设备：\n\n";

                    foreach (var server in parentServers)
                    {
                        if (server == null) continue;

                        var featuresFromThisServer = selectedOnlineFeatures
                            .Where(f => f.ParentServer == server)
                            .ToList();

                        errorMessage += $"设备: {server.ServerName}\n";
                        errorMessage += $"地址: {server.IPAddress}:{server.Port}\n";
                        errorMessage += $"特性数量: {featuresFromThisServer.Count}\n";
                        errorMessage += "特性列表:\n";

                        foreach (var feature in featuresFromThisServer)
                        {
                            errorMessage += $"  • {feature.DisplayName}\n";
                        }

                        errorMessage += "\n";
                    }

                    errorMessage += "请只选择来自同一个服务器的特性。";

                    return (false, errorMessage, false, null);
                }

                return (true, string.Empty, true, selectedOnlineFeatures);
            }

            // 规则4：本地特性必须来自同一个节点（提示）
            if (selectedLocalFeatures.Any())
            {
                var nodes = selectedLocalFeatures.Select(f => f.ParentNode).Distinct().ToList();
                if (nodes.Count > 1)
                {
                    var result = MessageBox.Show(
                        "您选择了来自不同文件夹的特性文件。\n\n这些文件应该属于同一个SiLA2服务器。\n\n是否继续？",
                        "警告",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        return (false, "用户取消", false, null);
                    }
                }

                return (true, string.Empty, false, null);
            }

            return (false, "未知错误", false, null);
        }

        private bool CanExecuteGenerate() => !IsGenerating;

        #endregion

        #region 使用现有项目编译

        /// <summary>
        /// 使用现有项目编译
        /// </summary>
        [RelayCommand]
        private async Task CompileWithExistingProjectAsync()
        {
            ClearProcessLog();
            AppendProcessLog("开始使用现有项目编译...");

            try
            {
                // 检查是否已经生成了代码文件
                if (string.IsNullOrEmpty(CurrentProjectPath) || !Directory.Exists(CurrentProjectPath))
                {
                    AppendProcessLog("❌ 尚未生成D3驱动代码，请先执行'生成并编译'");
                    MessageBox.Show("请先执行\"生成并编译\"生成D3驱动代码", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 让用户选择现有项目文件（.sln或.csproj）
                AppendProcessLog("请选择现有项目文件（.sln或.csproj）...");
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择现有项目文件",
                    Filter = "项目文件 (*.sln;*.csproj)|*.sln;*.csproj|解决方案文件 (*.sln)|*.sln|项目文件 (*.csproj)|*.csproj",
                    InitialDirectory = CurrentProjectPath  // 默认为当前项目目录
                };

                if (dialog.ShowDialog() != true)
                {
                    AppendProcessLog("用户取消了项目选择");
                    return;
                }

                var selectedProjectPath = dialog.FileName;
                AppendProcessLog($"选择的项目: {selectedProjectPath}");

                // 确定目标项目目录
                var targetProjectDir = Path.GetDirectoryName(selectedProjectPath);
                AppendProcessLog($"目标项目目录: {targetProjectDir}");

                // 复制生成的代码文件到目标项目
                AppendProcessLog("复制生成的代码文件到目标项目...");
                var sourceFiles = new[]
                {
                    "AllSila2Client.cs",
                    "D3Driver.cs",
                    "Sila2Base.cs",
                    "CommunicationPars.cs"
                };

                foreach (var file in sourceFiles)
                {
                    var sourcePath = Path.Combine(CurrentProjectPath, file);
                    if (File.Exists(sourcePath))
                    {
                        var targetPath = Path.Combine(targetProjectDir!, file);
                        File.Copy(sourcePath, targetPath, overwrite: true);
                        AppendProcessLog($"✓ 复制文件: {file}");
                    }
                }

                // 复制生成的客户端代码文件
                AppendProcessLog("复制Sila2客户端代码...");

                // 尝试新路径：ProjectName/Sila2Client
                string? sourceClientDir = null;
                var projectDirs = Directory.GetDirectories(CurrentProjectPath);
                foreach (var dir in projectDirs)
                {
                    var sila2ClientPath = Path.Combine(dir, "Sila2Client");
                    if (Directory.Exists(sila2ClientPath))
                    {
                        sourceClientDir = sila2ClientPath;
                        break;
                    }
                }

                // 兼容旧路径：GeneratedClient
                if (sourceClientDir == null)
                {
                    var legacyPath = Path.Combine(CurrentProjectPath, "GeneratedClient");
                    if (Directory.Exists(legacyPath))
                    {
                        sourceClientDir = legacyPath;
                        AppendProcessLog("  使用旧版GeneratedClient路径");
                    }
                }

                if (sourceClientDir != null && Directory.Exists(sourceClientDir))
                {
                    // 目标也应该是Sila2Client（新结构）
                    var targetClientDir = Path.Combine(targetProjectDir!, "Sila2Client");
                    if (!Directory.Exists(targetClientDir))
                    {
                        Directory.CreateDirectory(targetClientDir);
                    }

                    foreach (var file in Directory.GetFiles(sourceClientDir, "*.cs", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(sourceClientDir, file);
                        var targetFile = Path.Combine(targetClientDir, relativePath);
                        var targetSubDir = Path.GetDirectoryName(targetFile);
                        if (!Directory.Exists(targetSubDir))
                        {
                            Directory.CreateDirectory(targetSubDir!);
                        }
                        File.Copy(file, targetFile, overwrite: true);
                        AppendProcessLog($"✓ 复制生成文件: {relativePath}");
                    }
                }

                // 编译项目
                AppendProcessLog("开始编译项目...");
                UpdateStatus("正在编译项目...", StatusType.Info);
                if (_generatorService == null)
                {
                    _generatorService = new D3DriverGeneratorService();
                }

                var compileResult = await Task.Run(() => _generatorService.CompileProject(
                    selectedProjectPath,
                    message => Application.Current.Dispatcher.Invoke(() => AppendProcessLog(message))));

                if (!compileResult.Success)
                {
                    AppendProcessLog($"❌ 编译失败（{compileResult.ErrorCount} 个错误）");
                    AppendProcessLog(compileResult.Message);
                    MessageBox.Show($"编译失败！\n\n{compileResult.Message}", "编译错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                AppendProcessLog("✓ 编译成功");
                CurrentDllPath = compileResult.DllPath;
                UpdateStatus("✓ 使用现有项目编译完成", StatusType.Success);

                // 提示完成
                var dialogResult = MessageBox.Show(
                    $"使用现有项目编译完成！\n\n项目文件: {selectedProjectPath}\nDLL目录: {CurrentDllPath}\n\n是否打开DLL目录？",
                    "编译成功",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (dialogResult == MessageBoxResult.Yes)
                {
                    OpenDirectory(CurrentDllPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "使用现有项目编译失败");
                AppendProcessLog($"❌ 发生异常: {ex.Message}");
                MessageBox.Show($"发生未预期的错误:\n\n{ex.Message}\n\n{ex.StackTrace}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 打开目录

        /// <summary>
        /// 打开项目目录
        /// </summary>
        [RelayCommand]
        private void OpenProjectDirectory()
        {
            if (string.IsNullOrEmpty(CurrentProjectPath) || !Directory.Exists(CurrentProjectPath))
            {
                MessageBox.Show("项目目录不存在，请先生成驱动代码", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenDirectory(CurrentProjectPath);
        }

        /// <summary>
        /// 打开DLL目录
        /// </summary>
        [RelayCommand]
        private void OpenDllDirectory()
        {
            if (string.IsNullOrEmpty(CurrentProjectPath) || !Directory.Exists(CurrentProjectPath))
            {
                MessageBox.Show("项目目录不存在，请先生成并编译驱动代码", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dllPath = Path.Combine(CurrentProjectPath, "bin", "Release");
            if (!Directory.Exists(dllPath))
            {
                MessageBox.Show("DLL目录不存在，请先编译项目", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenDirectory(dllPath);
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

        #endregion

        #region 辅助方法

        /// <summary>
        /// 特性选择变化的回调（用于检查冲突）
        /// </summary>
        private void OnFeatureSelectionChanged(ServerInfoViewModel server, FeatureInfoViewModel feature)
        {
            // 如果是取消选择，无需检查
            if (!feature.IsSelected)
                return;

            try
            {
                // 检查是否有其他服务器的特性已被选中
                var allSelectedFeatures = OnlineServers
                    .SelectMany(s => s.Features)
                    .Where(f => f.IsSelected && f != feature)
                    .ToList();

                if (!allSelectedFeatures.Any())
                    return; // 没有其他选中的特性，允许

                // 检查是否来自不同服务器
                var otherServers = allSelectedFeatures
                    .Select(f => f.ParentServer)
                    .Where(s => s != null && s != server)
                    .Distinct()
                    .ToList();

                if (otherServers.Any())
                {
                    // 有冲突！取消当前选择
                    var errorMessage = $"❌ 不能选择来自不同服务器的特性！\n\n";
                    errorMessage += $"当前尝试选择: {feature.DisplayName}\n";
                    errorMessage += $"来自服务器: {server.ServerName} ({server.IPAddress}:{server.Port})\n\n";
                    errorMessage += "已选择的特性来自:\n";

                    foreach (var otherServer in otherServers)
                    {
                        var featuresFromServer = allSelectedFeatures
                            .Where(f => f.ParentServer == otherServer)
                            .ToList();

                        errorMessage += $"\n服务器: {otherServer?.ServerName} ({otherServer?.IPAddress}:{otherServer?.Port})\n";
                        foreach (var f in featuresFromServer)
                        {
                            errorMessage += $"  • {f.DisplayName}\n";
                        }
                    }

                    errorMessage += "\n请先取消其他服务器的特性选择。";

                    // 记录日志
                    AppendProcessLog(errorMessage);
                    _logger.LogWarning($"特性选择冲突: {feature.DisplayName} 来自 {server.ServerName}");

                    // 取消当前选择
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        feature.SilentSetSelection(false);
                        server.UpdatePartialSelection();
                    });

                    // 可选：显示提示对话框
                    MessageBox.Show(errorMessage, "特性选择冲突", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查特性选择冲突时发生错误");
                AppendProcessLog($"❌ 检查特性选择冲突时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新状态
        /// </summary>
        private void UpdateStatus(string message, StatusType type)
        {
            StatusText = message;
            StatusColor = type switch
            {
                StatusType.Success => "#27ae60",
                StatusType.Warning => "#f39c12",
                StatusType.Error => "#e74c3c",
                _ => "#7f8c8d"
            };
        }

        /// <summary>
        /// 添加过程日志（支持错误高亮）
        /// </summary>
        private void AppendProcessLog(string message, bool isError = false)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var prefix = isError ? "❌" : "ℹ️";
            _processLogBuilder.AppendLine($"[{timestamp}] {prefix} {message}");
            ProcessLog = _processLogBuilder.ToString();

            if (isError)
            {
                ProcessLogColor = System.Windows.Media.Brushes.Red;
            }
        }

        /// <summary>
        /// 清空过程日志
        /// </summary>
        private void ClearProcessLog()
        {
            _processLogBuilder.Clear();
            ProcessLog = string.Empty;
            ProcessLogColor = System.Windows.Media.Brushes.Black;
        }

        /// <summary>
        /// 同步方法分类信息（从 MethodPreviewData 同步到 MethodGenerationInfo）
        /// </summary>
        private void SyncMethodClassification(List<ClientFeatureInfo> features)
        {
            foreach (var previewData in MethodPreviewData)
            {
                var feature = features.FirstOrDefault(f => f.FeatureName == previewData.FeatureName);
                if (feature == null) continue;

                var method = feature.Methods.FirstOrDefault(m => m.Name == previewData.MethodName);
                if (method == null) continue;

                // 同步方法标记
                method.IsIncluded = previewData.IsIncluded;
                method.IsOperations = previewData.IsOperations;
                method.IsMaintenance = previewData.IsMaintenance;

                // 保持向后兼容（已废弃的Category字段）
#pragma warning disable CS0618
                if (previewData.IsMaintenance)
                    method.Category = MethodCategory.Maintenance;
                else if (previewData.IsOperations)
                    method.Category = MethodCategory.Operations;
#pragma warning restore CS0618
            }
        }

        /// <summary>
        /// 显示方法预览窗口
        /// </summary>
        private bool ShowMethodPreviewWindow()
        {
            try
            {
                var viewModel = new MethodPreviewViewModel(MethodPreviewData);
                var window = new Views.MethodPreviewWindow(viewModel)
                {
                    Owner = Application.Current.MainWindow
                };

                var result = window.ShowDialog();
                return result == true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "显示方法预览窗口失败");
                AppendProcessLog($"❌ 显示方法预览窗口失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 编译D3项目

        /// <summary>
        /// 编译D3项目（独立操作）
        /// </summary>
        [RelayCommand]
        private async Task CompileD3ProjectAsync()
        {
            AppendProcessLog("开始编译D3项目...");

            try
            {
                // 验证是否已生成项目
                if (string.IsNullOrEmpty(CurrentProjectPath) || _currentConfig == null)
                {
                    AppendProcessLog("错误：尚未生成D3项目", isError: true);
                    MessageBox.Show("请先点击\"生成D3项目\"按钮生成项目", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 查找.csproj文件（项目名称与命名空间一致）
                var projectFile = Path.Combine(CurrentProjectPath, $"{_currentConfig.Namespace}.csproj");
                if (!File.Exists(projectFile))
                {
                    AppendProcessLog($"错误：找不到项目文件 {projectFile}", isError: true);
                    MessageBox.Show($"找不到项目文件：\n{projectFile}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 执行编译
                AppendProcessLog($"编译项目：{projectFile}");
                UpdateStatus("正在编译项目...", StatusType.Info);

                if (_generatorService == null)
                {
                    _generatorService = new D3DriverGeneratorService();
                }

                var compileResult = await Task.Run(() => _generatorService.CompileProject(
                    projectFile,
                    message => Application.Current.Dispatcher.Invoke(() => AppendProcessLog(message))));

                if (!compileResult.Success)
                {
                    AppendProcessLog($"编译失败（{compileResult.ErrorCount} 个错误）", isError: true);
                    AppendProcessLog(compileResult.Message, isError: true);
                    UpdateStatus("编译失败", StatusType.Error);
                    MessageBox.Show($"编译失败！\n\n{compileResult.Message}", "编译错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                AppendProcessLog("✅ 编译成功");
                CurrentDllPath = compileResult.DllPath;
                UpdateStatus("编译成功", StatusType.Success);
                _logger.LogInformation("D3项目编译成功");

                // 更新项目信息
                ProjectInfoText = $"项目：{_currentConfig.Brand}_{_currentConfig.Model}\n" +
                                  $"路径：{CurrentProjectPath}\n" +
                                  $"DLL：{CurrentDllPath}\n" +
                                  $"状态：已编译";

                // 提示完成
                var dialogResult = MessageBox.Show(
                    $"D3项目编译完成！\n\nDLL目录: {CurrentDllPath}\n\n是否打开DLL目录？",
                    "编译成功",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (dialogResult == MessageBoxResult.Yes)
                {
                    OpenDirectory(CurrentDllPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "编译D3项目失败");
                AppendProcessLog($"发生异常: {ex.Message}", isError: true);
                MessageBox.Show($"发生未预期的错误:\n\n{ex.Message}\n\n{ex.StackTrace}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 调整方法特性

        /// <summary>
        /// 调整方法特性（重新打开方法预览窗口，并重新生成D3Driver.cs）
        /// </summary>
        [RelayCommand]
        private void AdjustMethodAttributes()
        {
            AppendProcessLog("打开方法特性调整窗口...");

            try
            {
                // 验证是否有可调整的方法数据
                if (MethodPreviewData == null || !MethodPreviewData.Any())
                {
                    AppendProcessLog("错误：没有可调整的方法数据", isError: true);
                    MessageBox.Show("没有可调整的方法数据", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 打开方法预览窗口
                var result = ShowMethodPreviewWindow();

                if (!result)
                {
                    AppendProcessLog("用户取消了方法特性调整");
                    return;
                }

                // 同步到分析结果
                if (_currentAnalysisResult != null)
                {
                    SyncMethodClassification(_currentAnalysisResult.Features);
                }

                // 重新生成D3Driver.cs文件
                AppendProcessLog("重新生成D3Driver.cs文件...");
                if (_currentConfig != null && _currentAnalysisResult != null)
                {
                    _currentConfig.Features = _currentAnalysisResult.Features;

                    if (_generatorService == null)
                    {
                        _generatorService = new D3DriverGeneratorService();
                    }

                    var regenerateResult = _generatorService.RegenerateD3Driver(_currentConfig,
                        message => AppendProcessLog(message));

                    if (regenerateResult.Success)
                    {
                        AppendProcessLog("✅ D3Driver.cs文件已更新");
                        MessageBox.Show(
                            "方法特性已调整！D3Driver.cs文件已更新。\n\n请重新编译项目以应用更改。",
                            "调整成功",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        AppendProcessLog($"重新生成失败: {regenerateResult.Message}", isError: true);
                        MessageBox.Show($"重新生成D3Driver.cs失败：\n{regenerateResult.Message}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendProcessLog($"发生异常: {ex.Message}", isError: true);
                MessageBox.Show($"发生未预期的错误:\n\n{ex.Message}\n\n{ex.StackTrace}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 导出选择特性

        /// <summary>
        /// 导出选择的特性到指定文件夹
        /// </summary>
        [RelayCommand]
        private async Task ExportFeaturesAsync()
        {
            try
            {
                // 验证选择
                var (isValid, errorMessage, isOnline, selectedFeatures) = ValidateSelection();
                if (!isValid)
                {
                    MessageBox.Show(errorMessage, "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 选择输出文件夹
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择特性文件导出目录",
                    ShowNewFolderButton = true
                };

                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                var outputDir = dialog.SelectedPath;
                AppendProcessLog($"导出特性到: {outputDir}");

                if (isOnline)
                {
                    // 从在线服务器导出
                    var parentServer = selectedFeatures?.FirstOrDefault()?.ParentServer;
                    if (parentServer == null || selectedFeatures == null) return;

                    foreach (var feature in selectedFeatures)
                    {
                        try
                        {
                            await File.WriteAllTextAsync(Path.Combine(outputDir,$"{feature.DisplayName.Replace(" ","")}-v{feature.Version.Replace(".","_")}.sila.xml"), feature.FeatureXml, System.Text.Encoding.UTF8);

                            AppendProcessLog($"导出特性: {feature.DisplayName}");
                        }
                        catch (Exception ex)
                        {
                            AppendProcessLog($"❌ 导出失败: {feature.DisplayName} - {ex.Message}");
                        }
                    }
                }
                else
                {
                    // 从本地复制特性文件
                    var localFeatures = LocalNodes
                        .SelectMany(n => n.Files.Where(f => f.IsSelected))
                        .ToList();

                    foreach (var file in localFeatures)
                    {
                        try
                        {
                            var destFile = Path.Combine(outputDir, file.FileName);
                            File.Copy(file.FilePath, destFile, overwrite: true);
                            AppendProcessLog($"✓ 复制: {file.FileName}");
                        }
                        catch (Exception ex)
                        {
                            AppendProcessLog($"❌ 复制失败: {file.FileName} - {ex.Message}");
                        }
                    }
                }

                MessageBox.Show($"特性导出完成！\n\n输出目录: {outputDir}", "导出成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出特性失败");
                MessageBox.Show($"导出失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
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

    #region 辅助数据类

    /// <summary>
    /// 验证结果
    /// </summary>
    internal class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public List<FeatureInfoViewModel>? SelectedFeatures { get; set; }
    }

    /// <summary>
    /// 设备信息
    /// </summary>
    internal class DeviceInfo
    {
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string Developer { get; set; } = string.Empty;
    }

    /// <summary>
    /// 项目信息
    /// </summary>
    internal class ProjectInfo
    {
        public string Namespace { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
    }

    /// <summary>
    /// 客户端代码生成结果
    /// </summary>
    internal class ClientCodeGenerationResult
    {
        public bool Success { get; set; }
        public string? ClientCodePath { get; set; }
        public string? ServerIp { get; set; }
        public int? ServerPort { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    #endregion
}
