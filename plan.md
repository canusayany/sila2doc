# SiLA2 D3驱动生成工具 - 统一实施计划

## 零、项目背景与阶段说明

### 0.1 项目整体架构
本工具是"**一站式设备驱动生成工具**"，支持三种协议的驱动生成：
- **JsonRPC协议**（第一、二阶段）： 支持TCP JsonRPC通信的设备驱动生成
- **SiLA2/gRPC协议**（第三阶段）- **本计划聚焦此阶段**
- **统一D3系统集成**（第四阶段）- WPF可视化界面整合

### 0.2 本计划范围
当前计划专注于 **SiLA2协议的D3驱动生成**（对应需求文档第三阶段），包括：
- ✅ 在线SiLA2服务器发现与连接（通过mDNS/DNS-SD）
- ✅ 本地.sila.xml特性文件导入与管理
- ✅ 自动生成客户端代码（接口、DTOs、Client） 使用Tecan Generator
- ✅ 自动生成D3驱动封装层（AllSila2Client、Sila2Base、D3Driver） 使用CodeDOM
- ✅ 一键编译输出DLL和XML文档
- ✅ 符合D3系统调用规范（同步方法、方法分类、命名规则）

### 0.3 依赖的外部工具
- **Tecan Generator**：从.sila.xml文件生成C# gRPC客户端代码（接口、Client、DTOs）
- **BR.PC.Device.Sila2Discovery**：SiLA2服务器的mDNS扫描和连接管理
- **Microsoft CodeDOM**：生成D3驱动封装层代码（类型安全、XML注释）
- **.NET SDK**：项目编译和DLL生成
- **newtonsoft.json**: json序列化与反序列化

### 0.4 参考示例项目
- **BR.ECS.DeviceDriver.Sample.Test**：完整的D3驱动生成示例
  - `AllSila2Client.cs`：多特性整合、方法平铺、可观察命令阻塞化与结果获取,属性改方法(set,get)
  - `D3Driver.cs`：D3驱动主类、方法分类特性、XML注释,入参返回值不支持类型json转换
  - `Sila2Base.cs`：不需要做修改
  - `CommunicationPars.cs`：IP/Port配置管理

## 一、需求概述

在现有 WPF 项目 `Sila2DriverGen/SilaGeneratorWpf` 中添加第三个 Tab 页面 **"🎯 生成D3驱动"**，用于从在线SiLA2服务器或本地特性XML文件自动生成 D3 驱动封装层。

### 1.0 完整生成流程概览（已更新）

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. 扫描网络服务器的特性 或 导入本地特性文件                   │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │ 在线模式：mDNS扫描 → 连接服务器 → 获取特性列表            │   │
│   │ 本地模式：选择.sila.xml文件 → 添加到本地特性树          │   │
│   │ 父节点三态显示：未选/半选/全选                          │   │
│   │ 单服务器限制：只能选择同一服务器的特性                  │   │
│   └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ 2. 点击"生成D3项目"按钮                                       │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │ 弹出设备信息输入对话框                                     │   │
│   │ 用户输入：品牌、型号、设备类型、开发者                     │   │
│   │ 验证输入完整性                                             │   │
│   └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ 3. 生成强类型C#代码（使用Tecan Generator）                     │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │ 下载或读取.sila.xml文件                                     │   │
│   │ 调用Tecan Generator生成：                                  │   │
│   │   - Interface（接口定义）                                     │   │
│   │   - Client（客户端实现）                                     │   │
│   │   - DTOs（数据传输对象）                                      │   │
│   └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ 4. 分析生成的代码并提取方法信息                                 │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │ 反射分析接口和方法                                         │   │
│   │ 提取XML文档注释                                             │   │
│   │ 识别可观察命令和属性                                       │   │
│   │ 检测数据类型支持情况                                       │   │
│   └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ 5. 弹出"方法预览与特性调整"窗口                               │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │ 显示所有检测到的方法                                       │   │
│   │ 用户勾选"维护方法"（MethodMaintenance）                    │   │
│   │ 未勾选的默认为"调度方法"（MethodOperations）              │   │
│   │ 提供"全部设为维护/调度"快捷按钮                             │   │
│   │ 用户点击"确定"继续生成                                      │   │
│   └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ 6. 生成D3驱动代码文件（使用CodeDOM）                            │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │ AllSila2Client.cs - 多特性整合、方法平铺                  │   │
│   │ D3Driver.cs - 驱动主类、方法分类特性                       │   │
│   │ Sila2Base.cs - 基类实现（固定内容）                         │   │
│   │ CommunicationPars.cs - IP/Port配置（DeviceCommunicationItem）│
│   └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ 7. 填充CommunicationPars的IP和Port                               │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │ 在线服务器模式：使用ServerData中的实际IP和Port              │   │
│   │ 本地XML模式：使用默认值（192.168.1.100:50051）            │   │
│   │ 生成DeviceCommunicationItem格式的配置                      │   │
│   └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ 8. 生成项目文件和解决方案                                       │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │ 生成.csproj文件（包含所有依赖）                             │   │
│   │ 生成.sln文件                                                │   │
│   │ 显示项目路径和生成完成消息                                 │   │
│   └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ 9. 点击"编译D3项目"按钮（独立操作）                           │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │ 执行 dotnet build                                           │   │
│   │ 显示编译进度和结果                                         │   │
│   │ 如有错误，显示详细错误信息                                 │   │
│   └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ 10. 显示编译后的DLL                                              │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │ 显示DLL输出路径                                             │   │
│   │ 提供"打开DLL目录"按钮                                       │   │
│   │ 提供"调整方法特性"按钮（重新打开方法预览窗口）          │   │
│   │ 显示编译过程日志                                            │   │
│   └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### 1.1 核心架构决策（已更新）

- ✅ **采用 MVVM Toolkit** 实现 WPF 界面和业务逻辑分离
- ✅ **添加控制台测试项目** - 用于测试生成的驱动功能（非单元测试）
- ✅ **侧边栏选择特性** - 支持在线服务器和本地.sila.xml文件
- ✅ **单服务器特性选择限制** - 只能选择同一服务器的特性，跨服务器自动校验并取消
- ✅ **父节点三态显示** - 服务器节点显示未选/半选/全选状态
- ✅ **导出特性功能** - 导出选中的.sila.xml文件到指定文件夹
- ✅ **自动生成命名空间** - `BR.ECS.DeviceDrivers.{DeviceType}.{Brand}_{Model}`
- ✅ **自动生成输出目录** - 临时目录 `{Temp}/Sila2D3Gen/{Brand}_{Model}_{Timestamp}`
- ✅ **分离生成和编译** - 生成D3项目和编译D3项目是两个独立步骤
- ✅ **方法分类由用户控制** - 在独立的"方法预览与特性调整"窗口中勾选
- ✅ **设备信息对话框输入** - 点击生成时弹出对话框输入品牌、型号、类型、作者
- ✅ **方法预览窗口** - 在生成D3Driver.cs前弹出，用户确认后继续
- ✅ **实时过程日志** - 显示详细的生成和编译过程信息，支持错误高亮

### 1.2 技术方案

- ✅ 使用 Tecan Generator 生成客户端代码
- ✅ 使用 `BR.PC.Device.Sila2Discovery` 扫描和连接服务器
- ✅ 使用 CodeDOM 生成所有 D3 驱动代码
- ✅ **通过 AllSila2Client 中间封装类整合多个特性**（命名冲突添加前缀 `FeatureName_Method`）
- ✅ 可观察命令使用 `command.Response.GetAwaiter().GetResult()` 阻塞等待
- ✅ 数据类型限制：int, byte, sbyte, string, DateTime, double, float, byte[], Enum, bool, List/Array（元素仅基础类型）、class/struct（仅包含基础类型，不嵌套）
- ✅ 超出预期类型使用 JSON 序列化/反序列化
- ✅ 集成 XML 文档注释到生成的代码

## 二、最终UI设计

### 2.1 D3DriverView.xaml 布局（三列式 - 已更新）

