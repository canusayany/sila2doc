# SiLA2 D3驱动生成工具实施计划

## 一、需求概述

在现有 WPF 项目 `Sila2DriverGen/SilaGeneratorWpf` 中添加第三个 Tab 页面 **"🎯 生成D3驱动"**，用于从 Tecan 生成的客户端代码自动生成 D3 驱动封装层。

**⚠️ 重要架构决策：**
- ✅ **采用 MVVM Toolkit** 实现 WPF 界面和业务逻辑分离
- ✅ **不使用独立控制台应用** - 所有功能都在 WPF 界面中完成
- ✅ **测试控制台是可选的** - 只生成一个简单的测试壳子程序

### 1.1 技术方案确认

**已确定的技术决策：**
- ✅ 使用 Tecan Generator 生成客户端代码（前两个Tab已实现）
- ✅ 使用 `BR.PC.Device.Sila2Discovery` 扫描服务器和连接
- ✅ 使用 Tecan 库的连接方式：`_server = _connector.Connect(info.IPAddress, info.Port, info.Uuid, info.TxtRecords)`
- ✅ 可观察命令使用 `command.Response.GetAwaiter().GetResult()` 阻塞等待（或 `await command.Response`）
- ✅ **通过 AllSila2Client 中间封装类整合多个特性**（命名冲突添加前缀 `FeatureName_Method`）
- ✅ 使用 CodeDOM 生成所有 D3 驱动代码
- ✅ 数据类型限制明确：int, byte, sbyte, string, DateTime, double, float, byte[], Enum, bool, List/Array（元素仅基础类型）, class/struct（仅包含基础类型，不嵌套）
- ✅ 支持一个服务器多个特性
- ✅ 超出预期类型使用 JSON 序列化/反序列化（可选扩展）

### 1.2 更新项目描述文档

更新 `项目描述与要求.md`，记录本次讨论的所有决策和实现细节。

## 二、在 WPF 中添加第三个 Tab

### 2.1 修改 MainWindow.xaml

在现有 TabControl 中添加第三个 TabItem **"🎯 生成D3驱动"**：

```xml
<TabItem Header="🎯 生成D3驱动">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>  <!-- 客户端代码选择 -->
            <RowDefinition Height="Auto"/>  <!-- 设备信息配置 -->
            <RowDefinition Height="Auto"/>  <!-- 生成选项 -->
            <RowDefinition Height="*"/>     <!-- 特性方法预览 -->
            <RowDefinition Height="Auto"/>  <!-- 操作按钮 -->
        </Grid.RowDefinitions>
        
        <!-- 第一部分：客户端代码选择 -->
        <GroupBox Header="1. 选择客户端代码目录" Grid.Row="0" Margin="0,0,0,10">
            <StackPanel Margin="10">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <TextBox x:Name="ClientCodePathTextBox" 
                             Grid.Column="0"
                             IsReadOnly="True" 
                             Padding="5"
                             VerticalAlignment="Center"/>
                    <Button Grid.Column="1" 
                            Content="📁 浏览" 
                            Padding="10,5"
                            Margin="5,0,0,0"
                            Click="BrowseClientCode_Click"/>
                </Grid>
                <TextBlock x:Name="DetectedFeaturesText" 
                           Text="检测到的特性: (空)" 
                           Margin="0,5,0,0"
                           Foreground="#7f8c8d"/>
            </StackPanel>
        </GroupBox>
        
        <!-- 第二部分：设备信息 -->
        <GroupBox Header="2. 配置设备信息" Grid.Row="1" Margin="0,0,0,10">
            <Grid Margin="10">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                
                <TextBlock Grid.Row="0" Grid.Column="0" Text="品牌：" VerticalAlignment="Center" Margin="0,0,5,5"/>
                <TextBox x:Name="DeviceBrandTextBox" Grid.Row="0" Grid.Column="1" Padding="5" Margin="0,0,10,5"/>
                
                <TextBlock Grid.Row="0" Grid.Column="2" Text="型号：" VerticalAlignment="Center" Margin="0,0,5,5"/>
                <TextBox x:Name="DeviceModelTextBox" Grid.Row="0" Grid.Column="3" Padding="5" Margin="0,0,0,5"/>
                
                <TextBlock Grid.Row="1" Grid.Column="0" Text="类型：" VerticalAlignment="Center" Margin="0,0,5,0"/>
                <TextBox x:Name="DeviceTypeTextBox" Grid.Row="1" Grid.Column="1" Padding="5" Margin="0,0,10,0"/>
                
                <TextBlock Grid.Row="1" Grid.Column="2" Text="开发者：" VerticalAlignment="Center" Margin="0,0,5,0"/>
                <TextBox x:Name="DeveloperNameTextBox" Grid.Row="1" Grid.Column="3" Padding="5"/>
            </Grid>
        </GroupBox>
        
        <!-- 第三部分：生成选项 -->
        <GroupBox Header="3. 生成选项" Grid.Row="2" Margin="0,0,0,10">
            <StackPanel Margin="10">
                <Grid Margin="0,0,0,5">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column="0" Text="输出目录：" VerticalAlignment="Center" Margin="0,0,5,0"/>
                    <TextBox x:Name="D3OutputPathTextBox" Grid.Column="1" IsReadOnly="True" Padding="5"/>
                    <Button Grid.Column="2" Content="📁" Padding="10,5" Margin="5,0,0,0" 
                            Click="BrowseD3Output_Click"/>
                </Grid>
                
                <Grid Margin="0,0,0,5">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column="0" Text="命名空间：" VerticalAlignment="Center" Margin="0,0,5,0"/>
                    <TextBox x:Name="D3NamespaceTextBox" Grid.Column="1" 
                             Text="BR.ECS.DeviceDriver.Generated" Padding="5"/>
                </Grid>
                
                <CheckBox x:Name="GenerateTestConsoleCheckBox" 
                          Content="生成测试控制台项目（可选）" 
                          IsChecked="True"/>
            </StackPanel>
        </GroupBox>
        
        <!-- 第四部分：特性方法预览 -->
        <GroupBox Header="4. 特性方法预览" Grid.Row="3" Margin="0,0,0,10">
            <DataGrid x:Name="FeatureMethodsDataGrid"
                      AutoGenerateColumns="False"
                      IsReadOnly="True"
                      HeadersVisibility="Column"
                      GridLinesVisibility="Horizontal"
                      AlternatingRowBackground="#F8F9FA">
                <DataGrid.Columns>
                    <DataGridTextColumn Header="特性名称" Binding="{Binding FeatureName}" Width="150"/>
                    <DataGridTextColumn Header="方法名称" Binding="{Binding MethodName}" Width="200"/>
                    <DataGridTextColumn Header="类型" Binding="{Binding MethodType}" Width="100"/>
                    <DataGridTextColumn Header="返回值" Binding="{Binding ReturnType}" Width="100"/>
                    <DataGridTextColumn Header="说明" Binding="{Binding Description}" Width="*"/>
                </DataGrid.Columns>
            </DataGrid>
        </GroupBox>
        
        <!-- 操作按钮 -->
        <Grid Grid.Row="4">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            
            <TextBlock x:Name="D3StatusText" 
                       Grid.Column="0"
                       Text="就绪" 
                       VerticalAlignment="Center"
                       Foreground="#27ae60"/>
            
            <Button Grid.Column="1"
                    Content="🗂️ 打开输出文件夹"
                    Padding="15,8"
                    Margin="0,0,5,0"
                    Background="#16a085"
                    Foreground="White"
                    BorderThickness="0"
                    Click="OpenD3OutputFolder_Click"/>
            
            <Button x:Name="GenerateD3DriverButton" 
                    Grid.Column="2"
                    Content="⚡ 生成D3驱动" 
                    Padding="20,10"
                    Background="#27ae60"
                    Foreground="White"
                    BorderThickness="0"
                    FontWeight="Bold"
                    Click="GenerateD3Driver_Click"/>
        </Grid>
    </Grid>
</TabItem>
```

### 2.2 修改 MainWindow.xaml.cs

添加 D3 驱动生成相关的事件处理方法和字段：

