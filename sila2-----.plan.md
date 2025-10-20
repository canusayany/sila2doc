<!-- 53132f7c-88ac-4d85-9dfd-1cc80a9364ac 49720762-9f78-4c4f-b169-7eac85cb2e32 -->
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

在现有 TabControl 中添加第三个 TabItem **"🎯 生成D3驱动"**（界面设计详见 plan.md）

### 2.2 修改 MainWindow.xaml.cs

添加 D3 驱动生成相关的事件处理方法和字段（代码详见 plan.md）

## 三、核心服务实现

### 3.1 D3DriverGeneratorService.cs

**核心功能：**
1. 解析 Tecan 生成的客户端代码（反射分析）
2. 生成 AllSila2Client.cs（整合所有特性）
3. 生成 D3Driver.cs（D3 驱动类）
4. 生成 Sila2Base.cs（基类）
5. 生成 CommunicationPars.cs（通信参数）
6. 生成测试控制台项目（可选）

### 3.2 ClientCodeAnalyzer.cs

**功能：**分析客户端代码，提取特性和方法信息
- 扫描 `I*.cs` 和 `*Client.cs` 文件
- 编译成 DLL
- 使用反射分析接口和方法
- 识别 Observable 特性
- 检测命名冲突

## 四、CodeDOM 生成器

### 4.1 AllSila2ClientGenerator.cs（重点）

参考 `BR.ECS.DeviceDriver.Sample.Test/AllSila2Client.cs` 生成：
- 整合所有特性客户端
- 属性转为 `Get{Property}()` 方法
- 可观察命令转为阻塞方法（`command.Response.GetAwaiter().GetResult()`）
- 命名冲突处理（添加 `FeatureName_` 前缀）
- 连接状态管理
- DiscoverFactories 方法

### 4.2 D3DriverGenerator.cs

参考 `BR.ECS.DeviceDriver.Sample.Test/D3Driver.cs` 生成：
- DeviceClass 特性
- 继承 Sila2Base
- 带 MethodOperations/MethodMaintenance 特性的方法
- 调用 `_sila2Device.{Method}()`

### 4.3 Sila2BaseGenerator.cs

参考 `BR.ECS.DeviceDriver.Sample.Test/Sila2Base.cs` 生成：
- 抽象基类
- `_sila2Device` 字段
- Connect/Disconnect 方法
- UpdateDeviceInfo 方法
- ConnectionInfo 类

### 4.4 CommunicationParsGenerator.cs

参考 `BR.ECS.DeviceDriver.Sample.Test/CommunicationPars.cs` 生成：
- IDeviceCommunication 实现
- IP 和 Port 配置

### 4.5 TestConsoleGenerator.cs（可选）

生成简单的测试控制台壳子程序

## 五、输出项目结构

```
Output/{Brand}_{Model}_D3Driver_{Timestamp}/
├── AllSila2Client.cs                   # 中间封装类
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
├── {Brand}{Model}.D3Driver.csproj
├── TestConsole/                        # 测试控制台（可选）
│   ├── Program.cs
│   └── TestConsole.csproj
└── {Brand}{Model}.sln
```

## 六、用户操作流程（全在 WPF 中）

1. **切换到 "🎯 生成D3驱动" Tab**
2. **点击 "📁 浏览" 选择客户端代码目录**
   - 自动检测所有特性
   - 显示检测到的特性数量
   - DataGrid 预览所有方法
3. **配置设备信息**（品牌、型号、类型、开发者）
4. **配置生成选项**（输出目录、命名空间、是否生成测试控制台）
5. **点击 "⚡ 生成D3驱动" 按钮**
   - 实时状态更新
   - 生成完成后提示打开文件夹

## 七、实施顺序

### 阶段1：更新文档和 UI（0.5天）

- [ ] 更新 `项目描述与要求.md`，记录所有技术决策
- [ ] 在 `MainWindow.xaml` 添加第三个 TabItem
- [ ] 在 `MainWindow.xaml.cs` 添加事件处理方法和字段

### 阶段2：客户端代码分析（1天）

- [ ] 创建 `Services/ClientCodeAnalyzer.cs`
- [ ] 实现编译客户端代码到 DLL（使用 Roslyn）
- [ ] 实现反射分析接口和方法
- [ ] 实现特性识别（Observable、返回值类型）
- [ ] 实现命名冲突检测

### 阶段3：CodeDOM 生成器（2天）

- [ ] 创建 `Services/CodeDom/AllSila2ClientGenerator.cs`（重点，参考示例代码）
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

## 八、关键技术实现

### 8.1 AllSila2Client 方法平铺示例

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

### 8.2 命名冲突处理

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

### 8.3 可观察命令返回值处理

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

## 九、数据模型

### 9.1 ClientFeatureInfo

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

### 9.2 MethodGenerationInfo

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

### 9.3 D3DriverGenerationConfig

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

## 十一、注意事项

1. **不使用独立控制台应用** - 所有功能都在 WPF 界面中完成
2. **测试控制台是可选的** - 只是生成一个简单的测试壳子
3. **AllSila2Client 是核心** - 必须正确实现方法平铺和命名冲突处理
4. **参考示例代码** - `BR.ECS.DeviceDriver.Sample.Test/` 目录下的所有文件都是生成目标的参考
5. **使用 CodeDOM** - 所有代码生成都使用 System.CodeDom

### To-dos

- [ ] 更新项目描述与要求.md，记录所有技术决策
- [ ] 在 MainWindow.xaml 添加第三个 TabItem
- [ ] 在 MainWindow.xaml.cs 添加事件处理方法和字段
- [ ] 创建 Services/D3DriverGeneratorService.cs 核心服务类
- [ ] 创建 Services/ClientCodeAnalyzer.cs 客户端代码分析器
- [ ] 创建 Services/CodeDom/AllSila2ClientGenerator.cs（CodeDOM生成AllSila2Client.cs）
- [ ] 创建 Services/CodeDom/D3DriverGenerator.cs（CodeDOM生成D3Driver.cs）
- [ ] 创建 Services/CodeDom/Sila2BaseGenerator.cs（CodeDOM生成Sila2Base.cs）
- [ ] 创建 Services/CodeDom/CommunicationParsGenerator.cs（CodeDOM生成CommunicationPars.cs）
- [ ] 创建 Services/CodeDom/TestConsoleGenerator.cs（生成测试控制台项目）
- [ ] 实现客户端代码编译到DLL功能（使用Roslyn）
- [ ] 实现反射分析DLL中的接口、方法、属性
- [ ] 实现方法命名冲突检测和解决（添加FeatureName_前缀）
- [ ] 端到端测试完整生成流程，验证生成的代码可编译运行
- [ ] 检查以上是否已经解决用户的问题，进行最终验证和优化

