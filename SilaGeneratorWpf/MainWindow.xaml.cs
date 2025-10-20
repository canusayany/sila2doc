using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SilaGeneratorWpf.Models;
using SilaGeneratorWpf.Services;
using Tecan.Sila2;
using WinForms = System.Windows.Forms;

namespace SilaGeneratorWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<string> _featureFiles = new();
        private readonly ObservableCollection<ServerInfoViewModel> _servers = new();
        private ClientCodeGenerator? _codeGenerator;
        private ServerDiscoveryService? _discoveryService;
        private ServerInteractionService? _interactionService;
        private ILogger? _logger;
        private string _outputDirectory = string.Empty;
        private string _discoveryOutputDirectory = string.Empty;
        private bool _isSidebarVisible = true;
        
        // 用于存储当前选中的服务器和特性
        private ServerInfoViewModel? _currentServerViewModel;
        private ServerData? _currentServerData;
        private Feature? _currentFeature;
        
        // 用于跟踪可观察操作的订阅
        private readonly Dictionary<string, string> _activeSubscriptions = new(); // UI元素ID -> 订阅ID
        private readonly Dictionary<string, Stack<UIElement>> _propertyResponseStacks = new(); // 属性ID -> 响应堆栈（最多5个）

        public MainWindow()
        {
            try
            {
                // 初始化日志系统
                LoggerService.Initialize();
                _logger = LoggerService.GetLogger<MainWindow>();
                _logger.LogInformation("应用程序启动");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"日志初始化失败: {ex.Message}");
                _logger = null;
            }

            // 初始化服务对象
            try
            {
                _codeGenerator = new ClientCodeGenerator();
                _discoveryService = new ServerDiscoveryService();
                _interactionService = new ServerInteractionService();
            }
            catch (Exception ex)
            {
                var msg = $"服务初始化失败: {ex.Message}\n{ex.StackTrace}";
                System.Diagnostics.Debug.WriteLine(msg);
                _logger?.LogError(msg);
                
                // 创建虚拟对象以防止 null 引用
                try
                {
                    _codeGenerator ??= new ClientCodeGenerator();
                    _discoveryService ??= new ServerDiscoveryService();
                    _interactionService ??= new ServerInteractionService();
                }
                catch { }
            }
            
            try
            {
                InitializeComponent();
                FileListBox.ItemsSource = _featureFiles;
                ServerTreeView.ItemsSource = _servers;
                InitializeOutputDirectory();
                InitializeDiscoveryOutputDirectory();
            }
            catch (Exception ex)
            {
                var msg = $"MainWindow 初始化失败: {ex.Message}\n{ex.StackTrace}";
                System.Diagnostics.Debug.WriteLine(msg);
                
                // 写入错误文件
                try
                {
                    var errorFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.txt");
                    File.WriteAllText(errorFile, msg);
                }
                catch { }
                
                // 不重新抛出异常，而是记录并继续
                System.Windows.MessageBox.Show(msg, "初始化错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void InitializeOutputDirectory()
        {
            // 在系统临时目录创建专用文件夹
            var tempPath = Path.GetTempPath();
            _outputDirectory = Path.Combine(tempPath, "SilaGeneratedClients", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            FileGenerationOutputPathTextBox.Text = _outputDirectory;
            
            try
            {
                if (!Directory.Exists(_outputDirectory))
                {
                    Directory.CreateDirectory(_outputDirectory);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建输出目录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeDiscoveryOutputDirectory()
        {
            var tempPath = Path.GetTempPath();
            _discoveryOutputDirectory = Path.Combine(tempPath, "SilaDiscoveredServers", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        }

        #region Drag and Drop

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                
                // 修改边框样式以提示用户
                if (sender is Border border)
                {
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(46, 204, 113)); // Green
                    border.Background = new SolidColorBrush(Color.FromRgb(232, 248, 245));
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            // 恢复原始样式
            if (sender is Border border)
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 152, 219)); // Blue
                border.Background = new SolidColorBrush(Color.FromRgb(236, 240, 241));
            }
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            // 恢复原始样式
            if (sender is Border border)
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                border.Background = new SolidColorBrush(Color.FromRgb(236, 240, 241));
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddFiles(files);
            }
        }

        #endregion

        #region File Management

        private void SelectFiles_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "选择 Feature 文件",
                Filter = "SiLA Feature Files (*.sila.xml)|*.sila.xml|XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                AddFiles(openFileDialog.FileNames);
            }
        }

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
                if (!_featureFiles.Contains(file))
                {
                    _featureFiles.Add(file);
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                UpdateStatus($"已添加 {addedCount} 个文件", MessageType.Success);
                GenerateButton.IsEnabled = _featureFiles.Any();
            }
            else if (files.Length > 0)
            {
                UpdateStatus("未添加新文件（可能已存在或格式不正确）", MessageType.Warning);
            }
        }

        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string filePath)
            {
                _featureFiles.Remove(filePath);
                GenerateButton.IsEnabled = _featureFiles.Any();
                UpdateStatus($"已移除文件", MessageType.Info);
            }
        }

        private void ClearFiles_Click(object sender, RoutedEventArgs e)
        {
            if (_featureFiles.Any())
            {
                var result = MessageBox.Show(
                    $"确定要清空列表中的 {_featureFiles.Count} 个文件吗？",
                    "确认清空",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _featureFiles.Clear();
                    GenerateButton.IsEnabled = false;
                    UpdateStatus("列表已清空", MessageType.Info);
                }
            }
        }

        #endregion

        #region Code Generation (File Mode)

        private async void GenerateClient_Click(object sender, RoutedEventArgs e)
        {
            if (!_featureFiles.Any())
            {
                MessageBox.Show("请先添加 Feature 文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 创建新的输出目录
            InitializeOutputDirectory();

            GenerateButton.IsEnabled = false;
            UpdateStatus("正在生成代码...", MessageType.Info);

            try
            {
                var customNamespace = string.IsNullOrWhiteSpace(NamespaceTextBox.Text) 
                    ? "Sila2Client" 
                    : NamespaceTextBox.Text;

                var result = await System.Threading.Tasks.Task.Run(() =>
                {
                    return _codeGenerator.GenerateClientCode(
                        _featureFiles.ToList(),
                        _outputDirectory,
                        customNamespace,
                        message => Dispatcher.Invoke(() => UpdateStatus(message, MessageType.Info)));
                });

                if (result.Success)
                {
                    UpdateStatus($"✓ {result.Message}", MessageType.Success);

                    var detailMessage = $"生成完成！\n\n" +
                                      $"输出目录: {_outputDirectory}\n" +
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
                        OpenDirectory(_outputDirectory);
                    }
                }
                else
                {
                    UpdateStatus($"✗ 生成失败", MessageType.Error);
                    
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
                UpdateStatus("✗ 生成过程中发生错误", MessageType.Error);
                MessageBox.Show($"发生未预期的错误:\n\n{ex.Message}\n\n{ex.StackTrace}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                GenerateButton.IsEnabled = true;
            }
        }

        #endregion

        #region Folder Operations

        private void OpenOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_outputDirectory) && Directory.Exists(_outputDirectory))
            {
                OpenDirectory(_outputDirectory);
            }
            else
            {
                MessageBox.Show("输出目录不存在，请先生成代码", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OpenDirectory(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo
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

        #endregion

        #region Server Discovery

        private async void ScanServers_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("用户点击扫描服务器");
            UpdateDiscoveryStatus("正在扫描服务器...", StatusType.Info);
            
            try
            {
                var servers = await _discoveryService.ScanServersAsync(TimeSpan.FromSeconds(5));
                
                _servers.Clear();
                foreach (var server in servers)
                {
                    _servers.Add(server);
                }

                _logger.LogInformation($"扫描完成，发现 {servers.Count} 个服务器");
                UpdateDiscoveryStatus($"发现 {servers.Count} 个服务器", StatusType.Success);

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
                UpdateDiscoveryStatus("扫描失败", StatusType.Error);
                MessageBox.Show($"扫描失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedServers = _servers.Where(s => s.IsSelected || s.Features.Any(f => f.IsSelected)).ToList();
            
            if (!selectedServers.Any())
            {
                MessageBox.Show("请先选择要刷新的服务器", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            UpdateDiscoveryStatus("正在加载特性...", StatusType.Info);
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

            UpdateDiscoveryStatus($"成功加载 {successCount}/{selectedServers.Count} 个服务器的特性", StatusType.Success);
        }

        #endregion

        #region Sidebar Toggle

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _isSidebarVisible = !_isSidebarVisible;
            
            if (_isSidebarVisible)
            {
                SidebarColumn.Width = new GridLength(350);
            }
            else
            {
                SidebarColumn.Width = new GridLength(0);
            }
        }

        #endregion

        #region Server Details Display

        private async void ServerTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            DetailPanel.Children.Clear();

            if (e.NewValue is ServerInfoViewModel server)
            {
                _currentServerViewModel = server;
                _currentServerData = _discoveryService.GetServerData(server.Uuid);
                _currentFeature = null;
                ShowServerDetails(server);
            }
            else if (e.NewValue is FeatureInfoViewModel featureViewModel)
            {
                _currentServerViewModel = featureViewModel.ParentServer;
                _currentServerData = _currentServerViewModel != null ? 
                    _discoveryService.GetServerData(_currentServerViewModel.Uuid) : null;
                
                // 加载完整的 Feature 对象
                if (_currentServerViewModel != null && !string.IsNullOrEmpty(featureViewModel.FeatureXml))
                {
                    try
                    {
                        var tempFile = Path.GetTempFileName();
                        await File.WriteAllTextAsync(tempFile, featureViewModel.FeatureXml);
                        _currentFeature = FeatureSerializer.Load(tempFile);
                        File.Delete(tempFile);
                        
                        ShowFeatureDetailsWithInteraction(featureViewModel, _currentFeature);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"加载特性失败: {ex.Message}", "错误", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        ShowFeatureDetails(featureViewModel);
                    }
                }
                else
                {
                    ShowFeatureDetails(featureViewModel);
                }
            }
        }

        private void ShowServerDetails(ServerInfoViewModel server)
        {
            DetailPanel.Children.Add(CreateTitle("服务器信息"));
            DetailPanel.Children.Add(CreateInfoRow("名称", server.ServerName));
            DetailPanel.Children.Add(CreateInfoRow("UUID", server.Uuid.ToString()));
            DetailPanel.Children.Add(CreateInfoRow("IP地址", server.IPAddress));
            DetailPanel.Children.Add(CreateInfoRow("端口", server.Port.ToString()));
            DetailPanel.Children.Add(CreateInfoRow("类型", server.ServerType));
            DetailPanel.Children.Add(CreateInfoRow("描述", server.Description));
            DetailPanel.Children.Add(CreateInfoRow("特性数量", server.Features.Count.ToString()));
            DetailPanel.Children.Add(CreateInfoRow("最后发现", server.LastSeen.ToString("yyyy-MM-dd HH:mm:ss")));

            if (server.Features.Any())
            {
                DetailPanel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
                DetailPanel.Children.Add(CreateTitle("特性列表"));
                
                foreach (var feature in server.Features)
                {
                    var featurePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
                    featurePanel.Children.Add(new TextBlock { Text = "📦", Margin = new Thickness(0, 0, 5, 0) });
                    featurePanel.Children.Add(new TextBlock { Text = feature.DisplayText });
                    DetailPanel.Children.Add(featurePanel);
                }
            }
        }

        private void ShowFeatureDetails(FeatureInfoViewModel feature)
        {
            DetailPanel.Children.Add(CreateTitle($"特性: {feature.DisplayName ?? feature.Identifier}"));
            DetailPanel.Children.Add(CreateInfoRow("标识符", feature.Identifier ?? ""));
            DetailPanel.Children.Add(CreateInfoRow("显示名称", feature.DisplayName ?? ""));
            DetailPanel.Children.Add(CreateInfoRow("版本", feature.Version ?? ""));
            DetailPanel.Children.Add(CreateInfoRow("命名空间", feature.Namespace ?? ""));
            DetailPanel.Children.Add(CreateInfoRow("描述", feature.Description ?? ""));

            if (feature.Commands.Any())
            {
                DetailPanel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
                DetailPanel.Children.Add(CreateTitle($"命令 ({feature.Commands.Count})"));
                
                foreach (var command in feature.Commands)
                {
                    DetailPanel.Children.Add(CreateDetailItem(command.DisplayText, command.Description));
                }
            }

            if (feature.Properties.Any())
            {
                DetailPanel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
                DetailPanel.Children.Add(CreateTitle($"属性 ({feature.Properties.Count})"));
                
                foreach (var property in feature.Properties)
                {
                    DetailPanel.Children.Add(CreateDetailItem(property.DisplayText, property.Description));
                }
            }

            if (feature.Metadata.Any())
            {
                DetailPanel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
                DetailPanel.Children.Add(CreateTitle($"元数据 ({feature.Metadata.Count})"));
                
                foreach (var metadata in feature.Metadata)
                {
                    DetailPanel.Children.Add(CreateDetailItem(metadata.DisplayText, metadata.Description));
                }
            }
        }

        /// <summary>
        /// 显示特性详情（带交互功能）
        /// </summary>
        private void ShowFeatureDetailsWithInteraction(FeatureInfoViewModel featureViewModel, Feature feature)
        {
            DetailTitleText.Text = $"特性: {feature.DisplayName ?? feature.Identifier}";
            
            // 基本信息
            DetailPanel.Children.Add(CreateTitle("基本信息"));
            DetailPanel.Children.Add(CreateInfoRow("标识符", feature.Identifier ?? ""));
            DetailPanel.Children.Add(CreateInfoRow("显示名称", feature.DisplayName ?? ""));
            DetailPanel.Children.Add(CreateInfoRow("版本", feature.FeatureVersion ?? ""));
            DetailPanel.Children.Add(CreateInfoRow("命名空间", $"{feature.Originator}.{feature.Category}"));
            DetailPanel.Children.Add(CreateInfoRow("描述", feature.Description ?? ""));

            // 属性部分
            var properties = feature.Items?.OfType<FeatureProperty>().ToList() ?? new List<FeatureProperty>();
            if (properties.Any())
            {
                DetailPanel.Children.Add(new Separator { Margin = new Thickness(0, 20, 0, 20) });
                DetailPanel.Children.Add(CreateSectionTitle($"属性 ({properties.Count})"));
                
                foreach (var property in properties)
                {
                    DetailPanel.Children.Add(CreatePropertyPanel(property));
                }
            }

            // 命令部分
            var commands = feature.Items?.OfType<FeatureCommand>().ToList() ?? new List<FeatureCommand>();
            if (commands.Any())
            {
                DetailPanel.Children.Add(new Separator { Margin = new Thickness(0, 20, 0, 20) });
                DetailPanel.Children.Add(CreateSectionTitle($"命令 ({commands.Count})"));
                
                foreach (var command in commands)
                {
                    DetailPanel.Children.Add(CreateCommandPanel(command));
                }
            }

            // 元数据部分
            var metadata = feature.Items?.OfType<FeatureMetadata>().ToList() ?? new List<FeatureMetadata>();
            if (metadata.Any())
            {
                DetailPanel.Children.Add(new Separator { Margin = new Thickness(0, 20, 0, 20) });
                DetailPanel.Children.Add(CreateSectionTitle($"元数据 ({metadata.Count})"));
                
                foreach (var meta in metadata)
                {
                    DetailPanel.Children.Add(CreateMetadataPanel(meta));
                }
            }
        }

        private TextBlock CreateTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            };
        }

        private Grid CreateInfoRow(string label, string value)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelBlock = new TextBlock
            {
                Text = label + ":",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141))
            };
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            var valueBlock = new TextBlock
            {
                Text = value,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            };
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);

            return grid;
        }

        private StackPanel CreateDetailItem(string title, string description)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
            
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            });

            if (!string.IsNullOrWhiteSpace(description))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = description,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 0, 0, 0)
                });
            }

            return panel;
        }

        private TextBlock CreateSectionTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 128)) // Navy
            };
        }

        /// <summary>
        /// 创建属性交互面板
        /// </summary>
        private Border CreatePropertyPanel(FeatureProperty property)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(32, 178, 170)), // Light Sea Green
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 128, 128)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(15)
            };

            var stackPanel = new StackPanel();

            // 标题行
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = property.DisplayName ?? property.Identifier,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameText, 0);
            headerGrid.Children.Add(nameText);

            var observableText = new TextBlock
            {
                Text = property.Observable == FeaturePropertyObservable.Yes ? "✓ Observable" : "✗ Not Observable",
                FontSize = 14,
                Foreground = Brushes.White,
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(observableText, 1);
            headerGrid.Children.Add(observableText);

            var getButton = new Button
            {
                Name = $"GetBtn_{property.Identifier}",
                Content = "Get",
                Width = 80,
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(0, 128, 128)), // Teal
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(5, 0, 0, 0),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            getButton.Click += async (s, e) => await OnGetProperty(property, stackPanel, getButton);
            Grid.SetColumn(getButton, 2);
            headerGrid.Children.Add(getButton);

            // 如果是可观察属性，添加订阅和停止按钮
            if (property.Observable == FeaturePropertyObservable.Yes)
            {
                var subscribeButton = new Button
                {
                    Name = $"SubBtn_{property.Identifier}",
                    Content = "订阅",
                    Width = 80,
                    Height = 32,
                    Background = new SolidColorBrush(Color.FromRgb(0, 150, 136)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(5, 0, 0, 0),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Padding = new Thickness(8, 4, 8, 4),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                subscribeButton.Click += async (s, e) => await OnSubscribeProperty(property, stackPanel, subscribeButton);
                Grid.SetColumn(subscribeButton, 3);
                headerGrid.Children.Add(subscribeButton);

                var stopButton = new Button
                {
                    Name = $"StopBtn_{property.Identifier}",
                    Content = "停止",
                    Width = 80,
                    Height = 32,
                    Background = new SolidColorBrush(Color.FromRgb(200, 50, 50)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(5, 0, 0, 0),
                    IsEnabled = false,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Padding = new Thickness(8, 4, 8, 4),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                stopButton.Click += (s, e) => OnStopProperty(property, stopButton, subscribeButton);
                Grid.SetColumn(stopButton, 4);
                headerGrid.Children.Add(stopButton);
            }

            stackPanel.Children.Add(headerGrid);

            // 描述
            if (!string.IsNullOrEmpty(property.Description))
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = property.Description,
                    Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                    Margin = new Thickness(0, 5, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            // 类型信息
            var typeInfo = GetDataTypeDescription(property.DataType);
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"类型: {typeInfo}",
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                FontSize = 12,
                Margin = new Thickness(0, 5, 0, 0)
            });

            // 响应容器（用 StackPanel 容纳多个响应）
            var responseStack = new StackPanel
            {
                Name = $"Response_{property.Identifier}",
                Margin = new Thickness(0, 10, 0, 0),
                Visibility = Visibility.Collapsed
            };
            stackPanel.Children.Add(responseStack);

            border.Child = stackPanel;
            return border;
        }

        /// <summary>
        /// 创建命令交互面板
        /// </summary>
        private Border CreateCommandPanel(FeatureCommand command)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(32, 178, 170)), // Light Sea Green
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 128, 128)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(15)
            };

            var stackPanel = new StackPanel();

            // 标题行
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = command.DisplayName ?? command.Identifier,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameText, 0);
            headerGrid.Children.Add(nameText);

            var observableText = new TextBlock
            {
                Text = command.Observable == FeatureCommandObservable.Yes ? "✓ Observable" : "✗ Not Observable",
                FontSize = 14,
                Foreground = Brushes.White,
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(observableText, 1);
            headerGrid.Children.Add(observableText);

            var runButton = new Button
            {
                Name = $"RunBtn_{command.Identifier}",
                Content = "Run",
                Width = 80,
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(0, 128, 128)), // Teal
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(5, 0, 0, 0),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            runButton.Click += async (s, e) => await OnRunCommand(command, stackPanel, runButton);
            Grid.SetColumn(runButton, 2);
            headerGrid.Children.Add(runButton);

            // 如果是可观察命令，添加停止按钮
            if (command.Observable == FeatureCommandObservable.Yes)
            {
                var stopButton = new Button
                {
                    Name = $"StopCmdBtn_{command.Identifier}",
                    Content = "停止",
                    Width = 80,
                    Height = 32,
                    Background = new SolidColorBrush(Color.FromRgb(200, 50, 50)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(5, 0, 0, 0),
                    IsEnabled = false,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Padding = new Thickness(8, 4, 8, 4),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                stopButton.Click += (s, e) => OnStopCommand(command, stopButton, runButton);
                Grid.SetColumn(stopButton, 3);
                headerGrid.Children.Add(stopButton);
            }

            stackPanel.Children.Add(headerGrid);

            // 描述
            if (!string.IsNullOrEmpty(command.Description))
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = command.Description,
                    Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                    Margin = new Thickness(0, 5, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            // 参数输入区域
            if (command.Parameter != null && command.Parameter.Any())
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = "参数:",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 10, 0, 5)
                });

                foreach (var param in command.Parameter)
                {
                    stackPanel.Children.Add(CreateParameterInput(param));
                }
            }

            // 响应容器（用 StackPanel 容纳多个响应）
            var responseStack = new StackPanel
            {
                Name = $"Response_{command.Identifier}",
                Margin = new Thickness(0, 10, 0, 0),
                Visibility = Visibility.Collapsed
            };
            stackPanel.Children.Add(responseStack);

            border.Child = stackPanel;
            return border;
        }

        /// <summary>
        /// 创建元数据面板
        /// </summary>
        private Border CreateMetadataPanel(FeatureMetadata metadata)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(32, 178, 170)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 128, 128)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(15)
            };

            var stackPanel = new StackPanel();

            stackPanel.Children.Add(new TextBlock
            {
                Text = metadata.DisplayName ?? metadata.Identifier,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });

            if (!string.IsNullOrEmpty(metadata.Description))
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = metadata.Description,
                    Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                    Margin = new Thickness(0, 5, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            var typeInfo = GetDataTypeDescription(metadata.DataType);
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"类型: {typeInfo}",
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                FontSize = 12,
                Margin = new Thickness(0, 5, 0, 0)
            });

            border.Child = stackPanel;
            return border;
        }

        /// <summary>
        /// 创建参数输入控件
        /// </summary>
        private StackPanel CreateParameterInput(SiLAElement parameter)
        {
            var mainPanel = new StackPanel
            {
                Margin = new Thickness(10, 5, 0, 10)
            };

            // 参数名和输入框行
            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 参数名
            var namePanel = new StackPanel();
            namePanel.Children.Add(new TextBlock
            {
                Text = parameter.DisplayName ?? parameter.Identifier,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold
            });

            var typeInfo = GetDataTypeDescription(parameter.DataType);
            namePanel.Children.Add(new TextBlock
            {
                Text = $"({typeInfo})",
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                FontSize = 11
            });

            Grid.SetColumn(namePanel, 0);
            inputGrid.Children.Add(namePanel);

            // 输入框
            var input = new TextBox
            {
                Name = $"Param_{parameter.Identifier}",
                Padding = new Thickness(5),
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(input, 1);
            inputGrid.Children.Add(input);

            mainPanel.Children.Add(inputGrid);

            // 描述
            if (!string.IsNullOrEmpty(parameter.Description))
            {
                mainPanel.Children.Add(new TextBlock
                {
                    Text = $"  ▪ {parameter.Description}",
                    Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            // 单位和约束信息
            var constraints = GetConstraintDescription(parameter.DataType);
            if (!string.IsNullOrEmpty(constraints))
            {
                mainPanel.Children.Add(new TextBlock
                {
                    Text = $"  ▪ {constraints}",
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 100)),
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            return mainPanel;
        }

        /// <summary>
        /// 获取约束描述（单位、范围等）
        /// </summary>
        private string GetConstraintDescription(DataTypeType dataType)
        {
            if (dataType?.Item is ConstrainedType constrainedType && constrainedType.Constraints != null)
            {
                var parts = new List<string>();

                // 单位
                if (constrainedType.Constraints.Unit != null)
                {
                    var unit = constrainedType.Constraints.Unit;
                    parts.Add($"单位: {unit.Label}");
                }

                // 范围约束
                if (constrainedType.Constraints.MinimalInclusive != null)
                {
                    parts.Add($"最小值(含): {constrainedType.Constraints.MinimalInclusive}");
                }
                if (constrainedType.Constraints.MinimalExclusive != null)
                {
                    parts.Add($"最小值(不含): {constrainedType.Constraints.MinimalExclusive}");
                }
                if (constrainedType.Constraints.MaximalInclusive != null)
                {
                    parts.Add($"最大值(含): {constrainedType.Constraints.MaximalInclusive}");
                }
                if (constrainedType.Constraints.MaximalExclusive != null)
                {
                    parts.Add($"最大值(不含): {constrainedType.Constraints.MaximalExclusive}");
                }

                // 长度约束
                if (constrainedType.Constraints.MinimalLength != null)
                {
                    parts.Add($"最小长度: {constrainedType.Constraints.MinimalLength}");
                }
                if (constrainedType.Constraints.MaximalLength != null)
                {
                    parts.Add($"最大长度: {constrainedType.Constraints.MaximalLength}");
                }

                // 模式约束
                if (!string.IsNullOrEmpty(constrainedType.Constraints.Pattern))
                {
                    parts.Add($"模式: {constrainedType.Constraints.Pattern}");
                }

                // 集合约束
                if (constrainedType.Constraints.Set != null && constrainedType.Constraints.Set.Any())
                {
                    var values = string.Join(", ", constrainedType.Constraints.Set);
                    parts.Add($"允许值: {values}");
                }

                return string.Join(", ", parts);
            }

            return string.Empty;
        }

        /// <summary>
        /// 获取数据类型描述
        /// </summary>
        private string GetDataTypeDescription(DataTypeType dataType)
        {
            if (dataType?.Item is BasicType basicType)
                return basicType.ToString();
            if (dataType?.Item is ListType)
                return "List";
            if (dataType?.Item is StructureType)
                return "Structure";
            if (dataType?.Item is ConstrainedType constrainedType)
                return GetDataTypeDescription(constrainedType.DataType) + " (Constrained)";
            return "Unknown";
        }

        #endregion

        #region Feature Save and Code Generation (Discovery Mode)

        private void BrowseFileGenerationOutput_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "选择输出目录",
                UseDescriptionForTitle = true,
                SelectedPath = _outputDirectory,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                _outputDirectory = dialog.SelectedPath;
                FileGenerationOutputPathTextBox.Text = _outputDirectory;
            }
        }

        private void BrowseDiscoveryOutput_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "选择输出目录",
                UseDescriptionForTitle = true,
                SelectedPath = _discoveryOutputDirectory,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                _discoveryOutputDirectory = dialog.SelectedPath;
            }
        }

        private void SaveFeatures_Click(object sender, RoutedEventArgs e)
        {
            var selectedServers = _servers.Where(s => s.IsSelected || s.Features.Any(f => f.IsSelected)).ToList();
            
            if (!selectedServers.Any())
            {
                MessageBox.Show("请先选择要保存的服务器或特性", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                if (!Directory.Exists(_discoveryOutputDirectory))
                {
                    Directory.CreateDirectory(_discoveryOutputDirectory);
                }

                var result = _discoveryService.SaveFeatures(selectedServers, _discoveryOutputDirectory, selectedOnly: true);

                if (result.Success)
                {
                    var message = $"{result.Message}\n\n输出目录: {_discoveryOutputDirectory}";
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
                        OpenDirectory(_discoveryOutputDirectory);
                    }

                    UpdateDiscoveryStatus(result.Message, StatusType.Success);
                }
                else
                {
                    MessageBox.Show($"保存失败:\n\n{result.Message}", 
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateDiscoveryStatus("保存失败", StatusType.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存特性时发生错误：\n\n{ex.Message}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateDiscoveryStatus("保存失败", StatusType.Error);
            }
        }

        private async void DirectGenerateClients_Click(object sender, RoutedEventArgs e)
        {
            var selectedServers = _servers.Where(s => s.IsSelected || s.Features.Any(f => f.IsSelected)).ToList();
            
            if (!selectedServers.Any())
            {
                MessageBox.Show("请先选择要生成的服务器或特性", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            UpdateDiscoveryStatus("正在准备生成代码...", StatusType.Info);

            try
            {
                // 创建输出目录
                if (!Directory.Exists(_discoveryOutputDirectory))
                {
                    Directory.CreateDirectory(_discoveryOutputDirectory);
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

                    var serverOutputDir = Path.Combine(_discoveryOutputDirectory, serverFolderName);
                    
                    UpdateDiscoveryStatus($"正在为服务器 {serverFolderName} 生成代码...", StatusType.Info);
                    
                    var result = await System.Threading.Tasks.Task.Run(() =>
                        _codeGenerator.GenerateClientCodeFromFeatures(
                            features,
                            serverOutputDir,
                            customNamespace,
                            message => Dispatcher.Invoke(() => UpdateDiscoveryStatus(message, StatusType.Info))
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

                UpdateDiscoveryStatus($"完成！已生成 {totalGenerated} 个文件", StatusType.Success);
                
                var dialogResult = MessageBox.Show(
                    $"客户端代码生成完成！\n\n生成代码文件: {totalGenerated}\n命名空间: {customNamespace}\n\n是否打开输出文件夹？",
                    "生成成功",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (dialogResult == MessageBoxResult.Yes)
                {
                    OpenDirectory(_discoveryOutputDirectory);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"生成代码时发生错误：\n\n{ex.Message}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateDiscoveryStatus("生成失败", StatusType.Error);
            }
        }

        private async void GenerateDiscoveryClients_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(_discoveryOutputDirectory))
            {
                MessageBox.Show("输出目录不存在，请先保存特性文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 查找所有已保存的特性文件
            var featureFiles = Directory.GetFiles(_discoveryOutputDirectory, "*.sila.xml", SearchOption.AllDirectories).ToList();
            
            if (!featureFiles.Any())
            {
                MessageBox.Show("未找到特性文件，请先保存特性", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UpdateDiscoveryStatus("正在生成客户端代码...", StatusType.Info);

            try
            {
                // 为每个服务器文件夹生成代码
                var serverFolders = Directory.GetDirectories(_discoveryOutputDirectory);
                
                foreach (var serverFolder in serverFolders)
                {
                    var serverFeatureFiles = Directory.GetFiles(serverFolder, "*.sila.xml").ToList();
                    if (!serverFeatureFiles.Any()) continue;

                    var serverOutputDir = Path.Combine(serverFolder, "Generated");
                    
                    var result = await System.Threading.Tasks.Task.Run(() =>
                        _codeGenerator.GenerateClientCode(
                            serverFeatureFiles,
                            serverOutputDir,
                            null,
                            message => Dispatcher.Invoke(() => UpdateDiscoveryStatus(message, StatusType.Info))
                        ));

                    if (!result.Success)
                    {
                        MessageBox.Show($"生成代码失败：\n{result.Message}", 
                            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                UpdateDiscoveryStatus("代码生成完成", StatusType.Success);
                
                var dialogResult = MessageBox.Show(
                    $"客户端代码生成完成！\n\n是否打开输出文件夹？",
                    "生成成功",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (dialogResult == MessageBoxResult.Yes)
                {
                    OpenDirectory(_discoveryOutputDirectory);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"生成代码时发生错误：\n\n{ex.Message}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateDiscoveryStatus("生成失败", StatusType.Error);
            }
        }

        private async void SaveAndGenerate_Click(object sender, RoutedEventArgs e)
        {
            var selectedServers = _servers.Where(s => s.IsSelected || s.Features.Any(f => f.IsSelected)).ToList();
            
            if (!selectedServers.Any())
            {
                MessageBox.Show("请先选择要保存的服务器或特性", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 1. 保存特性
                if (!Directory.Exists(_discoveryOutputDirectory))
                {
                    Directory.CreateDirectory(_discoveryOutputDirectory);
                }

                UpdateDiscoveryStatus("正在保存特性文件...", StatusType.Info);
                var saveResult = _discoveryService.SaveFeatures(selectedServers, _discoveryOutputDirectory, selectedOnly: true);

                if (!saveResult.Success)
                {
                    MessageBox.Show($"保存失败:\n\n{saveResult.Message}", 
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 2. 生成代码
                UpdateDiscoveryStatus("正在生成客户端代码...", StatusType.Info);
                
                // 获取自定义命名空间
                var customNamespace = "Sila2Client";

                var serverFolders = Directory.GetDirectories(_discoveryOutputDirectory);
                var totalGenerated = 0;

                foreach (var serverFolder in serverFolders)
                {
                    var serverFeatureFiles = Directory.GetFiles(serverFolder, "*.sila.xml").ToList();
                    if (!serverFeatureFiles.Any()) continue;

                    var serverOutputDir = serverFolder; // 直接在服务器文件夹中生成，不创建子文件夹
                    
                    var result = await System.Threading.Tasks.Task.Run(() =>
                        _codeGenerator.GenerateClientCode(
                            serverFeatureFiles,
                            serverOutputDir,
                            customNamespace,
                            message => Dispatcher.Invoke(() => UpdateDiscoveryStatus(message, StatusType.Info))
                        ));

                    if (result.Success)
                    {
                        totalGenerated += result.GeneratedFiles.Count;
                    }
                }

                UpdateDiscoveryStatus($"完成！已保存并生成 {totalGenerated} 个文件", StatusType.Success);
                
                var dialogResult = MessageBox.Show(
                    $"保存并生成完成！\n\n特性文件: {saveResult.SavedFiles.Count}\n生成代码文件: {totalGenerated}\n命名空间: {customNamespace}\n\n是否打开输出文件夹？",
                    "完成",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (dialogResult == MessageBoxResult.Yes)
                {
                    OpenDirectory(_discoveryOutputDirectory);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作失败：\n\n{ex.Message}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateDiscoveryStatus("操作失败", StatusType.Error);
            }
        }

        #endregion

        #region Property and Command Interaction

        /// <summary>
        /// 获取属性值（一次性获取）
        /// </summary>
        private async Task OnGetProperty(FeatureProperty property, StackPanel container, Button button)
        {
            if (_currentServerData == null || _currentFeature == null) return;

            // 查找响应容器
            var responseStack = container.Children.OfType<StackPanel>()
                .FirstOrDefault(s => s.Name == $"Response_{property.Identifier}");

            if (responseStack == null) return;

            // 清除旧的响应（每次开启新操作时）
            responseStack.Children.Clear();
            responseStack.Visibility = Visibility.Visible;

            // 禁用按钮
            button.IsEnabled = false;

            // 创建新的响应Border
            var responseBorder = CreateResponseBorder($"{DateTime.Now:HH:mm:ss} - 正在获取...");
            responseStack.Children.Add(responseBorder);

            try
            {
                var result = await _interactionService.GetPropertyValueAsync(_currentServerData, _currentFeature, property);
                
                // 更新响应
                UpdateResponseBorder(responseBorder, $"{DateTime.Now:HH:mm:ss} - 成功", result, false);
            }
            catch (Exception ex)
            {
                UpdateResponseBorder(responseBorder, $"{DateTime.Now:HH:mm:ss} - 错误", $"❌ {ex.Message}", true);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        /// <summary>
        /// 订阅属性（持续接收更新）
        /// </summary>
        private async Task OnSubscribeProperty(FeatureProperty property, StackPanel container, Button subscribeButton)
        {
            if (_currentServerData == null || _currentFeature == null) return;

            var responseStack = container.Children.OfType<StackPanel>()
                .FirstOrDefault(s => s.Name == $"Response_{property.Identifier}");

            if (responseStack == null) return;

            // 清除旧的响应
            responseStack.Children.Clear();
            responseStack.Visibility = Visibility.Visible;

            var subscriptionId = $"Prop_{property.Identifier}_{Guid.NewGuid()}";
            _activeSubscriptions[$"SubBtn_{property.Identifier}"] = subscriptionId;

            // 禁用订阅按钮，启用停止按钮
            subscribeButton.IsEnabled = false;
            var stopButton = container.Children.OfType<Grid>().FirstOrDefault()?.Children.OfType<Button>()
                .FirstOrDefault(b => b.Name == $"StopBtn_{property.Identifier}");
            if (stopButton != null) stopButton.IsEnabled = true;

            try
            {
                await _interactionService.SubscribePropertyAsync(
                    _currentServerData,
                    _currentFeature,
                    property,
                    value => Dispatcher.Invoke(() =>
                    {
                        // 添加新响应
                        var responseBorder = CreateResponseBorder($"{DateTime.Now:HH:mm:ss}", value, false);
                        responseStack.Children.Add(responseBorder);

                        // 保持最多5个回复
                        while (responseStack.Children.Count > 5)
                        {
                            responseStack.Children.RemoveAt(0);
                        }
                    }),
                    subscriptionId
                );
            }
            catch (Exception ex)
            {
                var responseBorder = CreateResponseBorder($"{DateTime.Now:HH:mm:ss} - 错误", $"❌ {ex.Message}", true);
                responseStack.Children.Add(responseBorder);
            }
        }

        /// <summary>
        /// 停止属性订阅
        /// </summary>
        private void OnStopProperty(FeatureProperty property, Button stopButton, Button subscribeButton)
        {
            var key = $"SubBtn_{property.Identifier}";
            if (_activeSubscriptions.TryGetValue(key, out var subscriptionId))
            {
                _interactionService.UnsubscribeProperty(subscriptionId);
                _activeSubscriptions.Remove(key);
            }

            stopButton.IsEnabled = false;
            subscribeButton.IsEnabled = true;
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        private async Task OnRunCommand(FeatureCommand command, StackPanel container, Button runButton)
        {
            if (_currentServerData == null || _currentFeature == null) return;

            // 查找响应容器
            var responseStack = container.Children.OfType<StackPanel>()
                .FirstOrDefault(s => s.Name == $"Response_{command.Identifier}");

            if (responseStack == null) return;

            // 收集参数
            var parameters = new Dictionary<string, object>();
            if (command.Parameter != null && command.Parameter.Any())
            {
                foreach (var param in command.Parameter)
                {
                    // 查找参数输入框
                    var input = FindVisualChild<TextBox>(container, $"Param_{param.Identifier}");
                    if (input != null && !string.IsNullOrWhiteSpace(input.Text))
                    {
                        parameters[param.Identifier] = ParseParameterValue(input.Text, param.DataType);
                    }
                }
            }

            // 清除旧的响应（每次开启新操作时）
            responseStack.Children.Clear();
            responseStack.Visibility = Visibility.Visible;

            // 禁用运行按钮
            runButton.IsEnabled = false;

            try
            {
                if (command.Observable == FeatureCommandObservable.Yes)
                {
                    // 可观察命令：中间过程追加显示
                    var commandId = Guid.NewGuid().ToString();
                    _activeSubscriptions[$"RunBtn_{command.Identifier}"] = commandId;

                    // 启用停止按钮
                    var stopButton = container.Children.OfType<Grid>().FirstOrDefault()?.Children.OfType<Button>()
                        .FirstOrDefault(b => b.Name == $"StopCmdBtn_{command.Identifier}");
                    if (stopButton != null) stopButton.IsEnabled = true;

                    // 添加初始状态
                    var startBorder = CreateResponseBorder($"{DateTime.Now:HH:mm:ss} - 命令启动", "正在执行...", false);
                    responseStack.Children.Add(startBorder);

                    var result = await _interactionService.ExecuteObservableCommandAsync(
                        _currentServerData,
                        _currentFeature,
                        command,
                        parameters,
                        progress => Dispatcher.Invoke(() =>
                        {
                            // 追加中间进度
                            var progressBorder = CreateResponseBorder($"{DateTime.Now:HH:mm:ss} - 进度更新", progress, false);
                            responseStack.Children.Add(progressBorder);
                        }),
                        commandId
                    );

                    // 添加最终结果（不覆盖中间数据）
                    var resultBorder = CreateResponseBorder($"{DateTime.Now:HH:mm:ss} - 完成", result, false);
                    responseStack.Children.Add(resultBorder);

                    // 禁用停止按钮
                    if (stopButton != null) stopButton.IsEnabled = false;
                    _activeSubscriptions.Remove($"RunBtn_{command.Identifier}");
                }
                else
                {
                    // 不可观察命令：单次结果
                    var responseBorder = CreateResponseBorder($"{DateTime.Now:HH:mm:ss} - 执行中...");
                    responseStack.Children.Add(responseBorder);

                    var result = await _interactionService.ExecuteUnobservableCommandAsync(
                        _currentServerData,
                        _currentFeature,
                        command,
                        parameters
                    );

                    UpdateResponseBorder(responseBorder, $"{DateTime.Now:HH:mm:ss} - 完成", result, false);
                }
            }
            catch (Exception ex)
            {
                var errorBorder = CreateResponseBorder($"{DateTime.Now:HH:mm:ss} - 错误", $"❌ {ex.Message}", true);
                responseStack.Children.Add(errorBorder);
            }
            finally
            {
                runButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// 停止可观察命令
        /// </summary>
        private void OnStopCommand(FeatureCommand command, Button stopButton, Button runButton)
        {
            var key = $"RunBtn_{command.Identifier}";
            if (_activeSubscriptions.TryGetValue(key, out var commandId))
            {
                _interactionService.CancelCommand(commandId);
                _activeSubscriptions.Remove(key);
            }

            stopButton.IsEnabled = false;
            runButton.IsEnabled = true;
        }

        /// <summary>
        /// 创建响应Border（用于显示单个响应）
        /// </summary>
        private Border CreateResponseBorder(string title, string content = "", bool isError = false)
        {
            var border = new Border
            {
                Background = isError 
                    ? new SolidColorBrush(Color.FromRgb(139, 0, 0))  // Dark red for errors
                    : new SolidColorBrush(Color.FromRgb(0, 102, 102)), // Darker teal for normal
                BorderBrush = isError
                    ? new SolidColorBrush(Color.FromRgb(200, 50, 50))
                    : new SolidColorBrush(Color.FromRgb(0, 150, 150)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 5, 0, 0)
            };

            var stackPanel = new StackPanel();

            // 标题
            stackPanel.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 5)
            });

            // 内容
            if (!string.IsNullOrEmpty(content))
            {
                stackPanel.Children.Add(new TextBox
                {
                    Text = content,
                    Foreground = Brushes.White,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 250,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 11
                });
            }

            border.Child = stackPanel;
            return border;
        }

        /// <summary>
        /// 更新响应Border的内容
        /// </summary>
        private void UpdateResponseBorder(Border border, string title, string content, bool isError)
        {
            border.Background = isError
                ? new SolidColorBrush(Color.FromRgb(139, 0, 0))
                : new SolidColorBrush(Color.FromRgb(0, 102, 102));
            border.BorderBrush = isError
                ? new SolidColorBrush(Color.FromRgb(200, 50, 50))
                : new SolidColorBrush(Color.FromRgb(0, 150, 150));

            if (border.Child is StackPanel stackPanel)
            {
                // 更新标题
                if (stackPanel.Children[0] is TextBlock titleBlock)
                {
                    titleBlock.Text = title;
                }

                // 更新或添加内容
                if (stackPanel.Children.Count > 1 && stackPanel.Children[1] is TextBox contentBox)
                {
                    contentBox.Text = content;
                }
                else
                {
                    stackPanel.Children.Add(new TextBox
                    {
                        Text = content,
                        Foreground = Brushes.White,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.Wrap,
                        MaxHeight = 250,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize = 11
                    });
                }
            }
        }

        /// <summary>
        /// 解析参数值
        /// </summary>
        private object ParseParameterValue(string value, DataTypeType dataType)
        {
            var basicType = GetBasicType(dataType);
            
            try
            {
                switch (basicType)
                {
                    case BasicType.Integer:
                        return long.Parse(value);
                    case BasicType.Real:
                        return double.Parse(value);
                    case BasicType.Boolean:
                        return bool.Parse(value);
                    case BasicType.String:
                    default:
                        return value;
                }
            }
            catch
            {
                return value;
            }
        }

        private BasicType? GetBasicType(DataTypeType dataType)
        {
            if (dataType?.Item is BasicType basicType)
                return basicType;
            if (dataType?.Item is ConstrainedType constrainedType)
                return GetBasicType(constrainedType.DataType);
            return null;
        }

        /// <summary>
        /// 查找可视化树中的子控件
        /// </summary>
        private T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild && typedChild.Name == name)
                    return typedChild;

                var result = FindVisualChild<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        #endregion

        #region UI Helpers

        private enum MessageType
        {
            Info,
            Success,
            Warning,
            Error
        }

        private enum StatusType
        {
            Info,
            Success,
            Warning,
            Error
        }

        private void UpdateStatus(string message, MessageType type = MessageType.Info)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Foreground = type switch
            {
                MessageType.Success => new SolidColorBrush(Color.FromRgb(39, 174, 96)),   // Green
                MessageType.Warning => new SolidColorBrush(Color.FromRgb(243, 156, 18)),  // Orange
                MessageType.Error => new SolidColorBrush(Color.FromRgb(231, 76, 60)),     // Red
                _ => new SolidColorBrush(Color.FromRgb(52, 73, 94))                       // Dark Gray
            };
        }

        private void UpdateDiscoveryStatus(string message, StatusType type = StatusType.Info)
        {
            DiscoveryStatusText.Text = message;
            DiscoveryStatusText.Foreground = type switch
            {
                StatusType.Success => new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                StatusType.Warning => new SolidColorBrush(Color.FromRgb(243, 156, 18)),
                StatusType.Error => new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                _ => new SolidColorBrush(Color.FromRgb(127, 140, 141))
            };
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _logger.LogInformation("应用程序关闭");
            _logger.LogInformation($"日志文件位置: {LoggerService.GetLogsDirectory()}");
            LoggerService.Shutdown();
        }

        #endregion
    }
}