```csharp
#region D3 Driver Generation Tab

private List<ClientFeatureInfo> _detectedFeatures = new();
private string _clientCodePath = string.Empty;
private string _d3OutputPath = string.Empty;
private D3DriverGeneratorService? _d3DriverGenerator;

private void BrowseClientCode_Click(object sender, RoutedEventArgs e)
{
    using var dialog = new WinForms.FolderBrowserDialog
    {
        Description = "选择客户端代码目录",
        UseDescriptionForTitle = true,
        ShowNewFolderButton = false
    };

    if (dialog.ShowDialog() == WinForms.DialogResult.OK)
    {
        _clientCodePath = dialog.SelectedPath;
        ClientCodePathTextBox.Text = _clientCodePath;
        
        // 自动检测特性
        AnalyzeClientCode();
    }
}

private void BrowseD3Output_Click(object sender, RoutedEventArgs e)
{
    using var dialog = new WinForms.FolderBrowserDialog
    {
        Description = "选择D3驱动输出目录",
        UseDescriptionForTitle = true,
        ShowNewFolderButton = true
    };

    if (dialog.ShowDialog() == WinForms.DialogResult.OK)
    {
        _d3OutputPath = dialog.SelectedPath;
        D3OutputPathTextBox.Text = _d3OutputPath;
    }
}

private void AnalyzeClientCode()
{
    try
    {
        UpdateD3Status("正在分析客户端代码...", StatusType.Info);
        
        var analyzer = new ClientCodeAnalyzer();
        var analysisResult = analyzer.Analyze(_clientCodePath);
        
        _detectedFeatures = analysisResult.Features;
        
        // 更新检测到的特性文本
        var featureNames = string.Join(", ", _detectedFeatures.Select(f => f.FeatureName));
        DetectedFeaturesText.Text = $"检测到的特性: {featureNames} ({_detectedFeatures.Count}个)";
        
        // 更新预览表格
        var previewData = analysisResult.GetMethodPreviewData();
        FeatureMethodsDataGrid.ItemsSource = previewData;
        
        UpdateD3Status($"成功分析 {_detectedFeatures.Count} 个特性", StatusType.Success);
        GenerateD3DriverButton.IsEnabled = _detectedFeatures.Any();
    }
    catch (Exception ex)
    {
        UpdateD3Status("分析失败", StatusType.Error);
        MessageBox.Show($"分析客户端代码失败：\n\n{ex.Message}", 
            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

private async void GenerateD3Driver_Click(object sender, RoutedEventArgs e)
{
    if (!_detectedFeatures.Any())
    {
        MessageBox.Show("请先选择客户端代码目录", "提示", 
            MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }
    
    // 验证设备信息
    if (string.IsNullOrWhiteSpace(DeviceBrandTextBox.Text) ||
        string.IsNullOrWhiteSpace(DeviceModelTextBox.Text))
    {
        MessageBox.Show("请填写设备品牌和型号", "提示", 
            MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }
    
    // 确保输出目录
    if (string.IsNullOrWhiteSpace(_d3OutputPath))
    {
        _d3OutputPath = Path.Combine(
            Path.GetTempPath(), 
            "SiLA2_D3Driver", 
            $"{DeviceBrandTextBox.Text}_{DeviceModelTextBox.Text}_{DateTime.Now:yyyyMMdd_HHmmss}");
        D3OutputPathTextBox.Text = _d3OutputPath;
    }
    
    GenerateD3DriverButton.IsEnabled = false;
    UpdateD3Status("正在生成D3驱动...", StatusType.Info);
    
    try
    {
        var config = new D3DriverGenerationConfig
        {
            Brand = DeviceBrandTextBox.Text.Trim(),
            Model = DeviceModelTextBox.Text.Trim(),
            DeviceType = DeviceTypeTextBox.Text.Trim(),
            Developer = DeveloperNameTextBox.Text.Trim(),
            Namespace = D3NamespaceTextBox.Text.Trim(),
            OutputPath = _d3OutputPath,
            ClientCodePath = _clientCodePath,
            Features = _detectedFeatures,
            GenerateTestConsole = GenerateTestConsoleCheckBox.IsChecked == true
        };
        
        _d3DriverGenerator = new D3DriverGeneratorService();
        
        var result = await Task.Run(() => _d3DriverGenerator.Generate(
            config,
            message => Dispatcher.Invoke(() => UpdateD3Status(message, StatusType.Info))));
        
        if (result.Success)
        {
            UpdateD3Status($"✓ {result.Message}", StatusType.Success);
            
            var dialogResult = MessageBox.Show(
                $"D3驱动生成完成！\n\n输出目录: {_d3OutputPath}\n\n是否打开输出文件夹？",
                "生成成功",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            
            if (dialogResult == MessageBoxResult.Yes)
            {
                OpenDirectory(_d3OutputPath);
            }
        }
        else
        {
            UpdateD3Status($"✗ 生成失败", StatusType.Error);
            MessageBox.Show($"生成失败！\n\n{result.Message}", 
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    catch (Exception ex)
    {
        UpdateD3Status("✗ 生成过程中发生错误", StatusType.Error);
        MessageBox.Show($"发生未预期的错误:\n\n{ex.Message}\n\n{ex.StackTrace}", 
            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
    finally
    {
        GenerateD3DriverButton.IsEnabled = true;
    }
}

private void OpenD3OutputFolder_Click(object sender, RoutedEventArgs e)
{
    if (!string.IsNullOrEmpty(_d3OutputPath) && Directory.Exists(_d3OutputPath))
    {
        OpenDirectory(_d3OutputPath);
    }
    else
    {
        MessageBox.Show("输出目录不存在，请先生成驱动代码", "提示", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

private void UpdateD3Status(string message, StatusType type = StatusType.Info)
{
    D3StatusText.Text = message;
    D3StatusText.Foreground = type switch
    {
        StatusType.Success => new SolidColorBrush(Color.FromRgb(39, 174, 96)),
        StatusType.Warning => new SolidColorBrush(Color.FromRgb(243, 156, 18)),
        StatusType.Error => new SolidColorBrush(Color.FromRgb(231, 76, 60)),
        _ => new SolidColorBrush(Color.FromRgb(127, 140, 141))
    };
}

#endregion
```

## 三、创建 D3 驱动生成服务

### 3.1 新建 `Services/D3DriverGeneratorService.cs`

**核心功能：**
1. 解析 Tecan 生成的客户端代码（反射分析）
2. 生成 AllSila2Client.cs（整合所有特性）
3. 生成 D3Driver.cs（D3 驱动类）
4. 生成 Sila2Base.cs（基类）
5. 生成 CommunicationPars.cs（通信参数）
6. 生成测试控制台项目（可选）

```csharp
public class D3DriverGeneratorService
{
    public GenerationResult Generate(
        D3DriverGenerationConfig config, 
        Action<string>? progressCallback = null)
    {
        try
        {
            progressCallback?.Invoke("创建输出目录结构...");
            CreateOutputDirectories(config);
            
            progressCallback?.Invoke("复制客户端代码文件...");
            CopyClientCode(config);
            
            progressCallback?.Invoke("生成 AllSila2Client.cs...");
            GenerateAllSila2Client(config);
            
            progressCallback?.Invoke("生成 Sila2Base.cs...");
            GenerateSila2Base(config);
            
            progressCallback?.Invoke("生成 CommunicationPars.cs...");
            GenerateCommunicationPars(config);
            
            progressCallback?.Invoke("生成 D3Driver.cs...");
            GenerateD3Driver(config);
            
            progressCallback?.Invoke("生成项目文件...");
            GenerateProjectFiles(config);
            
            if (config.GenerateTestConsole)
            {
                progressCallback?.Invoke("生成测试控制台...");
                GenerateTestConsole(config);
            }
            
            progressCallback?.Invoke("生成解决方案文件...");
            GenerateSolutionFile(config);
            
            return new GenerationResult
            {
                Success = true,
                Message = $"成功生成 D3 驱动（{config.Features.Count} 个特性）"
            };
        }
        catch (Exception ex)
        {
            return new GenerationResult
            {
                Success = false,
                Message = ex.Message,
                ErrorDetails = ex.StackTrace
            };
        }
    }
    
    private void CreateOutputDirectories(D3DriverGenerationConfig config)
    {
        Directory.CreateDirectory(config.OutputPath);
        Directory.CreateDirectory(Path.Combine(config.OutputPath, "Sila2Client"));
        Directory.CreateDirectory(Path.Combine(config.OutputPath, "lib"));
    }
    
    private void GenerateAllSila2Client(D3DriverGenerationConfig config)
    {
        var generator = new AllSila2ClientGenerator();
        var outputPath = Path.Combine(config.OutputPath, "AllSila2Client.cs");
        generator.Generate(config.Features, outputPath, config.Namespace);
    }
    
    private void GenerateSila2Base(D3DriverGenerationConfig config)
    {
        var generator = new Sila2BaseGenerator();
        var outputPath = Path.Combine(config.OutputPath, "Sila2Base.cs");
        generator.Generate(outputPath, config.Namespace);
    }
    
    private void GenerateCommunicationPars(D3DriverGenerationConfig config)
    {
        var generator = new CommunicationParsGenerator();
        var outputPath = Path.Combine(config.OutputPath, "CommunicationPars.cs");
        generator.Generate(outputPath, config.Namespace);
    }
    
    private void GenerateD3Driver(D3DriverGenerationConfig config)
    {
        var generator = new D3DriverGenerator();
        var outputPath = Path.Combine(config.OutputPath, "D3Driver.cs");
        
        // 收集所有方法信息
        var methods = CollectAllMethods(config.Features);
        
        generator.Generate(config, methods, outputPath, config.Namespace);
    }
    
    private List<MethodGenerationInfo> CollectAllMethods(List<ClientFeatureInfo> features)
    {
        var methods = new List<MethodGenerationInfo>();
        var methodNameCount = new Dictionary<string, int>();
        
        // 统计方法名出现次数
        foreach (var feature in features)
        {
            foreach (var method in feature.Methods)
            {
                if (!methodNameCount.ContainsKey(method.Name))
                    methodNameCount[method.Name] = 0;
                methodNameCount[method.Name]++;
            }
        }
        
        // 生成方法信息
        foreach (var feature in features)
        {
            foreach (var method in feature.Methods)
            {
                var finalName = method.Name;
                
                // 处理命名冲突
                if (methodNameCount[method.Name] > 1)
                {
                    finalName = $"{feature.FeatureName}_{method.Name}";
                }
                
                methods.Add(new MethodGenerationInfo
                {
                    Name = finalName,
                    OriginalName = method.Name,
                    ReturnType = method.ReturnType,
                    Parameters = method.Parameters,
                    Description = method.Description,
                    Category = DetermineMethodCategory(method),
                    IsProperty = method.IsProperty,
                    IsObservableCommand = method.IsObservableCommand,
                    FeatureName = feature.FeatureName
                });
            }
        }
        
        return methods;
    }
}
```

