# Sila2DriverGen.TestConsole

## 概述

这是一个功能测试控制台应用程序，用于指导测试 `SilaGeneratorWpf` 项目中的 **"🎯 生成D3驱动"** Tab 页面功能。

## 目的

由于 `SilaGeneratorWpf` 是 WPF 图形界面应用，无法进行传统的自动化单元测试。本控制台提供：
- 测试场景指南
- 测试步骤说明
- 预期行为验证
- 三态CheckBox行为测试理论验证

## 运行方式

### 方式1：从 Visual Studio 运行

1. 打开 `Sila2Gen.sln` 解决方案
2. 将 `Sila2DriverGen.TestConsole` 设为启动项目
3. 按 F5 或点击"运行"

### 方式2：从命令行运行

```bash
cd Sila2DriverGen/TestConsole
dotnet run
```

## 测试菜单

### 测试1：从本地XML生成D3项目
测试从本地 `.sila.xml` 文件生成完整的D3驱动项目。

**测试内容**：
- 自动查找示例XML文件（TemperatureController-v1_0.sila.xml）
- 调用 `GenerateD3ProjectAsync` 生成项目
- 生成客户端代码、分析、生成D3驱动代码
- 显示生成结果和项目路径

**验证点**：
- ✓ 项目目录已创建
- ✓ 客户端代码已生成（GeneratedClient目录）
- ✓ D3驱动文件已生成（AllSila2Client.cs, D3Driver.cs等）
- ✓ 项目文件已生成（.csproj, .sln）

### 测试2：编译已生成的D3项目
测试编译上一步生成的D3项目。

**测试内容**：
- 使用测试1生成的项目路径
- 或手动输入项目路径
- 调用 `CompileD3ProjectAsync` 编译项目
- 显示编译结果和DLL路径

**验证点**：
- ✓ 编译成功，无错误
- ✓ DLL文件已生成在 bin/Release 目录
- ✓ 编译警告数量合理

### 测试3：完整流程（生成+编译）
端到端测试完整的生成到编译流程。

**测试内容**：
- 依次执行测试1和测试2
- 验证完整流程的连贯性

**验证点**：
- ✓ 生成和编译均成功
- ✓ 所有文件均已正确生成

### 测试4：调整方法分类并重新生成
测试方法分类调整功能（Operations vs Maintenance）。

**测试内容**：
- 使用已生成的项目
- 创建示例方法分类配置
- 调用 `AdjustMethodClassificationsAsync`
- 重新生成D3Driver.cs文件

**验证点**：
- ✓ 方法分类已应用
- ✓ D3Driver.cs文件已更新
- ✓ 可以重新编译项目

### 测试5：错误处理测试（无效文件）
测试错误处理机制：使用不存在的XML文件。

**测试内容**：
- 使用不存在的XML文件路径
- 验证错误捕获和报告机制

**验证点**：
- ✓ 成功捕获错误
- ✓ 返回清晰的错误信息
- ✓ 程序不会崩溃

### 测试6：错误处理测试（编译失败）
测试编译错误处理：尝试编译不存在的项目。

**测试内容**：
- 尝试编译不存在的项目目录
- 验证编译错误处理

**验证点**：
- ✓ 成功捕获编译错误
- ✓ 返回清晰的错误信息
- ✓ 程序不会崩溃

### 测试7：查看测试说明
显示详细的测试说明和验证内容。

## 测试说明

### 测试架构

本测试控制台直接调用 `D3DriverOrchestrationService` 服务类，验证其无UI依赖的功能接口。

**关键服务类**：
- `D3DriverOrchestrationService` - 编排服务（无UI依赖）
- `ClientCodeGenerator` - 客户端代码生成
- `ClientCodeAnalyzer` - 代码分析
- `D3DriverGeneratorService` - D3驱动代码生成

### 测试流程

```
[测试1] 生成D3项目
    ↓
[测试2] 编译项目
    ↓
[测试4] 调整方法分类 → 重新编译
    ↓
验证完成

[测试5/6] 错误处理测试（独立运行）
```

### 完整测试步骤

#### 步骤 1：运行测试控制台
```bash
cd Sila2DriverGen/TestConsole
dotnet run
```

