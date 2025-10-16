# Generator 生成的客户端使用指南

## 概述

Tecan SiLA2 Generator 可以从 SiLA Feature 定义（XML）或 C# 接口生成强类型的客户端代码。本指南详细说明如何使用生成的客户端代码。

---

## 一、生成客户端代码

### 1.1 从 Feature XML 生成客户端

#### 方法一：使用 `generate-client` 命令

```powershell
# 基本用法
SilaGen.exe generate-client <FeaturePath> <ProjectPath> [选项]

# 示例
SilaGen.exe generate-client ^
    "Features\TemperatureController_1.0.sila.xml" ^
    "Client\Client.csproj" ^
    --namespace "MyCompany.SiLA.Client"
```

**参数说明：**
- `<FeaturePath>`: SiLA Feature XML 文件路径
- `<ProjectPath>`: 目标 C# 项目文件路径
- `--namespace` / `-n`: 可选，指定命名空间（默认使用 Feature 的 namespace）
- `--adjust-to-interface` / `-i`: 可选，调整到现有接口
- `--no-lazy-properties` / `-l`: 可选，每次查询不可观察属性（默认使用懒加载）

#### 方法二：使用 `generate-provider` 命令（仅生成客户端）

```powershell
SilaGen.exe generate-provider ^
    "Features\TemperatureController_1.0.sila.xml" ^
    "Generated\Dtos.cs" ^
    "Generated\Client.cs" ^
    --client-only ^
    --namespace "MyCompany.SiLA.Client"
```

**参数说明：**
- `--client-only` / `-c`: 仅生成客户端（不生成服务器）
- 第一个路径：DTOs 输出路径
- 第二个路径：客户端输出路径

### 1.2 生成的文件结构

```
Generated/
├── Dtos.cs                          # 数据传输对象
├── Client.cs                        # 客户端实现
└── ITemperatureController.cs        # 接口定义（可选）
```

---

## 二、生成的客户端代码结构

### 2.1 主要组件

生成的客户端代码包含以下主要类：

```csharp
// 1. Feature 接口（如果生成）
public interface ITemperatureController
{
    double TargetTemperature { get; }
    double CurrentTemperature { get; }
    void SetTargetTemperature(double temperature);
    IObservableCommand StartHeating();
}

// 2. 客户端实现类
public partial class TemperatureControllerClient : ITemperatureController
{
    private readonly IClientChannel _channel;
    private readonly IClientExecutionManager _executionManager;
    
    public TemperatureControllerClient(
        IClientChannel channel, 
        IClientExecutionManager executionManager)
    {
        // ...
    }
}

// 3. 客户端工厂类
public partial class TemperatureControllerClientFactory : IClientFactory
{
    public string FeatureIdentifier { get; }
    public Type InterfaceType { get; }
    public object CreateClient(IClientChannel channel, IClientExecutionManager executionManager);
}

// 4. DTOs（数据传输对象）
[ProtoContract]
public class SetTargetTemperatureRequestDto : ISilaRequestObject
{
    [ProtoMember(1)]
    public DoubleDto Temperature { get; set; }
    // ...
}
```

---

## 三、使用生成的客户端

### 3.1 方式一：直接实例化（推荐用于简单场景）

```csharp
using Tecan.Sila2.Client;
using Tecan.Sila2.Discovery;
using MyCompany.SiLA.Client.TemperatureController;

// 1. 创建服务器连接器
var connector = new ServerConnector(new DiscoveryExecutionManager());

// 2. 连接到 SiLA 服务器
var server = connector.Connect("192.168.1.100", 50051);
// 或使用 mDNS 发现
// var server = discovery.Connect(Guid.Parse("..."), TimeSpan.FromSeconds(5));

// 3. 创建执行管理器
var executionManagerFactory = new ExecutionManagerFactory(
    new IClientRequestInterceptor[0]  // 可选的拦截器
);
var executionManager = executionManagerFactory.CreateExecutionManager(server);

// 4. 直接创建客户端
var client = new TemperatureControllerClient(
    server.Channel, 
    executionManager
);

// 5. 使用客户端
try
{
    // 读取属性
    double currentTemp = client.CurrentTemperature;
    Console.WriteLine($"当前温度: {currentTemp}°C");
    
    // 执行不可观察命令
    client.SetTargetTemperature(37.5);
    
    // 执行可观察命令
    var heatingCommand = client.StartHeating();
    heatingCommand.StateChanged += (sender, state) =>
    {
        Console.WriteLine($"进度: {state.Progress}%");
    };
    heatingCommand.Start();
    await heatingCommand.Response;
}
catch (SilaException ex)
{
    Console.WriteLine($"SiLA 错误: {ex.Message}");
}
catch (DefinedErrorException ex)
{
    Console.WriteLine($"定义的执行错误: {ex.ErrorIdentifier} - {ex.Message}");
}
```