### 3.2 新建 `Services/ClientCodeAnalyzer.cs`

**功能：**分析客户端代码，提取特性和方法信息（含 XML 注释）

```csharp
public class ClientCodeAnalyzer
{
    private XDocument _xmlDocumentation;
    
    public ClientAnalysisResult Analyze(string clientCodePath)
    {
        var result = new ClientAnalysisResult();
        
        // 1. 查找所有接口文件 (I*.cs)
        var interfaceFiles = Directory.GetFiles(clientCodePath, "I*.cs");
        
        // 2. 查找所有客户端文件 (*Client.cs)
        var clientFiles = Directory.GetFiles(clientCodePath, "*Client.cs");
        
        // 3. 编译成 DLL（含 XML 文档）
        var (dllPath, xmlDocPath) = CompileToAssembly(clientCodePath, interfaceFiles, clientFiles);
        
        // 4. 加载 XML 文档注释
        if (File.Exists(xmlDocPath))
        {
            _xmlDocumentation = XDocument.Load(xmlDocPath);
        }
        
        // 5. 加载程序集
        var assembly = Assembly.LoadFrom(dllPath);
        
        // 6. 分析所有接口
        var interfaceTypes = assembly.GetTypes()
            .Where(t => t.IsInterface && t.GetCustomAttribute<SilaFeatureAttribute>() != null);
        
        foreach (var interfaceType in interfaceTypes)
        {
            var featureInfo = AnalyzeFeature(interfaceType);
            result.Features.Add(featureInfo);
        }
        
        return result;
    }
    
    private ClientFeatureInfo AnalyzeFeature(Type interfaceType)
    {
        var featureAttr = interfaceType.GetCustomAttribute<SilaFeatureAttribute>();
        var identifierAttr = interfaceType.GetCustomAttribute<SilaIdentifierAttribute>();
        
        var featureInfo = new ClientFeatureInfo
        {
            InterfaceType = interfaceType,
            FeatureName = identifierAttr?.Identifier ?? interfaceType.Name.TrimStart('I'),
            InterfaceName = interfaceType.Name,
            Methods = new List<MethodGenerationInfo>()
        };
        
        // 分析属性
        foreach (var property in interfaceType.GetProperties())
        {
            var method = new MethodGenerationInfo
            {
                Name = $"Get{property.Name}",
                IsProperty = true,
                PropertyName = property.Name,
                ReturnType = property.PropertyType,
                IsObservable = property.GetCustomAttribute<ObservableAttribute>() != null,
                Description = ExtractXmlComment(property),
                XmlDocumentation = GetXmlDocumentation(property)  // ⭐ 提取完整 XML 文档
            };
            featureInfo.Methods.Add(method);
        }
        
        // 分析方法
        foreach (var method in interfaceType.GetMethods().Where(m => !m.IsSpecialName))
        {
            var methodInfo = new MethodGenerationInfo
            {
                Name = method.Name,
                IsProperty = false,
                ReturnType = method.ReturnType,
                IsObservableCommand = IsObservableCommand(method.ReturnType),
                Parameters = method.GetParameters().Select(p => new ParameterInfo
                {
                    Name = p.Name ?? "param",
                    Type = p.ParameterType,
                    Description = ExtractParameterDescription(p),
                    XmlDocumentation = GetXmlDocumentation(p)  // ⭐ 提取参数文档
                }).ToList(),
                Description = ExtractXmlComment(method),
                XmlDocumentation = GetXmlDocumentation(method)  // ⭐ 提取完整 XML 文档
            };
            
            // ⭐ 检测不支持的类型，标记需要 JSON 参数
            foreach (var param in methodInfo.Parameters)
            {
                if (!IsSupportedType(param.Type))
                {
                    param.RequiresJsonParameter = true;
                }
            }
            
            if (!IsSupportedType(methodInfo.ReturnType))
            {
                methodInfo.RequiresJsonReturn = true;
            }
            
            featureInfo.Methods.Add(methodInfo);
        }
        
        return featureInfo;
    }
    
    /// <summary>
    /// 从 XML 文档中提取成员的注释
    /// </summary>
    private XmlDocumentationInfo GetXmlDocumentation(MemberInfo member)
    {
        if (_xmlDocumentation == null)
            return null;
        
        var memberName = GetXmlMemberName(member);
        var memberElement = _xmlDocumentation.Descendants("member")
            .FirstOrDefault(m => m.Attribute("name")?.Value == memberName);
        
        if (memberElement == null)
            return null;
        
        return new XmlDocumentationInfo
        {
            Summary = memberElement.Element("summary")?.Value.Trim(),
            Remarks = memberElement.Element("remarks")?.Value.Trim(),
            Returns = memberElement.Element("returns")?.Value.Trim(),
            Parameters = memberElement.Elements("param")
                .ToDictionary(
                    p => p.Attribute("name")?.Value ?? string.Empty,
                    p => p.Value.Trim()
                )
        };
    }
    
    /// <summary>
    /// 生成 XML 文档的成员名称（如：M:Namespace.Class.Method）
    /// </summary>
    private string GetXmlMemberName(MemberInfo member)
    {
        var prefix = member.MemberType switch
        {
            MemberTypes.Method => "M:",
            MemberTypes.Property => "P:",
            MemberTypes.Field => "F:",
            MemberTypes.TypeInfo => "T:",
            _ => ""
        };
        
        return $"{prefix}{member.DeclaringType.FullName}.{member.Name}";
    }
    
    /// <summary>
    /// 检查类型是否为支持的类型
    /// </summary>
    private bool IsSupportedType(Type type)
    {
        var supportedTypes = new[]
        {
            typeof(int), typeof(byte), typeof(sbyte), typeof(string),
            typeof(DateTime), typeof(double), typeof(float), typeof(bool),
            typeof(byte[])
        };
        
        if (supportedTypes.Contains(type))
            return true;
        
        if (type.IsEnum)
            return true;
        
        if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
        {
            var elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
            return supportedTypes.Contains(elementType);
        }
        
        if (type.IsClass || type.IsValueType)
        {
            // 检查是否只包含基础类型（不嵌套）
            return ValidateSimpleCompositeType(type);
        }
        
        return false;
    }
    
    private bool IsObservableCommand(Type returnType)
    {
        if (returnType == typeof(IObservableCommand))
            return true;
        
        if (returnType.IsGenericType && 
            returnType.GetGenericTypeDefinition() == typeof(IObservableCommand<>))
            return true;
        
        return false;
    }
    
    private (string dllPath, string xmlDocPath) CompileToAssembly(
        string basePath, 
        string[] interfaceFiles, 
        string[] clientFiles)
    {
        // 使用 MSBuild 编译（生成 XML 文档）
        // 或使用 Roslyn，配置生成 XML 文档
        var compilation = CSharpCompilation.Create(
            "TempClientAnalysis",
            interfaceFiles.Concat(clientFiles).Select(f => 
                CSharpSyntaxTree.ParseText(File.ReadAllText(f))),
            new[] 
            { 
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IObservableCommand).Assembly.Location)
            },
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                xmlReferenceResolver: null)
            );
        
        var dllPath = Path.Combine(Path.GetTempPath(), "TempClientAnalysis.dll");
        var xmlDocPath = Path.Combine(Path.GetTempPath(), "TempClientAnalysis.xml");
        
        // ⭐ 同时生成 XML 文档
        using var dllStream = new FileStream(dllPath, FileMode.Create);
        using var xmlStream = new FileStream(xmlDocPath, FileMode.Create);
        
        var emitResult = compilation.Emit(
            dllStream,
            xmlDocumentationStream: xmlStream);
        
        if (!emitResult.Success)
        {
            var errors = string.Join("\n", emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            throw new Exception($"编译客户端代码失败：\n{errors}");
        }
        
        return (dllPath, xmlDocPath);
    }
}

/// <summary>
/// XML 文档信息
/// </summary>
public class XmlDocumentationInfo
{
    public string Summary { get; set; }
    public string Remarks { get; set; }
    public string Returns { get; set; }
    public Dictionary<string, string> Parameters { get; set; }
}
```

