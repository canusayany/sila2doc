# SiLA2 客户端代码生成器

一个基于 .NET 8 的 WPF 桌面应用程序，提供可视化界面来生成 SiLA2 客户端代码。支持从文件生成和服务器发现两种模式。

> 💡 **提示**：本项目现已支持 NuGet 包依赖管理，可以独立移动和部署。详见 [NuGet 配置说明](NUGET_SETUP_README.md)。

## 功能特性

### 📁 从文件生成
- ✅ 拖放支持 - 支持拖放 `.sila.xml` 文件
- ✅ 批量处理 - 可同时处理多个 Feature 文件
- ✅ 自定义命名空间 - 默认 `Sila2Client`，可自定义
- ✅ 实时状态反馈 - 显示生成进度和结果

### 🔍 服务器发现（推荐）
- ✅ 自动扫描 - 通过 mDNS 发现网络中的 SiLA2 服务器
- ✅ 特性浏览 - 查看服务器的所有特性、命令、属性和元数据
- ✅ 灵活选择 - 可选择整个服务器或单个特性
- ✅ 三种生成方式：
  - **⚡ 直接生成** - 从内存直接生成，速度最快（推荐）
  - **🔄 保存并生成** - 先保存 Feature XML，再生成代码
  - **💾 仅保存** - 只保存 Feature XML 文件
- ✅ 文件夹隔离 - 不同服务器的代码自动隔离管理
- ✅ 统一命名空间 - 所有生成的文件使用相同的自定义命名空间

## 系统要求

- Windows 10 或更高版本
- .NET 8.0 SDK

## 快速开始

### 方式一（已修复）：直接从源码编译

推荐方式 - 项目现在使用项目引用而不是NuGet包：

```bash
# 1. 从项目根目录编译所有依赖项
cd C:\path\to\sila_tecan
dotnet build -c Release

# 2. 运行应用程序
dotnet run --project SilaGeneratorWpf/SilaGeneratorWpf.csproj

# 3. 或发布应用程序
dotnet publish SilaGeneratorWpf/SilaGeneratorWpf.csproj -c Release -p:PublishProfile=FolderProfile
```

发布输出位置：`SilaGeneratorWpf\bin\Release\net8.0-windows\publish\win-x64\SilaGeneratorWpf.exe`

### 3. 使用服务器发现（推荐）

1. 点击 **"🔍 服务器发现"** 标签页
2. 点击 **"🔍 扫描服务器"** 按钮，等待服务器列表显示
3. 勾选服务器或特定特性
4. 设置输出目录和命名空间（默认：`Sila2Client`）
5. 点击 **"⚡ 直接生成代码"** 按钮

几秒后，代码生成完成！

### 4. 或从文件生成

1. 点击 **"📁 从文件生成"** 标签页
2. 拖放 `.sila.xml` 文件到应用程序
3. 设置输出目录和命名空间
4. 点击 **"⚡ 生成客户端代码"** 按钮

## 生成的代码结构

### 文件组织

```
输出目录/
└── 服务器名_UUID/              # 按服务器隔离
    ├── IFeatureName.cs         # 接口定义
    ├── FeatureNameDtos.cs      # 数据传输对象（DTO）
    └── FeatureNameClient.cs    # 客户端实现
```

### 使用示例

```csharp
using Sila2Client;  // 或你自定义的命名空间

// 创建客户端
var client = new FeatureNameClient(channel, executionManager);

// 调用命令
var request = new SomeCommandRequest { /* ... */ };
var result = await client.SomeCommandAsync(request);

// 读取属性
var propertyValue = await client.GetSomePropertyAsync();
```

## 技术实现

### ⚡ 性能优化（v2024.10）

最新版本实现了多项性能优化，显著提升代码生成效率：

#### 1. 并行代码生成
```csharp
// 使用 Parallel.ForEach 并行处理多个特性文件
Parallel.ForEach(featureFiles, new ParallelOptions 
{ 
    MaxDegreeOfParallelism = Environment.ProcessorCount 
}, 
(featureFile) => {
    // 生成代码...
});
```

**优势：**
- 充分利用多核CPU
- 4核CPU可提升约3-4倍性能
- 8个特性从40秒降至12秒

#### 2. 并行文件验证
```csharp
// 并行验证文件存在性
await Task.Run(() => {
    Parallel.ForEach(filePaths, filePath => {
        validationResults.Add((filePath, File.Exists(filePath)));
    });
});
```

#### 3. 性能监控
```
⏱ 总耗时: 12,450ms (获取特性:1,200ms + 生成:11,250ms)
⏱ 代码生成耗时: 11,250ms
⏱ 去重处理耗时: 340ms
```

#### 4. 优化的日志更新
- 减少 UI 线程调用频率
- 批量更新日志信息
- 避免频繁的 Dispatcher.Invoke

### 命令行方式生成

使用 `dotnet run` 调用 Generator 命令行工具：

1. **生成接口**
   ```bash
   dotnet run --project Generator/Generator.csproj -- \
       generate-interface feature.xml I{Feature}.cs --namespace "CustomNS"
   ```