### 3.2 方式二：使用 ClientProvider（推荐用于多 Feature）

```csharp
using Tecan.Sila2.Client;
using Tecan.Sila2.Discovery;

// 1. 创建服务发现和连接器
var connector = new ServerConnector(new DiscoveryExecutionManager());
var executionManagerFactory = new ExecutionManagerFactory(
    new IClientRequestInterceptor[0]
);

// 2. 注册客户端工厂
var factories = new List<IClientFactory>
{
    new TemperatureControllerClientFactory(),
    new ShakingControlClientFactory(),
    // ... 其他 Feature 的工厂
};

var clientProvider = new ClientProvider(executionManagerFactory, factories);

// 3. 发现并连接服务器
var discovery = new ServerDiscovery(connector);
discovery.DiscoverServers(server =>
{
    // 4. 尝试为每个服务器创建客户端
    if (clientProvider.TryCreateClient<ITemperatureController>(server, out var tempClient))
    {
        Console.WriteLine($"找到温度控制器: {server.Config.Name}");
        double temp = tempClient.CurrentTemperature;
        Console.WriteLine($"当前温度: {temp}°C");
    }
    
    if (clientProvider.TryCreateClient<IShakingControl>(server, out var shakeClient))
    {
        Console.WriteLine($"找到振荡控制器: {server.Config.Name}");
        // 使用 shakeClient...
    }
}, 
timeout: TimeSpan.FromSeconds(5));
```

### 3.3 方式三：使用 .NET Core 依赖注入（推荐用于现代应用）

**注意：** 仅当使用 `Client.Core` 时可用。

```csharp
using Microsoft.Extensions.DependencyInjection;
using Tecan.Sila2.Client;
using Tecan.Sila2.Discovery;

// 1. 配置依赖注入
var services = new ServiceCollection();

// 添加 SiLA2 客户端服务
services.AddSila2Client();

// 注册自定义客户端工厂
services.AddSingleton<IClientFactory, TemperatureControllerClientFactory>();
services.AddSingleton<IClientFactory, ShakingControlClientFactory>();

var serviceProvider = services.BuildServiceProvider();

// 2. 使用服务
var discovery = serviceProvider.GetRequiredService<IServerDiscovery>();
var clientProvider = serviceProvider.GetRequiredService<IClientProvider>();

discovery.DiscoverServers(server =>
{
    if (clientProvider.TryCreateClient<ITemperatureController>(server, out var client))
    {
        // 使用客户端...
    }
}, TimeSpan.FromSeconds(5));
```

---

## 四、客户端功能详解

### 4.1 属性访问

#### 不可观察属性（默认懒加载）

```csharp
// 第一次访问时从服务器获取，之后返回缓存值
double temp = client.CurrentTemperature;

// 如果生成时使用 --no-lazy-properties，每次都会查询服务器
```

#### 可观察属性（实时订阅）

```csharp
// 客户端实现 INotifyPropertyChanged
client.PropertyChanged += (sender, e) =>
{
    if (e.PropertyName == nameof(ITemperatureController.CurrentTemperature))
    {
        Console.WriteLine($"温度已更新: {client.CurrentTemperature}");
    }
};

// 第一次访问会自动订阅
double temp = client.CurrentTemperature;

// 使用完毕后释放
client.Dispose();  // 取消所有订阅
```

### 4.2 执行不可观察命令

```csharp
try
{
    // 无返回值的命令
    client.SetTargetTemperature(37.5);
    
    // 有返回值的命令
    var result = client.CalculateSomething(param1, param2);
}
catch (ValidationException ex)
{
    // 参数验证失败
    Console.WriteLine($"验证错误 ({ex.Parameter}): {ex.Message}");
}
catch (DefinedErrorException ex)
{
    // Feature 定义的执行错误
    Console.WriteLine($"执行错误: {ex.ErrorIdentifier}");
}
catch (SilaException ex)
{
    // 其他 SiLA 错误
    Console.WriteLine($"SiLA 错误: {ex.Message}");
}
```

### 4.3 执行可观察命令

#### 基本用法