```xml
<UserControl>
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="{Binding SidebarWidth}"/>  <!-- 侧边栏，可折叠 -->
            <ColumnDefinition Width="5"/>                        <!-- GridSplitter -->
            <ColumnDefinition Width="*"/>                        <!-- 主区域 -->
        </Grid.ColumnDefinitions>
        
        <!-- 侧边栏切换按钮（竖向三点，位于左边框） -->
        <ToggleButton Grid.Column="0" 
                      IsChecked="{Binding IsSidebarVisible}"
                      VerticalAlignment="Center"
                      HorizontalAlignment="Left"
                      Margin="-15,0,0,0"
                      Width="15" Height="60"
                      Content="⋮" 
                      FontSize="16"
                      ToolTip="切换侧边栏"/>
        
        <!-- 左侧栏：特性选择 -->
        <Border Grid.Column="0" BorderBrush="#bdc3c7" BorderThickness="0,0,1,0" 
                Visibility="{Binding IsSidebarVisible, Converter={StaticResource BoolToVisibilityConverter}}">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>  <!-- 工具栏 -->
                    <RowDefinition Height="250"/>   <!-- 在线服务器树（固定高度） -->
                    <RowDefinition Height="*"/>     <!-- 本地特性树（条件显示） -->
                </Grid.RowDefinitions>
                
                <!-- 工具栏 -->
                <StackPanel Orientation="Horizontal" Margin="5">
                    <Button Content="🔍 扫描服务器" Command="{Binding ScanServersCommand}"/>
                    <Button Content="📁 添加本地特性" Command="{Binding AddLocalFeaturesCommand}"/>
                    <Button Content="📤 导出特性" Command="{Binding ExportFeaturesCommand}" 
                            ToolTip="导出选中的特性文件到指定文件夹"/>
                </StackPanel>
                
                <!-- 在线服务器树（固定高度250px，支持父节点半选状态） -->
                <GroupBox Header="在线服务器" Grid.Row="1">
                    <TreeView Height="250" ItemsSource="{Binding OnlineServers}" 
                              VerticalScrollBarVisibility="Auto">
                        <!-- 服务器节点：CheckBox（三态） + ServerName + IP:Port -->
                        <!-- 特性节点：CheckBox + FeatureName + Identifier -->
                        <!-- 选择校验：只能选择同一服务器的特性 -->
                    </TreeView>
                </GroupBox>
                
                <!-- 本地特性树（条件显示，没有本地特性时隐藏） -->
                <GroupBox Header="本地特性" Grid.Row="2" 
                          Visibility="{Binding LocalNodes.Count, Converter={StaticResource CountToVisibilityConverter}}">
                    <TreeView ItemsSource="{Binding LocalNodes}">
                        <!-- CheckBox + Node + Files -->
                    </TreeView>
                </GroupBox>
            </Grid>
        </Border>
        
        <!-- GridSplitter -->
        <GridSplitter Grid.Column="1" Width="5" HorizontalAlignment="Stretch" 
                      Visibility="{Binding IsSidebarVisible, Converter={StaticResource BoolToVisibilityConverter}}"/>
        
        <!-- 主区域（移除方法预览） -->
        <Grid Grid.Column="2" Margin="10">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>  <!-- 过程日志 -->
                <RowDefinition Height="*"/>     <!-- 占位 -->
                <RowDefinition Height="Auto"/>  <!-- 操作按钮 -->
            </Grid.RowDefinitions>
            
            <!-- 过程信息显示区域 -->
            <Expander Header="生成过程信息" Grid.Row="0" IsExpanded="True">
                <TextBox Text="{Binding ProcessLog, Mode=OneWay}" 
                         IsReadOnly="True" 
                         Height="200" 
                         FontFamily="Consolas"
                         VerticalScrollBarVisibility="Auto"
                         Foreground="{Binding ProcessLogColor}"/>
            </Expander>
            
            <!-- 占位区域：显示项目信息 -->
            <GroupBox Header="项目信息" Grid.Row="1" Margin="0,10,0,10">
                <StackPanel>
                    <TextBlock Text="{Binding ProjectInfoText}" TextWrapping="Wrap"/>
                </StackPanel>
            </GroupBox>
            
            <!-- 操作按钮（5个） -->
            <WrapPanel Grid.Row="2" HorizontalAlignment="Right" Orientation="Horizontal">
                <Button Content="🗂️ 打开项目目录" 
                        Command="{Binding OpenProjectDirectoryCommand}"
                        Margin="5" Padding="10,5"
                        Background="#16a085" Foreground="White"/>
                
                <Button Content="📦 打开DLL目录" 
                        Command="{Binding OpenDllDirectoryCommand}"
                        Margin="5" Padding="10,5"
                        Background="#8e44ad" Foreground="White"/>
                
                <Button Content="✨ 生成D3项目" 
                        Command="{Binding GenerateD3ProjectCommand}"
                        Margin="5" Padding="10,5"
                        Background="#27ae60" Foreground="White"/>
                
                <Button Content="🔨 编译D3项目" 
                        Command="{Binding CompileD3ProjectCommand}"
                        Margin="5" Padding="10,5"
                        Background="#2980b9" Foreground="White"
                        IsEnabled="{Binding CanCompile}"/>
                
                <Button Content="🔧 调整方法特性" 
                        Command="{Binding AdjustMethodAttributesCommand}"
                        Margin="5" Padding="10,5"
                        Background="#e67e22" Foreground="White"
                        Visibility="{Binding CanAdjustMethods, Converter={StaticResource BoolToVisibilityConverter}}"/>
            </WrapPanel>
        </Grid>
    </Grid>
</UserControl>
```

### 2.2 关键UI特性（已更新）

1. **侧边栏切换按钮** - 竖向三点"⋮"，位于侧边栏左边框，使用ToggleButton
2. **在线服务器树固定高度** - 避免占用过多空间
3. **父节点三态显示** - 服务器节点使用三态CheckBox（未选/半选/全选）
4. **单服务器特性选择限制** - 只能选择同一服务器的特性，跨服务器选择时显示错误并取消勾选
5. **导出特性功能** - 导出选中的.sila.xml文件到指定文件夹
6. **本地特性树条件显示** - 没有本地特性时自动隐藏
7. **移除主界面方法预览** - 改为独立窗口"方法预览与特性调整"
8. **过程日志实时显示** - 展示详细的生成和编译步骤，支持错误高亮
9. **操作按钮（5个）** -  
   - 🗂️ 打开项目目录：打开生成的项目目录
   - 📦 打开DLL目录：打开编译输出的DLL目录
   - ✨ 生成D3项目：生成客户端代码和D3驱动代码（不编译）
   - 🔨 编译D3项目：编译已生成的项目（需要先生成）
   - 🔧 调整方法特性：打开方法预览与特性调整窗口（条件显示）

### 2.3 方法预览与特性调整窗口（新增）

**MethodPreviewWindow.xaml 设计**：

```xml
<Window x:Class="SilaGeneratorWpf.Views.MethodPreviewWindow"
        Title="方法预览与特性调整" 
        Width="1000" Height="600"
        WindowStartupLocation="CenterOwner">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>  <!-- 标题和说明 -->
            <RowDefinition Height="*"/>     <!-- 方法列表 -->
            <RowDefinition Height="Auto"/>  <!-- 操作按钮 -->
        </Grid.RowDefinitions>
        
        <!-- 标题和说明 -->
        <StackPanel Grid.Row="0" Margin="0,0,0,10">
            <TextBlock Text="方法预览与特性调整" FontSize="18" FontWeight="Bold"/>
            <TextBlock Text="请勾选需要标记为"维护方法"的方法，未勾选的默认为"调度方法"" 
                       Foreground="Gray" Margin="0,5,0,0"/>
            <Separator Margin="0,10,0,0"/>
        </StackPanel>
        
        <!-- 方法列表 -->
        <DataGrid Grid.Row="1" 
                  ItemsSource="{Binding MethodPreviewData}" 
                  AutoGenerateColumns="False"
                  CanUserAddRows="False"
                  CanUserDeleteRows="False"
                  SelectionMode="Extended">
            <DataGrid.Columns>
                <DataGridTextColumn Header="特性名称" Binding="{Binding FeatureName}" Width="150" IsReadOnly="True"/>
                <DataGridTextColumn Header="方法名称" Binding="{Binding MethodName}" Width="200" IsReadOnly="True"/>
                <DataGridTextColumn Header="类型" Binding="{Binding MethodType}" Width="100" IsReadOnly="True"/>
                <DataGridTextColumn Header="返回值" Binding="{Binding ReturnType}" Width="120" IsReadOnly="True"/>
                <DataGridCheckBoxColumn Header="维护方法" Binding="{Binding IsMaintenance, Mode=TwoWay}" Width="80"/>
                <DataGridTextColumn Header="说明" Binding="{Binding Description}" Width="*" IsReadOnly="True"/>
            </DataGrid.Columns>
        </DataGrid>
        
        <!-- 操作按钮 -->
        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,10,0,0">
            <Button Content="全部设为维护方法" Command="{Binding SetAllMaintenanceCommand}" 
                    Margin="5,0" Padding="10,5"/>
            <Button Content="全部设为调度方法" Command="{Binding SetAllOperationsCommand}" 
                    Margin="5,0" Padding="10,5"/>
            <Button Content="确定" Command="{Binding ConfirmCommand}" IsDefault="True"
                    Margin="5,0" Padding="20,5" Background="#27ae60" Foreground="White"/>
            <Button Content="取消" Command="{Binding CancelCommand}" IsCancel="True"
                    Margin="5,0" Padding="20,5"/>
        </StackPanel>
    </Grid>
</Window>
```