## 四、CodeDOM 生成器实现

### 4.1 新建 `Services/CodeDom/AllSila2ClientGenerator.cs`

参考 `BR.ECS.DeviceDriver.Sample.Test/AllSila2Client.cs` 生成 AllSila2Client.cs：

```csharp
public class AllSila2ClientGenerator
{
    public void Generate(
        List<ClientFeatureInfo> features,
        string outputPath,
        string namespaceName)
    {
        var codeUnit = new CodeCompileUnit();
        var codeNamespace = new CodeNamespace(namespaceName);
        
        // 导入命名空间
        codeNamespace.Imports.Add(new CodeNamespaceImport("System"));
        codeNamespace.Imports.Add(new CodeNamespaceImport("System.Collections.Generic"));
        codeNamespace.Imports.Add(new CodeNamespaceImport("System.Linq"));
        codeNamespace.Imports.Add(new CodeNamespaceImport("System.Reflection"));
        codeNamespace.Imports.Add(new CodeNamespaceImport("Tecan.Sila2.Client"));
        codeNamespace.Imports.Add(new CodeNamespaceImport("Tecan.Sila2.Client.ExecutionManagement"));
        codeNamespace.Imports.Add(new CodeNamespaceImport("Tecan.Sila2.Discovery"));
        codeNamespace.Imports.Add(new CodeNamespaceImport("Tecan.Sila2.Locking"));
        codeNamespace.Imports.Add(new CodeNamespaceImport("BR.PC.Device.Sila2Discovery"));
        codeNamespace.Imports.Add(new CodeNamespaceImport("Sila2Client"));
        
        // 创建类
        var clientClass = new CodeTypeDeclaration("AllSila2Client");
        clientClass.IsClass = true;
        clientClass.TypeAttributes = TypeAttributes.Public;
        
        // 添加字段
        AddFields(clientClass, features);
        
        // 添加构造函数
        AddConstructor(clientClass);
        
        // 添加 Connect 方法
        AddConnectMethod(clientClass, features);
        
        // 添加 Disconnect 方法
        AddDisconnectMethod(clientClass);
        
        // 添加所有平铺方法
        AddFlattenedMethods(clientClass, features);
        
        // 添加连接状态事件
        var eventField = new CodeMemberField(typeof(Action<bool>), "OnConnectionStatusChanged");
        eventField.Attributes = MemberAttributes.Public;
        clientClass.Members.Add(eventField);
        
        // 添加 DiscoverFactories 方法
        AddDiscoverFactoriesMethod(clientClass);
        
        codeNamespace.Types.Add(clientClass);
        codeUnit.Namespaces.Add(codeNamespace);
        
        // 生成代码文件
        using var writer = new StreamWriter(outputPath);
        var provider = CodeDomProvider.CreateProvider("CSharp");
        var options = new CodeGeneratorOptions 
        { 
            BracingStyle = "C",
            IndentString = "    "
        };
        provider.GenerateCodeFromCompileUnit(codeUnit, writer, options);
    }
    
    private void AddFields(CodeTypeDeclaration clientClass, List<ClientFeatureInfo> features)
    {
        // 为每个特性添加客户端字段
        foreach (var feature in features)
        {
            var field = new CodeMemberField(
                feature.InterfaceName, 
                ToCamelCase(feature.InterfaceName));
            field.Attributes = MemberAttributes.Private;
            clientClass.Members.Add(field);
        }
        
        // 添加连接相关字段
        clientClass.Members.Add(new CodeMemberField(
            "Tecan.Sila2.Discovery.ServerConnector", "_connector"));
        clientClass.Members.Add(new CodeMemberField(
            "ExecutionManagerFactory", "executionManagerFactory"));
        clientClass.Members.Add(new CodeMemberField(
            "IEnumerable<Tecan.Sila2.ServerData>", "_servers"));
        clientClass.Members.Add(new CodeMemberField(
            "Tecan.Sila2.ServerData", "_server"));
    }
    
    private void AddFlattenedMethods(CodeTypeDeclaration clientClass, List<ClientFeatureInfo> features)
    {
        // 检测命名冲突
        var methodNames = new Dictionary<string, List<(string Feature, MethodGenerationInfo Method)>>();
        
        foreach (var feature in features)
        {
            foreach (var method in feature.Methods)
            {
                if (!methodNames.ContainsKey(method.Name))
                    methodNames[method.Name] = new List<(string, MethodGenerationInfo)>();
                methodNames[method.Name].Add((feature.FeatureName, method));
            }
        }
        
        // 生成方法
        foreach (var feature in features)
        {
            foreach (var method in feature.Methods)
            {
                var finalName = method.Name;
                if (methodNames[method.Name].Count > 1)
                {
                    // 命名冲突，添加前缀
                    finalName = $"{feature.FeatureName}_{method.Name}";
                }
                
                GenerateMethod(clientClass, feature, method, finalName);
            }
        }
    }
    
    private void GenerateMethod(
        CodeTypeDeclaration clientClass,
        ClientFeatureInfo feature,
        MethodGenerationInfo method,
        string finalName)
    {
        var codeMethod = new CodeMemberMethod();
        codeMethod.Name = finalName;
        codeMethod.Attributes = MemberAttributes.Public;
        
        // ⭐ 添加完整的 XML 注释（从 SiLA2 客户端代码集成）
        if (method.XmlDocumentation != null)
        {
            var xmlDoc = method.XmlDocumentation;
            
            // Summary
            if (!string.IsNullOrEmpty(xmlDoc.Summary))
            {
                codeMethod.Comments.Add(new CodeCommentStatement(
                    $"<summary>{xmlDoc.Summary}</summary>", true));
            }
            
            // Parameters
            foreach (var param in method.Parameters)
            {
                if (xmlDoc.Parameters != null && 
                    xmlDoc.Parameters.TryGetValue(param.Name, out var paramDoc))
                {
                    codeMethod.Comments.Add(new CodeCommentStatement(
                        $"<param name=\"{param.Name}\">{paramDoc}</param>", true));
                }
                
                // ⭐ 如果需要 JSON 参数，添加额外的参数注释
                if (param.RequiresJsonParameter)
                {
                    codeMethod.Comments.Add(new CodeCommentStatement(
                        $"<param name=\"{param.Name}JsonString\">JSON 字符串格式的 {param.Name}（可选，优先使用）</param>", 
                        true));
                }
            }
            
            // Returns
            if (!string.IsNullOrEmpty(xmlDoc.Returns))
            {
                var returnsDoc = xmlDoc.Returns;
                
                // ⭐ 如果返回类型不支持，添加提示
                if (method.RequiresJsonReturn)
                {
                    returnsDoc += " [注意：返回类型为复杂对象，建议使用 JSON 序列化]";
                }
                
                codeMethod.Comments.Add(new CodeCommentStatement(
                    $"<returns>{returnsDoc}</returns>", true));
            }
            
            // Remarks
            if (!string.IsNullOrEmpty(xmlDoc.Remarks))
            {
                codeMethod.Comments.Add(new CodeCommentStatement(
                    $"<remarks>{xmlDoc.Remarks}</remarks>", true));
            }
        }
        else if (!string.IsNullOrEmpty(method.Description))
        {
            // 回退：使用简单描述
            codeMethod.Comments.Add(new CodeCommentStatement(
                $"<summary>{method.Description}</summary>", true));
        }
        
        // 确定返回类型
        Type returnType = method.ReturnType;
        if (method.IsObservableCommand)
        {
            // IObservableCommand<T> -> T，IObservableCommand -> void
            if (method.ReturnType.IsGenericType)
            {
                returnType = method.ReturnType.GetGenericArguments()[0];
            }
            else
            {
                returnType = typeof(void);
            }
        }
        codeMethod.ReturnType = new CodeTypeReference(returnType);
        
        // ⭐ 添加参数（包括 JSON 参数）
        foreach (var param in method.Parameters)
        {
            codeMethod.Parameters.Add(new CodeParameterDeclarationExpression(
                param.Type, param.Name));
            
            // ⭐ 如果类型不支持，添加额外的 JSON 字符串参数
            if (param.RequiresJsonParameter)
            {
                codeMethod.Parameters.Add(new CodeParameterDeclarationExpression(
                    typeof(string), $"{param.Name}JsonString"));
            }
        }
        
        // 添加方法体
        if (method.IsProperty)
        {
            // 属性 getter：return _client.Property;
            GeneratePropertyGetBody(codeMethod, feature, method);
        }
        else if (method.IsObservableCommand)
        {
            // 可观察命令：阻塞等待
            GenerateObservableCommandBody(codeMethod, feature, method);
        }
        else
        {
            // 普通方法
            GenerateNormalMethodBody(codeMethod, feature, method);
        }
        
        clientClass.Members.Add(codeMethod);
    }
    
    private void GenerateObservableCommandBody(
        CodeMemberMethod codeMethod,
        ClientFeatureInfo feature,
        MethodGenerationInfo method)
    {
        // var command = _client.Method(...);
        var invokeExpression = new CodeMethodInvokeExpression(
            new CodeFieldReferenceExpression(null, ToCamelCase(feature.InterfaceName)),
            method.OriginalName,
            method.Parameters.Select(p => new CodeArgumentReferenceExpression(p.Name)).ToArray());
        
        var commandVar = new CodeVariableDeclarationStatement("var", "command", invokeExpression);
        codeMethod.Statements.Add(commandVar);
        
        // 阻塞等待：command.Response.GetAwaiter().GetResult()
        var awaitExpression = new CodeMethodInvokeExpression(
            new CodeMethodInvokeExpression(
                new CodePropertyReferenceExpression(
                    new CodeVariableReferenceExpression("command"),
                    "Response"),
                "GetAwaiter"),
            "GetResult");
        
        if (codeMethod.ReturnType.BaseType == "System.Void")
        {
            codeMethod.Statements.Add(new CodeExpressionStatement(awaitExpression));
        }
        else
        {
            codeMethod.Statements.Add(new CodeMethodReturnStatement(awaitExpression));
        }
    }
}
```