```csharp
// 执行命令（立即返回 IObservableCommand）
var command = client.StartHeating();

// 订阅状态变化
command.StateChanged += (sender, state) =>
{
    Console.WriteLine($"状态: {state.State}");
    Console.WriteLine($"进度: {state.Progress}%");
    Console.WriteLine($"预计剩余时间: {state.EstimatedRemainingTime}");
};

// 等待命令完成
try
{
    await command.Response;
    Console.WriteLine("加热完成！");
}
catch (OperationCanceledException)
{
    Console.WriteLine("命令被取消");
}
```

#### 带返回值的可观察命令

```csharp
var command = client.PerformAnalysis();

command.StateChanged += (sender, state) =>
{
    Console.WriteLine($"分析进度: {state.Progress}%");
};

try
{
    var result = await command.Response;
    Console.WriteLine($"分析结果: {result}");
}
catch (Exception ex)
{
    Console.WriteLine($"错误: {ex.Message}");
}
```

#### 中间结果（Intermediates）

```csharp
var command = client.RunExperiment();

// 订阅中间结果
await foreach (var intermediate in command.IntermediateValues.ReadAllAsync())
{
    Console.WriteLine($"中间结果: {intermediate}");
}

// 等待最终结果
var finalResult = await command.Response;
```

#### 取消命令

```csharp
var command = client.LongRunningOperation();

// 使用 CancellationToken（如果方法支持）
var cts = new CancellationTokenSource();
var task = client.LongRunningOperationAsync(cts.Token);

// 5秒后取消
await Task.Delay(5000);
cts.Cancel();

try
{
    await task;
}
catch (OperationCanceledException)
{
    Console.WriteLine("操作已取消");
}
```

### 4.4 二进制数据处理

```csharp
// 上传二进制数据
using (var fileStream = File.OpenRead("data.bin"))
{
    client.UploadData(fileStream);
}

// 下载二进制数据
var binaryData = client.GetBinaryData();
using (var stream = binaryData.OpenRead())
{
    // 处理二进制数据
}
```

---

## 五、高级特性

### 5.1 元数据和拦截器

```csharp
// 创建自定义拦截器
public class CustomInterceptor : IClientRequestInterceptor
{
    public string MetadataIdentifier => null;  // null 表示应用于所有请求
    
    public IClientRequestInterception Intercept(
        ServerData server, 
        string commandIdentifier, 
        IClientExecutionManager executionManager, 
        IDictionary<string, byte[]> metadata)
    {
        // 添加自定义元数据
        metadata["X-Custom-Header"] = Encoding.UTF8.GetBytes("CustomValue");
        
        return new CustomInterception();
    }
}

public class CustomInterception : IClientRequestInterception
{
    public void CompleteSuccessfully()
    {
        Console.WriteLine("请求成功完成");
    }
    
    public void CompleteWithError(Exception exception)
    {
        Console.WriteLine($"请求失败: {exception.Message}");
    }
}

// 使用拦截器
var interceptors = new IClientRequestInterceptor[] 
{ 
    new CustomInterceptor() 
};
var executionManagerFactory = new ExecutionManagerFactory(interceptors);
```

### 5.2 处理 Feature 依赖

```csharp
// 如果 Feature A 依赖 Feature B
var serverData = connector.Connect("192.168.1.100", 50051);

// 检查服务器是否实现了所需的 Feature
var requiredFeatures = new[] 
{
    "org.silastandard/core/TemperatureController/v1",
    "org.silastandard/core/ShakingControl/v1"
};

var implementedFeatures = serverData.Features
    .Select(f => f.FullyQualifiedIdentifier)
    .ToHashSet();

foreach (var required in requiredFeatures)
{
    if (!implementedFeatures.Contains(required))
    {
        Console.WriteLine($"缺少必需的 Feature: {required}");
        return;
    }
}

// 创建客户端...
```

### 5.3 错误处理最佳实践

```csharp
try
{
    var result = client.PerformOperation(param);
}
catch (ArgumentException ex) when (ex.ParamName == "temperature")
{
    // 参数验证失败（来自 ValidationError）
    Console.WriteLine($"温度参数无效: {ex.Message}");
}
catch (TemperatureOutOfRangeException ex)  // Feature 定义的错误
{
    // 处理特定的定义错误
    Console.WriteLine($"温度超出范围: {ex.Message}");
}
catch (DefinedErrorException ex)
{
    // 其他定义的执行错误
    Console.WriteLine($"执行错误: {ex.ErrorIdentifier}");
}
catch (SilaFrameworkException ex)
{
    // SiLA 框架错误
    Console.WriteLine($"框架错误: {ex.ErrorType}");
}
catch (SilaException ex)
{
    // 未定义的执行错误
    Console.WriteLine($"SiLA 错误: {ex.Message}");
}
catch (RpcException ex)
{
    // gRPC 通信错误
    Console.WriteLine($"通信错误: {ex.Status}");
}
```