## 三、数据模型

### 3.1 ClientFeatureInfo

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

### 3.2 MethodGenerationInfo

```csharp
public class MethodGenerationInfo
{
    public string Name { get; set; }
    public string OriginalName { get; set; }
    public Type ReturnType { get; set; }
    public List<ParameterInfo> Parameters { get; set; }
    public string Description { get; set; }
    public bool IsProperty { get; set; }
    public string PropertyName { get; set; }
    public bool IsObservableCommand { get; set; }
    public bool IsObservable { get; set; }
    public string FeatureName { get; set; }
    
    // XML 文档注释
    public XmlDocumentationInfo XmlDocumentation { get; set; }
    
    // 不支持类型标识
    public bool RequiresJsonReturn { get; set; }
    
    // 方法分类（由用户在UI中勾选）
    public bool IsMaintenance { get; set; } = false;  // 默认为Operations
}

public class ParameterInfo
{
    public string Name { get; set; }
    public Type Type { get; set; }
    public string Description { get; set; }
    public XmlDocumentationInfo XmlDocumentation { get; set; }
    public bool RequiresJsonParameter { get; set; }
}

public class XmlDocumentationInfo
{
    public string Summary { get; set; }
    public string Remarks { get; set; }
    public string Returns { get; set; }
    public Dictionary<string, string> Parameters { get; set; }
}
```

### 3.3 D3DriverGenerationConfig

```csharp
public class D3DriverGenerationConfig
{
    public string Brand { get; set; }
    public string Model { get; set; }
    public string DeviceType { get; set; }
    public string Developer { get; set; }
    
    // 自动生成：BR.ECS.DeviceDrivers.{DeviceType}.{Brand}_{Model}
    public string Namespace { get; set; }
    
    // 自动生成：{Temp}/Sila2D3Gen/{Brand}_{Model}_{Timestamp}
    public string OutputPath { get; set; }
    
    public List<ClientFeatureInfo> Features { get; set; }
    
    // 阶段10：强制启用编译，不生成测试控制台
    // public bool GenerateTestConsole { get; set; } = false;  // 已删除
    // public bool AutoCompile { get; set; } = true;  // 已删除，强制启用
    
    // 特性来源
    public bool IsOnlineSource { get; set; }  // true=在线服务器，false=本地特性文件
    public string ServerUuid { get; set; }  // 在线服务器UUID
    public List<string> LocalFeatureXmlPaths { get; set; }  // 本地特性XML文件路径
    
    // 阶段10：服务器IP和Port
    public string ServerIp { get; set; }
    public int? ServerPort { get; set; }
}
```

### 3.4 MethodPreviewData

```csharp
public class MethodPreviewData
{
    public string FeatureName { get; set; }
    public string MethodName { get; set; }
    public string MethodType { get; set; }  // "Command", "Property", "ObservableCommand"
    public string ReturnType { get; set; }
    public string Description { get; set; }
    
    // 阶段10：用户勾选方法是否为维护方法
    public bool IsMaintenance { get; set; } = false;
}
```

### 3.5 LocalFeatureNodeViewModel

```csharp
public class LocalFeatureNodeViewModel : ObservableObject
{
    public string NodeName { get; set; }  // 节点名称（父文件夹名）
    public string NodePath { get; set; }  // 节点路径
    public ObservableCollection<LocalFeatureFileViewModel> Files { get; set; }
}

public class LocalFeatureFileViewModel : ObservableObject
{
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public string Identifier { get; set; }  // 特性标识符（从XML解析）
    public bool IsSelected { get; set; }
    public LocalFeatureNodeViewModel ParentNode { get; set; }
}
```

### 3.6 ServerNodeViewModel 扩展（新增 - 阶段10.1.1更新）

```csharp
public class ServerNodeViewModel : ObservableObject
{
    public string ServerName { get; set; }
    public string ServerUuid { get; set; }
    public string IpAddress { get; set; }
    public int Port { get; set; }
    public string DeviceType { get; set; }  // 设备类型（从服务器元数据获取）
    
    // 用于控制选择（用户点击父节点）
    [ObservableProperty]
    private bool isSelected;
    
    // 用于显示三态状态：false=未选，null=半选，true=全选
    // 默认为 false（未选择）
    [ObservableProperty]
    private bool? isPartiallySelected = false;
    
    // 特性列表
    public ObservableCollection<FeatureNodeViewModel> Features { get; set; }
    
    // 当用户点击父节点时，同步所有子节点
    partial void OnIsSelectedChanged(bool value)
    {
        foreach (var feature in Features)
        {
            feature.SilentSetSelection(value);
        }
        
        UpdatePartialSelection();
    }
    
    // 更新父节点的三态显示状态（由子节点选择变化触发）
    public void UpdatePartialSelection()
    {
        if (!Features.Any())
        {
            IsPartiallySelected = false;
            return;
        }

        var selectedCount = Features.Count(f => f.IsSelected);
        
        if (selectedCount == 0)
            IsPartiallySelected = false;  // 未选
        else if (selectedCount == Features.Count)
            IsPartiallySelected = true;  // 全选
        else
            IsPartiallySelected = null;  // 半选
    }
}

public class FeatureNodeViewModel : ObservableObject
{
    public string FeatureName { get; set; }
    public string FeatureIdentifier { get; set; }  // FQI (Fully Qualified Identifier)
    
    [ObservableProperty]
    private bool isSelected;
    
    public ServerNodeViewModel ParentServer { get; set; }
    
    // 当选择状态变化时，通知父节点更新状态
    partial void OnIsSelectedChanged(bool value)
    {
        ParentServer?.UpdateParentSelectionState();
    }
}
```

### 3.7 DeviceInfoDialogViewModel（新增）

用于弹出对话框输入设备信息：

```csharp
public class DeviceInfoDialogViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string brand;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string model;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string deviceType;
    
    [ObservableProperty]
    private string developer = "Bioyond";  // 默认值
    
    public bool DialogResult { get; private set; }
    
    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        DialogResult = true;
        CloseAction?.Invoke();
    }
    
    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        CloseAction?.Invoke();
    }
    
    private bool CanConfirm()
    {
        return !string.IsNullOrWhiteSpace(Brand) &&
               !string.IsNullOrWhiteSpace(Model) &&
               !string.IsNullOrWhiteSpace(DeviceType);
    }
    
    public Action CloseAction { get; set; }
}
```

### 3.8 MethodPreviewViewModel（新增）

用于方法预览与特性调整窗口：

```csharp
public class MethodPreviewViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<MethodPreviewData> methodPreviewData;
    
    public bool DialogResult { get; private set; }
    
    [RelayCommand]
    private void SetAllMaintenance()
    {
        foreach (var method in MethodPreviewData)
        {
            method.IsMaintenance = true;
        }
    }
    
    [RelayCommand]
    private void SetAllOperations()
    {
        foreach (var method in MethodPreviewData)
        {
            method.IsMaintenance = false;
        }
    }
    
    [RelayCommand]
    private void Confirm()
    {
        DialogResult = true;
        CloseAction?.Invoke();
    }
    
    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        CloseAction?.Invoke();
    }
    
    public Action CloseAction { get; set; }
}
```

### 3.9 DeviceClass特性规则

根据 `BR.ECS.DeviceDriver.Sample.Test/D3Driver.cs` 的详细注释：

```csharp
[DeviceClass(brand, model, injectionKey, deviceType, developer)]
```

**参数说明**：
1. **brand**（品牌）：必须使用英文、下划线、数字或组合
2. **model**（型号）：必须使用英文、下划线、数字或组合  
3. **injectionKey**（注入键）：通常为 `{Brand}{Model}`（无分隔符）
4. **deviceType**（设备类型）：必须使用英文、下划线、数字或组合
5. **developer**（开发者）：开发者名称

**示例**：
```csharp
[DeviceClass("Bioyond", "MD", "BioyondMD", "Robot", "Name")]
public class D3Driver : Sila2Base
{
    // ...
}
```

**生成规则**：
```csharp
public CodeAttributeDeclaration GenerateDeviceClassAttribute(D3DriverGenerationConfig config)
{
    var injectionKey = $"{config.Brand}{config.Model}";
    
    return new CodeAttributeDeclaration("DeviceClass",
        new CodeAttributeArgument(new CodePrimitiveExpression(config.Brand)),
        new CodeAttributeArgument(new CodePrimitiveExpression(config.Model)),
        new CodeAttributeArgument(new CodePrimitiveExpression(injectionKey)),
        new CodeAttributeArgument(new CodePrimitiveExpression(config.DeviceType)),
        new CodeAttributeArgument(new CodePrimitiveExpression(config.Developer))
    );
}
```

### 3.10 D3Driver方法生成规范

