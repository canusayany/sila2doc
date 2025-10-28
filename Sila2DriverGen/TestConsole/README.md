# SiLA2 D3驱动生成工具 - 测试控制台

## 📋 概述

本测试控制台用于验证 D3DriverOrchestrationService 的功能，提供自动化测试和交互式测试两种模式。

## 🚀 快速开始

### 运行自动化测试

```powershell
cd TestConsole
dotnet run -- --auto
```

### 运行性能测试

```powershell
cd TestConsole
dotnet run -- --performance
```

### 运行日志系统测试

```powershell
cd TestConsole
dotnet run -- --logging
```

### 运行代码清理验证测试

```powershell
cd TestConsole
dotnet run -- --cleanup
```

### 运行交互式测试

```powershell
cd TestConsole
dotnet run
```

## 📦 测试架构

测试代码采用模块化设计，便于维护和扩展：

### 核心组件

```
TestConsole/
├── TestBase.cs               # 测试基类（通用辅助方法）
├── TestDefinitions.cs        # 测试项定义和枚举
├── AutomatedTest.cs          # 自动化测试运行器
├── TestRunner.cs             # 交互式测试运行器
├── ConsoleHelper.cs          # 控制台输出辅助类
└── Program.cs                # 程序入口
```

### 测试基类 (TestBase)

提供所有测试共享的功能：

- **XML文件查找**：`FindXmlFile()`, `FindAllXmlFiles()`
- **请求创建**：`CreateTestRequest()`
- **路径验证**：`ValidateProjectPath()`

### 测试定义 (TestDefinitions)

- **TestItem 枚举**：定义所有测试项
- **TestCategory 枚举**：测试分类（基础/集成/错误处理/在线服务器）
- **TestInfo 类**：测试项详细信息和元数据

## 🧪 测试项清单

### 基础功能测试

| 测试编号 | 测试名称 | 说明 |
|---------|---------|------|
| 测试1 | 从本地XML生成D3项目 | 使用本地.sila.xml文件生成D3驱动项目 |
| 测试2 | 编译已生成的D3项目 | 编译已生成的D3驱动项目并输出DLL |

### 集成测试

| 测试编号 | 测试名称 | 说明 |
|---------|---------|------|
| 测试3 | 完整流程（生成+编译） | 完整测试从生成到编译的整个流程 |
| 测试4 | 调整方法分类并重新生成 | 调整方法的分类（操作/维护）并重新生成D3Driver |
| 测试5 | 多特性完整流程 | 使用多个特性文件测试完整流程 |

### 错误处理测试

| 测试编号 | 测试名称 | 说明 |
|---------|---------|------|
| 测试6 | 错误处理：无效文件 | 测试处理不存在的文件的错误处理 |
| 测试7 | 错误处理：编译失败 | 测试处理编译失败的错误处理 |

### 在线服务器测试

| 测试编号 | 测试名称 | 说明 |
|---------|---------|------|
| 测试8 | 在线服务器完整流程 | 扫描在线SiLA2服务器并生成D3驱动（如无服务器则跳过） |

## ✅ 最新测试结果

**测试时间**：2024-10-24 16:49  
**测试状态**：✅ **所有测试通过**

### 测试详情

| 测试项 | 状态 | 备注 |
|--------|------|------|
| 基础功能测试 | ✅ 通过 | 生成和编译功能正常 |
| 集成测试 | ✅ 通过 | 方法分类调整、多特性集成正常 |
| 错误处理测试 | ✅ 通过 | 异常捕获和报告机制正常 |
| 在线服务器测试 | ✅ 通过 | 服务器扫描、生成、编译正常 |

### 验证内容

- ✅ D3DriverOrchestrationService 无UI依赖
- ✅ 客户端代码生成功能正常
- ✅ 代码分析功能正常  
- ✅ D3驱动代码生成功能正常
- ✅ 项目编译功能正常
- ✅ 方法分类调整功能正常
- ✅ 错误处理机制正常
- ✅ 多特性集成正常
- ✅ JSON序列化支持正常

## 📖 如何添加新测试

### 1. 在 TestDefinitions.cs 中添加枚举项

```csharp
public enum TestItem
{
    // ... 现有测试项
    NewTest = 9  // 新测试项
}
```

### 2. 在 TestInfo.GetAllTests() 中添加定义

```csharp
new TestInfo
{
    Item = TestItem.NewTest,
    Name = "新测试名称",
    Description = "新测试描述",
    Category = TestCategory.Basic,
    RequiresPrerequisite = false
}
```

### 3. 在 AutomatedTest.cs 中实现测试

```csharp
private async Task<bool> Test_NewTestAsync()
{
    // 实现测试逻辑
    return true; // 返回测试结果
}
```

### 4. 在 RunTestAsync 的 switch 中添加分支

```csharp
TestItem.NewTest => await Test_NewTestAsync(),
```

### 5. 在 TestRunner.cs 中同样添加实现

```csharp
private async Task Test_NewTestAsync()
{
    // 实现交互式版本的测试逻辑
}
```

## 🔧 前置条件

- **必需文件**：TemperatureController-v1_0.sila.xml（用于基础测试）
- **开发环境**：.NET 8.0 SDK
- **网络连接**：用于NuGet包还原
- **在线服务器**（可选）：用于测试8

## 📝 测试日志

测试运行时会生成以下日志文件：

- `test_output.log` - 常规测试输出
- `test_output_final.log` - 最终测试结果
- `test7_output.log` - 测试7的详细输出

## 💡 最佳实践

### 测试开发规范

1. **使用TestBase基类**：复用通用功能，避免代码重复
2. **遵循命名约定**：测试方法以 `Test_` 开头
3. **提供清晰的输出**：使用 ConsoleHelper 格式化输出
4. **处理异常情况**：正确捕获和报告错误
5. **支持进度回调**：长时间操作提供进度反馈

### 测试运行建议

1. **开发阶段**：使用交互式模式逐个调试测试
2. **提交前验证**：运行自动化测试确保所有功能正常
3. **持续集成**：将自动化测试集成到CI/CD流程

## 🐛 常见问题

### Q: 测试失败：找不到XML文件

**A**: 确保 `TemperatureController-v1_0.sila.xml` 文件在项目根目录或指定的搜索路径中。

### Q: 编译测试失败

**A**: 检查以下内容：
- .NET 8.0 SDK 是否正确安装
- 网络连接是否正常（NuGet包还原）
- 是否有足够的磁盘空间

### Q: 在线服务器测试跳过

**A**: 这是正常的。如果没有在线SiLA2服务器，测试会自动跳过且不影响整体结果。

### Q: 如何查看详细的测试输出

**A**: 查看生成的日志文件，或在交互式模式下运行特定测试。

## 📚 相关文档

- [测试运行指南](./测试运行指南.md) - 详细的测试说明和验证步骤
- [项目文档](../plan.md) - 完整的项目文档和实现历史
- [Tecan Generator使用指南](../../Tecan%20Generator生成的客户端使用指南.md) - 客户端代码生成说明

## 🔄 更新日志

### 2024-10-24

- ✅ 重构测试架构，引入TestBase基类
- ✅ 创建TestDefinitions统一管理测试项
- ✅ 修复在线服务器测试的Newtonsoft.Json引用问题
- ✅ 所有测试通过验证

### 之前的更新

- ✅ 添加多特性测试支持
- ✅ 添加在线服务器测试
- ✅ 完善错误处理测试

## 📞 支持

如有问题，请联系 Bioyond Team 或查看项目文档。