### 4.2 新建 `Services/CodeDom/D3DriverGenerator.cs`

参考 `BR.ECS.DeviceDriver.Sample.Test/D3Driver.cs` 生成 D3Driver.cs

### 4.3 新建 `Services/CodeDom/Sila2BaseGenerator.cs`

参考 `BR.ECS.DeviceDriver.Sample.Test/Sila2Base.cs` 生成 Sila2Base.cs

### 4.4 新建 `Services/CodeDom/CommunicationParsGenerator.cs`

参考 `BR.ECS.DeviceDriver.Sample.Test/CommunicationPars.cs` 生成 CommunicationPars.cs

### 4.5 新建 `Services/CodeDom/TestConsoleGenerator.cs`

生成简单的测试控制台壳子程序

## 五、输出项目结构

生成的 D3 驱动项目结构：

```
Output/{Brand}_{Model}_D3Driver_{Timestamp}/
├── AllSila2Client.cs                   # 中间封装类（整合所有特性）
├── D3Driver.cs                         # D3 驱动类
├── Sila2Base.cs                        # 基类
├── CommunicationPars.cs                # 通信参数
├── Sila2Client/                        # 复制的客户端代码
│   ├── ITemperatureController.cs
│   ├── TemperatureControllerClient.cs
│   ├── TemperatureControllerDtos.cs
│   └── ...
├── lib/                                # D3 依赖库
│   ├── BR.ECS.Executor.Device.Domain.Contracts.dll
│   ├── BR.ECS.Executor.Device.Domain.Share.dll
│   ├── BR.ECS.Executor.Device.Infrastructure.dll
│   └── BR.PC.Device.Sila2Discovery.dll
├── {Brand}{Model}.D3Driver.csproj      # 项目文件
├── TestConsole/                        # 测试控制台（可选）
│   ├── Program.cs
│   └── TestConsole.csproj
└── {Brand}{Model}.sln                  # 解决方案文件
```

## 六、用户操作流程

1. **切换到 "🎯 生成D3驱动" Tab**
2. **点击 "📁 浏览" 选择客户端代码目录**
   - 自动检测所有特性
   - 显示 "检测到的特性: TemperatureController, ShakingControl (2个)"
   - 在 DataGrid 中显示所有方法预览
3. **配置设备信息**
   - 品牌：`Bioyond`
   - 型号：`MD`
   - 类型：`Robot`
   - 开发者：`YourName`
4. **配置生成选项**
   - 输出目录：`C:\Output\Bioyond_MD_D3Driver_20250120`
   - 命名空间：`BR.ECS.DeviceDriver.Generated`
   - 勾选 "生成测试控制台项目"
5. **点击 "⚡ 生成D3驱动" 按钮**
   - 状态显示：正在生成...
   - 生成完成：弹出提示 "是否打开输出文件夹？"

## 七、实施顺序

### 阶段1：更新文档和 UI（0.5天）

- [ ] 更新 `项目描述与要求.md`，记录所有技术决策
- [ ] 在 `MainWindow.xaml` 添加第三个 TabItem
- [ ] 在 `MainWindow.xaml.cs` 添加事件处理方法和字段

### 阶段2：客户端代码分析（1天）

- [ ] 创建 `Services/ClientCodeAnalyzer.cs`
- [ ] 实现编译客户端代码到 DLL
- [ ] 实现反射分析接口和方法
- [ ] 实现特性识别（Observable、返回值类型）
- [ ] 实现命名冲突检测

### 阶段3：CodeDOM 生成器（2天）

- [ ] 创建 `Services/CodeDom/AllSila2ClientGenerator.cs`（重点）
- [ ] 创建 `Services/CodeDom/D3DriverGenerator.cs`
- [ ] 创建 `Services/CodeDom/Sila2BaseGenerator.cs`
- [ ] 创建 `Services/CodeDom/CommunicationParsGenerator.cs`
- [ ] 创建 `Services/CodeDom/TestConsoleGenerator.cs`

### 阶段4：服务类和集成（1天）

- [ ] 创建 `Services/D3DriverGeneratorService.cs`
- [ ] 实现完整生成流程
- [ ] 实现项目文件和解决方案文件生成
- [ ] 集成到 WPF UI

### 阶段5：测试和优化（1天）

- [ ] 端到端测试生成流程
- [ ] 验证生成的代码可编译运行
- [ ] 测试命名冲突处理
- [ ] 测试测试控制台
- [ ] 错误处理和友好提示
- [ ] 性能优化

### 总计：约 5.5 天

## 八、数据模型

### 8.1 ClientFeatureInfo

```csharp
public class ClientFeatureInfo
{
    public Type InterfaceType { get; set; }
    public string FeatureName { get; set; }
    public string InterfaceName { get; set; }
    public string ClientName { get; set; }
    public List<MethodGenerationInfo> Methods { get; set; }
}
```

### 8.2 MethodGenerationInfo