根据 `BR.ECS.DeviceDriver.Sample.Test/D3Driver.cs` 的详细注释，D3调用方法有严格要求：

#### 3.10.1 同步性要求（关键约束）

**能被D3调用的方法必须是同步的**，包括：
- 基础设备方法：`Reset`, `EStop`, `SafeEnter`, `Prepare`, `GStop`, `Dispose`, `PrepareRetry`, `ConnectDevice`, `DisconnectDevice`
- 带有 `[MethodOperations]` 特性的方法（调度方法）
- 带有 `[MethodMaintenance]` 特性的方法（维护方法）

⚠️ **禁止使用 `async/await`**：D3系统的直接调用机制不支持异步方法。

#### 3.10.2 Override关键字要求

以下基础设备方法必须使用 `override` 关键字重写：
```csharp
public override int Reset() { /* ... */ }
public override int EStop() { /* ... */ }
public override int SafeEnter() { /* ... */ }
public override int Prepare() { /* ... */ }
public override int GStop() { /* ... */ }
public override void Dispose() { /* ... */ }
public override int PrepareRetry() { /* ... */ }
public override int ConnectDevice(string info) { /* ... */ }
public override int DisconnectDevice() { /* ... */ }
```

#### 3.10.3 方法重载限制

**被D3调用的方法不能有重载**（方法名相同但参数不同）：
- ❌ 禁止：`GetTemperature()` 和 `GetTemperature(int sensorId)`
- ✅ 正确：`GetTemperature()` 和 `GetTemperatureById(int sensorId)`

#### 3.10.4 方法特性标记

```csharp
// 调度方法（Operations）
[MethodOperations]
public double GetCurrentTemperature()
{
    return _sila2Device.GetCurrentTemperature();
}

// 维护方法（Maintenance） 参数为顺序编号，从1开始
[MethodMaintenance(1)]
public bool GetDeviceState()
{
    return _sila2Device.GetDeviceState();
}

[MethodMaintenance(2)]
public void SwitchDeviceState(bool isOn)
{
    _sila2Device.SwitchDeviceState(isOn);
}
```

#### 3.10.5 XML注释要求

所有带有 `[MethodOperations]` 或 `[MethodMaintenance]` 特性的方法**必须**添加完整的XML注释。这些注释信息需要从Tecan Generator生成的代码中提取。

```csharp
/// <summary>
/// Control the temperature gradually to a set target.
/// </summary>
/// <param name="targetTemperature">The target temperature that the server will try to reach.</param>
[MethodOperations]
public void ControlTemperature(double targetTemperature)
{
    _sila2Device.ControlTemperature(targetTemperature);
}
```

## 四、核心服务实现

### 4.1 D3DriverViewModel（关键属性和方法 - 已更新）

```csharp
public partial class D3DriverViewModel : ObservableObject
{
    // 侧边栏管理
    [ObservableProperty] private bool isSidebarVisible = true;
    [ObservableProperty] private GridLength sidebarWidth = new GridLength(400);
    [ObservableProperty] private ObservableCollection<ServerNodeViewModel> onlineServers = new();
    [ObservableProperty] private ObservableCollection<LocalFeatureNodeViewModel> localNodes = new();
    
    // 生成状态（影响按钮可见性和启用状态）
    [ObservableProperty] private string statusText = "就绪";
    [ObservableProperty] private string currentProjectPath;
    [ObservableProperty] private string currentDllPath;
    [ObservableProperty] private string projectInfoText = "尚未生成项目";
    [ObservableProperty] private bool canCompile = false;  // 是否可以编译
    [ObservableProperty] private bool canAdjustMethods = false;  // 是否显示"调整方法特性"按钮
    
    // 方法预览数据（用于窗口）
    [ObservableProperty] private ObservableCollection<MethodPreviewData> methodPreviewData;
    
    // 当前分析结果（存储用于调整方法特性）
    private ClientAnalysisResult _currentAnalysisResult;
    private D3DriverGenerationConfig _currentConfig;
    
    // 过程日志
    [ObservableProperty] private string processLog;
    [ObservableProperty] private Brush processLogColor = Brushes.Black;
    private StringBuilder _processLogBuilder = new StringBuilder();
    
    // 命令
    [RelayCommand] private async Task ScanServersAsync() { /* 扫描在线服务器 */ }
    [RelayCommand] private void AddLocalFeatures() { /* 添加本地特性文件 */ }
    [RelayCommand] private void DeleteLocalNode(LocalFeatureNodeViewModel node) { /* 删除本地节点 */ }
    [RelayCommand] private async Task ExportFeaturesAsync() { /* 导出选中的特性 */ }
    
    // 拆分的生成和编译命令
    [RelayCommand] private async Task GenerateD3ProjectAsync() { /* 生成D3项目（不编译） */ }
    [RelayCommand] private async Task CompileD3ProjectAsync() { /* 编译D3项目（独立） */ }
    [RelayCommand] private void AdjustMethodAttributes() { /* 调整方法特性（打开窗口） */ }
    
    [RelayCommand] private void OpenProjectDirectory() { /* 打开项目目录 */ }
    [RelayCommand] private void OpenDllDirectory() { /* 打开DLL目录 */ }
    
    // 过程日志方法（支持错误高亮）
    private void AppendProcessLog(string message, bool isError = false)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var prefix = isError ? "❌" : "ℹ️";
        _processLogBuilder.AppendLine($"[{timestamp}] {prefix} {message}");
        ProcessLog = _processLogBuilder.ToString();
        
        if (isError)
        {
            ProcessLogColor = Brushes.Red;
        }
    }
    
    private void ClearProcessLog()
    {
        _processLogBuilder.Clear();
        ProcessLog = string.Empty;
        ProcessLogColor = Brushes.Black;
    }
    
    // 单服务器特性选择校验
    private bool ValidateServerSelection(FeatureNodeViewModel selectedFeature)
    {
        // 查找已选择的特性所属的服务器
        var selectedServer = OnlineServers.FirstOrDefault(s => 
            s.Features.Any(f => f.IsSelected && f != selectedFeature));
        
        if (selectedServer != null && selectedServer != selectedFeature.ParentServer)
        {
            // 跨服务器选择，显示错误
            var selectedServerName = selectedServer.ServerName;
            var currentServerName = selectedFeature.ParentServer.ServerName;
            
            AppendProcessLog(
                $"错误：只能选择同一服务器的特性！\n" +
                $"  已选择服务器：{selectedServerName}\n" +
                $"  当前服务器：{currentServerName}\n" +
                $"  请取消其他服务器的选择后再试。", 
                isError: true);
            
            // 取消当前选择
            selectedFeature.IsSelected = false;
            return false;
        }
        
        return true;
    }
}
```

### 4.2 GenerateD3ProjectAsync 流程（核心 - 已更新）