---

## 六、完整示例

### 6.1 简单温度控制器客户端

```csharp
using System;
using System.Threading.Tasks;
using Tecan.Sila2.Client;
using Tecan.Sila2.Discovery;

namespace TemperatureControllerExample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("温度控制器客户端示例");
            
            // 1. 设置连接
            var connector = new ServerConnector(new DiscoveryExecutionManager());
            var discovery = new ServerDiscovery(connector);
            
            // 2. 发现服务器
            var servers = discovery.GetServers(TimeSpan.FromSeconds(5));
            var temperatureServer = servers.FirstOrDefault();
            
            if (temperatureServer == null)
            {
                Console.WriteLine("未找到 SiLA 服务器");
                return;
            }
            
            Console.WriteLine($"已连接到: {temperatureServer.Config.Name}");
            
            // 3. 创建客户端
            var execManagerFactory = new ExecutionManagerFactory(null);
            var execManager = execManagerFactory.CreateExecutionManager(temperatureServer);
            
            var client = new TemperatureControllerClient(
                temperatureServer.Channel, 
                execManager
            );
            
            // 4. 使用客户端
            try
            {
                // 读取当前温度
                Console.WriteLine($"当前温度: {client.CurrentTemperature}°C");
                Console.WriteLine($"目标温度: {client.TargetTemperature}°C");
                
                // 设置新的目标温度
                Console.Write("输入目标温度: ");
                if (double.TryParse(Console.ReadLine(), out double targetTemp))
                {
                    client.SetTargetTemperature(targetTemp);
                    Console.WriteLine("目标温度已设置");
                }
                
                // 启动加热（可观察命令）
                Console.WriteLine("正在加热...");
                var heatingCommand = client.StartHeating();
                
                heatingCommand.StateChanged += (sender, state) =>
                {
                    Console.WriteLine($"进度: {state.Progress:F1}% - " +
                                    $"预计剩余时间: {state.EstimatedRemainingTime.TotalSeconds:F0}秒");
                };
                
                await heatingCommand.Response;
                Console.WriteLine("加热完成！");
                
                // 最终温度
                Console.WriteLine($"最终温度: {client.CurrentTemperature}°C");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
        }
    }
}
```

### 6.2 使用依赖注入的 ASP.NET Core 示例

```csharp
// Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // 添加 SiLA2 客户端服务
    services.AddSila2Client();
    
    // 注册客户端工厂
    services.AddSingleton<IClientFactory, TemperatureControllerClientFactory>();
    
    // 注册业务服务
    services.AddScoped<ITemperatureService, TemperatureService>();
    
    services.AddControllers();
}

// TemperatureService.cs
public class TemperatureService : ITemperatureService
{
    private readonly IServerDiscovery _discovery;
    private readonly IClientProvider _clientProvider;
    
    public TemperatureService(
        IServerDiscovery discovery, 
        IClientProvider clientProvider)
    {
        _discovery = discovery;
        _clientProvider = clientProvider;
    }
    
    public async Task<double> GetTemperatureAsync()
    {
        var servers = _discovery.GetServers(TimeSpan.FromSeconds(5));
        var server = servers.FirstOrDefault();
        
        if (server == null)
            throw new Exception("未找到温度控制器");
        
        if (_clientProvider.TryCreateClient<ITemperatureController>(
            server, out var client))
        {
            return client.CurrentTemperature;
        }
        
        throw new Exception("无法创建客户端");
    }
}

// TemperatureController.cs (API)
[ApiController]
[Route("api/[controller]")]
public class TemperatureController : ControllerBase
{
    private readonly ITemperatureService _temperatureService;
    
    public TemperatureController(ITemperatureService temperatureService)
    {
        _temperatureService = temperatureService;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetTemperature()
    {
        try
        {
            var temp = await _temperatureService.GetTemperatureAsync();
            return Ok(new { temperature = temp });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
}
```

---

## 七、常见问题

### Q1: 如何选择使用 Client 还是 Client.Core？

**A:** 参见 [Client与Client.Core对比分析.md](./Client与Client.Core对比分析.md)。推荐现代 .NET 应用使用 `Client.Core`。

### Q2: 生成的客户端是线程安全的吗？

**A:** 
- **属性读取**: 线程安全（每次调用都是独立的 gRPC 请求）
- **可观察属性**: 需要注意，PropertyChanged 事件可能在不同线程触发
- **命令执行**: 每个命令实例是独立的，但同一个客户端实例不应在多线程中同时使用