```csharp
public class MethodGenerationInfo
{
    public string Name { get; set; }
    public string OriginalName { get; set; }
    public Type ReturnType { get; set; }
    public List<ParameterInfo> Parameters { get; set; }
    public string Description { get; set; }
    public MethodCategory Category { get; set; }
    public bool IsProperty { get; set; }
    public string PropertyName { get; set; }
    public bool IsObservableCommand { get; set; }
    public bool IsObservable { get; set; }
    public string FeatureName { get; set; }
    
    // ⭐ 新增：XML 文档注释
    public XmlDocumentationInfo XmlDocumentation { get; set; }
    
    // ⭐ 新增：不支持类型标记
    public bool RequiresJsonReturn { get; set; }  // 返回值是否需要 JSON 处理
}

public class ParameterInfo
{
    public string Name { get; set; }
    public Type Type { get; set; }
    public string Description { get; set; }
    
    // ⭐ 新增：XML 文档注释
    public XmlDocumentationInfo XmlDocumentation { get; set; }
    
    // ⭐ 新增：不支持类型标记
    public bool RequiresJsonParameter { get; set; }  // 是否需要额外的 JSON 字符串参数
}

public enum MethodCategory
{
    Operations,      // MethodOperations
    Maintenance      // MethodMaintenance
}
```

### 8.3 D3DriverGenerationConfig

```csharp
public class D3DriverGenerationConfig
{
    public string Brand { get; set; }
    public string Model { get; set; }
    public string DeviceType { get; set; }
    public string Developer { get; set; }
    public string Namespace { get; set; }
    public string OutputPath { get; set; }
    public string ClientCodePath { get; set; }
    public List<ClientFeatureInfo> Features { get; set; }
    public bool GenerateTestConsole { get; set; }
}
```

## 九、关键技术点

### 9.1 AllSila2Client 方法平铺示例

```csharp
// 参考 BR.ECS.DeviceDriver.Sample.Test/AllSila2Client.cs

public class AllSila2Client
{
    ITemperatureController temperatureController;
    
    // 属性转为 Get 方法
    public double GetCurrentTemperature()
    {
        return temperatureController.CurrentTemperature;
    }
    
    // 可观察命令转为阻塞方法
    public void ControlTemperature(double targetTemperature)
    {
        var command = temperatureController.ControlTemperature(targetTemperature);
        command.Response.GetAwaiter().GetResult();
    }
    
    // 普通命令
    public void SwitchDeviceState(bool isOn)
    {
        temperatureController.SwitchDeviceState(isOn);
    }
}
```

### 9.2 命名冲突处理

```csharp
// 示例：两个特性都有 GetTemperature 方法
// TemperatureController.GetTemperature() -> GetTemperature()
// TemperatureSensor.GetTemperature() -> TemperatureSensor_GetTemperature()

private string ResolveMethodName(
    string originalName, 
    string featureName, 
    Dictionary<string, int> nameCount)
{
    if (nameCount[originalName] > 1)
    {
        return $"{featureName}_{originalName}";
    }
    return originalName;
}
```

### 9.3 可观察命令返回值处理

```csharp
// IObservableCommand -> void
// IObservableCommand<T> -> T

Type GetActualReturnType(Type observableCommandType)
{
    if (observableCommandType == typeof(IObservableCommand))
        return typeof(void);
    
    if (observableCommandType.IsGenericType && 
        observableCommandType.GetGenericTypeDefinition() == typeof(IObservableCommand<>))
        return observableCommandType.GetGenericArguments()[0];
    
    return observableCommandType;
}
```

## 十、错误处理和验证

1. **客户端代码目录验证**
   - 检查目录是否包含 `*Client.cs` 文件
   - 提示用户选择正确的目录

2. **编译错误处理**
   - 捕获编译错误并显示详细信息
   - 检查缺少的引用
   - 使用 MSBuild 而非 Roslyn 进行编译

3. **设备信息验证**
   - 品牌和型号不能为空
   - 只能包含字母、数字、下划线

4. **CodeDOM 生成错误**
   - 捕获生成异常
   - 提供详细的堆栈跟踪

5. **输出目录权限**
   - 检查是否有写入权限
   - 提示用户选择其他目录

## 十一、关键注意事项

### 11.1 架构设计原则

1. **MVVM 架构**
   - 使用 MVVM Toolkit（CommunityToolkit.Mvvm）实现
   - ViewModel 负责业务逻辑和数据绑定
   - View 仅负责 UI 展示
   - Service 负责代码生成核心逻辑

2. **不使用独立控制台应用**
   - 所有功能都集成在 WPF 界面的第三个 Tab 中
   - 通过 WPF UI 进行所有用户交互
   - 进度反馈通过界面上的状态文本显示

3. **测试控制台是可选的**
   - 用户可勾选是否生成测试控制台项目
   - 测试控制台仅是一个简单的壳子程序
   - 主要用于快速验证生成的驱动代码

### 11.2 核心实现要点

1. **AllSila2Client 是核心**
   - 这是整个方案的关键中间层
   - 必须正确实现方法平铺（属性转 Get 方法）
   - 必须正确处理命名冲突（添加 `FeatureName_` 前缀）
   - 必须正确处理可观察命令的阻塞等待

2. **参考示例代码**
   - `BR.ECS.DeviceDriver.Sample.Test/` 目录下所有文件都是生成目标的参考
   - 特别关注 `AllSila2Client.cs` 的实现方式
   - 严格按照示例的模式生成代码

3. **使用 CodeDOM 生成所有代码**
   - 不使用字符串拼接或模板引擎
   - 使用 System.CodeDom 命名空间下的类
   - 确保生成的代码格式良好、可读性强

4. **客户端代码分析方式**
   - 使用 MSBuild 编译客户端代码到 DLL
   - 使用反射分析编译后的程序集
   - 提取接口、方法、属性、特性（Attribute）信息

5. **⭐ 注释集成（重要）**
   - **必须从生成的 SiLA2 强类型客户端代码中提取 XML 注释**
   - 使用反射获取方法、属性、参数的 XML 文档注释
   - 将提取的注释集成到生成的 D3 驱动代码中
   - 确保生成的代码具有完整的智能提示和文档说明
   
   **注释提取方式：**
   ```csharp
   // 方法1：从 XML 文档文件读取
   var xmlDocPath = Path.ChangeExtension(assemblyPath, ".xml");
   var xmlDoc = XDocument.Load(xmlDocPath);
   
   // 方法2：从特性中读取（如果 Tecan Generator 生成了特性）
   var descriptionAttr = method.GetCustomAttribute<DescriptionAttribute>();
   
   // 方法3：从反射元数据中提取
   // 需要配合编译时生成的 XML 文档
   ```
   
   **生成的注释格式：**
   ```csharp
   /// <summary>
   /// 控制温度到指定目标值
   /// [原始注释来自 SiLA2 Feature Definition]
   /// </summary>
   /// <param name="targetTemperature">目标温度（摄氏度）</param>
   /// <returns>控制结果状态</returns>
   public void ControlTemperature(double targetTemperature)
   {
       // ...
   }
   ```

### 11.3 数据类型处理

**支持的基础类型：**
- 数值类型：`int`, `byte`, `sbyte`, `double`, `float`
- 字符串：`string`
- 时间：`DateTime`
- 布尔：`bool`
- 二进制：`byte[]`
- 枚举：`Enum`

**支持的复合类型：**
- 数组：`T[]`（T 必须是基础类型）
- 列表：`List<T>`（T 必须是基础类型）
- 简单类/结构：仅包含基础类型字段，不嵌套

**不支持的类型处理策略：**
- 嵌套的复杂对象
- 字典、集合等复杂泛型类型

**⚠️ 对于不支持的类型，采用以下处理方式：**

1. **入参处理**：在原有复杂类型参数基础上，额外添加一个 `string jsonString` 参数
   ```csharp
   // 原始方法签名：void Method(ComplexType complexParam)
   // 生成的方法签名：
   void Method(ComplexType complexParam, string complexParamJsonString)
   {
       // 优先使用 jsonString 反序列化
       var actualParam = string.IsNullOrEmpty(complexParamJsonString) 
           ? complexParam 
           : JsonConvert.DeserializeObject<ComplexType>(complexParamJsonString);
       
       _sila2Device.Method(actualParam);
   }
   ```

2. **返回值处理**：返回原类型，但在方法注释中说明可以使用 JSON 序列化
   ```csharp
   /// <summary>
   /// 获取复杂配置对象
   /// 注意：返回类型为复杂对象，建议使用 JsonConvert.SerializeObject() 序列化后使用
   /// </summary>
   public ComplexType GetComplexConfig()
   {
       return _sila2Device.GetComplexConfig();
   }
   
   // 或者同时提供 JSON 版本：
   public string GetComplexConfigAsJson()
   {
       var result = _sila2Device.GetComplexConfig();
       return JsonConvert.SerializeObject(result);
   }
   ```