```csharp
private async Task GenerateD3ProjectAsync()
{
    ClearProcessLog();
    AppendProcessLog("开始生成D3项目...");
    
    try
    {
        // 1. 验证特性选择
        AppendProcessLog("验证特性选择...");
        var selectedFeatures = ValidateSelection();
        if (selectedFeatures == null || !selectedFeatures.Any())
        {
            AppendProcessLog("错误：未选择任何特性", isError: true);
            MessageBox.Show("请至少选择一个特性", "提示");
            return;
        }
        
        // 2. 弹出设备信息输入对话框
        AppendProcessLog("等待用户输入设备信息...");
        var deviceInfoDialog = new DeviceInfoDialog();
        var deviceInfoViewModel = new DeviceInfoDialogViewModel();
        deviceInfoDialog.DataContext = deviceInfoViewModel;
        deviceInfoViewModel.CloseAction = () => deviceInfoDialog.Close();
        
        // 如果是在线服务器，自动填充DeviceType
        if (IsOnlineSource)
        {
            var selectedServer = GetSelectedOnlineServer();
            deviceInfoViewModel.DeviceType = selectedServer?.DeviceType ?? "";
        }
        
        if (deviceInfoDialog.ShowDialog() != true)
        {
            AppendProcessLog("用户取消了设备信息输入");
            return;
        }
        
        var brand = deviceInfoViewModel.Brand;
        var model = deviceInfoViewModel.Model;
        var deviceType = deviceInfoViewModel.DeviceType;
        var developer = deviceInfoViewModel.Developer;
        
        AppendProcessLog($"设备信息：{brand} {model} ({deviceType}) - 开发者：{developer}");
        
        // 3. 自动生成命名空间和输出目录
        AppendProcessLog("生成命名空间和输出目录...");
        var namespaceName = $"BR.ECS.DeviceDrivers.{deviceType}.{brand}_{model}";
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputPath = Path.Combine(Path.GetTempPath(), "Sila2D3Gen", $"{brand}_{model}_{timestamp}");
        
        CurrentProjectPath = outputPath;
        AppendProcessLog($"命名空间: {namespaceName}");
        AppendProcessLog($"输出目录: {outputPath}");
        
        // 4. 生成客户端代码（使用Tecan Generator）
        AppendProcessLog("调用Tecan Generator生成客户端代码...");
        if (IsOnlineSource)
        {
            await GenerateClientCodeFromOnlineServerAsync(selectedFeatures, outputPath);
        }
        else
        {
            await GenerateClientCodeFromLocalXmlAsync(selectedFeatures, outputPath);
        }
        AppendProcessLog("✅ 客户端代码生成完成");
        
        // 5. 分析生成的代码
        AppendProcessLog("分析生成的代码...");
        var analyzer = new ClientCodeAnalyzer();
        var analysisResult = analyzer.Analyze(outputPath);
        AppendProcessLog($"检测到 {analysisResult.Features.Count} 个特性");
        
        _currentAnalysisResult = analysisResult;  // 保存用于后续调整
        
        // 6. 准备方法预览数据
        AppendProcessLog("准备方法预览数据...");
        MethodPreviewData = new ObservableCollection<MethodPreviewData>();
        foreach (var feature in analysisResult.Features)
        {
            foreach (var method in feature.Methods)
            {
                MethodPreviewData.Add(new MethodPreviewData
                {
                    FeatureName = feature.FeatureName,
                    MethodName = method.Name,
                    MethodType = method.IsObservableCommand ? "ObservableCommand" : 
                                 method.IsProperty ? "Property" : "Command",
                    ReturnType = method.ReturnType.Name,
                    Description = method.XmlDocumentation?.Summary ?? "",
                    IsMaintenance = false  // 默认为调度方法
                });
            }
        }
        
        // 7. 弹出方法预览与特性调整窗口
        AppendProcessLog("等待用户调整方法特性...");
        var methodPreviewWindow = new MethodPreviewWindow();
        var methodPreviewViewModel = new MethodPreviewViewModel 
        { 
            MethodPreviewData = MethodPreviewData 
        };
        methodPreviewWindow.DataContext = methodPreviewViewModel;
        methodPreviewViewModel.CloseAction = () => methodPreviewWindow.Close();
        
        if (methodPreviewWindow.ShowDialog() != true)
        {
            AppendProcessLog("用户取消了方法特性调整");
            return;
        }
        
        // 8. 同步方法分类信息
        AppendProcessLog("同步方法分类信息...");
        SyncMethodClassification(analysisResult.Features);
        
        // 9. 获取服务器IP和Port
        string serverIp = null;
        int? serverPort = null;
        if (IsOnlineSource)
        {
            var selectedServer = GetSelectedOnlineServer();
            serverIp = selectedServer?.IpAddress;
            serverPort = selectedServer?.Port;
            AppendProcessLog($"服务器地址: {serverIp}:{serverPort}");
        }
        
        // 10. 创建生成配置
        var config = new D3DriverGenerationConfig
        {
            Brand = brand,
            Model = model,
            DeviceType = deviceType,
            Developer = developer,
            Namespace = namespaceName,
            OutputPath = outputPath,
            Features = analysisResult.Features,
            IsOnlineSource = IsOnlineSource,
            ServerUuid = IsOnlineSource ? GetSelectedServerUuid() : null,
            LocalFeatureXmlPaths = IsOnlineSource ? null : selectedFeatures,
            ServerIp = serverIp,
            ServerPort = serverPort
        };
        
        _currentConfig = config;  // 保存用于后续编译
        
        // 11. 生成D3驱动代码（不编译）
        AppendProcessLog("生成D3驱动代码文件...");
        var generator = new D3DriverGeneratorService();
        var result = generator.Generate(config, message => AppendProcessLog(message));
        
        if (!result.Success)
        {
            AppendProcessLog($"生成失败: {result.Message}", isError: true);
            MessageBox.Show($"生成失败：\n{result.Message}", "错误");
            return;
        }
        
        AppendProcessLog("✅ D3项目生成完成");
        
        // 12. 更新UI状态
        CanCompile = true;
        CanAdjustMethods = true;
        ProjectInfoText = $"项目：{brand}_{model}\n路径：{outputPath}\n状态：已生成，待编译";
        StatusText = "项目已生成，可以编译";
        
        MessageBox.Show(
            $"D3项目生成完成！\n\n项目目录: {outputPath}\n\n请点击"编译D3项目"按钮进行编译。",
            "生成成功",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
    catch (Exception ex)
    {
        AppendProcessLog($"发生异常: {ex.Message}", isError: true);
        MessageBox.Show($"发生未预期的错误:\n\n{ex.Message}\n\n{ex.StackTrace}", "错误");
    }
}

// 同步方法分类信息
private void SyncMethodClassification(List<ClientFeatureInfo> features)
{
    foreach (var previewData in MethodPreviewData)
    {
        var feature = features.FirstOrDefault(f => f.FeatureName == previewData.FeatureName);
        if (feature == null) continue;
        
        var method = feature.Methods.FirstOrDefault(m => m.Name == previewData.MethodName);
        if (method == null) continue;
        
        method.IsMaintenance = previewData.IsMaintenance;
    }
}
```

### 4.3 CompileD3ProjectAsync 流程（独立编译 - 新增）

```csharp
private async Task CompileD3ProjectAsync()
{
    AppendProcessLog("开始编译D3项目...");
    
    try
    {
        // 验证是否已生成项目
        if (string.IsNullOrEmpty(CurrentProjectPath) || _currentConfig == null)
        {
            AppendProcessLog("错误：尚未生成D3项目", isError: true);
            MessageBox.Show("请先点击"生成D3项目"按钮生成项目", "提示");
            return;
        }
        
        // 查找.csproj文件
        var projectFile = Path.Combine(CurrentProjectPath, $"{_currentConfig.Brand}{_currentConfig.Model}.D3Driver.csproj");
        if (!File.Exists(projectFile))
        {
            AppendProcessLog($"错误：找不到项目文件 {projectFile}", isError: true);
            MessageBox.Show($"找不到项目文件：\n{projectFile}", "错误");
            return;
        }
        
        // 执行编译
        AppendProcessLog($"编译项目：{projectFile}");
        var generator = new D3DriverGeneratorService();
        var compileResult = await generator.CompileProjectAsync(
            projectFile,
            message => AppendProcessLog(message));
        
        if (!compileResult.Success)
        {
            AppendProcessLog($"编译失败（{compileResult.ErrorCount} 个错误）", isError: true);
            AppendProcessLog(compileResult.Message, isError: true);
            MessageBox.Show($"编译失败！\n\n{compileResult.Message}", "编译错误");
            return;
        }
        
        AppendProcessLog("✅ 编译成功");
        CurrentDllPath = compileResult.DllPath;
        ProjectInfoText = $"项目：{_currentConfig.Brand}_{_currentConfig.Model}\n" +
                          $"路径：{CurrentProjectPath}\n" +
                          $"DLL：{CurrentDllPath}\n" +
                          $"状态：已编译";
        StatusText = "编译成功";
        
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
        AppendProcessLog($"发生异常: {ex.Message}", isError: true);
        MessageBox.Show($"发生未预期的错误:\n\n{ex.Message}\n\n{ex.StackTrace}", "错误");
    }
}
```

### 4.4 AdjustMethodAttributes 流程（新增）

```csharp
private void AdjustMethodAttributes()
{
    AppendProcessLog("打开方法特性调整窗口...");
    
    try
    {
        // 验证是否有可调整的方法数据
        if (MethodPreviewData == null || !MethodPreviewData.Any())
        {
            AppendProcessLog("错误：没有可调整的方法数据", isError: true);
            MessageBox.Show("没有可调整的方法数据", "提示");
            return;
        }
        
        // 打开方法预览窗口
        var methodPreviewWindow = new MethodPreviewWindow();
        var methodPreviewViewModel = new MethodPreviewViewModel 
        { 
            MethodPreviewData = new ObservableCollection<MethodPreviewData>(MethodPreviewData) 
        };
        methodPreviewWindow.DataContext = methodPreviewViewModel;
        methodPreviewViewModel.CloseAction = () => methodPreviewWindow.Close();
        
        if (methodPreviewWindow.ShowDialog() != true)
        {
            AppendProcessLog("用户取消了方法特性调整");
            return;
        }
        
        // 更新方法预览数据
        MethodPreviewData = methodPreviewViewModel.MethodPreviewData;
        
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
            
            var generator = new D3DriverGeneratorService();
            var result = generator.RegenerateD3Driver(_currentConfig, message => AppendProcessLog(message));
            
            if (result.Success)
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
                AppendProcessLog($"重新生成失败: {result.Message}", isError: true);
                MessageBox.Show($"重新生成D3Driver.cs失败：\n{result.Message}", "错误");
            }
        }
    }
    catch (Exception ex)
    {
        AppendProcessLog($"发生异常: {ex.Message}", isError: true);
        MessageBox.Show($"发生未预期的错误:\n\n{ex.Message}\n\n{ex.StackTrace}", "错误");
    }
}
```

