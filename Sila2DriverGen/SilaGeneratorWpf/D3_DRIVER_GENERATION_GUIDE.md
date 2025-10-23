# 🎯 D3 驱动生成功能使用指南

## 概述

D3 驱动生成功能是 SiLA2 客户端代码生成器的第三个 Tab，用于从 Tecan Generator 生成的 SiLA2 客户端代码自动生成 D3 驱动封装层。

## 功能特点

### ✅ 智能代码分析
- 自动编译和分析客户端代码
- 提取接口、方法、属性和 XML 文档注释
- 识别可观察命令和属性
- 检测和处理方法命名冲突

### ✅ 完整的驱动生成
生成以下文件：
- `AllSila2Client.cs` - 整合所有特性的中间封装类
- `Sila2Base.cs` - RPC 通信基类
- `CommunicationPars.cs` - 通信参数配置
- `D3Driver.cs` - D3 驱动实现类
- 项目文件（`.csproj`）和解决方案文件（`.sln`）
- 测试控制台项目（可选）

### ✅ 智能特性处理
- **属性自动转换**：SiLA2 属性自动转换为 `Get{PropertyName}` 方法
- **可观察命令阻塞等待**：`IObservableCommand` 自动使用 `command.Response.GetAwaiter().GetResult()` 阻塞等待
- **命名冲突解决**：自动检测并为冲突方法添加 `{FeatureName}_` 前缀
- **XML 注释保留**：所有 XML 文档注释从客户端代码提取并集成到生成的驱动代码

### ✅ 数据类型支持
**支持的类型：**
- 基础类型：`int`, `string`, `double`, `float`, `bool`, `DateTime`, `byte`, `sbyte` 等
- 数组和列表：`T[]`, `List<T>`（T 为基础类型）
- 枚举类型
- 简单复合类型（不嵌套的 class/struct）

**不支持的复杂类型处理：**
- 自动添加额外的 JSON 字符串参数用于序列化/反序列化
- 在注释中添加提示信息

## 使用步骤

### 步骤 1：选择客户端代码目录

1. 打开 "🎯 生成D3驱动" Tab
2. 点击 "📁 浏览" 按钮
3. 选择 Tecan Generator 生成的客户端代码目录
4. 工具会自动：
   - 编译客户端代码
   - 分析所有特性和方法
   - 提取 XML 文档注释
   - 显示检测结果

**要求：**
- 目录中必须包含 `.cs` 文件
- 必须是 Tecan Generator 生成的标准 SiLA2 客户端代码
- 包含接口文件 (`I*.cs`) 和客户端类 (`*Client.cs`)

### 步骤 2：配置设备信息

填写以下信息（必填项标记 *）：

- **品牌\***：设备品牌名称（如：Bioyond）
- **型号\***：设备型号（如：MD）
- **类型**：设备类型（如：Robot、Incubator）
- **开发者**：开发者名称

**命名规则：**
- 品牌和型号只能使用英文字母、数字、下划线
- 不能包含空格或特殊字符
- 这些信息将用于生成项目名称和 `DeviceClass` 特性

### 步骤 3：配置生成选项

1. **输出目录**：
   - 点击 📁 选择输出目录
   - 或保持默认（系统临时目录）

2. **命名空间**：
   - 默认：`BR.ECS.DeviceDriver.Generated`
   - 可自定义

3. **自动编译生成的项目**：
   - 勾选：生成完成后自动编译项目（Release 配置）
   - 不勾选：只生成代码，不编译
   - 推荐：勾选，可以立即发现编译错误

4. **生成测试控制台**：
   - 勾选：生成测试控制台项目
   - 不勾选：只生成驱动项目

### 步骤 4：预览和生成

1. 在 "特性方法预览" 表格中查看所有将要生成的方法
2. 确认信息无误后，点击 "⚡ 生成D3驱动" 按钮
3. 等待生成完成（通常需要几秒到十几秒）
4. 如果启用了自动编译，会显示编译结果
5. 生成成功后，选择是否打开输出文件夹

**编译结果（如果启用自动编译）：**
- 编译成功：显示警告数量
- 编译失败：显示错误和警告数量，但不影响代码生成
- 编译输出保存在日志中，便于排查问题

## 生成的项目结构

```
{Brand}_{Model}_D3Driver_{Timestamp}/
├── AllSila2Client.cs           # 中间封装类（整合所有特性）
├── D3Driver.cs                 # D3 驱动实现类
├── Sila2Base.cs                # RPC 通信基类
├── CommunicationPars.cs        # 通信参数配置类
├── Sila2Client/                # 客户端代码（从源目录复制）
│   ├── ITemperatureController.cs
│   ├── TemperatureControllerClient.cs
│   ├── TemperatureControllerDtos.cs
│   └── ... (其他特性文件)
├── lib/                        # D3 依赖库
│   ├── BR.ECS.Executor.Device.Domain.Contracts.dll
│   ├── BR.ECS.Executor.Device.Domain.Share.dll
│   ├── BR.ECS.Executor.Device.Infrastructure.dll
│   └── BR.PC.Device.Sila2Discovery.dll
├── TestConsole/                # 测试控制台（可选）
│   ├── Program.cs
│   └── {Brand}{Model}.TestConsole.csproj
├── {Brand}{Model}.D3Driver.csproj    # 驱动项目文件
└── {Brand}{Model}.D3Driver.sln       # 解决方案文件
```