3. **类型检测逻辑**：
   ```csharp
   private bool IsSupportedType(Type type)
   {
       // 检查是否为支持的类型
       // 返回 false 时，触发 JSON 参数生成
   }
   ```

### 11.4 命名冲突解决策略

```csharp
// 示例场景：两个特性都有相同的方法名
// Feature A: TemperatureController.GetTemperature()
// Feature B: TemperatureSensor.GetTemperature()

// 解决方案：
// - 第一个出现的保持原名：GetTemperature()
// - 后续冲突的添加前缀：TemperatureSensor_GetTemperature()

// 实现逻辑：
// 1. 第一遍遍历所有特性，统计方法名出现次数
// 2. 第二遍生成代码时，如果方法名出现次数 > 1，则添加特性名前缀
```

### 11.5 可观察命令处理

```csharp
// SiLA2 可观察命令返回类型：
// - IObservableCommand（无返回值）
// - IObservableCommand<T>（返回类型 T）

// 生成的 D3 驱动方法：
// - IObservableCommand -> void
// - IObservableCommand<T> -> T

// 阻塞等待实现：
var command = _client.MethodName(params);
var result = command.Response.GetAwaiter().GetResult(); // 同步阻塞
// 或
var result = await command.Response; // 异步等待
```

### 11.6 不支持类型的 JSON 参数处理（重要）

**处理策略：在原参数基础上额外添加 JSON 字符串参数**

#### 11.6.1 入参处理示例

**原始 SiLA2 方法：**
```csharp
// 接口定义
public interface IDeviceControl
{
    void ConfigureDevice(ComplexConfig config);  // ComplexConfig 是不支持的复杂类型
}
```

**生成的 AllSila2Client 方法：**
```csharp
/// <summary>
/// 配置设备参数
/// </summary>
/// <param name="config">设备配置对象</param>
/// <param name="configJsonString">JSON 字符串格式的 config（可选，优先使用）</param>
public void ConfigureDevice(ComplexConfig config, string configJsonString)
{
    // 优先使用 JSON 字符串
    var actualConfig = string.IsNullOrEmpty(configJsonString) 
        ? config 
        : JsonConvert.DeserializeObject<ComplexConfig>(configJsonString);
    
    _deviceControlClient.ConfigureDevice(actualConfig);
}
```

**生成的 D3Driver 方法：**
```csharp
/// <summary>
/// 配置设备参数
/// </summary>
/// <param name="config">设备配置对象</param>
/// <param name="configJsonString">JSON 字符串格式的 config（可选，优先使用）</param>
[MethodOperations]
public void ConfigureDevice(ComplexConfig config, string configJsonString)
{
    _sila2Device.ConfigureDevice(config, configJsonString);
}
```

**用户调用方式：**
```csharp
// 方式1：直接传对象（如果 D3 支持）
driver.ConfigureDevice(myConfig, null);

// 方式2：传 JSON 字符串（推荐，适用于不支持的类型）
var json = JsonConvert.SerializeObject(myConfig);
driver.ConfigureDevice(null, json);
```

#### 11.6.2 返回值处理示例

**原始 SiLA2 方法：**
```csharp
public interface IDeviceControl
{
    ComplexStatus GetDeviceStatus();  // ComplexStatus 是不支持的复杂类型
}
```

**生成的 AllSila2Client 方法（保持原样，添加注释提示）：**
```csharp
/// <summary>
/// 获取设备状态
/// </summary>
/// <returns>设备状态对象 [注意：返回类型为复杂对象，建议使用 JSON 序列化]</returns>
public ComplexStatus GetDeviceStatus()
{
    return _deviceControlClient.GetDeviceStatus();
}

// ⭐ 可选：同时生成 JSON 版本
/// <summary>
/// 获取设备状态（JSON 格式）
/// </summary>
/// <returns>设备状态的 JSON 字符串</returns>
public string GetDeviceStatusAsJson()
{
    var result = _deviceControlClient.GetDeviceStatus();
    return JsonConvert.SerializeObject(result);
}
```

**生成的 D3Driver 方法：**
```csharp
/// <summary>
/// 获取设备状态
/// </summary>
/// <returns>设备状态对象 [注意：返回类型为复杂对象，建议使用 JSON 序列化]</returns>
[MethodOperations]
public ComplexStatus GetDeviceStatus()
{
    return _sila2Device.GetDeviceStatus();
}

// ⭐ 可选：JSON 版本
[MethodOperations]
public string GetDeviceStatusAsJson()
{
    return _sila2Device.GetDeviceStatusAsJson();
}
```

#### 11.6.3 CodeDOM 生成 JSON 参数的实现

```csharp
private void GenerateMethodWithJsonSupport(
    CodeMemberMethod codeMethod,
    MethodGenerationInfo method)
{
    // 1. 添加原始参数
    foreach (var param in method.Parameters)
    {
        codeMethod.Parameters.Add(new CodeParameterDeclarationExpression(
            param.Type, param.Name));
        
        // 2. 如果是不支持的类型，添加 JSON 参数
        if (param.RequiresJsonParameter)
        {
            codeMethod.Parameters.Add(new CodeParameterDeclarationExpression(
                typeof(string), $"{param.Name}JsonString"));
            
            // 3. 在方法体中添加 JSON 反序列化逻辑
            GenerateJsonDeserializationCode(codeMethod, param);
        }
    }
}

private void GenerateJsonDeserializationCode(
    CodeMemberMethod codeMethod,
    ParameterInfo param)
{
    // 生成代码：
    // var actualParam = string.IsNullOrEmpty(paramJsonString) 
    //     ? param 
    //     : JsonConvert.DeserializeObject<ParamType>(paramJsonString);
    
    var condition = new CodeConditionStatement(
        // 条件：string.IsNullOrEmpty(paramJsonString)
        new CodeMethodInvokeExpression(
            new CodeTypeReferenceExpression(typeof(string)),
            "IsNullOrEmpty",
            new CodeArgumentReferenceExpression($"{param.Name}JsonString")),
        
        // True 分支：使用原参数
        new CodeVariableDeclarationStatement(
            param.Type,
            $"actual{param.Name}",
            new CodeArgumentReferenceExpression(param.Name)),
        
        // False 分支：反序列化 JSON
        new CodeVariableDeclarationStatement(
            param.Type,
            $"actual{param.Name}",
            new CodeMethodInvokeExpression(
                new CodeTypeReferenceExpression(typeof(JsonConvert)),
                "DeserializeObject",
                new CodeTypeOfExpression(param.Type),
                new CodeArgumentReferenceExpression($"{param.Name}JsonString")))
    );
    
    codeMethod.Statements.Add(condition);
}
```

#### 11.6.4 需要添加的 NuGet 包引用

**生成的项目需要添加：**
- `Newtonsoft.Json` - JSON 序列化/反序列化

**在生成的代码中需要导入：**
```csharp
using Newtonsoft.Json;
```

### 11.6 项目引用关系

```
生成的项目结构：

TestConsole.csproj（可选）
  └─> 项目引用 D3Driver.csproj
        └─> 项目引用 Sila2Client.csproj（复制的客户端代码）
              └─> NuGet 包引用：
                    - Tecan.Sila2.Client.NetCore
                    - Tecan.Sila2.Features.Locking.Client
                    - BR.PC.Device.Sila2Discovery
                    - Newtonsoft.Json ⭐（用于不支持类型的 JSON 处理）
              └─> DLL 引用（lib 目录）：
                    - BR.ECS.Executor.Device.Domain.Contracts.dll
                    - BR.ECS.Executor.Device.Domain.Share.dll
                    - BR.ECS.Executor.Device.Infrastructure.dll
```

## 十二、实施 To-dos 列表

### 阶段1：基础准备（预计 0.5 天）
- [ ] 更新 `项目描述与要求.md`，整合所有技术决策和方案
- [ ] 在 `MainWindow.xaml` 添加第三个 TabItem "🎯 生成D3驱动"
- [ ] 创建 D3DriverViewModel（使用 MVVM Toolkit）
- [ ] 绑定 ViewModel 到 View

### 阶段2：数据模型和配置（预计 0.5 天）
- [ ] 创建 `Models/ClientFeatureInfo.cs` 数据模型
- [ ] 创建 `Models/MethodGenerationInfo.cs` 数据模型
- [ ] 创建 `Models/D3DriverGenerationConfig.cs` 配置模型
- [ ] 创建 `Models/GenerationResult.cs` 结果模型
- [ ] 创建 `Models/ClientAnalysisResult.cs` 分析结果模型