### 4.5 ExportFeaturesAsync 流程（新增）

```csharp
private async Task ExportFeaturesAsync()
{
    AppendProcessLog("开始导出特性...");
    
    try
    {
        // 验证选择
        var selectedFeatures = ValidateSelection();
        if (selectedFeatures == null || !selectedFeatures.Any())
        {
            AppendProcessLog("错误：未选择任何特性", isError: true);
            MessageBox.Show("请至少选择一个特性", "提示");
            return;
        }
        
        // 选择导出目录
        var folderDialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择特性文件导出目录",
            ShowNewFolderButton = true
        };
        
        if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            AppendProcessLog("用户取消了导出");
            return;
        }
        
        var exportPath = folderDialog.SelectedPath;
        AppendProcessLog($"导出目录：{exportPath}");
        
        // 导出特性文件
        var exportedCount = 0;
        
        if (IsOnlineSource)
        {
            // 在线服务器：下载.sila.xml文件
            AppendProcessLog("从在线服务器下载特性定义...");
            var selectedServer = GetSelectedOnlineServer();
            var client = new SilaServiceClient(selectedServer.IpAddress, selectedServer.Port);
            
            foreach (var featureId in selectedFeatures)
            {
                try
                {
                    var featureXml = await client.GetFeatureDefinitionAsync(featureId);
                    var fileName = $"{featureId.Replace('/', '_')}.sila.xml";
                    var filePath = Path.Combine(exportPath, fileName);
                    
                    File.WriteAllText(filePath, featureXml);
                    AppendProcessLog($"✅ 导出: {fileName}");
                    exportedCount++;
                }
                catch (Exception ex)
                {
                    AppendProcessLog($"导出失败 {featureId}: {ex.Message}", isError: true);
                }
            }
        }
        else
        {
            // 本地特性：复制.sila.xml文件
            AppendProcessLog("复制本地特性文件...");
            foreach (var xmlPath in selectedFeatures)
            {
                try
                {
                    var fileName = Path.GetFileName(xmlPath);
                    var targetPath = Path.Combine(exportPath, fileName);
                    
                    File.Copy(xmlPath, targetPath, overwrite: true);
                    AppendProcessLog($"✅ 导出: {fileName}");
                    exportedCount++;
                }
                catch (Exception ex)
                {
                    AppendProcessLog($"导出失败 {xmlPath}: {ex.Message}", isError: true);
                }
            }
        }
        
        AppendProcessLog($"✅ 导出完成，共导出 {exportedCount} 个特性文件");
        MessageBox.Show(
            $"特性文件导出完成！\n\n导出数量：{exportedCount}\n导出路径：{exportPath}",
            "导出成功",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        
        // 打开导出目录
        OpenDirectory(exportPath);
    }
    catch (Exception ex)
    {
        AppendProcessLog($"发生异常: {ex.Message}", isError: true);
        MessageBox.Show($"发生未预期的错误:\n\n{ex.Message}\n\n{ex.StackTrace}", "错误");
    }
}
```

### 4.6 D3DriverGeneratorService扩展（新增）

需要在D3DriverGeneratorService中添加RegenerateD3Driver方法：

```csharp
public class D3DriverGeneratorService
{
    // 原有Generate方法...
    
    // 新增：仅重新生成D3Driver.cs文件
    public GenerationResult RegenerateD3Driver(
        D3DriverGenerationConfig config,
        Action<string>? progressCallback = null)
    {
        try
        {
            progressCallback?.Invoke("重新生成 D3Driver.cs...");
            
            var d3DriverPath = Path.Combine(config.OutputPath, "D3Driver.cs");
            GenerateD3Driver(config, d3DriverPath);
            
            progressCallback?.Invoke("✅ D3Driver.cs 已更新");
            return new GenerationResult { Success = true };
        }
        catch (Exception ex)
        {
            return new GenerationResult { Success = false, Message = ex.Message };
        }
    }
    
    // ...
}
```

## 五、统一实施To-dos（已更新）

### 5.0 添加测试控制台项目（阶段10.1新增，预计 0.5 天）

**目的**：创建一个控制台应用程序，用于功能测试第三个Tab页面"生成D3驱动"的完整工作流程。

**项目结构**：
```
Sila2DriverGen.TestConsole/
├── Program.cs                 # 主程序入口
├── TestRunner.cs              # 测试运行器
├── Scenarios/                 # 测试场景
│   ├── OnlineServerTest.cs   # 在线服务器生成测试
│   ├── LocalFeatureTest.cs   # 本地特性生成测试
│   └── MethodAdjustmentTest.cs # 方法调整测试
├── Helpers/                   # 辅助类
│   └── ConsoleHelper.cs       # 控制台输出美化
└── README.md                  # 使用说明
```

**功能清单**：
- [ ] 创建 Sila2DriverGen.TestConsole 项目（.NET 8.0 Console App）
- [ ] 实现交互式测试菜单
- [ ] 场景1：测试在线服务器扫描和特性选择
- [ ] 场景2：测试本地特性文件导入和管理
- [ ] 场景3：测试设备信息对话框输入
- [ ] 场景4：测试方法预览窗口和方法分类
- [ ] 场景5：测试D3项目生成（不编译）
- [ ] 场景6：测试D3项目编译
- [ ] 场景7：测试方法特性调整和重新生成
- [ ] 场景8：测试特性导出功能
- [ ] 添加彩色控制台输出
- [ ] 添加测试结果记录
- [ ] 添加项目到解决方案
- [ ] 编写 README.md 使用说明

**测试场景示例**：
```csharp
public class OnlineServerTest
{
    public async Task<TestResult> Run()
    {
        Console.WriteLine("=== 测试场景1：在线服务器生成D3驱动 ===");
        
        // 1. 扫描服务器
        Console.WriteLine("1. 扫描在线服务器...");
        // 调用扫描逻辑
        
        // 2. 选择特性
        Console.WriteLine("2. 选择特性...");
        // 模拟选择
        
        // 3. 生成项目
        Console.WriteLine("3. 生成D3项目...");
        // 调用生成逻辑
        
        // 4. 编译项目
        Console.WriteLine("4. 编译项目...");
        // 调用编译逻辑
        
        // 5. 验证结果
        Console.WriteLine("5. 验证输出文件...");
        // 检查文件是否存在
        
        return new TestResult { Success = true, Message = "测试通过" };
    }
}
```

### 5.1 基础架构（预计 1.5 天）

- [ ] 创建数据模型：
  - [ ] `Models/ClientFeatureInfo.cs`
  - [ ] `Models/MethodGenerationInfo.cs`
  - [ ] `Models/ClientAnalysisResult.cs`
  - [ ] `Models/D3DriverGenerationConfig.cs`
  - [ ] `Models/GenerationResult.cs`
  - [ ] `Models/CompilationResult.cs`
  - [ ] `Models/LocalFeatureNodeViewModel.cs`
  - [ ] `Models/ServerNodeViewModel.cs`（扩展三态选择）
  - [ ] `Models/FeatureNodeViewModel.cs`（子节点选择）
  - [ ] `Models/DeviceInfoDialogViewModel.cs`（新增）
  - [ ] `Models/MethodPreviewViewModel.cs`（新增）

- [ ] 创建Converter：
  - [ ] `Converters/CountToVisibilityConverter.cs`
  - [ ] `Converters/BoolToVisibilityConverter.cs`（已有）

### 5.2 UI实现（预计 2 天）

- [ ] 更新D3DriverView.xaml：
  - [ ] 三列布局（侧边栏+GridSplitter+主区域）
  - [ ] 侧边栏切换按钮（竖向三点，位于左边框）
  - [ ] 侧边栏工具栏（扫描、添加、导出按钮）
  - [ ] 在线服务器树（三态CheckBox）
  - [ ] 本地特性树（条件显示）
  - [ ] 移除设备信息输入区（改为弹窗）
  - [ ] 移除方法预览控件（改为独立窗口）
  - [ ] 过程日志Expander（支持颜色显示）
  - [ ] 项目信息显示区
  - [ ] 操作按钮区（5个按钮，条件显示）

- [ ] 创建DeviceInfoDialog.xaml：
  - [ ] 设备信息输入表单（品牌、型号、类型、开发者）
  - [ ] 确定/取消按钮
  - [ ] 输入验证

- [ ] 创建MethodPreviewWindow.xaml：
  - [ ] 方法列表DataGrid
  - [ ] 维护方法CheckBox列
  - [ ] 全部设为维护/调度按钮
  - [ ] 确定/取消按钮

