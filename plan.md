# SiLA2 D3驱动生成工具实施计划

## 一、需求概述

在现有 WPF 项目 `Sila2DriverGen/SilaGeneratorWpf` 中添加第三个 Tab 页面 **"🎯 生成D3驱动"**，用于从 Tecan 生成的客户端代码自动生成 D3 驱动封装层。

### 1.1 技术方案确认

**已确定的技术决策：**
- ✅ 使用 Tecan Generator 生成客户端代码（前两个Tab已实现）
- ✅ 使用 `BR.PC.Device.Sila2Discovery` 扫描服务器和连接
- ✅ 可观察命令使用 `command.Response.GetAwaiter().GetResult()` 阻塞等待
- ✅ **通过 AllSila2Client 中间封装类整合多个特性**（命名冲突添加前缀 `FeatureName_Method`）
- ✅ 使用 CodeDOM 生成所有 D3 驱动代码
- ✅ 数据类型限制明确：int, byte, sbyte, string, DateTime, double, float, byte[], Enum, bool, List/Array（元素仅基础类型）, class/struct（仅包含基础类型，不嵌套）

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

**功能：**分析客户端代码，提取特性和方法信息

```csharp
public class ClientCodeAnalyzer
{
    public ClientAnalysisResult Analyze(string clientCodePath)
    {
        var result = new ClientAnalysisResult();
        
        // 1. 查找所有接口文件 (I*.cs)
        var interfaceFiles = Directory.GetFiles(clientCodePath, "I*.cs");
        
        // 2. 查找所有客户端文件 (*Client.cs)
        var clientFiles = Directory.GetFiles(clientCodePath, "*Client.cs");
        
        // 3. 编译成 DLL
        var dllPath = CompileToAssembly(clientCodePath, interfaceFiles, clientFiles);
        
        // 4. 加载程序集
        var assembly = Assembly.LoadFrom(dllPath);
        
        // 5. 分析所有接口
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
                Description = ExtractXmlComment(property)
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
                    Description = ExtractParameterDescription(p)
                }).ToList(),
                Description = ExtractXmlComment(method)
            };
            
            featureInfo.Methods.Add(methodInfo);
        }
        
        return featureInfo;
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
    
    private string CompileToAssembly(string basePath, string[] interfaceFiles, string[] clientFiles)
    {
        // 使用 Roslyn 编译所有 .cs 文件到 DLL
        var compilation = CSharpCompilation.Create(
            "TempClientAnalysis",
            interfaceFiles.Concat(clientFiles).Select(f => 
                CSharpSyntaxTree.ParseText(File.ReadAllText(f))),
            new[] 
            { 
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IObservableCommand).Assembly.Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        
        var dllPath = Path.Combine(Path.GetTempPath(), "TempClientAnalysis.dll");
        var emitResult = compilation.Emit(dllPath);
        
        if (!emitResult.Success)
        {
            var errors = string.Join("\n", emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            throw new Exception($"编译客户端代码失败：\n{errors}");
        }
        
        return dllPath;
    }
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
        
        // 添加 XML 注释
        if (!string.IsNullOrEmpty(method.Description))
        {
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
        
        // 添加参数
        foreach (var param in method.Parameters)
        {
            codeMethod.Parameters.Add(new CodeParameterDeclarationExpression(
                param.Type, param.Name));
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

3. **设备信息验证**
   - 品牌和型号不能为空
   - 只能包含字母、数字、下划线

4. **CodeDOM 生成错误**
   - 捕获生成异常
   - 提供详细的堆栈跟踪

5. **输出目录权限**
   - 检查是否有写入权限
   - 提示用户选择其他目录
