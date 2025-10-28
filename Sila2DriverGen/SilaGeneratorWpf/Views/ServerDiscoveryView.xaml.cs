using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SilaGeneratorWpf.Models;
using SilaGeneratorWpf.ViewModels;
using Tecan.Sila2;

namespace SilaGeneratorWpf.Views
{
    /// <summary>
    /// ServerDiscoveryView.xaml 的交互逻辑
    /// </summary>
    public partial class ServerDiscoveryView : UserControl
    {
        public ServerDiscoveryView()
        {
            InitializeComponent();
        }

        private async void ServerTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is not ServerDiscoveryViewModel viewModel)
                return;

            DetailPanel.Children.Clear();
            viewModel.SelectedItem = e.NewValue;

            if (e.NewValue is ServerInfoViewModel server)
            {
                ShowServerDetails(server);
            }
            else if (e.NewValue is FeatureInfoViewModel featureViewModel)
            {
                await ShowFeatureDetailsAsync(featureViewModel, viewModel);
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

        private async Task ShowFeatureDetailsAsync(FeatureInfoViewModel featureViewModel, ServerDiscoveryViewModel viewModel)
        {
            if (featureViewModel.ParentServer == null || string.IsNullOrEmpty(featureViewModel.FeatureXml))
            {
                ShowBasicFeatureDetails(featureViewModel);
                return;
            }

            try
            {
                var tempFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(tempFile, featureViewModel.FeatureXml);
                var feature = FeatureSerializer.Load(tempFile);
                File.Delete(tempFile);
                
                ShowFeatureDetailsWithInteraction(featureViewModel, feature, viewModel);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载特性失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ShowBasicFeatureDetails(featureViewModel);
            }
        }

        private void ShowBasicFeatureDetails(FeatureInfoViewModel feature)
        {
            DetailPanel.Children.Add(CreateTitle($"特性: {feature.DisplayName ?? feature.Identifier}"));
            DetailPanel.Children.Add(CreateInfoRow("标识符", feature.Identifier));
            DetailPanel.Children.Add(CreateInfoRow("显示名称", feature.DisplayName ?? ""));
            DetailPanel.Children.Add(CreateInfoRow("版本", feature.Version));
            DetailPanel.Children.Add(CreateInfoRow("命名空间", feature.Namespace));
            DetailPanel.Children.Add(CreateInfoRow("描述", feature.Description));
        }

        private void ShowFeatureDetailsWithInteraction(FeatureInfoViewModel featureViewModel, Feature feature, ServerDiscoveryViewModel viewModel)
        {
            DetailPanel.Children.Add(CreateTitle("基本信息"));
            DetailPanel.Children.Add(CreateInfoRow("标识符", feature.Identifier ?? ""));
            DetailPanel.Children.Add(CreateInfoRow("显示名称", feature.DisplayName ?? ""));
            DetailPanel.Children.Add(CreateInfoRow("版本", feature.FeatureVersion ?? ""));
            DetailPanel.Children.Add(CreateInfoRow("命名空间", $"{feature.Originator}.{feature.Category}"));
            DetailPanel.Children.Add(CreateInfoRow("描述", feature.Description ?? ""));

            // 直接从 ServerInfoViewModel 获取 ServerData 缓存
            var serverData = featureViewModel.ParentServer?.ServerDataCache;
            if (serverData == null)
            {
                DetailPanel.Children.Add(CreateInfoRow("错误", "无法获取服务器数据，请刷新服务器"));
                return;
            }

            var interactionService = viewModel.GetInteractionService();

            // 属性部分
            var properties = feature.Items?.OfType<FeatureProperty>().ToList() ?? new List<FeatureProperty>();
            if (properties.Any())
            {
                DetailPanel.Children.Add(new Separator { Margin = new Thickness(0, 20, 0, 20) });
                DetailPanel.Children.Add(CreateSectionTitle($"属性 ({properties.Count})"));
                
                foreach (var property in properties)
                {
                    DetailPanel.Children.Add(CreatePropertyPanel(property, serverData, feature, interactionService));
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
                    DetailPanel.Children.Add(CreateCommandPanel(command, serverData, feature, interactionService));
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

        #region UI Helper Methods

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

        private TextBlock CreateSectionTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 128))
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

        private Border CreatePropertyPanel(FeatureProperty property, ServerData serverData, Feature feature, Services.ServerInteractionService interactionService)
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

            // 标题行
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = property.DisplayName ?? property.Identifier,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameText, 0);
            headerGrid.Children.Add(nameText);

            var observableText = new TextBlock
            {
                Text = property.Observable == FeaturePropertyObservable.Yes ? "✓ Observable" : "✗ Not Observable",
                FontSize = 10,
                Foreground = Brushes.White,
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(observableText, 1);
            headerGrid.Children.Add(observableText);

            // 按钮面板
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            bool isObservable = property.Observable == FeaturePropertyObservable.Yes;

            // Get 按钮（获取一次）
            var getButton = new Button
            {
                Content = "Get",
                Width = 50,
                Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(3, 0, 3, 0),
                FontSize = 4,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(2)
            };
            getButton.Click += async (s, e) => await OnGetProperty(property, serverData, feature, interactionService, stackPanel, getButton);
            buttonPanel.Children.Add(getButton);

            if (isObservable)
            {
                // Subscribe 按钮（订阅/获取多次）
                var subscribeButton = new Button
                {
                    Content = "Sub",
                    Width = 50,
                    Height = 30,
                    Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(3, 0, 3, 0),
                    FontSize = 4,
                    FontWeight = FontWeights.Bold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Padding = new Thickness(2),
                    ToolTip = "Subscribe (持续接收)"
                };
                subscribeButton.Click += async (s, e) => await OnSubscribeProperty(property, serverData, feature, interactionService, stackPanel, subscribeButton);
                buttonPanel.Children.Add(subscribeButton);

                // Stop 按钮（停止订阅）
                var stopButton = new Button
                {
                    Content = "Stop",
                    Width = 50,
                    Height = 30,
                    Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(3, 0, 3, 0),
                    FontSize =4,
                    FontWeight = FontWeights.Bold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Padding = new Thickness(2),
                    IsEnabled = false,
                    ToolTip = "停止订阅"
                };
                stopButton.Click += (s, e) => OnStopProperty(property, serverData, feature, interactionService, stackPanel, subscribeButton, stopButton);
                buttonPanel.Children.Add(stopButton);
            }

            Grid.SetColumn(buttonPanel, 2);
            headerGrid.Children.Add(buttonPanel);

            stackPanel.Children.Add(headerGrid);

            if (!string.IsNullOrEmpty(property.Description))
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = property.Description,
                    Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                    Margin = new Thickness(0, 8, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 10
                });
            }