### 5.3 ViewModel实现（预计 3 天）

- [ ] 更新D3DriverViewModel.cs：
  - [ ] 侧边栏管理属性
  - [ ] 生成状态属性（CanCompile, CanAdjustMethods, ProjectInfoText）
  - [ ] 过程日志属性（支持颜色）
  - [ ] 当前分析结果和配置（_currentAnalysisResult, _currentConfig）
  - [ ] 实现所有命令：
    - [ ] ScanServersAsync（扫描在线服务器）
    - [ ] AddLocalFeatures（添加本地特性文件）
    - [ ] DeleteLocalNode（删除本地节点）
    - [ ] ExportFeaturesAsync（导出特性）
    - [ ] GenerateD3ProjectAsync（生成项目，不编译）
    - [ ] CompileD3ProjectAsync（独立编译）
    - [ ] AdjustMethodAttributes（调整方法特性）
    - [ ] OpenProjectDirectory（打开项目目录）
    - [ ] OpenDllDirectory（打开DLL目录）
  - [ ] 实现单服务器选择校验（ValidateServerSelection）
  - [ ] 实现错误日志高亮
  - [ ] 实现选择验证逻辑
  - [ ] 实现方法分类同步

- [ ] 创建DeviceInfoDialogViewModel.cs
- [ ] 创建MethodPreviewViewModel.cs

### 5.4 代码分析服务（预计 2 天）

- [ ] 创建TecanGeneratorWrapper.cs（已有）
- [ ] 创建ClientCodeAnalyzer.cs（已有）

### 5.5 代码生成服务（预计 3.5 天）

- [ ] 更新D3DriverGeneratorService.cs：
  - [ ] 实现Generate方法
  - [ ] 实现CompileProjectAsync方法（异步）
  - [ ] 实现RegenerateD3Driver方法（仅重新生成D3Driver.cs）
  - [ ] 实现错误解析和统计
  - [ ] 实现进度回调机制

- [ ] 创建CodeDOM生成器（已有计划）

### 5.6 本地特性管理（预计 0.5 天）

- [ ] 创建LocalFeaturePersistenceService.cs（已有）

### 5.7 集成和测试（预计 2 天）

- [ ] 在MainWindow中添加D3DriverView
- [ ] 绑定ViewModel到View
- [ ] 端到端测试：
  - [ ] 测试侧边栏切换按钮
  - [ ] 测试父节点三态显示
  - [ ] 测试单服务器选择限制和错误提示
  - [ ] 测试导出特性功能
  - [ ] 测试设备信息对话框
  - [ ] 测试方法预览窗口
  - [ ] 测试分离的生成和编译流程
  - [ ] 测试调整方法特性功能
  - [ ] 测试控制台项目加载和测试驱动
  - [ ] 验证过程信息实时显示和错误高亮
  - [ ] 验证编译成功/失败处理
  - [ ] 验证命名冲突处理
  - [ ] 验证可观察命令阻塞等待
  - [ ] 验证多特性整合
  - [ ] 验证XML注释集成
  
- [ ] 错误处理和友好提示优化
- [ ] 性能优化（如有必要）
- [ ] 代码清理和注释完善

### 5.8 文档更新（预计 0.5 天）

- [ ] 更新`D3_DRIVER_GENERATION_GUIDE.md`：
  - [ ] 说明新的UI布局和交互
  - [ ] 记录分离的生成和编译流程
  - [ ] 说明设备信息对话框和方法预览窗口
  - [ ] 添加导出特性功能说明
  - [ ] 添加测试控制台使用说明
  - [ ] 更新操作流程
  - [ ] 添加截图

### 5.9 最终验证（预计 0.5 天）

- [ ] 检查代码无编译错误和警告
- [ ] 检查是否符合C#最佳实践
- [ ] 检查异常处理是否完善
- [ ] 检查注释是否清晰
- [ ] 检查是否遵循MVVM架构
- [ ] 检查是否使用CodeDOM生成所有代码
- [ ] 检查生成的代码是否符合示例风格
- [ ] 检查用户体验是否流畅
- [ ] **最终确认：是否已经解决用户的所有需求**

---

## 六、关键技术点（已更新）

### 6.1 三态CheckBox实现

```xaml
<CheckBox IsThreeState="True" 
          IsChecked="{Binding IsSelected, Mode=TwoWay}"
          Content="{Binding ServerName}"/>
```

```csharp
// 三态逻辑：
// null  = 半选（部分子项被选中）
// false = 未选（所有子项未选）
// true  = 全选（所有子项被选）
```

### 6.2 单服务器选择校验

在FeatureNodeViewModel的IsSelected属性变化时触发：

```csharp
partial void OnIsSelectedChanged(bool value)
{
    if (value)
    {
        // 校验是否跨服务器选择
        var viewModel = GetD3DriverViewModel();
        if (!viewModel.ValidateServerSelection(this))
        {
            // 校验失败，IsSelected已被重置为false
            return;
        }
    }
    
    // 通知父节点更新状态
    ParentServer?.UpdateParentSelectionState();
}
```

### 6.3 错误日志高亮

```csharp
private void AppendProcessLog(string message, bool isError = false)
{
    var timestamp = DateTime.Now.ToString("HH:mm:ss");
    var prefix = isError ? "❌" : "ℹ️";
    _processLogBuilder.AppendLine($"[{timestamp}] {prefix} {message}");
    ProcessLog = _processLogBuilder.ToString();
    
    if (isError)
    {
        ProcessLogColor = Brushes.Red;  // 错误时显示红色
    }
}
```

### 6.4 侧边栏切换按钮位置

```xaml
<!-- 竖向三点按钮，位于侧边栏左边框 -->
<ToggleButton Grid.Column="0" 
              IsChecked="{Binding IsSidebarVisible}"
              VerticalAlignment="Center"
              HorizontalAlignment="Left"
              Margin="-15,0,0,0"  <!-- 负边距使其覆盖在边框上 -->
              Width="15" Height="60"
              Content="⋮" 
              FontSize="16"/>
```

## 七、预估时间（已更新）

**总计预估时间：约 13-15 天**

- 测试控制台项目：0.5 天
- 基础架构：1.5 天
- UI实现：2 天
- ViewModel实现：3 天
- 代码分析服务：2 天
- 代码生成服务：3.5 天
- 本地特性管理：0.5 天
- 集成和测试：2 天
- 文档更新：0.5 天
- 最终验证：0.5 天

## 八、To-dos 完成标准（继续有效）

每个 To-do 完成时应确保：
1. ✅ 代码无编译错误和警告
2. ✅ 代码符合 C# 最佳实践
3. ✅ 有必要的异常处理
4. ✅ 有清晰的注释说明
5. ✅ 通过基本功能测试

---

**计划更新完成！主要变更总结：**

1. ✅ 添加控制台测试项目
2. ✅ 单服务器特性选择限制和错误提示
3. ✅ 父节点三态显示
4. ✅ 导出特性功能
5. ✅ 侧边栏切换按钮位置调整
6. ✅ 拆分生成和编译按钮
7. ✅ 方法预览改为独立窗口
8. ✅ 设备信息对话框输入
9. ✅ 调整方法特性功能
10. ✅ 更新所有流程图和实现细节

---

## 九、实施完成总结（2024-10-24）

### 9.1 实施阶段1：核心功能实现与测试（第一天）

#### 9.1.1 问题修复

**问题1：ClientCodeAnalyzer编译错误**
- **现象**：生成D3项目时报错 `CS0012: 类型"List<>"在未引用的程序集中定义`
- **原因**：ClientCodeAnalyzer在动态编译客户端代码时缺少 `System.Collections` 程序集引用
- **解决方案**：
  ```csharp
  // ClientCodeAnalyzer.cs
  references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location));
  ```
- **影响**：修复后，客户端代码分析功能正常工作，可以正确提取方法信息

#### 9.1.2 测试控制台扩展

**新增测试7：多特性完整流程测试**
- 自动查找多个.sila.xml文件（最多2个）
- 从多个特性生成完整D3项目
- 编译并验证结果
- 验证多特性集成功能

**测试结果**：
```
✓ 测试1：生成D3项目 - 通过
✓ 测试2：编译项目 - 通过
✓ 测试3：调整方法分类 - 通过
✓ 测试4：无效文件处理 - 通过
✓ 测试5：编译失败处理 - 通过
✓ 测试6：多特性完整流程 - 通过
```

### 9.2 实施阶段2：方法特性标记系统重大改进（第二天）

#### 9.2.1 方法标记系统从单选变为多选

**之前的设计局限**：
- 只有一个 `IsMaintenance` 布尔字段
- 方法只能是"维护方法"或"调度方法"之一
- 使用 `MethodCategory` 枚举（只能是 Operations 或 Maintenance）

