# SiLA2 D3驱动生成工具

从 SiLA2 服务器或本地特性文件自动生成符合 D3 系统规范的设备驱动代码。



## 目录

- [项目架构](#项目架构)
- [核心功能](#核心功能)
- [组件说明](#组件说明)
- [工作流程](#工作流程)
- [文件清单](#文件清单)
- [设计原则](#设计原则)
- [依赖库](#依赖库)
- [使用指南](#使用指南)
- [故障排除](#故障排除)

---

## 项目架构

```
┌─────────────────────────────────────────────────────────┐
│                    WPF UI 层 (Views)                     │
│  • D3DriverView - 主界面（特性选择、项目生成）           │
│  • MethodPreviewWindow - 方法预览和特性调整              │
│  • DeviceInfoDialog - 设备信息输入                       │
└────────────────────┬────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────┐
│                ViewModel 层 (MVVM)                       │
│  • D3DriverViewModel - 主界面逻辑                        │
│  • MethodPreviewViewModel - 方法预览逻辑                 │
│  • ServerInfoViewModel - 服务器节点模型                  │
└────────────────────┬────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────┐
│                   Service 层                             │
│  ┌─────────────────────────────────────────────────┐   │
│  │ D3DriverOrchestrationService                     │   │
│  │ • 编排完整的生成和编译流程（无UI依赖）           │   │
│  └─────────────────────┬───────────────────────────┘   │
│                        ↓                                 │
│  ┌──────────────────────────────────────────────────┐  │
│  │ ClientCodeGenerator                               │  │
│  │ • 调用 Tecan Generator 生成 gRPC 客户端代码      │  │
│  │ • 复制必需的 DLL 到输出目录                      │  │
│  │ • 代码去重处理                                   │  │
│  └──────────────────┬───────────────────────────────┘  │
│                     ↓                                    │
│  ┌──────────────────────────────────────────────────┐  │
│  │ ClientCodeAnalyzer                                │  │
│  │ • 使用 Roslyn 编译生成的代码                     │  │
│  │ • 反射分析接口、方法、参数                       │  │
│  │ • 提取 XML 文档注释                              │  │
│  │ • 检测类型支持情况                               │  │
│  └──────────────────┬───────────────────────────────┘  │
│                     ↓                                    │
│  ┌──────────────────────────────────────────────────┐  │
│  │ D3DriverGeneratorService                          │  │
│  │ • 使用 CodeDOM 生成 D3 驱动封装代码              │  │
│  │ • 生成 .csproj 和 .sln 文件                      │  │
│  │ • 执行 dotnet build 编译项目                     │  │
│  └──────────────────────────────────────────────────┘  │
│                                                          │
│  其他服务:                                               │
│  • ServerDiscoveryService - mDNS 扫描                   │
│  • Sila2RealTimeDiscoveryService - 实时监控             │
│  • GeneratedCodeDeduplicator - 代码去重                 │
│  • LoggerService - 结构化日志                           │
└─────────────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────┐
│              Model 层 (数据模型)                         │
│  • D3DriverGenerationConfig - 生成配置                  │
│  • ClientFeatureInfo - 特性信息                         │
│  • MethodGenerationInfo - 方法信息                      │
│  • ServerInfoViewModel - 服务器模型                     │
└─────────────────────────────────────────────────────────┘
```

---

## 核心功能

### 1. 特性源管理
- **在线服务器扫描** - 使用 mDNS 发现网络中的 SiLA2 服务器
- **实时监控** - 自动监控服务器上线/下线
- **本地文件导入** - 支持导入 .sila.xml 文件
- **特性导出** - 导出选中的特性到本地

### 2. 代码生成
- **客户端代码** - 使用 Tecan Generator 生成强类型 gRPC 客户端
- **D3驱动封装** - 自动生成符合 D3 规范的驱动类
- **智能类型转换** - 不支持的类型自动转为 JSON 序列化
- **代码去重** - 多特性生成时自动去除重复定义

### 3. 方法分类
- **调度方法** - 标记 `[MethodOperations]`
- **维护方法** - 标记 `[MethodMaintenance(n)]`
- **灵活配置** - 支持同时标记或不标记

### 4. 项目编译
- **项目生成** - 自动生成 .csproj 和 .sln
- **依赖管理** - 从 reflib 复制必需的 DLL
- **一键编译** - 执行 dotnet build
- **XML文档** - 生成 XML 文档注释文件

---

## 组件说明

### 核心服务组件

#### D3DriverOrchestrationService
**职责**: 编排完整的生成和编译流程

**功能**:
- 协调各个服务组件
- 管理生成流程的六个步骤
- 提供进度回调
- 无 UI 依赖，可独立使用

**关键方法**:
```csharp
GenerateD3ProjectAsync()   // 生成 D3 项目
CompileD3ProjectAsync()    // 编译项目
RegenerateD3DriverAsync()  // 重新生成驱动类
```

#### ClientCodeGenerator
**职责**: 调用 Tecan Generator 生成客户端代码

**功能**:
- 使用 SilaGeneratorApi 生成接口、DTO、客户端
- 并行处理多个特性文件（性能优化）
- 自动复制依赖 DLL（Tecan.Sila2、gRPC、protobuf）
- 调用代码去重服务

**关键方法**:
```csharp
GenerateClientCode()       // 从 XML 文件生成
GenerateClientCodeFromObject() // 从对象生成
```

#### ClientCodeAnalyzer
**职责**: 分析生成的客户端代码

**功能**:
- 使用 Roslyn 编译 C# 代码到程序集
- 反射提取接口、方法、参数信息
- 解析 XML 文档注释
- 检测参数和返回类型是否支持
- 自动判断维护方法（根据方法名）

**支持的类型**:
- 基础类型：int, string, double, bool, DateTime 等
- 枚举类型
- 数组和 List<T>

**关键方法**:
```csharp
Analyze()                  // 分析客户端代码目录
CompileToAssembly()        // 编译代码
ExtractMethodInfo()        // 提取方法信息
IsSupportedType()          // 检测类型支持
```

#### D3DriverGeneratorService
**职责**: 生成 D3 驱动封装层代码

**功能**:
- 使用 CodeDOM 生成 4 个核心文件
- 生成 .csproj 和 .sln
- 复制依赖库到 lib 目录
- 执行 dotnet build 编译

**生成的文件**:
- `AllSila2Client.cs` - 多特性整合类
- `D3Driver.cs` - D3 驱动主类（带特性标记）
- `Sila2Base.cs` - 基类实现
- `CommunicationPars.cs` - IP/Port 配置

**关键方法**:
```csharp
Generate()                 // 生成所有代码
CompileProjectAsync()      // 编译项目
RegenerateD3DriverFile()   // 重新生成驱动类
```

#### GeneratedCodeDeduplicator
**职责**: 去除重复的类型定义

**功能**:
- 使用 Roslyn 解析 C# 代码
- 识别顶层类型（类、枚举、结构、接口、委托）
- 注释掉重复定义，保留第一次出现
- 不影响嵌套类型和不同命名空间的同名类型

**关键方法**:
```csharp
DeduplicateGeneratedCode() // 去重主方法
```

#### ServerDiscoveryService
**职责**: 扫描网络中的 SiLA2 服务器

**功能**:
- 使用 BR.PC.Device.Sila2Discovery 进行 mDNS 扫描
- 支持超时控制
- 获取服务器信息和特性列表

**关键方法**:
```csharp
ScanServersAsync()         // 扫描服务器
DownloadFeatureAsync()     // 下载特性定义
```

#### Sila2RealTimeDiscoveryService
**职责**: 实时监控 SiLA2 服务器

**功能**:
- 自动监控服务器上线/下线
- 事件驱动通知 UI
- 管理监控生命周期

**关键事件**:
```csharp
event ServerDiscoveredEventHandler ServerDiscovered   // 服务器上线
event ServerLostEventHandler ServerLost              // 服务器下线
```

#### LoggerService
**职责**: 结构化日志管理

**功能**:
- 基于 Serilog 的结构化日志
- 支持 Debug/Information/Warning/Error/Critical 级别
- 自动按日滚动
- 保留 30 天，单文件最大 1GB
- 添加线程 ID 和机器名

**关键方法**:
```csharp
Initialize()               // 初始化日志系统
GetLogger<T>()             // 获取日志记录器
CleanupOldLogs()           // 清理过期日志
```

### CodeDOM 代码生成器

位于 `Services/CodeDom/` 目录，使用 Microsoft.CodeAnalysis.CSharp.Workspaces 生成代码。

#### AllSila2ClientGenerator
生成 `AllSila2Client.cs`，整合多个特性客户端。

**特点**:
- 为每个特性创建私有字段
- 平铺所有方法到一个类
- 处理方法名冲突（添加特性前缀）
- 保持原始方法签名

#### D3DriverGenerator
生成 `D3Driver.cs`，D3 驱动主类。

**特点**:
- 添加 `[DeviceClass]` 特性
- 添加 `[MethodOperations]` 和 `[MethodMaintenance]` 特性
- 自动转换不支持的类型为 JSON 字符串
- 生成完整的 XML 注释
- 维护方法自动编号

**类型转换示例**:
```csharp
// 原始方法（AllSila2Client）
public virtual ComplexResponse Method(ComplexType param)

// 转换后（D3Driver）
[MethodOperations]
public virtual string Method(string paramJsonString)
{
    var param = JsonConvert.DeserializeObject<ComplexType>(paramJsonString);
    var result = this._sila2Device.Method(param);
    return JsonConvert.SerializeObject(result);
}
```

#### Sila2BaseGenerator
生成 `Sila2Base.cs`，实现基本的连接和断开逻辑。

#### CommunicationParsGenerator
生成 `CommunicationPars.cs`，IP 和 Port 配置。

---

## 工作流程

### 完整生成流程（6步）

```
1. 生成命名空间和输出目录
   └─> BR.ECS.DeviceDrivers.{DeviceType}.{Brand}_{Model}

2. 生成客户端代码（Tecan Generator）
   ├─> I*.cs (接口定义)
   ├─> *Dtos.cs (数据传输对象)
   ├─> *Client.cs (客户端实现)
   └─> 复制必需的 DLL

3. 代码去重
   └─> 注释掉重复的类型定义

4. 分析客户端代码（Roslyn + Reflection）
   ├─> 编译到程序集
   ├─> 提取方法、参数、返回值信息
   ├─> 提取 XML 文档注释
   └─> 检测类型支持情况

5. 生成 D3 驱动封装代码（CodeDOM）
   ├─> AllSila2Client.cs (多特性整合)
   ├─> D3Driver.cs (驱动主类)
   ├─> Sila2Base.cs (基类)
   ├─> CommunicationPars.cs (配置)
   ├─> .csproj 和 .sln
   └─> 复制依赖库到 lib/

6. 编译项目（dotnet build）
   ├─> 生成 DLL
   └─> 生成 XML 文档
```

### UI 交互流程

```
用户选择特性
    ↓
点击"生成项目"
    ↓
DeviceInfoDialog (输入设备信息)
    ↓
执行生成流程（步骤 1-4）
    ↓
MethodPreviewWindow (配置方法特性)
    ↓
继续生成流程（步骤 5）
    ↓
点击"编译项目"
    ↓
执行编译流程（步骤 6）
    ↓
打开 DLL 目录
```

### 方法特性调整流程

```
点击"调整特性"
    ↓
重新打开 MethodPreviewWindow
    ↓
修改方法的"调度"或"维护"标记
    ↓
点击"确定"
    ↓
只重新生成 D3Driver.cs（RegenerateD3DriverFile）
    ↓
重新编译项目
```

---

## 文件清单

### Models 目录（数据模型）

| 文件 | 功能 |
|------|------|
| `ClientAnalysisResult.cs` | 客户端分析结果，包含特性列表和错误信息 |
| `ClientFeatureInfo.cs` | 特性信息，包含接口类型、方法列表 |
| `CompilationResult.cs` | 编译结果，包含成功/失败和错误列表 |
| `D3DriverGenerationConfig.cs` | D3驱动生成配置，包含品牌、型号、特性列表 |
| `DynamicParameterViewModel.cs` | 动态参数视图模型 |
| `FeatureTreeNodeBase.cs` | 特性树节点基类（三态选择） |
| `GenerationResult.cs` | 生成结果，包含成功/失败和生成的文件列表 |
| `LocalFeatureNodeViewModel.cs` | 本地特性节点视图模型 |
| `MethodGenerationInfo.cs` | 方法信息，包含名称、参数、返回值、特性标记 |
| `ParameterGenerationInfo.cs` | 参数信息，包含名称、类型、描述 |
| `ServerInfoViewModel.cs` | 服务器节点视图模型（在线服务器） |
| `XmlDocumentationInfo.cs` | XML 文档注释信息 |

### Services 目录（业务服务）

| 文件 | 功能 |
|------|------|
| `ClientCodeGenerator.cs` | 调用 Tecan Generator 生成客户端代码 |
| `ClientCodeAnalyzer.cs` | 使用 Roslyn 分析客户端代码 |
| `D3DriverGeneratorService.cs` | 生成 D3 驱动封装层代码 |
| `D3DriverOrchestrationService.cs` | 编排完整的生成和编译流程 |
| `GeneratedCodeDeduplicator.cs` | 去除重复的类型定义 |
| `ServerDiscoveryService.cs` | 扫描 SiLA2 服务器 |
| `Sila2RealTimeDiscoveryService.cs` | 实时监控 SiLA2 服务器 |
| `ServerInteractionService.cs` | 服务器交互（下载特性） |
| `LocalFeaturePersistenceService.cs` | 本地特性持久化 |
| `LoggerService.cs` | 结构化日志服务 |
| `ConfigurationService.cs` | 配置管理 |
| `UserPreferencesService.cs` | 用户偏好设置 |
| `ServiceLocator.cs` | 服务定位器 |
| `ServiceCollectionExtensions.cs` | 依赖注入扩展 |

### Services/CodeDom 目录（代码生成器）

| 文件 | 功能 |
|------|------|
| `AllSila2ClientGenerator.cs` | 生成 AllSila2Client.cs |
| `D3DriverGenerator.cs` | 生成 D3Driver.cs |
| `Sila2BaseGenerator.cs` | 生成 Sila2Base.cs |
| `CommunicationParsGenerator.cs` | 生成 CommunicationPars.cs |
| `TestConsoleGenerator.cs` | 生成测试控制台代码（可选） |

### ViewModels 目录（视图模型）

| 文件 | 功能 |
|------|------|
| `D3DriverViewModel.cs` | 主界面逻辑，管理特性选择和项目生成 |
| `MethodPreviewViewModel.cs` | 方法预览和特性调整逻辑 |
| `DeviceInfoDialogViewModel.cs` | 设备信息输入对话框逻辑 |
| `MainViewModel.cs` | 主窗口视图模型 |
| `DesignTimeData.cs` | 设计时数据（用于 XAML 预览） |

### Views 目录（用户界面）

| 文件 | 功能 |
|------|------|
| `D3DriverView.xaml/.cs` | 主界面（特性选择、项目生成） |
| `MethodPreviewWindow.xaml/.cs` | 方法预览和特性调整窗口 |
| `DeviceInfoDialog.xaml/.cs` | 设备信息输入对话框 |

### Converters 目录（值转换器）

| 文件 | 功能 |
|------|------|
| `BooleanToVisibilityConverter.cs` | 布尔值转可见性 |
| `InverseBooleanConverter.cs` | 布尔值取反 |
| `NullToVisibilityConverter.cs` | 空值转可见性 |
| `StringEmptyToVisibilityConverter.cs` | 空字符串转可见性 |

### Behaviors 目录（行为）

| 文件 | 功能 |
|------|------|
| `TreeViewSelectionBehavior.cs` | TreeView 选择行为 |

### 根目录文件

| 文件 | 功能 |
|------|------|
| `App.xaml/.cs` | 应用程序入口，初始化日志和服务 |
| `MainWindow.xaml/.cs` | 主窗口 |
| `appsettings.json` | 应用配置 |
| `SilaGeneratorWpf.csproj` | 项目文件 |

---

## 设计原则

### 1. MVVM 架构
- **View** - 纯 UI，不包含业务逻辑
- **ViewModel** - UI 逻辑和命令处理，使用 CommunityToolkit.Mvvm
- **Model** - 数据模型和业务实体
- **Service** - 业务服务，无 UI 依赖

### 2. 关注点分离
- **UI 层** - 只负责显示和用户交互
- **业务层** - 编排服务负责流程，生成服务负责具体任务
- **数据层** - 模型只包含数据和简单验证

### 3. 依赖倒置
- 使用接口定义服务契约（IServiceLocator）
- 通过依赖注入管理服务生命周期
- 服务之间通过接口通信

### 4. 单一职责
- 每个服务只负责一个明确的功能
- ClientCodeGenerator 只负责生成
- ClientCodeAnalyzer 只负责分析
- D3DriverGeneratorService 只负责 D3 驱动生成

### 5. 开闭原则
- CodeDOM 生成器可扩展（新增生成器类）
- 类型支持检测可扩展（IsSupportedType 方法）
- 日志级别可配置

### 6. 同步执行
- D3 调用的方法必须是同步的
- 可观察命令使用 `GetAwaiter().GetResult()` 阻塞等待
- 避免在 D3Driver 中使用 async/await

### 7. 友好的错误处理
- 捕获所有异常并记录日志
- 提供友好的错误提示
- 编译错误提供详细信息和位置

### 8. 性能优化
- 并行处理多个特性文件
- 使用 ConcurrentBag 线程安全集合
- 缓存反射结果

---

## 依赖库

### 核心依赖（reflib 目录）

| 库 | 版本 | 用途 |
|---|------|------|
| **Tecan.Sila2** | 4.4.1 | SiLA2 核心库 |
| **Tecan.Sila2.Client** | 4.4.1 | SiLA2 客户端 |
| **Tecan.Sila2.DynamicClient** | 4.4.1 | 动态客户端支持 |
| **Tecan.Sila2.Generator** | 4.4.1 | 代码生成器 API |
| **BR.PC.Device.Sila2Discovery** | - | mDNS 扫描和发现 |
| **BR.ECS.Executor.Device.Domain.Contracts** | - | D3 设备契约 |
| **BR.ECS.Executor.Device.Domain.Share** | - | D3 共享类型 |
| **BR.ECS.Executor.Device.Infrastructure** | - | D3 基础设施 |
| **Grpc.Core** | - | gRPC 核心库 |
| **Grpc.Core.Api** | - | gRPC API |
| **Google.Protobuf** | - | Protobuf 序列化 |
| **Newtonsoft.Json** | 13.0.3 | JSON 序列化 |

### NuGet 包

| 包 | 用途 |
|---|------|
| **CommunityToolkit.Mvvm** | MVVM 框架 |
| **Serilog** | 日志库 |
| **Serilog.Sinks.File** | 文件日志 |
| **Serilog.Sinks.Debug** | 调试日志 |
| **Microsoft.Extensions.Logging** | 日志抽象 |
| **Microsoft.CodeAnalysis.CSharp** | Roslyn 编译器 |
| **Microsoft.CodeAnalysis.CSharp.Workspaces** | Roslyn 工作空间 |

---

## 使用指南

### 快速开始

#### 方式 1: 使用本地文件（推荐）

1. 点击 **📁 添加** 导入 .sila.xml 文件
2. 在左侧树形列表勾选需要的特性
3. 点击 **✨ 生成项目**
4. 输入设备信息（品牌、型号、类型、开发者）
5. 在方法预览窗口配置"调度方法"和"维护方法"
6. 等待生成完成
7. 点击 **🔨 编译项目**
8. 编译成功后，点击 **📦 DLL目录** 查看输出

#### 方式 2: 使用在线服务器

1. 点击 **🔍 扫描** 扫描网络服务器（或等待实时监控）
2. 勾选需要的特性（限同一服务器）
3. 后续步骤同方式 1 的第 3-8 步

### 高级功能

#### 导出特性文件
1. 勾选需要导出的特性
2. 点击 **📤 导出** 按钮
3. 选择导出目录

#### 调整方法特性
1. 生成项目后，点击 **🔧 调整特性**
2. 重新配置方法的"调度"或"维护"标记
3. 点击"确定"后自动重新生成 D3Driver.cs
4. 重新编译项目

#### 批量操作
在方法预览窗口中：
- **全部调度** - 将所有方法标记为调度方法
- **全部维护** - 将所有方法标记为维护方法
- **清除特性** - 清除所有方法的特性标记

### 生成的文件结构

```
BR.ECS.DeviceDrivers.{DeviceType}.{Brand}_{Model}/
├── BR.ECS.DeviceDrivers.{DeviceType}.{Brand}_{Model}.csproj
├── BR.ECS.DeviceDrivers.{DeviceType}.{Brand}_{Model}.sln
├── AllSila2Client.cs          # 多特性整合类
├── D3Driver.cs                 # D3驱动主类
├── Sila2Base.cs                # 基类实现
├── CommunicationPars.cs        # IP/Port 配置
├── GeneratedClient/            # Tecan Generator 生成
│   ├── I*.cs                   # 接口定义
│   ├── *Dtos.cs                # 数据传输对象
│   ├── *Client.cs              # 客户端实现
│   └── *.dll                   # 依赖库
└── lib/                        # D3 依赖库
    ├── BR.PC.Device.Sila2Discovery.dll
    ├── BR.ECS.Executor.Device.*.dll
    └── ...
```

---

## 故障排除

### 常见问题

#### 1. 扫描不到在线服务器

**现象**: 点击"扫描"后没有服务器显示

**可能原因**:
- SiLA2 服务器未运行
- 不在同一网络内
- 防火墙阻止 mDNS（端口 5353）
- 网络适配器配置问题

**解决方案**:
1. 确认 SiLA2 服务器正在运行
2. 检查网络连接（ping 服务器 IP）
3. 临时关闭防火墙测试
4. 使用本地文件方式（导出 .sila.xml）

#### 2. 编译失败

**现象**: 编译时报错

**可能原因**:
- 缺少 .NET SDK 8.0
- reflib 目录缺少必需的 DLL
- 生成的代码有语法错误
- 依赖库版本不兼容

**解决方案**:
1. 安装 .NET SDK 8.0+：`dotnet --version`
2. 检查 reflib 目录是否完整
3. 查看编译错误详情
4. 检查日志文件：`Logs/SilaGenerator_yyyyMMdd.log`

#### 3. 类型不支持错误

**现象**: 方法预览显示"不支持的类型"

**说明**: 这是正常的，系统会自动转换为 JSON 字符串

**使用方法**:
```csharp
// 调用时传递 JSON 字符串
var json = JsonConvert.SerializeObject(complexObject);
driver.Method(json);

// 返回值也是 JSON 字符串
var resultJson = driver.Method(json);
var result = JsonConvert.DeserializeObject<ComplexType>(resultJson);
```

#### 4. 方法命名冲突

**现象**: 不同特性有相同的方法名

**解决方案**: 系统自动添加 `FeatureName_` 前缀

```csharp
// 原始方法名
Feature1.GetValue()
Feature2.GetValue()

// 生成后
Feature1_GetValue()
Feature2_GetValue()
```

#### 5. 跨服务器选择

**现象**: 选择不同服务器的特性时提示错误

**原因**: D3 驱动只能连接一个服务器

**解决方案**: 只选择同一服务器的特性

#### 6. 控制台窗口问题

**现象**: 启动时闪现控制台窗口

**说明**: 这是正常的，BR.PC.Device.Sila2Discovery 库需要控制台句柄



#### 7. Stream 类型过滤

**现象**: 方法预览中有些方法不显示

**原因**: Stream 类型方法被过滤（D3 不支持）



#### 8. JSON 转换问题

**现象**: 复杂类型参数或返回值报错

**解决方案**: 确保 DTO 类有默认构造函数和 `[JsonConstructor]` 特性



### 日志分析

日志文件位置：`Logs/SilaGenerator_yyyyMMdd.log`

**日志级别**:
- **Debug** - 详细的调试信息（Debug 模式）
- **Information** - 关键步骤和进度
- **Warning** - 警告信息（不影响运行）
- **Error** - 错误信息（影响功能）
- **Critical** - 严重错误（应用崩溃）

**常见日志关键词**:
- `开始生成D3项目` - 生成流程开始
- `编译成功` - 编译完成
- `编译失败` - 编译错误
- `CS####` - C# 编译器错误代码
- `Exception` - 异常堆栈

### 性能调优

#### 大型项目优化

**问题**: 生成多个特性时速度慢

**优化**:
- 系统已使用并行处理（ClientCodeGenerator）
- 减少选择的特性数量
- 使用 SSD 硬盘

#### 编译优化

**问题**: 编译时间长

**优化**:
- 项目配置不生成 nullable 警告
- 使用 Release 配置编译
- 关闭不必要的代码分析

---

## 系统要求

- **.NET SDK 8.0+** - 必需