### 阶段3：客户端代码分析（预计 1 天）
- [ ] 创建 `Services/ClientCodeAnalyzer.cs` 服务类
- [ ] 实现使用 MSBuild 编译客户端代码到 DLL（同时生成 XML 文档）
- [ ] 实现加载和反射分析编译后的程序集
- [ ] ⭐ 实现 XML 文档注释提取（从生成的 XML 文件）
- [ ] ⭐ 实现 XmlDocumentationInfo 数据模型
- [ ] 实现接口和方法提取逻辑
- [ ] 实现属性识别和转换（属性 -> Get 方法）
- [ ] 实现可观察命令识别（IObservableCommand/IObservableCommand<T>）
- [ ] ⭐ 实现数据类型检测（IsSupportedType 方法）
- [ ] ⭐ 实现不支持类型标记（RequiresJsonParameter/RequiresJsonReturn）
- [ ] 实现方法命名冲突检测和统计
- [ ] 测试分析功能，验证提取的信息准确性（包括 XML 注释）

### 阶段4：CodeDOM 生成器实现（预计 2 天）
- [ ] 创建 `Services/CodeDom/AllSila2ClientGenerator.cs`
  - [ ] 实现类结构生成（字段、构造函数）
  - [ ] 实现 Connect 方法生成
  - [ ] 实现 Disconnect 方法生成
  - [ ] 实现 DiscoverFactories 方法生成
  - [ ] ⭐ 实现 XML 注释集成到生成的代码（Summary, Param, Returns, Remarks）
  - [ ] 实现属性 Get 方法生成
  - [ ] 实现普通命令方法生成
  - [ ] 实现可观察命令方法生成（含阻塞等待）
  - [ ] ⭐ 实现不支持类型的 JSON 参数生成（额外添加 jsonString 参数）
  - [ ] ⭐ 实现不支持返回类型的注释提示
  - [ ] 实现命名冲突处理（添加前缀）
- [ ] 创建 `Services/CodeDom/D3DriverGenerator.cs`
  - [ ] 生成 DeviceClass 特性
  - [ ] 生成继承 Sila2Base 的类
  - [ ] 生成 MethodOperations 和 MethodMaintenance 方法
  - [ ] ⭐ 集成 XML 注释到 D3 驱动方法
  - [ ] 生成方法调用到 AllSila2Client
  - [ ] ⭐ 处理 JSON 参数传递逻辑
- [ ] 创建 `Services/CodeDom/Sila2BaseGenerator.cs`
  - [ ] 生成抽象基类
  - [ ] 生成 Connect/Disconnect 方法
  - [ ] 生成 UpdateDeviceInfo 方法
  - [ ] 生成 ConnectionInfo 嵌套类
- [ ] 创建 `Services/CodeDom/CommunicationParsGenerator.cs`
  - [ ] 生成 IDeviceCommunication 实现
  - [ ] 生成 IP 和 Port 配置属性
- [ ] 创建 `Services/CodeDom/TestConsoleGenerator.cs`
  - [ ] 生成 Program.cs 壳子程序
  - [ ] 生成测试控制台项目文件

### 阶段5：核心服务集成（预计 1 天）
- [ ] 创建 `Services/D3DriverGeneratorService.cs` 主服务类
- [ ] 实现输出目录结构创建
- [ ] 实现客户端代码文件复制
- [ ] 实现调用各个 CodeDOM 生成器
- [ ] 实现项目文件（.csproj）生成
- [ ] 实现解决方案文件（.sln）生成
- [ ] 实现 lib 目录依赖 DLL 复制
- [ ] 实现进度回调机制
- [ ] 集成到 D3DriverViewModel

### 阶段6：WPF UI 完善（预计 0.5 天）
- [ ] 实现浏览客户端代码目录功能
- [ ] 实现浏览输出目录功能
- [ ] 实现自动检测特性并显示列表
- [ ] 实现方法预览 DataGrid 数据绑定
- [ ] 实现设备信息输入验证
- [ ] 实现生成按钮命令绑定
- [ ] 实现状态文本实时更新
- [ ] 实现打开输出文件夹功能

### 阶段7：测试和优化（预计 1 天）
- [ ] 端到端测试：选择客户端代码 -> 分析 -> 生成
- [ ] 验证生成的项目可编译通过
- [ ] 验证生成的驱动代码逻辑正确
- [ ] ⭐ 验证 XML 注释是否正确集成到生成的代码
- [ ] ⭐ 验证不支持类型的 JSON 参数是否正确生成
- [ ] ⭐ 测试不支持类型的方法是否可以正常调用（JSON 反序列化）
- [ ] 测试命名冲突处理是否正确
- [ ] 测试可观察命令阻塞等待是否正确
- [ ] 测试多特性整合是否正确
- [ ] 测试生成的测试控制台是否可运行
- [ ] 验证生成的代码智能提示（XML 文档注释）是否完整
- [ ] 错误处理和友好提示优化
- [ ] 性能优化（如有必要）
- [ ] 代码清理和注释完善

### 阶段8：最终验证（预计 0.5 天）
- [ ] 回顾所有技术决策是否正确实现
- [ ] 检查是否遵循 MVVM 架构
- [ ] 检查是否使用 CodeDOM 生成所有代码
- [ ] 检查生成的代码是否符合示例代码风格
- [ ] 检查用户体验是否流畅
- [ ] 准备演示和文档
- [ ] **最终确认：是否已经解决用户的所有需求**

---

**总计预估时间：约 5.5 - 6 天**

### To-dos 完成标准

每个 To-do 完成时应确保：
1. ✅ 代码无编译错误和警告
2. ✅ 代码符合 C# 最佳实践
3. ✅ 有必要的异常处理
4. ✅ 有清晰的注释说明
5. ✅ 通过基本功能测试

---

## 十三、快速参考

### 13.1 关键文件路径

**参考示例代码：**
- `BR.ECS.DeviceDriver.Sample.Test/AllSila2Client.cs` - 核心参考
- `BR.ECS.DeviceDriver.Sample.Test/D3Driver.cs` - D3驱动参考
- `BR.ECS.DeviceDriver.Sample.Test/Sila2Base.cs` - 基类参考
- `BR.ECS.DeviceDriver.Sample.Test/CommunicationPars.cs` - 通信参数参考

**需要创建的文件：**
- `SilaGeneratorWpf/ViewModels/D3DriverViewModel.cs`
- `SilaGeneratorWpf/Models/ClientFeatureInfo.cs`
- `SilaGeneratorWpf/Models/MethodGenerationInfo.cs`
- `SilaGeneratorWpf/Models/D3DriverGenerationConfig.cs`
- `SilaGeneratorWpf/Services/D3DriverGeneratorService.cs`
- `SilaGeneratorWpf/Services/ClientCodeAnalyzer.cs`
- `SilaGeneratorWpf/Services/CodeDom/AllSila2ClientGenerator.cs`
- `SilaGeneratorWpf/Services/CodeDom/D3DriverGenerator.cs`
- `SilaGeneratorWpf/Services/CodeDom/Sila2BaseGenerator.cs`
- `SilaGeneratorWpf/Services/CodeDom/CommunicationParsGenerator.cs`
- `SilaGeneratorWpf/Services/CodeDom/TestConsoleGenerator.cs`

### 13.2 关键 NuGet 包

**WPF 项目需要的包：**
- `CommunityToolkit.Mvvm` - MVVM Toolkit
- `Microsoft.CodeAnalysis.CSharp` - Roslyn（用于代码分析，可选）
- `System.CodeDom` - CodeDOM（.NET Framework 已内置）

**生成的项目需要的包：**
- `Tecan.Sila2.Client.NetCore` - Tecan 客户端库
- `Tecan.Sila2.Discovery` - 设备发现
- `BR.PC.Device.Sila2Discovery` - BR 扩展库

### 13.3 常用命令

**编译客户端代码：**
```bash
dotnet build ClientCode.csproj -o temp/bin
```

**反射加载程序集：**
```csharp
var assembly = Assembly.LoadFrom("path/to/client.dll");
```

**CodeDOM 生成代码：**
```csharp
var provider = CodeDomProvider.CreateProvider("CSharp");
var options = new CodeGeneratorOptions { BracingStyle = "C", IndentString = "    " };
provider.GenerateCodeFromCompileUnit(codeUnit, writer, options);
```