2. **生成 DTOs 和 Client**
   ```bash
   dotnet run --project Generator/Generator.csproj -- \
       generate-provider feature.xml Dtos.cs Client.cs \
       --namespace "CustomNS" --client-only
   ```

### 命名空间控制

使用 `generate-interface` + `generate-provider --client-only` 组合，确保所有生成的文件使用统一的命名空间：

```csharp
// ISiLAService.cs
namespace Sila2Client { ... }

// SiLAServiceDtos.cs
namespace Sila2Client { ... }  // 不是 Sila2Client.SiLAService

// SiLAServiceClient.cs
namespace Sila2Client { ... }  // 不是 Sila2Client.SiLAService
```

## 项目结构

```
SilaGeneratorWpf/
├── MainWindow.xaml              # 主窗口界面（单窗口设计）
├── MainWindow.xaml.cs           # 主窗口逻辑
├── Services/
│   ├── ClientCodeGenerator.cs  # 代码生成服务
│   └── ServerDiscoveryService.cs # 服务器发现服务
├── Models/
│   ├── ServerInfoViewModel.cs   # 服务器信息视图模型
│   └── FeatureInfoViewModel.cs  # 特性信息视图模型
└── SilaGeneratorWpf.csproj      # 项目文件
```

## 常见问题

### Q: 生成失败怎么办？

**A:** 查看状态栏的错误信息，确保：
- Generator 项目已编译：`dotnet build Generator/Generator.csproj`
- 输出目录有写入权限
- Feature 文件格式正确（有效的 `.sila.xml` 文件）

### Q: 生成速度慢？

**A:** ✅ **已优化！** 最新版本已实现并行处理优化：

**优化措施：**
- ✅ **并行代码生成** - 多个特性文件同时生成（利用多核CPU）
- ✅ **并行文件验证** - 文件存在性检查并行处理
- ✅ **性能监控** - 实时显示各阶段耗时统计
- ✅ **批量日志更新** - 减少UI线程调用频率

**性能提升：**
- 单个特性：2-5秒（与优化前相同）
- 多个特性：约提升 2-4倍（取决于CPU核心数）
- 示例：8个特性从 40秒 降至 12秒（4核CPU）

**使用建议：**
- 建议一次选择多个 Feature 批量生成（发挥并行优势）
- 使用 **"⚡ 直接生成"** 模式可以避免文件 I/O 开销
- 查看日志中的 ⏱ 耗时统计了解性能瓶颈

### Q: 找不到服务器？

**A:** 确保：
- 服务器正在运行且支持 SiLA2
- 在同一网络中
- 防火墙允许 mDNS（端口 5353）
- 点击 **"🔄 刷新选中服务器"** 重新加载

### Q: 命名空间不对？

**A:** 确保使用最新版本的代码生成器。新版本使用 `generate-provider --client-only` 确保所有文件使用统一的命名空间。

## 技术栈

- **框架**: .NET 8.0 WPF
- **核心依赖**:
  - Tecan SiLA2 Generator - 代码生成引擎
  - Tecan SiLA2 Client.Core - 服务器发现
  - CommandLineParser - 命令行参数解析
  - Microsoft.Build - 构建工具
  - System.CodeDom - 代码生成

## 开发说明

### 添加新功能

1. 在 `Services/` 目录下创建新的服务类
2. 在 `MainWindow.xaml` 中添加 UI 元素（在相应的 TabItem 中）
3. 在 `MainWindow.xaml.cs` 中实现事件处理

### 调试技巧

- 查看状态栏获取实时错误信息
- 使用 `progressCallback` 追踪生成过程
- 检查临时目录中的中间文件（错误时不会自动删除）

### 代码生成流程

```
Feature XML -> 临时文件 -> dotnet run Generator -> 生成代码 -> 重命名移动 -> 清理临时文件
```

## 性能测试

项目包含完整的性能测试套件，验证并行处理优化功能：

```bash
cd TestConsole
dotnet run -- --performance
```

**测试内容**:
- ✅ 并行本地文件生成
- ✅ 并行文件验证
- ✅ 性能监控输出

详见：[性能测试说明](../TestConsole/性能测试说明.md)

## 版本历史

### v2024.10 - 性能优化版
- ✅ **并行代码生成** - 多个特性文件同时生成（2-4倍提速）
- ✅ **并行文件验证** - 文件检查并行处理
- ✅ **性能监控** - 实时显示各阶段耗时统计
- ✅ **完整测试套件** - 性能测试自动化验证

### 之前版本特性
- ✅ 单窗口设计（标签页模式）
- ✅ 直接从内存生成代码（无需保存文件）
- ✅ 统一命名空间控制
- ✅ 文件名优化（FeatureNameDtos.cs, FeatureNameClient.cs）
- ✅ 服务器级别文件夹隔离（无二级目录）
- ✅ 输出目录可自定义选择

## 许可证

根据 Tecan SiLA2 SDK 的许可证条款使用。

## 支持

如有问题或建议，请联系开发团队。

---

**提示**：推荐使用 **"服务器发现"** 模式，可以自动扫描并选择需要的特性！