            var responseStack = new StackPanel();
            
            // 添加滚动视图包装响应区域
            var scrollViewer = new ScrollViewer
            {
                MaxHeight = 300,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 10, 0, 0),
                Visibility = Visibility.Collapsed
            };
            scrollViewer.Content = responseStack;
            stackPanel.Children.Add(scrollViewer);

            border.Child = stackPanel;
            return border;
        }

        private Border CreateCommandPanel(FeatureCommand command, ServerData serverData, Feature feature, Services.ServerInteractionService interactionService)
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

            // 标题行
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = command.DisplayName ?? command.Identifier,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameText, 0);
            headerGrid.Children.Add(nameText);

            var observableText = new TextBlock
            {
                Text = command.Observable == FeatureCommandObservable.Yes ? "✓ Observable" : "✗ Not Observable",
                FontSize = 10,
                Foreground = Brushes.White,
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(observableText, 1);
            headerGrid.Children.Add(observableText);

            // 按钮面板
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            bool isObservable = command.Observable == FeatureCommandObservable.Yes;

            // Run/Execute 按钮
            var runButton = new Button
            {
                Content = "Run",
                Width = 50,
                Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(3, 0, 3, 0),
                FontSize = 4,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(2)
            };

            if (isObservable)
            {
                // Stop 按钮（停止Observable命令）
                var stopButton = new Button
                {
                    Content = "Stop",
                    Width = 50,
                    Height = 30,
                    Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(3, 0, 3, 0),
                    FontSize = 4,
                    FontWeight = FontWeights.Bold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Padding = new Thickness(2),
                    IsEnabled = false,
                    ToolTip = "停止执行"
                };

                runButton.Click += async (s, e) => await OnRunObservableCommand(command, serverData, feature, interactionService, stackPanel, runButton, stopButton);
                stopButton.Click += (s, e) => OnStopCommand(command, serverData, feature, interactionService, stackPanel, runButton, stopButton);

                buttonPanel.Children.Add(runButton);
                buttonPanel.Children.Add(stopButton);
            }
            else
            {
                runButton.Click += async (s, e) => await OnRunCommand(command, serverData, feature, interactionService, stackPanel, runButton);
                buttonPanel.Children.Add(runButton);
            }

            Grid.SetColumn(buttonPanel, 2);
            headerGrid.Children.Add(buttonPanel);

            stackPanel.Children.Add(headerGrid);

            if (!string.IsNullOrEmpty(command.Description))
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = command.Description,
                    Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                    Margin = new Thickness(0, 5, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11
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
                    Margin = new Thickness(0, 10, 0, 5),
                    FontSize = 11
                });

                foreach (var param in command.Parameter)
                {
                    stackPanel.Children.Add(CreateParameterInput(param));
                }
            }

            var responseStack = new StackPanel();
            
            // 添加滚动视图包装响应区域
            var scrollViewer = new ScrollViewer
            {
                MaxHeight = 300,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 10, 0, 0),
                Visibility = Visibility.Collapsed
            };
            scrollViewer.Content = responseStack;
            stackPanel.Children.Add(scrollViewer);

            border.Child = stackPanel;
            return border;
        }

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
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11
                });
            }

            border.Child = stackPanel;
            return border;
        }

        private StackPanel CreateParameterInput(SiLAElement parameter)
        {
            var mainPanel = new StackPanel
            {
                Margin = new Thickness(10, 5, 0, 10)
            };

            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var namePanel = new StackPanel();
            namePanel.Children.Add(new TextBlock
            {
                Text = parameter.DisplayName ?? parameter.Identifier,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11
            });

            // 添加数据类型信息
            if (parameter.DataType != null)
            {
                var typeInfo = GetDataTypeDescription(parameter.DataType);
                if (!string.IsNullOrEmpty(typeInfo))
                {
                    namePanel.Children.Add(new TextBlock
                    {
                        Text = $"({typeInfo})",
                        Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                        FontSize = 9,
                        FontStyle = FontStyles.Italic
                    });
                }
            }

            Grid.SetColumn(namePanel, 0);
            inputGrid.Children.Add(namePanel);

            var input = new TextBox
            {
                Name = $"Param_{parameter.Identifier}",
                Padding = new Thickness(5),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11
            };

            Grid.SetColumn(input, 1);
            inputGrid.Children.Add(input);

            mainPanel.Children.Add(inputGrid);

            // 显示约束信息
            if (parameter.DataType != null)
            {
                var constraintInfo = GetConstraintDescription(parameter.DataType);
                if (!string.IsNullOrEmpty(constraintInfo))
                {
                    mainPanel.Children.Add(new TextBlock
                    {
                        Text = $"  ⚙ 约束: {constraintInfo}",
                        Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 200, 100)),
                        FontSize = 9,
                        Margin = new Thickness(0, 2, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    });
                }
            }

            if (!string.IsNullOrEmpty(parameter.Description))
            {
                mainPanel.Children.Add(new TextBlock
                {
                    Text = $"  ▪ {parameter.Description}",
                    Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    FontSize = 10,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            return mainPanel;
        }

        private string GetDataTypeDescription(DataTypeType dataType)
        {
            if (dataType?.Item is BasicType basicType)
            {
                return basicType.ToString();
            }
            if (dataType?.Item is ConstrainedType constrainedType)
            {
                var baseType = GetDataTypeDescription(constrainedType.DataType);
                return baseType;
            }
            if (dataType?.Item is ListType listType)
            {
                var elementType = GetDataTypeDescription(listType.DataType);
                return $"List<{elementType}>";
            }
            if (dataType?.Item is StructureType)
            {
                return "Structure";
            }
            return "Unknown";
        }

        private string GetConstraintDescription(DataTypeType dataType)
        {
            if (dataType?.Item is ConstrainedType constrainedType && constrainedType.Constraints != null)
            {
                var constraints = new List<string>();
                var constraintsObj = constrainedType.Constraints;

                // 使用反射获取约束属性
                var constraintType = constraintsObj.GetType();
                var allProperties = constraintType.GetProperties();
                
                // Range Constraint - 最小值和最大值
                var minInclusiveProp = constraintType.GetProperty("MinimalInclusive");
                if (minInclusiveProp != null)
                {
                    var value = minInclusiveProp.GetValue(constraintsObj);
                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                        constraints.Add($"最小值: {value}");
                }

                var maxInclusiveProp = constraintType.GetProperty("MaximalInclusive");
                if (maxInclusiveProp != null)
                {
                    var value = maxInclusiveProp.GetValue(constraintsObj);
                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                        constraints.Add($"最大值: {value}");
                }

                var minExclusiveProp = constraintType.GetProperty("MinimalExclusive");
                if (minExclusiveProp != null)
                {
                    var value = minExclusiveProp.GetValue(constraintsObj);
                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                        constraints.Add($"最小值(不含): {value}");
                }

                var maxExclusiveProp = constraintType.GetProperty("MaximalExclusive");
                if (maxExclusiveProp != null)
                {
                    var value = maxExclusiveProp.GetValue(constraintsObj);
                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                        constraints.Add($"最大值(不含): {value}");
                }

                // Length Constraint - 长度约束
                var lengthProp = constraintType.GetProperty("Length");
                if (lengthProp != null)
                {
                    var value = lengthProp.GetValue(constraintsObj);
                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                        constraints.Add($"固定长度: {value}");
                }

                var minLengthProp = constraintType.GetProperty("MinimalLength");
                if (minLengthProp != null)
                {
                    var value = minLengthProp.GetValue(constraintsObj);
                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                        constraints.Add($"最小长度: {value}");
                }

                var maxLengthProp = constraintType.GetProperty("MaximalLength");
                if (maxLengthProp != null)
                {
                    var value = maxLengthProp.GetValue(constraintsObj);
                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                        constraints.Add($"最大长度: {value}");
                }

                // Pattern/Regex Constraint - 正则表达式约束
                var patternProp = constraintType.GetProperty("Pattern");
                if (patternProp != null)
                {
                    var value = patternProp.GetValue(constraintsObj) as string;
                    if (!string.IsNullOrEmpty(value))
                        constraints.Add($"模式: {value}");
                }

                // Enumeration Constraint - 枚举值约束
                var setProp = constraintType.GetProperty("Set");
                if (setProp != null)
                {
                    var value = setProp.GetValue(constraintsObj);
                    if (value is Array arr && arr.Length > 0)
                    {
                        var items = new List<string>();
                        foreach (var item in arr)
                            items.Add(item?.ToString() ?? "");
                        if (items.Count <= 5)
                            constraints.Add($"枚举: [{string.Join(", ", items)}]");
                        else
                            constraints.Add($"枚举: [{string.Join(", ", items.Take(5))}... 共{items.Count}项]");
                    }
                }

                // Step Constraint - 步长约束
                var stepProp = constraintType.GetProperty("Step");
                if (stepProp != null)
                {
                    var value = stepProp.GetValue(constraintsObj);
                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                        constraints.Add($"步长: {value}");
                }

                // Unit Constraint - 单位约束
                var unitProp = constraintType.GetProperty("Unit");
                if (unitProp != null)
                {
                    var value = unitProp.GetValue(constraintsObj) as string;
                    if (!string.IsNullOrEmpty(value))
                        constraints.Add($"单位: {value}");
                }

                // Schema Constraint - 模式约束
                var schemaProp = constraintType.GetProperty("Schema");
                if (schemaProp != null)
                {
                    var value = schemaProp.GetValue(constraintsObj);
                    if (value != null)
                    {
                        var schemaType = value.GetType();
                        var typeProp = schemaType.GetProperty("Type");
                        if (typeProp != null)
                        {
                            var typeValue = typeProp.GetValue(value);
                            if (typeValue != null)
                                constraints.Add($"Schema类型: {typeValue}");
                        }
                    }
                }

                // ContentType Constraint - 内容类型约束
                var contentTypeProp = constraintType.GetProperty("ContentType");
                if (contentTypeProp != null)
                {
                    var value = contentTypeProp.GetValue(constraintsObj);
                    if (value != null)
                    {
                        var ctType = value.GetType();
                        var typeProp = ctType.GetProperty("Type");
                        var subtypeProp = ctType.GetProperty("Subtype");
                        if (typeProp != null && subtypeProp != null)
                        {
                            var type = typeProp.GetValue(value);
                            var subtype = subtypeProp.GetValue(value);
                            if (type != null && subtype != null)
                                constraints.Add($"内容类型: {type}/{subtype}");
                        }
                    }
                }

                // FullyQualifiedIdentifier - 标识符约束
                var fqiProp = constraintType.GetProperty("FullyQualifiedIdentifier");
                var fqiSpecifiedProp = constraintType.GetProperty("FullyQualifiedIdentifierSpecified");
                if (fqiProp != null && fqiSpecifiedProp != null)
                {
                    var specified = fqiSpecifiedProp.GetValue(constraintsObj);
                    if (specified is bool b && b)
                    {
                        var value = fqiProp.GetValue(constraintsObj);
                        if (value != null)
                            constraints.Add($"标识符类型: {value}");
                    }
                }
                
                if (constraints.Any())
                    return string.Join("; ", constraints);
                
                // 递归检查基础类型
                return GetConstraintDescription(constrainedType.DataType);
            }
            return string.Empty;
        }

        #endregion

        #region Property and Command Interaction Handlers

        private async Task OnGetProperty(FeatureProperty property, ServerData serverData, Feature feature, 
            Services.ServerInteractionService interactionService, StackPanel container, Button button)
        {
            var scrollViewer = container.Children.OfType<ScrollViewer>().LastOrDefault();
            if (scrollViewer?.Content is not StackPanel responseStack) return;

            responseStack.Children.Clear();
            scrollViewer.Visibility = Visibility.Visible;

            button.IsEnabled = false;

            var responseBorder = CreateResponseBorder($"{DateTime.Now:HH:mm:ss} - 正在获取...");
            responseStack.Children.Add(responseBorder);

            try
            {
                var result = await Task.Run(() => interactionService.GetPropertyValueAsync(serverData, feature, property));
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

        private async Task OnSubscribeProperty(FeatureProperty property, ServerData serverData, Feature feature,
            Services.ServerInteractionService interactionService, StackPanel container, Button subscribeButton)
        {
            var scrollViewer = container.Children.OfType<ScrollViewer>().LastOrDefault();
            if (scrollViewer?.Content is not StackPanel responseStack) return;

            responseStack.Children.Clear();
            scrollViewer.Visibility = Visibility.Visible;

            subscribeButton.IsEnabled = false;

            // 启用停止按钮
            var buttonPanel = subscribeButton.Parent as StackPanel;
            var stopButton = buttonPanel?.Children.OfType<Button>().FirstOrDefault(b => b.Content.ToString() == "Stop");
            if (stopButton != null)
            {
                stopButton.IsEnabled = true;
            }

            var responseBorder = CreateResponseBorder($"{DateTime.Now:HH:mm:ss} - 开始订阅...");
            responseStack.Children.Add(responseBorder);

            // 使用唯一的订阅ID
            var subscriptionId = $"{feature.Identifier}_{property.Identifier}_{Guid.NewGuid()}";
            if (stopButton != null)
            {
                stopButton.Tag = subscriptionId; // 保存订阅ID到Tag
            }

            try
            {
                await interactionService.SubscribePropertyAsync(serverData, feature, property, (value) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var newResponse = CreateResponseBorder($"{DateTime.Now:HH:mm:ss} - 收到数据", value, false);
                        responseStack.Children.Add(newResponse);
                        
                        // 限制最多显示5个最近数据（加上初始的"开始订阅"消息，总共最多6个）
                        while (responseStack.Children.Count > 6)
                        {
                            responseStack.Children.RemoveAt(1); // 保留第一个"开始订阅"消息，删除旧的数据
                        }
                    });
                }, subscriptionId);
                
                Dispatcher.Invoke(() =>
                {
                    UpdateResponseBorder(responseBorder, $"{DateTime.Now:HH:mm:ss} - 订阅已结束", "订阅完成", false);
                    subscribeButton.IsEnabled = true;
                    if (stopButton != null)
                    {
                        stopButton.IsEnabled = false;
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateResponseBorder(responseBorder, $"{DateTime.Now:HH:mm:ss} - 订阅失败", $"❌ {ex.Message}", true);
                    subscribeButton.IsEnabled = true;
                    if (stopButton != null)
                    {
                        stopButton.IsEnabled = false;
                    }
                });
            }
        }

        private void OnStopProperty(FeatureProperty property, ServerData serverData, Feature feature,
            Services.ServerInteractionService interactionService, StackPanel container, Button subscribeButton, Button stopButton)
        {
            try
            {
                var subscriptionId = stopButton.Tag as string;
                if (!string.IsNullOrEmpty(subscriptionId))
                {
                    interactionService.UnsubscribeProperty(subscriptionId);
                }

                var scrollViewer = container.Children.OfType<ScrollViewer>().LastOrDefault();
                if (scrollViewer?.Content is StackPanel responseStack)
                {
                    var statusBorder = CreateResponseBorder($"{DateTime.Now:HH:mm:ss} - 已停止订阅", "✓ 订阅已停止", false);
                    responseStack.Children.Add(statusBorder);
                }

                subscribeButton.IsEnabled = true;
                stopButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止订阅失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OnRunCommand(FeatureCommand command, ServerData serverData, Feature feature,
            Services.ServerInteractionService interactionService, StackPanel container, Button runButton)
        {
            var scrollViewer = container.Children.OfType<ScrollViewer>().LastOrDefault();
            if (scrollViewer?.Content is not StackPanel responseStack) return;

            // 收集参数
            var parameters = new Dictionary<string, object>();
            if (command.Parameter != null && command.Parameter.Any())
            {
                foreach (var param in command.Parameter)
                {
                    var input = FindVisualChild<TextBox>(container, $"Param_{param.Identifier}");
                    if (input != null && !string.IsNullOrWhiteSpace(input.Text))
                    {
                        parameters[param.Identifier] = input.Text;
                    }
                }
            }

            responseStack.Children.Clear();
            scrollViewer.Visibility = Visibility.Visible;

            runButton.IsEnabled = false;

            try
            {
                var responseBorder = CreateResponseBorder($"{DateTime.Now:HH:mm:ss} - 执行中...");
                responseStack.Children.Add(responseBorder);

                var result = await Task.Run(() => interactionService.ExecuteUnobservableCommandAsync(
                    serverData, feature, command, parameters));

                UpdateResponseBorder(responseBorder, $"{DateTime.Now:HH:mm:ss} - 完成", result, false);
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

        private async Task OnRunObservableCommand(FeatureCommand command, ServerData serverData, Feature feature,
            Services.ServerInteractionService interactionService, StackPanel container, Button runButton, Button stopButton)
        {
            var scrollViewer = container.Children.OfType<ScrollViewer>().LastOrDefault();
            if (scrollViewer?.Content is not StackPanel responseStack) return;

            // 收集参数
            var parameters = new Dictionary<string, object>();
            if (command.Parameter != null && command.Parameter.Any())
            {
                foreach (var param in command.Parameter)
                {
                    var input = FindVisualChild<TextBox>(container, $"Param_{param.Identifier}");
                    if (input != null && !string.IsNullOrWhiteSpace(input.Text))
                    {
                        parameters[param.Identifier] = input.Text;
                    }
                }
            }

            responseStack.Children.Clear();
            scrollViewer.Visibility = Visibility.Visible;

            runButton.IsEnabled = false;
            stopButton.IsEnabled = true;

            var responseBorder = CreateResponseBorder($"{DateTime.Now:HH:mm:ss} - 开始执行可观察命令...");
            responseStack.Children.Add(responseBorder);

            // 使用唯一的命令ID
            var commandId = $"{feature.Identifier}_{command.Identifier}_{Guid.NewGuid()}";
            stopButton.Tag = commandId; // 保存命令ID到Tag

            try
            {
                var result = await interactionService.ExecuteObservableCommandAsync(
                    serverData, 
                    feature, 
                    command, 
                    parameters, 
                    (progress) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var progressResponse = CreateResponseBorder($"{DateTime.Now:HH:mm:ss} - 进度更新", progress, false);
                            responseStack.Children.Add(progressResponse);
                            
                            // 限制最多显示5条进度信息（加上初始的"开始执行"消息，总共最多6条）
                            while (responseStack.Children.Count > 6)
                            {
                                responseStack.Children.RemoveAt(1); // 保留第一个"开始执行"消息，删除旧的进度
                            }
                        });
                    },
                    commandId);

                Dispatcher.Invoke(() =>
                {
                    UpdateResponseBorder(responseBorder, $"{DateTime.Now:HH:mm:ss} - 命令完成", result, false);
                    runButton.IsEnabled = true;
                    stopButton.IsEnabled = false;
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateResponseBorder(responseBorder, $"{DateTime.Now:HH:mm:ss} - 执行失败", $"❌ {ex.Message}", true);
                    runButton.IsEnabled = true;
                    stopButton.IsEnabled = false;
                });
            }
        }

        private void OnStopCommand(FeatureCommand command, ServerData serverData, Feature feature,
            Services.ServerInteractionService interactionService, StackPanel container, Button runButton, Button stopButton)
        {
            try
            {
                var commandId = stopButton.Tag as string;
                if (!string.IsNullOrEmpty(commandId))
                {
                    interactionService.CancelCommand(commandId);
                }

                var scrollViewer = container.Children.OfType<ScrollViewer>().LastOrDefault();
                if (scrollViewer?.Content is StackPanel responseStack)
                {
                    var statusBorder = CreateResponseBorder($"{DateTime.Now:HH:mm:ss} - 已停止命令", "✓ 命令执行已停止", false);
                    responseStack.Children.Add(statusBorder);
                }

                runButton.IsEnabled = true;
                stopButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止命令失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Border CreateResponseBorder(string title, string content = "", bool isError = false)
        {
            var border = new Border
            {
                Background = isError 
                    ? new SolidColorBrush(Color.FromRgb(139, 0, 0))
                    : new SolidColorBrush(Color.FromRgb(0, 102, 102)),
                BorderBrush = isError
                    ? new SolidColorBrush(Color.FromRgb(200, 50, 50))
                    : new SolidColorBrush(Color.FromRgb(0, 150, 150)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 5, 0, 0)
            };

            var stackPanel = new StackPanel();

            stackPanel.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 5)
            });

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
                    FontSize = 10
                });
            }

            border.Child = stackPanel;
            return border;
        }

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
                if (stackPanel.Children[0] is TextBlock titleBlock)
                {
                    titleBlock.Text = title;
                }

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
                        FontSize = 10
                    });
                }
            }
        }

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
    }
}