## 生成的代码示例

### AllSila2Client.cs（中间封装类）

```csharp
namespace BR.ECS.DeviceDriver.Generated
{
    public class AllSila2Client
    {
        private ITemperatureController temperatureController;
        private ServerConnector _connector;
        
        public AllSila2Client()
        {
            _connector = new ServerConnector(new DiscoveryExecutionManager());
            Sila2Discovery.StartRealTimeMonitoring();
        }
        
        public bool Connect(string ip, int port)
        {
            var info = Sila2Discovery.GetServer(ip, port, TimeSpan.FromSeconds(5));
            if (info == null) return false;
            
            _server = _connector.Connect(info.IPAddress, info.Port, info.Uuid, info.TxtRecords);
            // ... 创建所有特性客户端 ...
            return true;
        }
        
        /// <summary>
        /// 属性转换为 Get 方法
        /// </summary>
        public double GetCurrentTemperature()
        {
            return temperatureController.CurrentTemperature;
        }
        
        /// <summary>
        /// 可观察命令转换为阻塞方法
        /// </summary>
        public void ControlTemperature(double targetTemperature)
        {
            var command = temperatureController.ControlTemperature(targetTemperature);
            command.Response.GetAwaiter().GetResult();  // 阻塞等待
        }
        
        /// <summary>
        /// 普通方法直接调用
        /// </summary>
        public void SwitchDeviceState(bool isOn)
        {
            temperatureController.SwitchDeviceState(isOn);
        }
    }
}
```

### D3Driver.cs（D3 驱动类）

```csharp
namespace BR.ECS.DeviceDriver.Generated
{
    [DeviceClass("Bioyond", "MD", "BioyondMD", "Robot", "Developer")]
    public class D3Driver : Sila2Base
    {
        /// <summary>
        /// The current temperature as measured by the controller.
        /// </summary>
        [MethodOperations]
        public double GetCurrentTemperature()
        {
            return _sila2Device.GetCurrentTemperature();
        }
        
        /// <summary>
        /// Control the temperature gradually to a set target.
        /// </summary>
        /// <param name="targetTemperature">The target temperature...</param>
        [MethodOperations]
        public void ControlTemperature(double targetTemperature)
        {
            _sila2Device.ControlTemperature(targetTemperature);
        }
        
        /// <summary>
        /// Switch device state from On to Off, or from Off to On.
        /// </summary>
        [MethodMaintenance(1)]
        public void SwitchDeviceState(bool isOn)
        {
            _sila2Device.SwitchDeviceState(isOn);
        }
    }
}
```

## 技术实现

### 代码分析流程

```
客户端代码目录
    ↓
1. 使用 Roslyn 编译 .cs 文件生成 DLL 和 XML 文档
    ↓
2. 使用反射加载 DLL 并分析
    ↓
3. 提取接口、方法、属性、特性
    ↓
4. 从 XML 文档提取注释
    ↓
5. 检测方法命名冲突
    ↓
6. 构建 MethodGenerationInfo 数据模型
```

### 代码生成流程

```
MethodGenerationInfo 列表
    ↓
1. AllSila2ClientGenerator 生成中间封装类
    ├─ 添加所有特性的客户端字段
    ├─ 生成 Connect/Disconnect 方法
    ├─ 平铺所有方法（处理命名冲突）
    └─ 集成 XML 注释
    ↓
2. D3DriverGenerator 生成驱动类
    ├─ 添加 DeviceClass 特性
    ├─ 继承 Sila2Base
    ├─ 生成所有操作和维护方法
    └─ 调用 AllSila2Client
    ↓
3. Sila2BaseGenerator 生成基类
    ├─ 继承 DeviceBase
    ├─ 实现 Connect/Disconnect
    └─ 实现 UpdateDeviceInfo
    ↓
4. CommunicationParsGenerator 生成通信参数
    └─ 实现 IDeviceCommunication
    ↓
5. TestConsoleGenerator 生成测试控制台（可选）
    ↓
6. 生成项目文件和解决方案文件
```

### 使用的技术

- **Roslyn**：编译客户端代码并生成 XML 文档
- **Reflection**：分析编译后的程序集
- **System.CodeDom**：生成所有 C# 代码
- **LINQ to XML**：提取 XML 文档注释

## 使用示例

### 示例 1：单特性驱动

**输入：**
- 客户端代码：TemperatureController

**输出方法：**
- `GetCurrentTemperature()` - 属性转方法
- `ControlTemperature(double)` - 可观察命令（阻塞）
- `GetDeviceState()` - 属性转方法
- `SwitchDeviceState(bool)` - 普通方法