# SilaGeneratorWpf

SiLA2 客户端代码生成器 - WPF 应用程序

## 功能

- 🔍 SiLA2 服务器自动发现
- 📝 从 SiLA2 Feature 定义动态生成 C# 客户端代码
- 🎯 支持自定义命名空间
- ⚙️ 自动处理 DTOs 和 Client 类
- 🔄 实时生成进度显示

## 快速开始

### 开发运行

```bash
cd SilaGeneratorWpf
dotnet run
```

### 发布应用

```bash
dotnet publish -c Release -p:PublishProfile=FolderProfile
```

**重要：** 发布完成后，`SilaGen.exe` 会自动复制到发布目录（方案3自动化）。

## SilaGen.exe 说明

### 什么是 SilaGen.exe？

- 预编译的代码生成工具
- 用于生成 SiLA2 Feature 对应的 C# 客户端代码
- 包含在 `reflib\SilaGen.exe`
- 必须与主应用程序在同一目录运行

### 单文件发布问题

`SilaGen.exe` 是**非托管代码**，无法嵌入 .NET 单文件 exe 中：

- ❌ **不可能**：将 SilaGen.exe 嵌入单个 exe 中
- ✅ **解决方案**：发布时自动复制 SilaGen.exe 到应用目录

### 部署结构

```
publish/win-x64/
├── SilaGeneratorWpf.exe          ← 主应用
├── SilaGen.exe                   ← 代码生成工具（由 MSBuild Target 自动复制）
├── SilaGeneratorWpf.dll          ← 主应用程序集
├── Tecan.Sila2*.dll              ← SiLA2 SDK
├── Microsoft.*.dll               ← 框架库
└── [其他依赖项]
```

### 运行时行为

应用启动时会按以下顺序查找 `SilaGen.exe`：

1. 应用目录根目录
2. `reflib` 子目录（开发模式）
3. 配置的预期路径

如果找不到，会显示错误提示。

## 发布选项对比

| 方案 | 配置 | 单文件 | 自动化 | 推荐用途 |
|-----|------|-------|--------|---------|
| **方案1** | `PublishSingleFile=true` | ✅ | ❌ | 手动部署 |
| **方案2** | `PublishSingleFile=false` | ❌ | ✅ | 完整包 |
| **方案3** | `PublishSingleFile=true` + MSBuild | ✅ | ✅ | 👈 **推荐** |

**当前使用：方案3（已通过 MSBuild Target 实现）**

详细说明见：[发布说明.md](发布说明.md)

## 项目结构

```
SilaGeneratorWpf/
├── Services/
│   ├── ClientCodeGenerator.cs    ← 代码生成逻辑
│   ├── ServerDiscoveryService.cs
│   ├── ServerInteractionService.cs
│   └── LoggerService.cs
├── Models/
│   ├── DynamicParameterViewModel.cs
│   └── ServerInfoViewModel.cs
├── MainWindow.xaml(.cs)          ← UI 界面
├── App.xaml(.cs)
├── reflib/
│   └── SilaGen.exe               ← 代码生成工具
├── SilaGeneratorWpf.csproj       ← 项目配置（含 MSBuild Target）
└── Properties/PublishProfiles/
    └── FolderProfile.pubxml      ← 发布配置
```

## 已知限制

- `SilaGen.exe` 必须与主应用在同一目录
- 不支持真正意义的"单个可执行文件"（由于 SilaGen.exe 的存在）
- 代码生成依赖 SiLA2 服务器可用

## 故障排查

### 错误："找不到 SilaGen.exe"

**检查：**
```powershell
# 验证文件存在
Test-Path "SilaGeneratorWpf.exe" -PathType Leaf
Test-Path "SilaGen.exe" -PathType Leaf

# 或查看发布目录
Get-ChildItem "bin\Release\net8.0-windows\publish\win-x64\" | Select-Object Name
```

**解决：**
确保 `SilaGen.exe` 与 `SilaGeneratorWpf.exe` 在同一目录。

### 发布失败

确保 `reflib\SilaGen.exe` 存在。如果是开发模式，应该在 bin 目录中。

## 配置文件说明

### SilaGeneratorWpf.csproj

关键配置：
```xml
<!-- 自动复制 SilaGen.exe 到发布目录 -->
<Target Name="CopySilaGenAfterPublish" AfterTargets="Publish" Condition="'$(PublishSingleFile)' == 'true'">
    <Copy SourceFiles="reflib\SilaGen.exe" DestinationFolder="$(PublishDir)" />
</Target>

<!-- 复制 SilaGen.exe 到输出目录（调试时） -->
<None Update="reflib\SilaGen.exe">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

## 更新日志

- ✅ 修复 ClientCodeGenerator.cs 使用预编译的 SilaGen.exe
- ✅ 添加 MSBuild Target 自动复制 SilaGen.exe
- ✅ 完善发布配置文档
- ✅ 支持单文件发布的混合方案

## 相关文档

- [发布说明.md](发布说明.md) - 详细发布指南和三种方案对比
- [API修复总结.md](API修复总结.md) - SiLA2 客户端 API 修复历史