**改进后的设计**：
- 三个独立的布尔字段：
  - `IsIncluded`：是否包含在D3Driver.cs中（默认 true）
  - `IsOperations`：是否为调度方法（默认 false）
  - `IsMaintenance`：是否为维护方法（默认根据方法名判断）
- 方法可以：
  - ✅ 同时是调度方法和维护方法
  - ✅ 只是调度方法
  - ✅ 只是维护方法
  - ✅ 两者都不是
  - ✅ 不被包含在D3Driver中

**UI改进**：
```xml
<!-- 方法预览窗口新增三列 -->
<DataGridCheckBoxColumn Header="包含" Binding="{Binding IsIncluded}" />
<DataGridCheckBoxColumn Header="调度方法" Binding="{Binding IsOperations}" />
<DataGridCheckBoxColumn Header="维护方法" Binding="{Binding IsMaintenance}" />
```

**批量操作按钮**：
- 全选/全不选：切换所有方法的包含状态
- 全部调度：将所有方法标记为调度方法
- 全部维护：将所有方法标记为维护方法
- 清除特性：清除所有方法的调度/维护标记

**代码生成逻辑**：
```csharp
// D3DriverGenerator.cs
// 可以同时标记两个特性
[MethodOperations]
[MethodMaintenance(1)]
public void Method1() { ... }

// 可以只标记一个
[MethodOperations]
public void Method2() { ... }

// 没有特性标记的方法不会被生成
```

**文件变更**：
1. `Models/MethodGenerationInfo.cs` - 添加新字段
2. `Models/ClientAnalysisResult.cs` - MethodPreviewData 添加新字段
3. `Views/MethodPreviewWindow.xaml` - UI 添加三个复选框列
4. `ViewModels/MethodPreviewViewModel.cs` - 添加批量操作命令
5. `Services/CodeDom/D3DriverGenerator.cs` - 支持多特性标记
6. `ViewModels/D3DriverViewModel.cs` - 更新同步逻辑
7. `Services/ClientCodeAnalyzer.cs` - 使用新字段
8. `Services/D3DriverGeneratorService.cs` - 使用新字段
9. `Services/D3DriverOrchestrationService.cs` - 使用新字段

#### 9.2.2 依赖库复制问题修复

**问题2：生成的D3项目未复制reflib文件**
- **现象**：生成的D3项目lib文件夹为空，编译时找不到依赖库
- **原因**：`CopyDependencyLibraries`方法只从示例项目查找lib目录，未考虑reflib目录
- **解决方案**：
  ```csharp
  // D3DriverGeneratorService.cs
  private string? FindReflibDirectory()
  {
      // 获取当前执行程序集的位置，向上查找reflib目录
      var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
      var currentDir = Path.GetDirectoryName(assemblyLocation);
      
      var searchDir = currentDir;
      for (int i = 0; i < 10; i++)
      {
          var reflibPath = Path.Combine(searchDir, "reflib");
          if (Directory.Exists(reflibPath))
              return reflibPath;
          
          var parent = Directory.GetParent(searchDir);
          if (parent == null) break;
          searchDir = parent.FullName;
      }
      return null;
  }
  ```
- **影响**：现在可以正确从reflib目录复制以下DLL：
  - `BR.PC.Device.Sila2Discovery.dll`
  - `BR.ECS.Executor.Device.Domain.Contracts.dll`
  - `BR.ECS.Executor.Device.Infrastructure.dll`
  - `BR.ECS.Executor.Device.Domain.Share.dll`

#### 9.2.3 方法预览窗口UI问题修复

**问题3：CheckBox点击两次才能勾选**
- **现象**：DataGrid中的CheckBox需要点击两次才能勾选或取消勾选
- **原因**：WPF DataGrid的CheckBox默认行为问题
- **解决方案**：移除不必要的ElementStyle设置，使用默认样式

**问题4：DataGrid有空行**
- **现象**：DataGrid底部显示一个空行，影响用户体验
- **原因**：`CanUserAddRows`属性默认为true
- **解决方案**：
  ```xml
  <DataGrid CanUserAddRows="False" 
            CanUserDeleteRows="False"
            SelectionMode="Single" />
  ```

#### 9.2.4 D3Driver生成逻辑优化

**问题5：没有特性标记的方法也被生成**
- **需求**：只有标记了特性（IsOperations或IsMaintenance）的方法才应该生成到D3Driver.cs
- **解决方案**：
  ```csharp
  // D3DriverGenerator.cs
  private void AddMethods(CodeTypeDeclaration driverClass, List<MethodGenerationInfo> methods)
  {
      // 只包含标记为 IsIncluded 且有特性标记的方法
      var includedMethods = methods
          .Where(m => m.IsIncluded && (m.IsOperations || m.IsMaintenance))
          .ToList();
      
      // 只有带特性标记的方法才会被生成
  }
  ```
- **影响**：D3Driver.cs现在只包含用户明确标记了特性的方法，代码更简洁

### 9.3 所有改进功能验证

**自动化测试结果**：
```
✓ ✓ 所有测试通过！

验证内容：
  ✓ D3DriverOrchestrationService 无UI依赖
  ✓ 客户端代码生成功能正常
  ✓ 代码分析功能正常
  ✓ D3驱动代码生成功能正常
  ✓ 项目编译功能正常
  ✓ 方法分类调整功能正常
  ✓ 错误处理机制正常
  ✓ reflib依赖库正确复制
  ✓ 方法预览窗口UI交互正常
  ✓ 多特性标记系统正常工作
```

### 9.4 技术亮点总结

1. **灵活的方法标记系统**：
   - 支持多维度方法标记（包含、调度、维护）
   - 方法可以同时拥有多个特性标记
   - 提供批量操作提高效率

2. **智能依赖库查找**：
   - 自动向上查找reflib目录
   - 支持多种查找路径策略
   - 确保生成的项目包含所有必需DLL

3. **优化的UI交互**：
   - 解决DataGrid CheckBox点击问题
   - 移除空行提高用户体验
   - 清晰的列标题和操作按钮

4. **严格的代码生成规则**：
   - 只生成有特性标记的方法
   - 遵循D3系统调用规范
   - 自动生成完整XML注释

5. **全面的测试覆盖**：
   - 6个自动化测试场景
   - 涵盖正常流程和异常处理
   - 确保代码质量和稳定性

### 9.5 向后兼容性

- ✅ 旧的 `Category` 枚举字段保留（标记为 `[Obsolete]`）
- ✅ 自动迁移旧字段到新字段
- ✅ 无破坏性更改
- ✅ 所有现有功能继续工作

### 9.6 用户文档

创建了以下文档：
1. `方法特性标记系统改进说明.md` - 详细说明新的方法标记系统
2. `测试运行指南.md` - 测试控制台使用说明和测试结果
3. 更新 `README.md` - 添加测试7的说明

### 9.7 关键数据模型

#### MethodGenerationInfo（更新）
```csharp
public class MethodGenerationInfo
{
    // 新增字段
    public bool IsIncluded { get; set; } = true;
    public bool IsOperations { get; set; } = false;
    public bool IsMaintenance { get; set; } = false;
    
    // 废弃字段（保留用于向后兼容）
    [Obsolete("请使用 IsOperations 和 IsMaintenance 替代")]
    public MethodCategory Category { get; set; }
}
```

#### MethodPreviewData（更新）
```csharp
public class MethodPreviewData : ObservableObject
{
    [ObservableProperty] private bool _isIncluded = true;
    [ObservableProperty] private bool _isOperations = false;
    [ObservableProperty] private bool _isMaintenance = false;
}
```

### 9.8 实施完成状态

**所有计划的功能均已实现并测试通过：**

✅ 基础架构完成（100%）
✅ UI实现完成（100%）
✅ ViewModel实现完成（100%）
✅ 代码分析服务完成（100%）
✅ 代码生成服务完成（100%）
✅ 本地特性管理完成（100%）
✅ 集成和测试完成（100%）
✅ 文档更新完成（100%）
✅ 最终验证完成（100%）
✅ 用户反馈问题全部修复（100%）

### 9.9 下一步建议

虽然当前所有功能已完成，但未来可以考虑以下增强：

1. **在线服务器功能增强**：
   - 支持保存常用服务器地址
   - 支持服务器连接状态实时监控

2. **方法预览增强**：
   - 支持方法搜索和过滤
   - 支持按特性分组显示
   - 支持导出/导入方法配置

3. **代码生成优化**：
   - 支持自定义代码模板
   - 支持更多数据类型转换
   - 支持异步方法包装

4. **测试工具增强**：
   - 添加性能测试场景
   - 添加压力测试工具
   - 生成测试报告

---

**项目状态**：✅ 已完成并验证
**最后更新**：2024-10-24
**维护者**：Bioyond Team