### 示例 2：多特性驱动（有命名冲突）

**输入：**
- 客户端代码：TemperatureController + PumpController

**输出方法：**
- `GetCurrentTemperature()` - 无冲突，保持原名
- `TemperatureController_Start()` - 有冲突，添加前缀
- `PumpController_Start()` - 有冲突，添加前缀
- `GetFlowRate()` - 无冲突，保持原名

## 常见问题

### Q1：生成失败，提示 "编译客户端代码失败"

**可能原因：**
- 客户端代码不完整或有语法错误
- 缺少必要的依赖引用

**解决方法：**
1. 确保客户端代码目录包含所有必要的 .cs 文件
2. 检查客户端代码是否由 Tecan Generator 正确生成
3. 查看错误详情中的编译错误信息

### Q2：未检测到有效的特性

**可能原因：**
- 选择的目录不正确
- 客户端代码没有 `SilaFeature` 特性标记

**解决方法：**
1. 确保选择的是包含客户端代码的正确目录
2. 检查接口文件是否有 `[SilaFeature]` 特性

### Q3：生成的驱动编译失败

**可能原因：**
- 缺少依赖库
- 命名空间配置错误

**解决方法：**
1. 检查 `lib` 目录是否包含所有必要的 DLL
2. 确保 NuGet 包已正确引用
3. 使用 Visual Studio 打开生成的 `.sln` 文件检查编译错误

### Q4：如何手动复制依赖库？

**步骤：**
1. 找到示例项目的 `BR.ECS.DeviceDriver.Sample.Test/lib` 目录
2. 复制以下 DLL 到生成项目的 `lib` 目录：
   - `BR.ECS.Executor.Device.Domain.Contracts.dll`
   - `BR.ECS.Executor.Device.Domain.Share.dll`
   - `BR.ECS.Executor.Device.Infrastructure.dll`
   - `BR.PC.Device.Sila2Discovery.dll`
3. 重新编译生成的项目

### Q5：方法命名不符合预期

**原因：**
工具按以下规则命名：
- 属性转为 `Get{PropertyName}` 方法
- 有命名冲突时添加 `{FeatureName}_` 前缀

**建议：**
如需自定义命名，可在生成后手动编辑 `AllSila2Client.cs` 和 `D3Driver.cs`

## 最佳实践

### 1. 组织客户端代码

将所有相关特性的客户端代码放在同一目录中：
```
ClientCode/
├── ITemperatureController.cs
├── TemperatureControllerClient.cs
├── TemperatureControllerDtos.cs
├── IPumpController.cs
├── PumpControllerClient.cs
└── PumpControllerDtos.cs
```

### 2. 设备信息命名

使用清晰的命名：
- 品牌：公司或产品线名称（如：Bioyond, Tecan）
- 型号：具体型号（如：MD, Fluent）
- 类型：设备类别（如：Robot, Incubator, Shaker）

### 3. 测试控制台使用

生成后在 `TestConsole/Program.cs` 中添加测试逻辑：
```csharp
var driver = new D3Driver();
driver.UpdateDeviceInfo();
var result = driver.Connect();
if (result == 0)
{
    Console.WriteLine("连接成功");
    var temp = driver.GetCurrentTemperature();
    Console.WriteLine($"当前温度: {temp}");
}
```

### 4. 版本管理

生成的代码建议纳入版本控制：
- 提交生成的 `.cs` 文件
- 提交项目和解决方案文件
- 不提交 `bin` 和 `obj` 目录

## 依赖项说明

### NuGet 包依赖

生成的项目包含以下 NuGet 包引用：
```xml
<PackageReference Include="Tecan.Sila2.Client.NetCore" Version="4.4.1" />
<PackageReference Include="Tecan.Sila2.Discovery" Version="4.4.1" />
<PackageReference Include="Tecan.Sila2.Locking" Version="4.4.1" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

### 本地 DLL 引用

以下 DLL 需要放在 `lib` 目录：
```xml
<Reference Include="BR.ECS.Executor.Device.Domain.Contracts">
  <HintPath>lib\BR.ECS.Executor.Device.Domain.Contracts.dll</HintPath>
</Reference>
<!-- ... 其他引用 ... -->
```

## 下一步

生成驱动后，您可以：

1. **编译项目**：
   ```bash
   cd {OutputDirectory}
   dotnet build
   ```

2. **运行测试控制台**：
   ```bash
   dotnet run --project TestConsole/*.csproj
   ```

3. **集成到 D3 系统**：
   - 将生成的驱动项目添加到 D3 解决方案
   - 在 D3 中配置设备连接参数（IP、Port）
   - 在 D3 调度系统中使用驱动方法

## 技术支持

如遇到问题或需要帮助，请：
1. 查看状态栏的错误信息
2. 检查日志文件（在应用程序目录的 `logs` 文件夹）
3. 联系开发团队

---

**提示**：建议先使用示例客户端代码进行测试，熟悉流程后再处理实际项目。