### Q3: 如何处理连接断开？

**A:**
```csharp
// 监听通道状态
if (server.Channel.State == ChannelState.Shutdown)
{
    Console.WriteLine("连接已断开");
    // 重新连接...
}

// 使用重试机制
int retries = 3;
while (retries > 0)
{
    try
    {
        var result = client.DoSomething();
        break;
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
    {
        retries--;
        if (retries == 0) throw;
        await Task.Delay(1000);
    }
}
```

### Q4: 如何调试生成的客户端代码？

**A:**
1. 生成的代码是 `partial class`，可以添加自定义调试方法
2. 使用拦截器记录所有请求
3. 启用 gRPC 日志：
```csharp
Environment.SetEnvironmentVariable("GRPC_VERBOSITY", "DEBUG");
Environment.SetEnvironmentVariable("GRPC_TRACE", "all");
```

### Q5: 生成的 DTOs 可以序列化吗？

**A:** 是的，DTOs 使用 ProtoBuf 标记，可以序列化：
```csharp
var dto = new TemperatureDto { Value = 37.5 };
byte[] bytes = ProtoBuf.Serializer.Serialize(dto);
```

---

## 八、性能优化

### 8.1 使用懒加载属性

```csharp
// 默认行为（推荐）
// 第一次访问时获取，后续使用缓存
var temp = client.Temperature;  // 从服务器获取
var temp2 = client.Temperature; // 使用缓存

// 如果需要强制刷新，重新创建客户端或使用方法
```

### 8.2 批量操作

```csharp
// 避免
for (int i = 0; i < 100; i++)
{
    client.SetValue(i);  // 100次网络调用
}

// 推荐：如果 Feature 支持批量操作
client.SetMultipleValues(Enumerable.Range(0, 100).ToArray());
```

### 8.3 复用客户端实例

```csharp
// 创建一次，多次使用
var client = new TemperatureControllerClient(channel, execManager);

// 多个操作
client.SetTarget(37.0);
client.StartHeating();
client.MonitorTemperature();

// 用完后释放
if (client is IDisposable disposable)
{
    disposable.Dispose();
}
```

---

## 九、测试

### 9.1 模拟客户端

```csharp
// 为接口创建 Mock
var mockClient = new Mock<ITemperatureController>();
mockClient.Setup(c => c.CurrentTemperature).Returns(25.0);
mockClient.Setup(c => c.SetTargetTemperature(It.IsAny<double>()))
          .Verifiable();

// 使用 Mock 进行测试
var service = new TemperatureService(mockClient.Object);
await service.HeatToTargetAsync(37.0);

mockClient.Verify(c => c.SetTargetTemperature(37.0), Times.Once);
```

### 9.2 集成测试

```csharp
[TestClass]
public class TemperatureControllerIntegrationTests
{
    private ServerData _server;
    private ITemperatureController _client;
    
    [TestInitialize]
    public void Setup()
    {
        var connector = new ServerConnector(new DiscoveryExecutionManager());
        _server = connector.Connect("localhost", 50051);
        
        var execManager = new ExecutionManagerFactory(null)
            .CreateExecutionManager(_server);
        _client = new TemperatureControllerClient(_server.Channel, execManager);
    }
    
    [TestMethod]
    public void TestReadTemperature()
    {
        var temp = _client.CurrentTemperature;
        Assert.IsTrue(temp >= -50 && temp <= 150);
    }
    
    [TestCleanup]
    public void Cleanup()
    {
        (_client as IDisposable)?.Dispose();
    }
}
```

---

## 十、总结

### 推荐实践

✅ **推荐做法：**
1. 使用 `Client.Core` 用于现代 .NET 应用
2. 使用依赖注入管理客户端生命周期
3. 使用懒加载属性（默认行为）
4. 实现完整的错误处理
5. 复用客户端实例
6. 可观察属性使用完毕后调用 `Dispose()`

❌ **避免做法：**
1. 在多线程中共享同一客户端实例
2. 忽略异常处理
3. 为每个请求创建新的客户端实例
4. 不检查服务器是否实现了所需的 Feature
5. 不处理连接断开的情况

---

## 相关文档

- [Generator技术详解与使用指南.md](./Generator技术详解与使用指南.md) - Generator 详细说明
- [Client与Client.Core对比分析.md](./Client与Client.Core对比分析.md) - 客户端库选择
- [项目架构分析.md](./项目架构分析.md) - 整体架构说明

---

*文档版本: 1.0*  
*最后更新: 2025-10-11*