#### 步骤 2：执行测试1（生成D3项目）
- 自动查找 TemperatureController-v1_0.sila.xml
- 生成完整的D3驱动项目
- 记录生成的项目路径

#### 步骤 3：执行测试2（编译项目）
- 使用测试1生成的项目路径
- 调用 dotnet build 编译
- 验证DLL生成成功

#### 步骤 4：执行测试4（调整方法分类）
- 重新分析客户端代码
- 应用方法分类配置
- 重新生成D3Driver.cs
- 可选：重新编译验证

#### 步骤 5：执行测试5和6（错误处理）
- 测试无效文件错误处理
- 测试编译失败错误处理
- 验证异常捕获和报告

#### 步骤 6：验证结果
检查生成的项目目录，确认以下文件存在：
- `GeneratedClient/` - 客户端代码目录
  - `I{FeatureName}.cs` - 接口文件
  - `{FeatureName}Client.cs` - 客户端实现
  - `DTOs/*.cs` - 数据传输对象
- `AllSila2Client.cs` - 所有客户端的聚合类
- `D3Driver.cs` - D3驱动主类
- `Sila2Base.cs` - Sila2基类
- `CommunicationPars.cs` - 通信参数类
- `{Brand}{Model}.D3Driver.csproj` - 项目文件
- `{Brand}{Model}.D3Driver.sln` - 解决方案文件
- `lib/` - 依赖库目录
- `bin/Release/` - 编译输出目录（编译后）

## 注意事项

1. **前置条件**：
   - 需要安装 .NET 8.0 SDK
   - 需要网络连接（用于NuGet包还原）
   - 需要示例XML文件（TemperatureController-v1_0.sila.xml）

2. **测试文件位置**：
   - 测试会自动搜索 `TemperatureController-v1_0.sila.xml`
   - 搜索路径包括当前目录、上级目录等
   - 也可手动将XML文件放在TestConsole项目根目录

3. **测试类型**：
   - 这是**功能测试**（非单元测试）
   - 直接测试服务类的公共接口
   - 验证完整的业务流程

4. **在线服务器测试**：
   - 本控制台暂不支持在线服务器模式测试
   - 在线服务器功能请在WPF应用中手动测试
   - 本地XML模式已足够验证核心功能

## 技术栈

- .NET 8.0
- C# Console Application
- 彩色控制台输出
- 交互式菜单

## 相关文档

- [D3驱动生成功能使用指南](../SilaGeneratorWpf/D3_DRIVER_GENERATION_GUIDE.md)
- [实施总结](../IMPLEMENTATION_SUMMARY.md)
- [项目计划](../../plan.md)

## 测试验证清单

### 核心功能验证

- [ ] 从本地XML生成D3项目成功
- [ ] 客户端代码生成正确
- [ ] 代码分析功能正常
- [ ] D3驱动代码生成正确
- [ ] 项目文件生成正确
- [ ] 项目编译成功
- [ ] DLL文件生成正确
- [ ] 方法分类调整功能正常
- [ ] 重新生成D3Driver.cs成功

### 错误处理验证

- [ ] 无效文件错误捕获正确
- [ ] 编译失败错误捕获正确
- [ ] 错误信息清晰明确
- [ ] 程序不会因错误崩溃

### 接口可测试性验证

- [ ] D3DriverOrchestrationService 无UI依赖
- [ ] 所有公共方法可测试
- [ ] 进度回调功能正常
- [ ] 异步操作正常

## 测试结果记录

请在测试后记录结果：

| 测试场景 | 测试日期 | 测试结果 | 备注 |
|---------|---------|---------|------|
| 测试1：生成D3项目 | | ⬜ 通过 / ❌ 失败 | |
| 测试2：编译项目 | | ⬜ 通过 / ❌ 失败 | |
| 测试3：完整流程 | | ⬜ 通过 / ❌ 失败 | |
| 测试4：方法分类调整 | | ⬜ 通过 / ❌ 失败 | |
| 测试5：无效文件处理 | | ⬜ 通过 / ❌ 失败 | |
| 测试6：编译失败处理 | | ⬜ 通过 / ❌ 失败 | |

---

**版本**: 1.0.0  
**更新日期**: 2024-10  
**作者**: AI Assistant

