# SiLA 2 C# 库实现指南

版本 1.1 | 基于 SiLA 2 标准 v1.1 | 包含转换工具实现

---

## 概述

本文档从 SiLA 2 规范（Part A、B、C）中提取关键技术信息，为实现 C# SiLA 2 库提供指导。

### 库功能目标

1. **服务器发现**：mDNS/DNS-SD 自动发现，缓存管理，设备上下线通知
2. **特性管理**：获取和解析服务器特性定义
3. **动态客户端**：运行时动态调用特性功能
4. **静态代码生成**：从特性定义生成强类型 C# 客户端代码

---

## 1. 服务器发现模块

### 1.1 技术基础

**实现技术**：
- **mDNS**（多播 DNS）- RFC 6762
- **DNS-SD**（基于 DNS 的服务发现）- RFC 6763

**服务命名规范**：
```
<SiLA服务器UUID>._sila._tcp.local.
```

**示例**：
```
25597b36-e9bf-11e8-aeb5-f2801f1b9fd1._sila._tcp.local.
```

### 1.2 服务属性（TXT 记录）

**必须字段**：
```
version=<SiLA 2版本>           # 例如: "1.1"
server_name=<服务器名称>        # 人类可读名称
description=<服务器描述>        # 用途描述
```

**不受信任证书的额外字段**：
```
ca0=<证书第0行PEM编码>
ca1=<证书第1行PEM编码>
...
```

### 1.3 实现要点

**推荐库**：使用 `Zeroconf` NuGet 包进行 mDNS/DNS-SD 服务发现

**Zeroconf 优势**：
- 跨平台支持（Windows、Linux、macOS）
- 现代异步 API 设计
- 活跃维护和良好的文档
- 简洁易用的接口

```csharp
using Zeroconf;
using Microsoft.Extensions.Logging;

// 服务发现类结构建议
public class SilaServerInfo
{
    public string UUID { get; set; }
    public string ServerName { get; set; }
    public string ServerType { get; set; }
    public string Version { get; set; }
    public string Description { get; set; }
    public string VendorUrl { get; set; }
    public string IPAddress { get; set; }
    public int Port { get; set; }
    public X509Certificate2 Certificate { get; set; }
    public DateTime LastSeen { get; set; }
}

// 发现管理器（使用 Zeroconf）
public class SilaDiscoveryManager
{
    private readonly ILogger<SilaDiscoveryManager> _logger;
    private readonly ConcurrentDictionary<string, SilaServerInfo> _discoveredServers = new();
    private CancellationTokenSource _discoveryCts;
    
    public SilaDiscoveryManager(ILogger<SilaDiscoveryManager> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// 开始监听 mDNS 服务
    /// </summary>
    public async Task StartDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        _discoveryCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        try
        {
            _logger.LogInformation("开始 SiLA 服务器发现...");
            
            // 持续监听 SiLA 服务
            await foreach (var response in ZeroconfResolver.ResolveAsync("_sila._tcp.local.", 
                cancellationToken: _discoveryCts.Token))
            {
                foreach (var service in response)
                {
                    ProcessDiscoveredService(service);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("服务发现已停止");
        }
    }
    
    /// <summary>
    /// 停止监听
    /// </summary>
    public void StopDiscovery()
    {
        _discoveryCts?.Cancel();
    }
    
    /// <summary>
    /// 获取所有已发现的服务器
    /// </summary>
    public IEnumerable<SilaServerInfo> GetDiscoveredServers()
    {
        return _discoveredServers.Values.ToList();
    }
    
    /// <summary>
    /// 事件：服务器上线
    /// </summary>
    public event EventHandler<ServerDiscoveredEventArgs> ServerDiscovered;
    
    /// <summary>
    /// 事件：服务器下线
    /// </summary>
    public event EventHandler<ServerLostEventArgs> ServerLost;
    
    private void ProcessDiscoveredService(IZeroconfHost service)
    {
        try
        {
            // 从服务名称提取 UUID
            var serviceName = service.Services.FirstOrDefault().Key;
            var uuid = serviceName?.Split('.').FirstOrDefault();
            
            if (string.IsNullOrEmpty(uuid))
                return;
            
            // 解析 TXT 记录
            var txtRecords = service.Services.FirstOrDefault().Value?.Properties;
            if (txtRecords == null)
                return;
            
            var serverInfo = new SilaServerInfo
            {
                UUID = uuid,
                ServerName = GetTxtValue(txtRecords, "server_name"),
                Description = GetTxtValue(txtRecords, "description"),
                Version = GetTxtValue(txtRecords, "version"),
                IPAddress = service.IPAddress,
                Port = service.Services.FirstOrDefault().Value?.Port ?? 0,
                LastSeen = DateTime.Now
            };
            
            // 添加或更新服务器信息
            if (_discoveredServers.TryAdd(uuid, serverInfo))
            {
                _logger.LogInformation("发现新的 SiLA 服务器: {ServerName} ({UUID}) at {IPAddress}:{Port}",
                    serverInfo.ServerName, serverInfo.UUID, serverInfo.IPAddress, serverInfo.Port);
                ServerDiscovered?.Invoke(this, new ServerDiscoveredEventArgs(serverInfo));
            }
            else
            {
                _discoveredServers[uuid] = serverInfo;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理发现的服务时出错");
        }
    }
    
    private string GetTxtValue(IReadOnlyDictionary<string, string> txtRecords, string key)
    {
        return txtRecords.TryGetValue(key, out var value) ? value : null;
    }
}
```

**缓存策略**：
- 使用 `ConcurrentDictionary` 维护服务器列表
- 记录每个服务器的最后见到时间（`LastSeen`）
- 实现定期清理过期服务器的机制
- 支持取消令牌以优雅停止发现过程

---

## 2. 服务器信息管理

### 2.1 SiLA 服务器属性定义

**核心属性**：

| 属性 | 类型 | 说明 | 规则 |
|------|------|------|------|
| 服务器名称 | String | 显示名称，最大255字符 | 可配置，无唯一性保证 |
| 服务器类型 | String | 标识符（如制造商+型号） | 以大写字母开头，驼峰命名 |
| 服务器UUID | UUID | 全局唯一标识符 | 生命周期内不变 |
| 服务器版本 | String | 主.次[.补丁][_文本] | 例如: "3.19.373" |
| 供应商URL | URL | 供应商网站 | RFC 1738 格式 |
| 服务器描述 | String | 用途和目的说明 | 人类可读文本 |

### 2.2 连接方法

**客户端发起连接**（必须支持）：
- SiLA 客户端连接到服务器的 IP:Port
- 所有服务通过该套接字公开

**服务器发起连接**（SiLA 2 v1.1+，应该支持）：
- 服务器连接到客户端
- 通过双向 gRPC 流通信
- 需实现 `ConnectionConfigurationService` 特性

---

## 3. 特性发现与管理

### 3.1 SiLA 服务特性

**强制实现**：每个 SiLA 服务器**必须**实现 `SiLAService` 特性（v1）。

**功能**：
- 获取服务器实现的所有特性列表
- 获取特性定义（XML 格式）
- 服务器元数据访问

### 3.2 特性框架结构

**特性定义包含**：
- **特性元数据**：标识符、显示名称、描述、版本、成熟度级别
- **命令**：可观察/不可观察命令，参数，响应
- **属性**：可观察/不可观察属性，数据类型
- **数据类型**：自定义数据类型定义
- **元数据**：SiLA 客户端元数据定义
- **错误**：定义的执行错误

### 3.3 完全限定标识符（FQI）系统

**特性标识符**：
```
<originator>/<category>/<FeatureIdentifier>/v<major>
```

**示例**：
```
org.silastandard/core/SiLAService/v1
```

**命令标识符**：
```
<完全限定特性标识符>/Command/<命令标识符>
```

**示例**：
```
org.silastandard/core/SiLAService/v1/Command/GetFeatureDefinition
```

**其他标识符**：
- 属性：`.../Property/<属性标识符>`
- 参数：`.../Parameter/<参数标识符>`
- 响应：`.../Response/<响应标识符>`
- 错误：`.../DefinedExecutionError/<错误标识符>`

### 3.4 特性版本控制

**版本格式**：主版本.次版本（例如：1.0）

**兼容性规则**：
- 相同主版本内应向后和向前兼容
- 主版本变更：重命名、删除、改变行为
- 次版本变更：澄清文档、修正拼写

---

## 4. 动态客户端 - 连接与通信

### 4.1 技术栈

**核心技术**：
- **HTTP/2**：传输层
- **Protocol Buffers**：数据序列化
- **gRPC**：RPC 框架（推荐使用 Grpc.Net.Client）

### 4.2 客户端发起连接

```csharp
// 创建 gRPC 通道
var channel = GrpcChannel.ForAddress($"https://{ipAddress}:{port}", new GrpcChannelOptions
{
    HttpHandler = new SocketsHttpHandler
    {
        SslOptions = new SslClientAuthenticationOptions
        {
            // 证书验证逻辑
            RemoteCertificateValidationCallback = ValidateCertificate
        }
    }
});

// 创建服务客户端（假设已生成）
var client = new SiLAService.SiLAServiceClient(channel);
```

### 4.3 服务器发起连接

**gRPC 服务定义**：
```protobuf
service CloudClientEndpoint {
    rpc ConnectSiLAServer (stream SILAServerMessage) 
        returns (stream SILAClientMessage) {}
}
```

**消息结构**：
- `SILAClientMessage`：客户端→服务器
- `SILAServerMessage`：服务器→客户端
- 每个消息包含 `requestUUID` 和具体消息类型（oneof）

### 4.4 TLS 加密（强制要求）

**必须**：
- 所有连接必须使用 TLS 加密
- 推荐使用受信任证书

**不受信任证书要求**：
- 通用名称（CN）设置为 "SiLA2"
- 推荐包含 OID "1.3.6.1.4.1.58583" 扩展（含服务器 UUID）

**私有 IP 地址例外**：
- IPv4: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16
- IPv6: 唯一本地地址（ULA）

---

## 5. 动态客户端 - 命令执行

### 5.1 不可观察命令

**映射**：
```protobuf
// 单个 RPC
rpc <CommandIdentifier> (<CommandIdentifier>_Parameters) 
    returns (<CommandIdentifier>_Responses) {}
```

**C# 调用示例**：
```csharp
// 假设命令：MoveSample(PlateSiteA, PlateSiteB) -> Precision
var request = new MoveSample_Parameters 
{
    PlateSiteA = new Integer { Value = 1 },
    PlateSiteB = new Integer { Value = 2 }
};

var response = await client.MoveSampleAsync(request);
double precision = response.Precision.Value;
```

### 5.2 可观察命令

**需要 4 个 RPC**：
```protobuf
// 1. 启动命令
rpc <Command> (<Command>_Parameters) 
    returns (CommandConfirmation) {}

// 2. 订阅执行信息
rpc <Command>_Info (Subscribe_<Command>_Info_Parameters) 
    returns (stream ExecutionInfo) {}

// 3. 订阅中间响应（可选）
rpc <Command>_Intermediate (Subscribe_<Command>_Intermediate_Parameters) 
    returns (stream <Command>_IntermediateResponses) {}

// 4. 获取结果
rpc <Command>_Result (<Command>_Result_Parameters) 
    returns (<Command>_Responses) {}
```

**执行流程**：
```csharp
// 1. 启动命令
var confirmation = await client.StartMeasurementAsync(parameters);
var executionUuid = confirmation.CommandExecutionUUID.Value;

// 2. 订阅执行信息
var infoCall = client.StartMeasurement_Info(
    new Subscribe_StartMeasurement_Info_Parameters 
    { 
        CommandExecutionUUID = confirmation.CommandExecutionUUID 
    });

await foreach (var info in infoCall.ResponseStream.ReadAllAsync())
{
    Console.WriteLine($"状态: {info.CommandStatus}");
    Console.WriteLine($"进度: {info.ProgressInfo.Value * 100}%");
    
    if (info.CommandStatus == ExecutionInfo.Types.CommandStatus.FinishedSuccessfully)
        break;
}

// 3. 获取结果
var result = await client.StartMeasurement_ResultAsync(
    new StartMeasurement_Result_Parameters 
    { 
        CommandExecutionUUID = confirmation.CommandExecutionUUID 
    });
```

**命令执行状态**：
- `waiting` (0)：等待执行
- `running` (1)：正在执行
- `finishedSuccessfully` (2)：成功完成
- `finishedWithError` (3)：执行出错

**ExecutionInfo 结构**：
```protobuf
message ExecutionInfo {
    CommandStatus commandStatus = 1;
    Real progressInfo = 2;              // 0.0 - 1.0
    Duration estimatedRemainingTime = 3;
    Duration updatedLifetimeOfExecution = 4;
}
```

---

## 6. 动态客户端 - 属性访问

### 6.1 不可观察属性

**映射**：
```protobuf
rpc Get_<PropertyIdentifier> (Get_<PropertyIdentifier>_Parameters) 
    returns (Get_<PropertyIdentifier>_Responses) {}

message Get_<PropertyIdentifier>_Parameters {}

message Get_<PropertyIdentifier>_Responses {
    <DataType> <PropertyIdentifier> = 1;
}
```

**C# 调用示例**：
```csharp
var response = await client.Get_DeviceNameAsync(
    new Get_DeviceName_Parameters());
string deviceName = response.DeviceName.Value;
```

### 6.2 可观察属性

**映射**：
```protobuf
rpc Subscribe_<PropertyIdentifier> (Subscribe_<PropertyIdentifier>_Parameters) 
    returns (stream Subscribe_<PropertyIdentifier>_Responses) {}
```

**C# 订阅示例**：
```csharp
// 订阅温度属性
var call = client.Subscribe_Temperature(
    new Subscribe_Temperature_Parameters());

await foreach (var update in call.ResponseStream.ReadAllAsync())
{
    double temperature = update.Temperature.Value;
    Console.WriteLine($"当前温度: {temperature}°C");
}

// 取消订阅
call.Dispose();
```

**特点**：
- 订阅时立即返回当前值
- 值变化时推送新值
- 客户端通过取消 gRPC 流来停止订阅

### 6.3 事件信息字段化处理

对于可观察命令和属性，推荐将事件携带的信息存储在字段中，并提供公共方法获取这些信息。同时提供阻塞等待方法，直到事件完成再返回。

#### 6.3.1 可观察属性事件处理

**设计模式**：将属性值存储在私有字段，提供公共方法获取

```csharp
public class TemperatureMonitor
{
    private double _currentTemperature;
    private DateTime _lastUpdateTime;
    private readonly object _lock = new object();
    
    /// <summary>
    /// 获取当前温度值
    /// </summary>
    public double GetCurrentTemperature()
    {
        lock (_lock)
        {
            return _currentTemperature;
        }
    }
    
    /// <summary>
    /// 获取最后更新时间
    /// </summary>
    public DateTime GetLastUpdateTime()
    {
        lock (_lock)
        {
            return _lastUpdateTime;
        }
    }
    
    /// <summary>
    /// 订阅温度变化
    /// </summary>
    public async Task SubscribeTemperatureAsync(CancellationToken cancellationToken = default)
    {
        var call = _client.Subscribe_Temperature(new Subscribe_Temperature_Parameters());
        
        await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))
        {
            lock (_lock)
            {
                _currentTemperature = update.Temperature.Value;
                _lastUpdateTime = DateTime.Now;
            }
            
            // 触发事件通知订阅者
            TemperatureChanged?.Invoke(this, _currentTemperature);
        }
    }
    
    public event EventHandler<double> TemperatureChanged;
}
```

#### 6.3.2 可观察命令事件处理与阻塞等待

**设计模式**：跟踪命令执行状态，提供阻塞等待方法

```csharp
/// <summary>
/// 命令执行状态
/// </summary>
public class CommandExecutionState
{
    public CommandStatus Status { get; set; }
    public double Progress { get; set; }
    public Duration EstimatedRemainingTime { get; set; }
    public DateTime LastUpdated { get; set; }
    public string ErrorMessage { get; set; }
    public SiLAError Error { get; set; }
}

/// <summary>
/// 可观察命令执行跟踪器
/// </summary>
public class CommandExecutionTracker
{
    private readonly ILogger<CommandExecutionTracker> _logger;
    private readonly ConcurrentDictionary<string, CommandExecutionState> _executionStates = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _completionSources = new();
    
    public CommandExecutionTracker(ILogger<CommandExecutionTracker> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// 获取命令执行状态
    /// </summary>
    public CommandExecutionState GetExecutionState(string executionUuid)
    {
        return _executionStates.TryGetValue(executionUuid, out var state) 
            ? state 
            : null;
    }
    
    /// <summary>
    /// 阻塞等待命令执行完成
    /// </summary>
    /// <param name="executionUuid">命令执行 UUID</param>
    /// <param name="timeout">超时时间（可选，默认无超时）</param>
    /// <returns>命令执行结果</returns>
    /// <exception cref="CommandExecutionException">命令执行失败时抛出</exception>
    /// <exception cref="TimeoutException">执行超时时抛出</exception>
    public async Task<TResult> WaitForCompletionAsync<TResult>(
        string executionUuid, 
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var cts = timeout.HasValue 
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        if (timeout.HasValue)
        {
            cts.CancelAfter(timeout.Value);
        }
        
        try
        {
            _logger.LogInformation("等待命令 {ExecutionUuid} 完成...", executionUuid);
            
            while (!cts.Token.IsCancellationRequested)
            {
                if (!_executionStates.TryGetValue(executionUuid, out var state))
                {
                    await Task.Delay(100, cts.Token);
                    continue;
                }
                
                switch (state.Status)
                {
                    case CommandStatus.FinishedSuccessfully:
                        _logger.LogInformation("命令 {ExecutionUuid} 执行成功", executionUuid);
                        return await GetCommandResultAsync<TResult>(executionUuid);
                        
                    case CommandStatus.FinishedWithError:
                        _logger.LogError("命令 {ExecutionUuid} 执行失败: {ErrorMessage}", 
                            executionUuid, state.ErrorMessage);
                        throw new CommandExecutionException(
                            $"命令执行失败: {state.ErrorMessage}", 
                            state.Error);
                        
                    case CommandStatus.Waiting:
                    case CommandStatus.Running:
                        _logger.LogDebug("命令 {ExecutionUuid} 执行中，进度: {Progress:P0}", 
                            executionUuid, state.Progress);
                        await Task.Delay(100, cts.Token);
                        break;
                        
                    default:
                        throw new InvalidOperationException($"未知的命令状态: {state.Status}");
                }
            }
            
            throw new OperationCanceledException("等待命令完成已取消");
        }
        catch (OperationCanceledException) when (timeout.HasValue && cts.IsCancellationRequested)
        {
            _logger.LogWarning("等待命令 {ExecutionUuid} 完成超时", executionUuid);
            throw new TimeoutException($"等待命令 {executionUuid} 完成超时（超时设置: {timeout.Value}）");
        }
        finally
        {
            cts?.Dispose();
        }
    }
    
    /// <summary>
    /// 订阅命令执行信息（内部方法）
    /// </summary>
    internal async Task SubscribeExecutionInfoAsync(
        string executionUuid,
        AsyncServerStreamingCall<ExecutionInfo> infoStream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await foreach (var info in infoStream.ResponseStream.ReadAllAsync(cancellationToken))
            {
                var state = new CommandExecutionState
                {
                    Status = info.CommandStatus,
                    Progress = info.ProgressInfo?.Value ?? 0,
                    EstimatedRemainingTime = info.EstimatedRemainingTime,
                    LastUpdated = DateTime.Now
                };
                
                _executionStates[executionUuid] = state;
                
                // 如果命令完成（成功或失败），通知等待者
                if (info.CommandStatus == CommandStatus.FinishedSuccessfully ||
                    info.CommandStatus == CommandStatus.FinishedWithError)
                {
                    if (_completionSources.TryRemove(executionUuid, out var tcs))
                    {
                        tcs.TrySetResult(null);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "订阅命令 {ExecutionUuid} 执行信息时出错", executionUuid);
            
            // 记录错误状态
            if (_executionStates.TryGetValue(executionUuid, out var state))
            {
                state.Status = CommandStatus.FinishedWithError;
                state.ErrorMessage = ex.Message;
            }
        }
    }
    
    private async Task<TResult> GetCommandResultAsync<TResult>(string executionUuid)
    {
        // 这里应该调用实际的 Result RPC 获取结果
        // 示例代码需要根据具体的命令类型实现
        throw new NotImplementedException("需要实现具体的结果获取逻辑");
    }
}

/// <summary>
/// 命令执行异常
/// </summary>
public class CommandExecutionException : Exception
{
    public SiLAError SilaError { get; }
    
    public CommandExecutionException(string message, SiLAError error) 
        : base(message)
    {
        SilaError = error;
    }
}
```

#### 6.3.3 使用示例

```csharp
public async Task ExecuteCommandExample()
{
    var tracker = new CommandExecutionTracker(_logger);
    
    // 启动可观察命令
    var confirmation = await _client.StartMeasurementAsync(parameters);
    string executionUuid = confirmation.CommandExecutionUUID.Value;
    
    // 在后台订阅执行信息
    var infoCall = _client.StartMeasurement_Info(
        new Subscribe_StartMeasurement_Info_Parameters 
        { 
            CommandExecutionUUID = confirmation.CommandExecutionUUID 
        });
    
    _ = tracker.SubscribeExecutionInfoAsync(executionUuid, infoCall);
    
    try
    {
        // 阻塞等待命令完成（最多等待 5 分钟）
        var result = await tracker.WaitForCompletionAsync<MeasurementResult>(
            executionUuid, 
            TimeSpan.FromMinutes(5));
        
        Console.WriteLine($"测量完成，结果: {result}");
    }
    catch (CommandExecutionException ex)
    {
        Console.WriteLine($"命令执行失败: {ex.Message}");
        // 可以通过 ex.SilaError 获取详细错误信息
        if (ex.SilaError?.DefinedExecutionError != null)
        {
            Console.WriteLine($"错误标识符: {ex.SilaError.DefinedExecutionError.ErrorIdentifier}");
        }
    }
    catch (TimeoutException ex)
    {
        Console.WriteLine($"命令执行超时: {ex.Message}");
        // 可以查询当前状态
        var state = tracker.GetExecutionState(executionUuid);
        Console.WriteLine($"当前进度: {state?.Progress:P0}");
    }
}
```

#### 6.3.4 关键设计要点

1. **线程安全**：使用 `ConcurrentDictionary` 和锁保护共享状态
2. **状态管理**：完整跟踪命令执行生命周期（Waiting → Running → Finished）
3. **异常处理**：区分不同错误场景（执行失败、超时、取消）
4. **超时保护**：使用 `CancellationToken` 避免无限等待
5. **日志记录**：关键操作都记录日志，便于调试和监控
6. **进度跟踪**：实时更新执行进度和剩余时间估算
7. **资源清理**：正确释放 gRPC 调用和取消令牌

---

## 7. 数据类型映射

### 7.1 SiLA 基本类型 → Protocol Buffer

| SiLA 类型 | Protocol Buffer 定义 | C# 类型 |
|-----------|---------------------|---------|
| String | `message String { string value = 1; }` | `String` |
| Integer | `message Integer { int64 value = 1; }` | `Integer` |
| Real | `message Real { double value = 1; }` | `Real` |
| Boolean | `message Boolean { bool value = 1; }` | `Boolean` |
| Binary | `message Binary { oneof union { bytes value = 1; string binaryTransferUUID = 2; } }` | `Binary` |
| Date | `message Date { uint32 day = 1; uint32 month = 2; uint32 year = 3; Timezone timezone = 4; }` | `Date` |
| Time | `message Time { uint32 second = 1; uint32 minute = 2; uint32 hour = 3; uint32 millisecond = 4; Timezone timezone = 5; }` | `Time` |
| Timestamp | `message Timestamp { uint32 second = 1; ... uint32 year = 6; Timezone timezone = 7; }` | `Timestamp` |

### 7.2 派生类型

**列表类型**：
```protobuf
// SiLA: List<Integer>
repeated Integer values = 1;
```

```csharp
// C# 使用
var list = new RepeatedField<Integer>();
list.Add(new Integer { Value = 1 });
list.Add(new Integer { Value = 2 });
```

**结构类型**：
```protobuf
message Point_Struct {
    Real X = 1;
    Real Y = 1;
}

message Command_Parameters {
    Point_Struct Point = 1;
}
```

```csharp
// C# 使用
var param = new Command_Parameters
{
    Point = new Point_Struct
    {
        X = new Real { Value = 1.5 },
        Y = new Real { Value = 2.5 }
    }
};
```

**约束类型**：
- Protocol Buffer 层面无映射
- **必须**在客户端/服务器端验证约束
- 违反约束抛出验证错误

### 7.3 特殊类型

**Any 类型**：
```protobuf
message Any {
    string type = 1;    // XML 描述的数据类型
    bytes payload = 2;  // 序列化的数据
}
```

**Void 类型**：
长度约束为 0 的 String 类型。

### 7.4 约束类型示例

**单位约束**：
```xml
<!-- 温度，单位：摄氏度 -->
<Constraint>
    <Type>Unit</Type>
    <Unit>
        <Label>°C</Label>
        <Factor>1</Factor>
        <Offset>273.15</Offset>
        <UnitComponent>
            <SIUnit>Kelvin</SIUnit>
            <Exponent>1</Exponent>
        </UnitComponent>
    </Unit>
</Constraint>
```

**转换公式**：
```
x_SI = x_original × factor + offset
```

---

## 8. 错误处理

### 8.1 错误分类

| 错误类型 | 发生时机 | 说明 |
|----------|----------|------|
| **验证错误** | 命令执行前 | 参数验证失败 |
| **定义的执行错误** | 执行期间 | 特性预定义的错误 |
| **未定义的执行错误** | 执行期间 | 未预见的执行错误 |
| **框架错误** | 任何时候 | 违反 SiLA 2 规范 |
| **连接错误** | 任何时候 | 网络/通信错误 |

### 8.2 SiLAError Protocol Buffer

```protobuf
message SiLAError {
    oneof error {
        ValidationError validationError = 1;
        DefinedExecutionError definedExecutionError = 2;
        UndefinedExecutionError undefinedExecutionError = 3;
        FrameworkError frameworkError = 4;
    }
}

message ValidationError {
    string parameter = 1;  // 完全限定参数标识符
    string message = 2;
}

message DefinedExecutionError {
    string errorIdentifier = 1;  // 完全限定错误标识符
    string message = 2;
}

message UndefinedExecutionError {
    string message = 1;
}

message FrameworkError {
    enum ErrorType {
        COMMAND_EXECUTION_NOT_ACCEPTED = 0;
        INVALID_COMMAND_EXECUTION_UUID = 1;
        COMMAND_EXECUTION_NOT_FINISHED = 2;
        INVALID_METADATA = 3;
        NO_METADATA_ALLOWED = 4;
    }
    ErrorType errorType = 1;
    string message = 2;
}
```

### 8.3 客户端错误处理

```csharp
try
{
    var response = await client.SomeCommandAsync(request);
}
catch (RpcException ex) when (ex.StatusCode == StatusCode.Aborted)
{
    // 解析 SiLA 错误
    var silaError = ParseSilaError(ex.Status.Detail);
    
    switch (silaError.ErrorCase)
    {
        case SiLAError.ErrorOneofCase.ValidationError:
            Console.WriteLine($"验证错误: {silaError.ValidationError.Message}");
            break;
            
        case SiLAError.ErrorOneofCase.DefinedExecutionError:
            Console.WriteLine($"执行错误: {silaError.DefinedExecutionError.ErrorIdentifier}");
            break;
            
        case SiLAError.ErrorOneofCase.FrameworkError:
            Console.WriteLine($"框架错误: {silaError.FrameworkError.ErrorType}");
            break;
    }
}
catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
{
    // 连接错误/超时
    Console.WriteLine("连接超时");
}

private SiLAError ParseSilaError(string base64Detail)
{
    var bytes = Convert.FromBase64String(base64Detail);
    return SiLAError.Parser.ParseFrom(bytes);
}
```

---

## 9. 二进制传输

### 9.1 使用场景

当二进制数据 **> 2 MiB** 时，必须使用二进制传输机制。

### 9.2 二进制上传（客户端→服务器）

**步骤**：
```csharp
// 1. 创建二进制存储
var createRequest = new CreateBinaryRequest { Size = fileSize };
var createResponse = await binaryUploadClient.CreateBinaryAsync(createRequest);
string binaryUuid = createResponse.BinaryTransferUUID.Value;

// 2. 分块上传
const int chunkSize = 2 * 1024 * 1024; // 2 MiB
using var call = binaryUploadClient.UploadChunk();

for (int offset = 0; offset < fileSize; offset += chunkSize)
{
    var chunk = ReadChunk(fileData, offset, chunkSize);
    await call.RequestStream.WriteAsync(new UploadChunkRequest
    {
        BinaryTransferUUID = new String { Value = binaryUuid },
        Offset = new Integer { Value = offset },
        Data = new Binary { Value = Google.Protobuf.ByteString.CopyFrom(chunk) }
    });
    
    var response = await call.ResponseStream.MoveNext();
    // 检查响应
}

await call.RequestStream.CompleteAsync();

// 3. 在命令中使用 UUID
var commandRequest = new SomeCommand_Parameters
{
    ImageData = new Binary { BinaryTransferUUID = binaryUuid }
};
await client.SomeCommandAsync(commandRequest);

// 4. 可选：删除二进制数据
await binaryUploadClient.DeleteBinaryAsync(new DeleteBinaryRequest
{
    BinaryTransferUUID = new String { Value = binaryUuid }
});
```

### 9.3 二进制下载（服务器→客户端）

**步骤**：
```csharp
// 1. 获取二进制信息
var infoResponse = await binaryDownloadClient.GetBinaryInfoAsync(
    new GetBinaryInfoRequest 
    { 
        BinaryTransferUUID = new String { Value = binaryUuid } 
    });

long totalSize = infoResponse.Size.Value;

// 2. 分块下载
using var call = binaryDownloadClient.GetChunk();
const int chunkSize = 2 * 1024 * 1024;

for (long offset = 0; offset < totalSize; offset += chunkSize)
{
    await call.RequestStream.WriteAsync(new GetChunkRequest
    {
        BinaryTransferUUID = new String { Value = binaryUuid },
        Offset = new Integer { Value = offset },
        Length = new Integer { Value = Math.Min(chunkSize, totalSize - offset) }
    });
    
    if (await call.ResponseStream.MoveNext())
    {
        var chunk = call.ResponseStream.Current.Data.Value.ToByteArray();
        WriteChunk(fileData, offset, chunk);
    }
}

await call.RequestStream.CompleteAsync();
```

---

## 10. 元数据处理

### 10.1 SiLA 客户端元数据定义

**用途**：服务器期望从客户端接收的额外信息，影响多个特性/命令/属性。

**限制**：
- 不得影响 SiLAService 特性
- 数据大小 < 1 KB

### 10.2 客户端发起连接的元数据

**作为 gRPC 元数据头发送**：
```csharp
var metadata = new Metadata();

// 键名格式: sila-<完全限定元数据ID>-bin
var metadataKey = "sila-org.silastandard.core.lockcontroller.v1.metadata.lockidentifier-bin";

// 序列化元数据值
var lockId = new String { Value = "my-lock-12345" };
var bytes = lockId.ToByteArray();
var base64 = Convert.ToBase64String(bytes);

metadata.Add(metadataKey, Encoding.UTF8.GetBytes(base64));

// 调用命令时附加元数据
var response = await client.SomeCommandAsync(request, metadata);
```

### 10.3 元数据示例

**LockController 特性**：
```csharp
// 1. 锁定服务器
var lockResponse = await client.LockServerAsync(new LockServer_Parameters
{
    LockIdentifier = new String { Value = "my-lock" },
    Timeout = new Duration { ... }
});

// 2. 后续调用附加锁标识符
var metadata = CreateLockMetadata("my-lock");
var response = await client.ProtectedCommandAsync(request, metadata);

// 3. 解锁
await client.UnlockServerAsync(new UnlockServer_Parameters
{
    LockIdentifier = new String { Value = "my-lock" }
});
```

---

## 11. 认证与授权

### 11.1 AuthenticationService 特性

**功能**：
- 提供 `Login` 命令，返回访问令牌（AccessToken）
- 令牌具有生命周期（Lifetime）

**流程**：
```csharp
// 1. 登录
var loginResponse = await authClient.LoginAsync(new Login_Parameters
{
    User = new String { Value = "admin" },
    Password = new String { Value = "password" },
    RequestedFeatures = new RepeatedField<String>
    {
        new String { Value = "org.silastandard/core/SiLAService/v1" },
        new String { Value = "com.vendor/device/Control/v1" }
    }
});

string accessToken = loginResponse.AccessToken.Value;
var lifetime = loginResponse.Lifetime;

// 2. 使用访问令牌（作为元数据）
var metadata = CreateAuthMetadata(accessToken);
var response = await client.SomeCommandAsync(request, metadata);

// 3. 注销
await authClient.LogoutAsync(new Logout_Parameters
{
    AccessToken = new String { Value = accessToken }
});
```

### 11.2 AuthorizationService 特性

**功能**：
- 定义 `AccessToken` 客户端元数据
- 所有命令和属性（SiLAService 除外）都需要有效令牌

**元数据格式**：
```csharp
private Metadata CreateAuthMetadata(string accessToken)
{
    var metadata = new Metadata();
    var key = "sila-org.silastandard.core.authorizationservice.v1.metadata.accesstoken-bin";
    
    var token = new String { Value = accessToken };
    var bytes = token.ToByteArray();
    var base64 = Convert.ToBase64String(bytes);
    
    metadata.Add(key, Encoding.UTF8.GetBytes(base64));
    return metadata;
}
```

### 11.3 授权提供者模式

**场景**：集成到现有认证基础设施

```
[SiLA Client] 
    ↓ 1. Configure
[SiLA Server] ← - - - - → [Authorization Provider]
    ↓ 2. Login                    ↑ (验证令牌)
[Auth Provider]
    ↓ 3. Get Token
[SiLA Client]
    ↓ 4. Use Token
[SiLA Server] → 5. Validate → [Authorization Provider]
```

---

## 12. 特性定义转换工具

### 12.1 XML → Proto 转换器概述

为了简化从 SiLA 特性定义（XML）到 Protocol Buffer 文件的转换过程，建议实现一个专用的转换工具。

**转换流程**：
```
SiLA Feature XML → [XSLT] → Protocol Buffer (.proto) → [protoc] → C# 代码
```

### 12.2 转换器核心实现

#### 基于 XSLT 的转换

SiLA 官方提供了 XSLT 转换模板，位于 `sila_base/xslt/` 目录：

| 文件 | 用途 |
|------|------|
| `fdl2proto.xsl` | 主转换文件，协调整个转换过程 |
| `fdl2proto-messages.xsl` | 生成 Protocol Buffer 消息定义 |
| `fdl2proto-service.xsl` | 生成 gRPC 服务定义 |
| `fdl-validation.xsl` | 验证 XML 是否符合 SiLA 规范 |

#### C# 转换器实现

```csharp
using System;
using System.IO;
using System.Xml;
using System.Xml.Xsl;

public static class SiLAConverter
{
    private static readonly string XsltPath = "xslt/fdl2proto.xsl";
    
    /// <summary>
    /// 将 SiLA XML 文件转换为 Proto 文件
    /// </summary>
    public static bool ConvertXmlToProto(string xmlPath, string protoPath)
    {
        try
        {
            // 加载 XSLT 转换器
            var xslt = new XslCompiledTransform();
            var settings = new XsltSettings(enableDocumentFunction: true, enableScript: true);
            xslt.Load(XsltPath, settings, new XmlUrlResolver());
            
            // 创建输出目录
            Directory.CreateDirectory(Path.GetDirectoryName(protoPath) ?? ".");
            
            // 执行转换
            using (var writer = new StreamWriter(protoPath))
            {
                xslt.Transform(xmlPath, null, writer);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"转换失败: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 从内存 XML 字符串转换为 Proto 字符串（运行时动态转换）
    /// </summary>
    public static string ConvertXmlStringToProto(string xmlContent)
    {
        var xslt = new XslCompiledTransform();
        var settings = new XsltSettings(enableDocumentFunction: true, enableScript: true);
        xslt.Load(XsltPath, settings, new XmlUrlResolver());
        
        using (var stringReader = new StringReader(xmlContent))
        using (var xmlReader = XmlReader.Create(stringReader))
        using (var stringWriter = new StringWriter())
        {
            xslt.Transform(xmlReader, null, stringWriter);
            return stringWriter.ToString();
        }
    }
    
    /// <summary>
    /// 批量转换多个特性定义
    /// </summary>
    public static Dictionary<string, string> ConvertXmlStringsToProto(
        Dictionary<string, string> xmlContents)
    {
        var results = new Dictionary<string, string>();
        
        foreach (var kvp in xmlContents)
        {
            try
            {
                results[kvp.Key] = ConvertXmlStringToProto(kvp.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{kvp.Key} 转换失败: {ex.Message}");
                results[kvp.Key] = string.Empty;
            }
        }
        
        return results;
    }
}
```

### 12.3 XML Schema 验证

在转换前验证 XML 文件是否符合 SiLA 规范：

```csharp
public static bool ValidateXml(string xmlPath)
{
    try
    {
        var settings = new XmlReaderSettings();
        
        // 添加 SiLA Feature Definition Schema
        if (File.Exists("schema/FeatureDefinition.xsd"))
        {
            settings.Schemas.Add(null, "schema/FeatureDefinition.xsd");
            settings.ValidationType = ValidationType.Schema;
            settings.ValidationEventHandler += (sender, e) =>
            {
                Console.WriteLine($"验证错误: {e.Message}");
            };
        }
        
        using (var reader = XmlReader.Create(xmlPath, settings))
        {
            while (reader.Read()) { }
        }
        
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"XML 验证失败: {ex.Message}");
        return false;
    }
}
```

### 12.4 Proto 到 C# 代码编译

使用 `protoc` 编译器（通过 Grpc.Tools NuGet 包提供）：

```csharp
public static bool CompileProtoToCSharp(string protoPath, string outputDir)
{
    try
    {
        // 查找 protoc.exe（来自 Grpc.Tools NuGet 包）
        var protocPath = FindProtocPath();
        if (string.IsNullOrEmpty(protocPath))
        {
            Console.WriteLine("找不到 protoc.exe，请安装 Grpc.Tools");
            return false;
        }
        
        Directory.CreateDirectory(outputDir);
        
        // 查找 gRPC C# 插件
        var grpcPluginPath = FindGrpcPluginPath();
        
        // 构建命令参数
        var args = $"--csharp_out={outputDir} " +
                  $"-I . -I protobuf " +
                  $"{protoPath}";
        
        if (!string.IsNullOrEmpty(grpcPluginPath))
        {
            args += $" --grpc_out={outputDir} " +
                   $"--plugin=protoc-gen-grpc={grpcPluginPath}";
        }
        
        // 执行 protoc
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = protocPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        
        process.Start();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        
        if (process.ExitCode == 0)
        {
            Console.WriteLine("C# 代码生成成功");
            return true;
        }
        else
        {
            Console.WriteLine($"protoc 编译失败: {error}");
            return false;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"编译失败: {ex.Message}");
        return false;
    }
}

// 辅助方法：查找 NuGet 包中的 protoc.exe
private static string FindProtocPath()
{
    var nugetPackages = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".nuget", "packages", "grpc.tools"
    );
    
    if (Directory.Exists(nugetPackages))
    {
        var versions = Directory.GetDirectories(nugetPackages);
        if (versions.Length > 0)
        {
            Array.Sort(versions);
            var latestVersion = versions[^1];
            var platform = Environment.Is64BitProcess ? "windows_x64" : "windows_x86";
            var protocPath = Path.Combine(latestVersion, "tools", platform, "protoc.exe");
            
            if (File.Exists(protocPath))
                return protocPath;
        }
    }
    
    return null;
}

private static string FindGrpcPluginPath()
{
    var nugetPackages = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".nuget", "packages", "grpc.tools"
    );
    
    if (Directory.Exists(nugetPackages))
    {
        var versions = Directory.GetDirectories(nugetPackages);
        if (versions.Length > 0)
        {
            Array.Sort(versions);
            var latestVersion = versions[^1];
            var platform = Environment.Is64BitProcess ? "windows_x64" : "windows_x86";
            var pluginPath = Path.Combine(latestVersion, "tools", platform, "grpc_csharp_plugin.exe");
            
            if (File.Exists(pluginPath))
                return pluginPath;
        }
    }
    
    return null;
}
```

### 12.5 完整转换流程

将 XML → Proto → C# 整合为一个完整流程：

```csharp
/// <summary>
/// 完整转换流程：XML → Proto → C#
/// </summary>
public static void ConvertComplete(string xmlPath, string outputDir)
{
    Console.WriteLine($"开始转换: {Path.GetFileName(xmlPath)}");
    
    // 创建输出目录结构
    Directory.CreateDirectory(outputDir);
    var protoDir = Path.Combine(outputDir, "proto");
    var csharpDir = Path.Combine(outputDir, "csharp");
    Directory.CreateDirectory(protoDir);
    Directory.CreateDirectory(csharpDir);
    
    // 步骤 1: 验证 XML
    if (!ValidateXml(xmlPath))
    {
        Console.WriteLine("XML 验证失败，转换已中止");
        return;
    }
    
    // 步骤 2: XML → Proto
    var fileName = Path.GetFileNameWithoutExtension(xmlPath);
    var protoPath = Path.Combine(protoDir, $"{fileName}.proto");
    
    if (!ConvertXmlToProto(xmlPath, protoPath))
    {
        Console.WriteLine("XML → Proto 转换失败");
        return;
    }
    
    // 步骤 3: Proto → C#
    if (CompileProtoToCSharp(protoPath, csharpDir))
    {
        Console.WriteLine($"转换完成！");
        Console.WriteLine($"  Proto 文件: {protoPath}");
        Console.WriteLine($"  C# 代码: {csharpDir}");
    }
}
```

### 12.6 转换器工具项目结构

建议的项目文件结构（.NET 8）：

```xml
<!-- SiLAConverter.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Grpc.Net.Client" Version="2.59.0" />
    <PackageReference Include="Grpc.Tools" Version="2.59.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Google.Protobuf" Version="3.25.1" />
  </ItemGroup>

  <ItemGroup>
    <!-- 复制转换所需文件到输出目录 -->
    <None Update="xslt/**/*" CopyToOutputDirectory="PreserveNewest" />
    <None Update="protobuf/**/*" CopyToOutputDirectory="PreserveNewest" />
    <None Update="schema/**/*" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

### 12.7 使用示例

```csharp
// 示例 1: 单个文件完整转换
SiLAConverter.ConvertComplete(
    "GreetingProvider-v1_0.sila.xml",
    "output/GreetingProvider"
);

// 示例 2: 仅转换为 Proto
SiLAConverter.ConvertXmlToProto(
    "SiLAService-v1_0.sila.xml",
    "output/SiLAService.proto"
);

// 示例 3: 运行时动态转换（从内存）
string xmlContent = await DownloadFeatureDefinitionAsync(serverUrl);
string protoContent = SiLAConverter.ConvertXmlStringToProto(xmlContent);
// 可以动态编译和使用

// 示例 4: 批量转换
var xmlFiles = Directory.GetFiles("features", "*.sila.xml");
foreach (var xmlFile in xmlFiles)
{
    var name = Path.GetFileNameWithoutExtension(xmlFile);
    SiLAConverter.ConvertComplete(xmlFile, $"output/{name}");
}
```

### 12.8 必需文件清单

转换器工具需要以下文件（来自 `sila_base` 仓库）：

| 目录/文件 | 说明 | 必需 |
|-----------|------|------|
| `xslt/fdl2proto.xsl` | 主转换文件 | ✅ |
| `xslt/fdl2proto-messages.xsl` | 消息生成 | ✅ |
| `xslt/fdl2proto-service.xsl` | 服务生成 | ✅ |
| `xslt/fdl-validation.xsl` | XML 验证 | ⭕ 推荐 |
| `protobuf/SiLAFramework.proto` | 框架类型定义 | ✅ |
| `protobuf/SiLABinaryTransfer.proto` | 二进制传输 | ⭕ 可选 |
| `schema/FeatureDefinition.xsd` | Feature Schema | ⭕ 推荐 |
| `schema/DataTypes.xsd` | 数据类型 Schema | ⭕ 推荐 |
| `schema/Constraints.xsd` | 约束 Schema | ⭕ 推荐 |

**获取方式**：
```bash
git clone https://gitlab.com/SiLA2/sila_base.git
# 或直接下载所需文件
```

### 12.9 集成到动态客户端

转换器可以集成到动态客户端，实现运行时代码生成：

```csharp
public class DynamicSilaClient
{
    public async Task<object> InvokeFeatureCommandAsync(
        string featureXml, 
        string commandName, 
        Dictionary<string, object> parameters)
    {
        // 1. 转换 XML → Proto
        string protoContent = SiLAConverter.ConvertXmlStringToProto(featureXml);
        
        // 2. 编译 Proto → C#（可使用 Roslyn 动态编译）
        var assembly = CompileProtoToAssembly(protoContent);
        
        // 3. 动态调用
        return await InvokeDynamically(assembly, commandName, parameters);
    }
}
```

### 12.10 XML 注释到 C# 代码的映射流程

本节分析从 SiLA 特性定义 XML 文件的注释信息（方法描述、入参返回值描述、值限制等）如何映射到最终生成的 C# 代码中。

#### 12.10.1 转换流程三阶段

```
阶段 1: XML → Proto (XSLT)
       ↓ 保留: Description, DisplayName, 约束信息
       
阶段 2: Proto → C# (protoc)
       ↓ 转换: /* */ → /// <summary>
       
阶段 3: Proto C# → 包装类 (手动/代码生成)
       ↓ 增强: 添加完整元数据和约束说明
```

#### 12.10.2 各阶段注释处理详解

**阶段 1：XML → Proto（当前 XSLT 实现状态）**

当前 XSLT 模板已经保留了部分注释信息：

```xml
<!-- XML 源文件示例 -->
<Command>
  <Identifier>SayHello</Identifier>
  <DisplayName>Say Hello</DisplayName>
  <Description>Returns "Hello SiLA 2 + [Name]" to the client.</Description>
  <Observable>No</Observable>
  <Parameter>
    <Identifier>Name</Identifier>
    <DisplayName>Name</DisplayName>
    <Description>The name to greet.</Description>
    <DataType><Basic>String</Basic></DataType>
  </Parameter>
  <Response>
    <Identifier>Greeting</Identifier>
    <DisplayName>Greeting</DisplayName>
    <Description>The greeting string, returned to the SiLA Client.</Description>
    <DataType><Basic>String</Basic></DataType>
  </Response>
</Command>
```

当前 XSLT 转换为 Proto（已保留 Description）：

```protobuf
/* Returns "Hello SiLA 2 + [Name]" to the client. */
rpc SayHello (sila2.org.silastandard.examples.greetingprovider.v1.SayHello_Parameters) 
    returns (sila2.org.silastandard.examples.greetingprovider.v1.SayHello_Responses) {}

/* Parameters for SayHello */
message SayHello_Parameters {
  sila2.org.silastandard.String Name = 1;  /* The name to greet. */
}

/* Responses of SayHello */
message SayHello_Responses {
  sila2.org.silastandard.String Greeting = 1;  /* The greeting string, returned to the SiLA Client. */
}
```

**阶段 2：Proto → C#（protoc 编译器自动处理）**

protoc 编译器会将 Proto 注释转换为 C# 的 XML 文档注释：

```csharp
/// <summary>
/// Returns "Hello SiLA 2 + [Name]" to the client. 
/// </summary>
/// <param name="request">The request received from the client.</param>
/// <param name="context">The context of the server-side call handler being invoked.</param>
/// <returns>The response to send back to the client (wrapped by a task).</returns>
public virtual Task<SayHello_Responses> SayHello(
    SayHello_Parameters request, 
    ServerCallContext context)
{
    throw new RpcException(new Status(StatusCode.Unimplemented, ""));
}

/// <summary>
/// Parameters for SayHello 
/// </summary>
public sealed partial class SayHello_Parameters : pb::IMessage<SayHello_Parameters>
{
    /// <summary>
    /// The name to greet. 
    /// </summary>
    public String Name { get; set; }
}
```

**阶段 3：生成包装类（需手动实现或代码生成）**

为了将所有 XML 信息完整映射到包装类，有以下实现方案：

#### 12.10.3 实现方案对比

**方案 A：增强 XSLT 模板（推荐用于团队开发）**

优点：
- 一次实现，自动化程度高
- 所有信息在 Proto 注释中可见
- 便于代码审查和维护

实现：修改 `fdl2proto-service.xsl` 和 `fdl2proto-messages.xsl`，在 Proto 注释中包含更丰富的元数据：

```protobuf
// [Feature: GreetingProvider v1.0]
// [DisplayName: Say Hello]
// [Observable: No]
// [Maturity: Verified]
/* Returns "Hello SiLA 2 + [Name]" to the client. */
rpc SayHello (SayHello_Parameters) returns (SayHello_Responses) {}

message SayHello_Parameters {
  // [DisplayName: Name]
  // [Required: Yes]
  // [Type: String]
  /* The name to greet. */
  String Name = 1;
}
```

然后编写后处理工具提取这些元数据标记：

```csharp
public class ProtoMetadataExtractor
{
    public Dictionary<string, MethodMetadata> ExtractMetadata(string protoFilePath)
    {
        var content = File.ReadAllText(protoFilePath);
        var metadata = new Dictionary<string, MethodMetadata>();
        
        // 使用正则表达式或解析器提取 // [Key: Value] 形式的元数据
        var methodPattern = @"// \[DisplayName: (.*?)\].*?rpc (\w+)";
        // ... 解析逻辑
        
        return metadata;
    }
}
```

**方案 B：运行时从 XML 提取（推荐用于动态客户端）**

优点：
- 无需修改XSLT模板
- 灵活性高，可按需提取信息
- 适合需要UI显示元数据的场景

实现：

```csharp
public class FeatureMetadata
{
    public string FeatureIdentifier { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Version { get; set; }
    public string MaturityLevel { get; set; }
    public Dictionary<string, CommandMetadata> Commands { get; set; }
    public Dictionary<string, PropertyMetadata> Properties { get; set; }
}

public class CommandMetadata
{
    public string Identifier { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public bool Observable { get; set; }
    public List<ParameterMetadata> Parameters { get; set; }
    public List<ResponseMetadata> Responses { get; set; }
    public List<string> DefinedErrors { get; set; }
}

public class ParameterMetadata
{
    public string Identifier { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string DataType { get; set; }
    public ConstraintInfo Constraints { get; set; }
}

public class FeatureDefinitionParser
{
    public FeatureMetadata ParseXml(string xmlContent)
    {
        var doc = XDocument.Parse(xmlContent);
        var ns = doc.Root.Name.Namespace;
        
        var feature = new FeatureMetadata
        {
            FeatureIdentifier = doc.Root.Element(ns + "Identifier")?.Value,
            DisplayName = doc.Root.Element(ns + "DisplayName")?.Value,
            Description = doc.Root.Element(ns + "Description")?.Value,
            Version = doc.Root.Attribute("FeatureVersion")?.Value,
            MaturityLevel = doc.Root.Attribute("MaturityLevel")?.Value,
            Commands = new Dictionary<string, CommandMetadata>(),
            Properties = new Dictionary<string, PropertyMetadata>()
        };
        
        // 解析命令
        foreach (var cmdElement in doc.Root.Elements(ns + "Command"))
        {
            var cmd = ParseCommand(cmdElement, ns);
            feature.Commands[cmd.Identifier] = cmd;
        }
        
        // 解析属性
        foreach (var propElement in doc.Root.Elements(ns + "Property"))
        {
            var prop = ParseProperty(propElement, ns);
            feature.Properties[prop.Identifier] = prop;
        }
        
        return feature;
    }
    
    private CommandMetadata ParseCommand(XElement element, XNamespace ns)
    {
        var command = new CommandMetadata
        {
            Identifier = element.Element(ns + "Identifier")?.Value,
            DisplayName = element.Element(ns + "DisplayName")?.Value,
            Description = element.Element(ns + "Description")?.Value,
            Observable = element.Element(ns + "Observable")?.Value == "Yes",
            Parameters = new List<ParameterMetadata>(),
            Responses = new List<ResponseMetadata>()
        };
        
        // 解析参数
        foreach (var paramElement in element.Elements(ns + "Parameter"))
        {
            command.Parameters.Add(ParseParameter(paramElement, ns));
        }
        
        // 解析响应
        foreach (var respElement in element.Elements(ns + "Response"))
        {
            command.Responses.Add(ParseResponse(respElement, ns));
        }
        
        return command;
    }
}
```

使用示例：

```csharp
// 获取特性定义XML
var featureXml = await silaClient.GetFeatureDefinitionAsync("org.silastandard/examples/GreetingProvider/v1");

// 解析元数据
var parser = new FeatureDefinitionParser();
var metadata = parser.ParseXml(featureXml);

// 在UI中显示
Console.WriteLine($"特性: {metadata.DisplayName}");
Console.WriteLine($"说明: {metadata.Description}");
Console.WriteLine($"成熟度: {metadata.MaturityLevel}");
Console.WriteLine();

foreach (var cmd in metadata.Commands.Values)
{
    Console.WriteLine($"命令: {cmd.DisplayName}");
    Console.WriteLine($"  标识符: {cmd.Identifier}");
    Console.WriteLine($"  说明: {cmd.Description}");
    Console.WriteLine($"  可观察: {cmd.Observable}");
    
    foreach (var param in cmd.Parameters)
    {
        Console.WriteLine($"  参数: {param.DisplayName} ({param.DataType})");
        Console.WriteLine($"    说明: {param.Description}");
        if (param.Constraints != null)
        {
            Console.WriteLine($"    约束: {param.Constraints}");
        }
    }
}
```

**方案 C：后处理生成的 C# 代码（推荐用于静态客户端生成）**

优点：
- 生成的包装类包含完整文档
- 符合C#文档标准
- 支持IDE智能提示

实现：

```csharp
public static class ClientWrapperGenerator
{
    public static void GenerateWrapper(
        string grpcClientPath,
        string featureXmlPath,
        string outputPath)
    {
        // 1. 解析 XML 获取完整元数据
        var xmlContent = File.ReadAllText(featureXmlPath);
        var parser = new FeatureDefinitionParser();
        var metadata = parser.ParseXml(xmlContent);
        
        // 2. 读取生成的 Grpc 类（用于参考）
        var grpcCode = File.ReadAllText(grpcClientPath);
        
        // 3. 生成包装类
        var sb = new StringBuilder();
        
        // 添加命名空间和using语句
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Grpc.Core;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine();
        
        // 生成类
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// {metadata.DisplayName} 客户端包装类");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"/// <remarks>");
        sb.AppendLine($"/// {metadata.Description}");
        sb.AppendLine($"/// <para>特性版本: {metadata.Version}</para>");
        sb.AppendLine($"/// <para>成熟度级别: {metadata.MaturityLevel}</para>");
        sb.AppendLine($"/// </remarks>");
        sb.AppendLine($"public class {metadata.FeatureIdentifier}Client : IDisposable");
        sb.AppendLine("{");
        
        // 生成字段
        sb.AppendLine($"    private readonly {metadata.FeatureIdentifier}.{metadata.FeatureIdentifier}Client _client;");
        sb.AppendLine($"    private readonly ILogger<{metadata.FeatureIdentifier}Client> _logger;");
        sb.AppendLine();
        
        // 生成构造函数
        sb.AppendLine($"    public {metadata.FeatureIdentifier}Client(");
        sb.AppendLine($"        GrpcChannel channel,");
        sb.AppendLine($"        ILogger<{metadata.FeatureIdentifier}Client> logger)");
        sb.AppendLine("    {");
        sb.AppendLine($"        _client = new {metadata.FeatureIdentifier}.{metadata.FeatureIdentifier}Client(channel);");
        sb.AppendLine("        _logger = logger;");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // 生成命令方法
        foreach (var cmd in metadata.Commands.Values)
        {
            GenerateCommandMethod(sb, cmd);
        }
        
        // 生成属性方法
        foreach (var prop in metadata.Properties.Values)
        {
            GeneratePropertyMethod(sb, prop);
        }
        
        // 生成Dispose方法
        sb.AppendLine("    public void Dispose()");
        sb.AppendLine("    {");
        sb.AppendLine("        // 清理资源");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        File.WriteAllText(outputPath, sb.ToString());
    }
    
    private static void GenerateCommandMethod(StringBuilder sb, CommandMetadata cmd)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// {cmd.DisplayName}");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine($"    /// {cmd.Description}");
        if (cmd.Observable)
        {
            sb.AppendLine("    /// <para>这是一个可观察命令，执行需要一定时间。</para>");
        }
        sb.AppendLine("    /// </remarks>");
        
        // 生成参数文档
        foreach (var param in cmd.Parameters)
        {
            sb.AppendLine($"    /// <param name=\"{ToCamelCase(param.Identifier)}\">");
            sb.AppendLine($"    /// {param.Description}");
            if (param.Constraints != null)
            {
                sb.AppendLine($"    /// 约束: {param.Constraints}");
            }
            sb.AppendLine("    /// </param>");
        }
        
        sb.AppendLine("    /// <returns>命令执行结果</returns>");
        
        // 生成方法签名
        var paramList = string.Join(", ", 
            cmd.Parameters.Select(p => $"{GetCSharpType(p.DataType)} {ToCamelCase(p.Identifier)}"));
        
        sb.AppendLine($"    public async Task<{cmd.Identifier}_Responses> {cmd.Identifier}Async({paramList})");
        sb.AppendLine("    {");
        sb.AppendLine($"        _logger.LogInformation(\"执行命令: {cmd.DisplayName}\");");
        
        // 生成方法体
        if (cmd.Observable)
        {
            sb.AppendLine("        // 可观察命令执行逻辑");
            sb.AppendLine("        // 1. 启动命令");
            sb.AppendLine("        // 2. 订阅执行信息");
            sb.AppendLine("        // 3. 等待完成");
            sb.AppendLine("        // 4. 获取结果");
        }
        else
        {
            sb.AppendLine("        var request = new {cmd.Identifier}_Parameters();");
            sb.AppendLine("        // 设置参数...");
            sb.AppendLine("        return await _client.{cmd.Identifier}Async(request);");
        }
        
        sb.AppendLine("    }");
        sb.AppendLine();
    }
    
    private static string GetCSharpType(string silaType)
    {
        // 类型映射逻辑
        return silaType switch
        {
            "String" => "string",
            "Integer" => "long",
            "Real" => "double",
            "Boolean" => "bool",
            _ => silaType
        };
    }
    
    private static string ToCamelCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;
        return char.ToLower(pascalCase[0]) + pascalCase.Substring(1);
    }
}
```

#### 12.10.4 推荐实现路径

根据项目需求选择合适的方案：

**短期方案**（立即可用）：
- 使用当前 XSLT 模板（已保留 Description）
- protoc 生成的 C# 代码已有基本注释
- 在包装类中手动添加 DisplayName 和约束说明
- 适用于：小型项目、快速原型

**中期方案**（适度投入）：
- 使用方案 B：运行时从 XML 提取元数据
- 用于 UI 显示和运行时验证
- 不修改转换工具链
- 适用于：动态客户端、需要元数据展示的应用

**长期方案**（完整实现）：
- 实施方案 A + 方案 C 组合
- 增强 XSLT 模板保留所有元数据
- 开发代码生成器创建完整包装类
- 集成到 CI/CD 流程
- 适用于：大型项目、企业级应用、需要维护多个特性

#### 12.10.5 约束信息的处理

对于 XML 中的约束信息（如单位、范围、长度等），建议：

1. **在 Proto 注释中保留**：
   ```protobuf
   // [Unit: °C, Range: -273.15 to 1000]
   /* Temperature value in Celsius */
   Real Temperature = 1;
   ```

2. **在 C# 代码中添加验证**：
   ```csharp
   /// <param name="temperature">
   /// 温度值（单位：°C，范围：-273.15 至 1000）
   /// </param>
   public void SetTemperature(double temperature)
   {
       if (temperature < -273.15 || temperature > 1000)
       {
           throw new ArgumentOutOfRangeException(nameof(temperature), 
               "温度必须在 -273.15°C 至 1000°C 之间");
       }
       // ...
   }
   ```

3. **提供元数据访问**：
   ```csharp
   public class ParameterConstraints
   {
       public string Unit { get; set; }
       public double? MinValue { get; set; }
       public double? MaxValue { get; set; }
       public int? MaxLength { get; set; }
       // ...
   }
   
   public static ParameterConstraints GetConstraints(string parameterFqi)
   {
       // 从元数据中查询约束信息
   }
   ```

---

## 13. 静态代码生成

### 13.1 Protocol Buffer 文件结构

**文件命名**：
```
<FeatureIdentifier>.proto
```

**包命名**：
```
package sila2.<originator>.<category>.<featureidentifier_lowercase>.v<major>;
```

**示例**：
```protobuf
syntax = "proto3";

package sila2.org.silastandard.core.silaservice.v1;

import "SiLAFramework.proto";

service SiLAService {
    rpc GetImplementedFeatures (GetImplementedFeatures_Parameters) 
        returns (GetImplementedFeatures_Responses) {}
    
    rpc GetFeatureDefinition (GetFeatureDefinition_Parameters) 
        returns (GetFeatureDefinition_Responses) {}
    
    // ...
}

message GetImplementedFeatures_Parameters {}

message GetImplementedFeatures_Responses {
    repeated FeatureIdentifier ImplementedFeatures = 1;
}
```

### 12.2 生成 C# 代码

**使用 protoc 编译器**：
```bash
protoc --csharp_out=./Generated \
       --grpc_out=./Generated \
       --plugin=protoc-gen-grpc=grpc_csharp_plugin \
       --proto_path=./Protos \
       SiLAService.proto SiLAFramework.proto
```

**生成的文件**：
- `SiLAService.cs`：消息定义
- `SiLAServiceGrpc.cs`：客户端和服务器存根

### 12.3 封装静态客户端类

```csharp
// 生成的静态客户端包装类
public class SiLAServiceStaticClient : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly SiLAService.SiLAServiceClient _client;
    private string _accessToken;
    
    // 构造函数
    public SiLAServiceStaticClient(string ipAddress, int port, 
        bool validateCertificate = true)
    {
        _channel = CreateChannel(ipAddress, port, validateCertificate);
        _client = new SiLAService.SiLAServiceClient(_channel);
    }
    
    // 认证方法
    public async Task<bool> AuthenticateAsync(string username, string password)
    {
        // 调用 AuthenticationService.Login
        // 存储 accessToken
        return true;
    }
    
    // 命令方法
    public async Task<List<string>> GetImplementedFeaturesAsync()
    {
        var metadata = CreateMetadata();
        var response = await _client.GetImplementedFeaturesAsync(
            new GetImplementedFeatures_Parameters(), metadata);
        
        return response.ImplementedFeatures
            .Select(f => f.Value)
            .ToList();
    }
    
    // 可观察命令
    public async Task<string> StartObservableCommandAsync(/* params */)
    {
        var metadata = CreateMetadata();
        var confirmation = await _client.SomeObservableCommandAsync(
            new SomeObservableCommand_Parameters(), metadata);
        return confirmation.CommandExecutionUUID.Value;
    }
    
    // 订阅执行信息
    public async IAsyncEnumerable<CommandExecutionInfo> 
        SubscribeCommandInfoAsync(string executionUuid)
    {
        var metadata = CreateMetadata();
        var call = _client.SomeObservableCommand_Info(
            new Subscribe_SomeObservableCommand_Info_Parameters
            {
                CommandExecutionUUID = new CommandExecutionUUID { Value = executionUuid }
            }, metadata);
        
        await foreach (var info in call.ResponseStream.ReadAllAsync())
        {
            yield return new CommandExecutionInfo
            {
                Status = info.CommandStatus.ToString(),
                Progress = info.ProgressInfo.Value,
                RemainingTime = info.EstimatedRemainingTime
            };
        }
    }
    
    // 可观察属性字段和事件
    private double _currentTemperature;
    public double CurrentTemperature => _currentTemperature;
    public event EventHandler<double> TemperatureChanged;
    
    public async Task StartTemperatureSubscriptionAsync()
    {
        var metadata = CreateMetadata();
        var call = _client.Subscribe_Temperature(
            new Subscribe_Temperature_Parameters(), metadata);
        
        _ = Task.Run(async () =>
        {
            await foreach (var update in call.ResponseStream.ReadAllAsync())
            {
                _currentTemperature = update.Temperature.Value;
                TemperatureChanged?.Invoke(this, _currentTemperature);
            }
        });
    }
    
    // 辅助方法
    private Metadata CreateMetadata()
    {
        var metadata = new Metadata();
        if (!string.IsNullOrEmpty(_accessToken))
        {
            // 添加授权令牌
            metadata.Add("sila-...-accesstoken-bin", 
                Encoding.UTF8.GetBytes(Convert.ToBase64String(
                    new String { Value = _accessToken }.ToByteArray())));
        }
        return metadata;
    }
    
    public void Dispose()
    {
        _channel?.Dispose();
    }
}

// 使用示例
public async Task ExampleUsage()
{
    using var client = new SiLAServiceStaticClient("192.168.1.100", 50051);
    
    // 认证
    await client.AuthenticateAsync("admin", "password");
    
    // 调用命令
    var features = await client.GetImplementedFeaturesAsync();
    
    // 订阅属性
    client.TemperatureChanged += (sender, temp) =>
    {
        Console.WriteLine($"温度变化: {temp}°C");
    };
    await client.StartTemperatureSubscriptionAsync();
    
    // 可观察命令
    var uuid = await client.StartObservableCommandAsync();
    await foreach (var info in client.SubscribeCommandInfoAsync(uuid))
    {
        Console.WriteLine($"进度: {info.Progress * 100}%");
    }
}
```

### 12.4 代码生成器设计

```csharp
public class SilaStaticCodeGenerator
{
    // 从特性定义 XML 生成代码
    public string GenerateClientCode(string featureDefinitionXml)
    {
        var feature = ParseFeatureDefinition(featureDefinitionXml);
        var code = new StringBuilder();
        
        // 生成类头
        code.AppendLine($"public class {feature.Identifier}StaticClient : IDisposable");
        code.AppendLine("{");
        
        // 生成字段
        GenerateFields(code, feature);
        
        // 生成构造函数
        GenerateConstructor(code, feature);
        
        // 生成命令方法
        foreach (var command in feature.Commands)
        {
            if (command.Observable)
                GenerateObservableCommandMethods(code, command);
            else
                GenerateUnobservableCommandMethod(code, command);
        }
        
        // 生成属性方法
        foreach (var property in feature.Properties)
        {
            if (property.Observable)
                GenerateObservablePropertyMethods(code, property);
            else
                GenerateUnobservablePropertyMethod(code, property);
        }
        
        // 生成辅助方法
        GenerateHelperMethods(code);
        
        code.AppendLine("}");
        return code.ToString();
    }
    
    private void GenerateObservablePropertyMethods(StringBuilder code, Property property)
    {
        // 字段
        code.AppendLine($"    private {GetCSharpType(property.DataType)} _{ToCamelCase(property.Identifier)};");
        
        // 公共属性
        code.AppendLine($"    public {GetCSharpType(property.DataType)} {property.Identifier}");
        code.AppendLine($"        => _{ToCamelCase(property.Identifier)};");
        
        // 事件
        code.AppendLine($"    public event EventHandler<{GetCSharpType(property.DataType)}> {property.Identifier}Changed;");
        
        // 订阅方法
        code.AppendLine($"    public async Task Start{property.Identifier}SubscriptionAsync()");
        code.AppendLine("    {");
        code.AppendLine($"        var call = _client.Subscribe_{property.Identifier}(");
        code.AppendLine($"            new Subscribe_{property.Identifier}_Parameters(), CreateMetadata());");
        code.AppendLine("        _ = Task.Run(async () =>");
        code.AppendLine("        {");
        code.AppendLine("            await foreach (var update in call.ResponseStream.ReadAllAsync())");
        code.AppendLine("            {");
        code.AppendLine($"                _{ToCamelCase(property.Identifier)} = update.{property.Identifier}.Value;");
        code.AppendLine($"                {property.Identifier}Changed?.Invoke(this, _{ToCamelCase(property.Identifier)});");
        code.AppendLine("            }");
        code.AppendLine("        });");
        code.AppendLine("    }");
    }
}
```

---

## 14. 特征定义解析

### 14.1 特征定义语言

**格式**：XML（符合 FeatureDefinition.xsd）

**位置**：
```
https://gitlab.com/SiLA2/sila_base/-/blob/master/schema/FeatureDefinition.xsd
```

### 13.2 解析特征定义

```csharp
public class FeatureDefinition
{
    public string Identifier { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string SiLA2Version { get; set; }
    public string FeatureVersion { get; set; }
    public string Originator { get; set; }
    public string Category { get; set; }
    public string MaturityLevel { get; set; }
    
    public List<Command> Commands { get; set; }
    public List<Property> Properties { get; set; }
    public List<DataTypeDefinition> DataTypes { get; set; }
    public List<Metadata> Metadata { get; set; }
    public List<DefinedExecutionError> DefinedErrors { get; set; }
}

public class SilaFeatureParser
{
    public FeatureDefinition ParseXml(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = doc.Root.Name.Namespace;
        
        var feature = new FeatureDefinition
        {
            Identifier = doc.Root.Element(ns + "Identifier")?.Value,
            DisplayName = doc.Root.Element(ns + "DisplayName")?.Value,
            Description = doc.Root.Element(ns + "Description")?.Value,
            // ... 解析其他元素
        };
        
        // 解析命令
        var commandsElement = doc.Root.Element(ns + "Command");
        if (commandsElement != null)
        {
            feature.Commands = commandsElement.Elements()
                .Select(ParseCommand)
                .ToList();
        }
        
        // 解析属性
        var propertiesElement = doc.Root.Element(ns + "Property");
        if (propertiesElement != null)
        {
            feature.Properties = propertiesElement.Elements()
                .Select(ParseProperty)
                .ToList();
        }
        
        return feature;
    }
    
    private Command ParseCommand(XElement element)
    {
        var ns = element.Name.Namespace;
        return new Command
        {
            Identifier = element.Element(ns + "Identifier")?.Value,
            DisplayName = element.Element(ns + "DisplayName")?.Value,
            Description = element.Element(ns + "Description")?.Value,
            Observable = element.Element(ns + "Observable")?.Value == "Yes",
            // 解析参数和响应
            Parameters = ParseParameters(element.Element(ns + "Parameter")),
            Responses = ParseResponses(element.Element(ns + "Response")),
            IntermediateResponses = ParseIntermediateResponses(
                element.Element(ns + "IntermediateResponse"))
        };
    }
}
```

### 13.3 获取特征定义

```csharp
public async Task<FeatureDefinition> GetFeatureDefinitionAsync(
    string fullyQualifiedFeatureIdentifier)
{
    var response = await _silaServiceClient.GetFeatureDefinitionAsync(
        new GetFeatureDefinition_Parameters
        {
            QualifiedFeatureIdentifier = new String 
            { 
                Value = fullyQualifiedFeatureIdentifier 
            }
        });
    
    string xml = response.FeatureDefinition.Value;
    return _parser.ParseXml(xml);
}
```

---

## 15. 实现检查清单

### 15.1 必须实现（MUST）

#### 服务器发现
- ✅ 实现 mDNS 响应器监听 `_sila._tcp.local.`
- ✅ 解析 DNS-SD TXT 记录（version, server_name, description）
- ✅ 解析 CA 证书（如果存在 ca0, ca1, ... 字段）

#### 连接
- ✅ 支持客户端发起连接方法
- ✅ 所有连接必须使用 TLS 加密
- ✅ 实现证书验证逻辑

#### 特性
- ✅ 访问 SiLAService 特性
- ✅ 获取服务器实现的特性列表
- ✅ 获取特性定义 XML
- ✅ 解析特性定义

#### 命令和属性
- ✅ 实现不可观察命令调用
- ✅ 实现可观察命令四步流程（启动、订阅信息、获取结果）
- ✅ 实现不可观察属性读取
- ✅ 实现可观察属性订阅

#### 错误处理
- ✅ 解析 SiLAError 消息（从 gRPC ABORTED 状态）
- ✅ 处理所有四种错误类型
- ✅ 优雅处理连接错误

#### 数据类型
- ✅ 映射所有 SiLA 基本类型
- ✅ 支持列表类型
- ✅ 支持结构类型
- ✅ 验证约束（客户端侧）

### 14.2 应该实现（SHOULD）

- ✅ 支持服务器发起连接方法（SiLA 2 v1.1+）
- ✅ 实现二进制传输（上传和下载）
- ✅ 支持 SiLA 客户端元数据
- ✅ 实现缓存管理和更新策略
- ✅ 提供设备上下线通知事件
- ✅ 实现认证和授权支持

### 14.3 可以实现（MAY）

- ✅ 支持可观察命令的中间响应
- ✅ 实现高级缓存策略（TTL 管理、心跳检测）
- ✅ 提供丰富的日志和诊断信息
- ✅ 实现连接池管理
- ✅ 支持多服务器并发连接

### 14.4 兼容性要求

- ✅ 支持 SiLA 2 版本 1.1
- ✅ 向后兼容 SiLA 2 v1.0
- ✅ 相同主版本的特性之间向后兼容

---

## 16. 库架构建议

### 16.1 模块划分

```
SiLA2.Client/
├── Discovery/
│   ├── MdnsDiscoveryService.cs
│   ├── ServerInfoCache.cs
│   └── DiscoveryEventArgs.cs
├── Connection/
│   ├── SilaChannel.cs
│   ├── CertificateValidator.cs
│   └── MetadataBuilder.cs
├── Dynamic/
│   ├── DynamicSilaClient.cs
│   ├── FeatureDefinitionParser.cs
│   └── DynamicInvoker.cs
├── Generated/
│   └── [protoc 生成的代码]
├── CodeGen/
│   ├── StaticClientGenerator.cs
│   ├── ProtoFileGenerator.cs
│   └── Templates/
├── Common/
│   ├── DataTypeConverter.cs
│   ├── ErrorHandler.cs
│   └── FQIBuilder.cs
└── Auth/
    ├── AuthenticationManager.cs
    └── AuthorizationManager.cs
```

### 15.2 核心接口

```csharp
// 服务器发现
public interface ISilaDiscoveryService
{
    void Start();
    void Stop();
    IReadOnlyList<SilaServerInfo> GetServers();
    event EventHandler<ServerDiscoveredEventArgs> ServerDiscovered;
    event EventHandler<ServerLostEventArgs> ServerLost;
}

// 动态客户端
public interface IDynamicSilaClient
{
    Task<List<string>> GetImplementedFeaturesAsync();
    Task<FeatureDefinition> GetFeatureDefinitionAsync(string fqi);
    Task<object> InvokeCommandAsync(string commandFqi, Dictionary<string, object> parameters);
    Task<object> GetPropertyAsync(string propertyFqi);
    IObservable<object> SubscribePropertyAsync(string propertyFqi);
}

// 代码生成
public interface IStaticClientGenerator
{
    string GenerateClientClass(FeatureDefinition feature);
    void GenerateProtoFile(FeatureDefinition feature, string outputPath);
    void CompileProtoFile(string protoPath, string outputPath);
}
```

---

## 17. 日志记录最佳实践

本章介绍如何在 SiLA 2 C# 库中实现结构化的日志记录，使用 Microsoft 官方的日志框架。

### 17.1 日志框架选择

**推荐使用**：`Microsoft.Extensions.Logging`

**优势**：
- 微软官方维护，与 .NET 生态系统深度集成
- 支持依赖注入（DI）模式
- 提供统一的日志抽象接口
- 支持多种日志输出提供程序（Console、File、Azure 等）
- 结构化日志支持，便于查询和分析
- 性能优化，支持日志级别过滤

**必需的 NuGet 包**：
```xml
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="8.0.0" />
```

### 17.2 日志级别定义

按严重程度从低到高：

| 级别 | 数值 | 使用场景 | 示例 |
|------|------|----------|------|
| **Trace** | 0 | 最详细的诊断信息，包含敏感数据 | 记录每个 gRPC 消息的完整内容 |
| **Debug** | 1 | 开发调试信息，生产环境通常禁用 | 记录方法进入/退出、中间变量值 |
| **Information** | 2 | 正常运行时的关键事件 | 服务器发现、连接建立、命令执行 |
| **Warning** | 3 | 异常但可恢复的情况 | 连接超时后重试、证书即将过期 |
| **Error** | 4 | 错误导致功能失败，但程序继续运行 | 命令执行失败、gRPC 调用异常 |
| **Critical** | 5 | 严重错误导致应用程序崩溃 | 无法启动服务、致命配置错误 |

### 17.3 各模块日志记录建议

#### 17.3.1 服务器发现模块

```csharp
public class SilaDiscoveryService
{
    private readonly ILogger<SilaDiscoveryService> _logger;
    
    public SilaDiscoveryService(ILogger<SilaDiscoveryService> logger)
    {
        _logger = logger;
    }
    
    public async Task StartDiscoveryAsync()
    {
        _logger.LogInformation("开始 SiLA 服务器发现");
        
        try
        {
            await foreach (var response in ZeroconfResolver.ResolveAsync("_sila._tcp.local."))
            {
                foreach (var service in response)
                {
                    _logger.LogInformation(
                        "发现 SiLA 服务器 {ServerName} ({ServerUUID}) at {IPAddress}:{Port}",
                        service.ServerName, 
                        service.UUID, 
                        service.IPAddress, 
                        service.Port);
                    
                    _logger.LogDebug(
                        "服务器详细信息: Version={Version}, Description={Description}",
                        service.Version,
                        service.Description);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "服务器发现过程中发生错误");
            throw;
        }
    }
    
    public void OnServerLost(string serverUuid)
    {
        _logger.LogWarning("SiLA 服务器 {ServerUUID} 已离线", serverUuid);
    }
}
```

#### 17.3.2 连接管理模块

```csharp
public class SilaConnectionManager
{
    private readonly ILogger<SilaConnectionManager> _logger;
    
    public async Task<GrpcChannel> ConnectAsync(string address, int port)
    {
        _logger.LogInformation("连接到 SiLA 服务器: {Address}:{Port}", address, port);
        
        var channel = GrpcChannel.ForAddress($"https://{address}:{port}", new GrpcChannelOptions
        {
            HttpHandler = CreateHttpHandler()
        });
        
        try
        {
            // 测试连接
            await TestConnectionAsync(channel);
            _logger.LogInformation("成功连接到 SiLA 服务器: {Address}:{Port}", address, port);
            return channel;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, 
                "连接 SiLA 服务器失败: {Address}:{Port}, StatusCode={StatusCode}",
                address, port, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, 
                "连接 SiLA 服务器时发生致命错误: {Address}:{Port}",
                address, port);
            throw;
        }
    }
}
```

#### 17.3.3 命令执行模块

```csharp
public class CommandExecutor
{
    private readonly ILogger<CommandExecutor> _logger;
    
    public async Task<TResponse> ExecuteCommandAsync<TResponse>(
        string commandName,
        object parameters)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CommandName"] = commandName,
            ["CorrelationId"] = Guid.NewGuid()
        });
        
        _logger.LogInformation("开始执行命令: {CommandName}", commandName);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await ExecuteInternalAsync<TResponse>(commandName, parameters);
            
            stopwatch.Stop();
            _logger.LogInformation(
                "命令执行成功: {CommandName}, 耗时={ElapsedMs}ms",
                commandName,
                stopwatch.ElapsedMilliseconds);
            
            return result;
        }
        catch (CommandExecutionException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "命令执行失败: {CommandName}, ErrorType={ErrorType}, 耗时={ElapsedMs}ms",
                commandName,
                ex.SilaError?.ErrorCase,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex,
                "命令执行超时: {CommandName}, 耗时={ElapsedMs}ms",
                commandName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
    
    public async Task<TResult> ExecuteObservableCommandAsync<TResult>(
        string commandName,
        object parameters,
        IProgress<double> progress = null)
    {
        var executionUuid = await StartCommandAsync(commandName, parameters);
        _logger.LogInformation(
            "可观察命令已启动: {CommandName}, ExecutionUUID={ExecutionUUID}",
            commandName,
            executionUuid);
        
        await foreach (var info in SubscribeExecutionInfoAsync(executionUuid))
        {
            _logger.LogDebug(
                "命令执行进度: {CommandName}, Status={Status}, Progress={Progress:P0}",
                commandName,
                info.CommandStatus,
                info.ProgressInfo?.Value ?? 0);
            
            progress?.Report(info.ProgressInfo?.Value ?? 0);
            
            if (info.CommandStatus == CommandStatus.FinishedSuccessfully)
            {
                _logger.LogInformation(
                    "可观察命令执行成功: {CommandName}, ExecutionUUID={ExecutionUUID}",
                    commandName,
                    executionUuid);
                break;
            }
            else if (info.CommandStatus == CommandStatus.FinishedWithError)
            {
                _logger.LogError(
                    "可观察命令执行失败: {CommandName}, ExecutionUUID={ExecutionUUID}",
                    commandName,
                    executionUuid);
                throw new CommandExecutionException("命令执行失败", null);
            }
        }
        
        return await GetCommandResultAsync<TResult>(executionUuid);
    }
}
```

#### 17.3.4 错误处理与日志

```csharp
public class SilaErrorHandler
{
    private readonly ILogger<SilaErrorHandler> _logger;
    
    public void HandleRpcException(RpcException ex, string context)
    {
        switch (ex.StatusCode)
        {
            case StatusCode.Unavailable:
                _logger.LogWarning(ex, 
                    "服务暂时不可用: {Context}, 建议重试",
                    context);
                break;
                
            case StatusCode.DeadlineExceeded:
                _logger.LogWarning(ex,
                    "请求超时: {Context}",
                    context);
                break;
                
            case StatusCode.Unauthenticated:
                _logger.LogError(ex,
                    "认证失败: {Context}",
                    context);
                break;
                
            case StatusCode.PermissionDenied:
                _logger.LogError(ex,
                    "权限不足: {Context}",
                    context);
                break;
                
            case StatusCode.Aborted:
                // 可能是 SiLA 错误
                var silaError = TryParseSilaError(ex);
                if (silaError != null)
                {
                    LogSilaError(silaError, context);
                }
                else
                {
                    _logger.LogError(ex, "操作被中止: {Context}", context);
                }
                break;
                
            default:
                _logger.LogError(ex,
                    "gRPC 调用失败: {Context}, StatusCode={StatusCode}",
                    context,
                    ex.StatusCode);
                break;
        }
    }
    
    private void LogSilaError(SiLAError error, string context)
    {
        switch (error.ErrorCase)
        {
            case SiLAError.ErrorOneofCase.ValidationError:
                _logger.LogError(
                    "参数验证失败: {Context}, Parameter={Parameter}, Message={Message}",
                    context,
                    error.ValidationError.Parameter,
                    error.ValidationError.Message);
                break;
                
            case SiLAError.ErrorOneofCase.DefinedExecutionError:
                _logger.LogError(
                    "定义的执行错误: {Context}, ErrorId={ErrorIdentifier}, Message={Message}",
                    context,
                    error.DefinedExecutionError.ErrorIdentifier,
                    error.DefinedExecutionError.Message);
                break;
                
            case SiLAError.ErrorOneofCase.UndefinedExecutionError:
                _logger.LogError(
                    "未定义的执行错误: {Context}, Message={Message}",
                    context,
                    error.UndefinedExecutionError.Message);
                break;
                
            case SiLAError.ErrorOneofCase.FrameworkError:
                _logger.LogError(
                    "框架错误: {Context}, ErrorType={ErrorType}, Message={Message}",
                    context,
                    error.FrameworkError.ErrorType,
                    error.FrameworkError.Message);
                break;
        }
    }
}
```

### 17.4 配置日志提供程序

#### 17.4.1 基本配置

```csharp
using Microsoft.Extensions.Logging;

// 创建日志工厂
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()           // 控制台输出
        .AddDebug()             // 调试输出
        .SetMinimumLevel(LogLevel.Information); // 设置最低级别
});

// 创建具体模块的日志器
var discoveryLogger = loggerFactory.CreateLogger<SilaDiscoveryService>();
var connectionLogger = loggerFactory.CreateLogger<SilaConnectionManager>();
```

#### 17.4.2 使用依赖注入配置

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();

// 配置日志
services.AddLogging(builder =>
{
    builder
        .AddConsole(options =>
        {
            options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
        })
        .AddDebug()
        .AddFilter("Microsoft", LogLevel.Warning)  // 过滤微软框架日志
        .AddFilter("System", LogLevel.Warning)
        .AddFilter("SiLA", LogLevel.Debug);        // SiLA 模块使用 Debug 级别
});

// 注册服务
services.AddSingleton<SilaDiscoveryService>();
services.AddSingleton<SilaConnectionManager>();

var serviceProvider = services.BuildServiceProvider();

// 使用服务（日志器自动注入）
var discovery = serviceProvider.GetRequiredService<SilaDiscoveryService>();
```

#### 17.4.3 从配置文件加载

`appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning",
      "SiLA.Discovery": "Debug",
      "SiLA.Connection": "Information",
      "SiLA.Command": "Debug"
    },
    "Console": {
      "IncludeScopes": true,
      "TimestampFormat": "[yyyy-MM-dd HH:mm:ss] "
    }
  }
}
```

加载配置：
```csharp
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

services.AddLogging(builder =>
{
    builder.AddConfiguration(configuration.GetSection("Logging"));
    builder.AddConsole();
    builder.AddDebug();
});
```

### 17.5 结构化日志最佳实践

#### 17.5.1 使用命名占位符

✅ **推荐**：
```csharp
_logger.LogInformation(
    "用户 {UserId} 从设备 {DeviceId} 执行命令 {CommandName}",
    userId, deviceId, commandName);
```

❌ **不推荐**：
```csharp
_logger.LogInformation($"用户 {userId} 从设备 {deviceId} 执行命令 {commandName}");
```

原因：命名占位符支持结构化日志查询和分析。

#### 17.5.2 使用日志范围（Scopes）

```csharp
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["SessionId"] = sessionId,
    ["UserId"] = userId,
    ["DeviceId"] = deviceId
}))
{
    _logger.LogInformation("会话开始");
    
    // 这个范围内的所有日志都会包含 SessionId、UserId、DeviceId
    await ExecuteCommandsAsync();
    
    _logger.LogInformation("会话结束");
}
```

#### 17.5.3 性能考虑

使用日志级别检查避免不必要的字符串构建：

```csharp
if (_logger.IsEnabled(LogLevel.Debug))
{
    var detailedInfo = BuildExpensiveDebugInfo();
    _logger.LogDebug("详细调试信息: {Info}", detailedInfo);
}
```

### 17.6 日志输出示例

配置完成后的典型日志输出：

```
[2025-10-10 14:23:15] info: SiLA.Discovery.SilaDiscoveryService[0]
      开始 SiLA 服务器发现
      
[2025-10-10 14:23:16] info: SiLA.Discovery.SilaDiscoveryService[0]
      发现 SiLA 服务器 MyDevice (25597b36-e9bf-11e8-aeb5-f2801f1b9fd1) at 192.168.1.100:50051
      
[2025-10-10 14:23:16] info: SiLA.Connection.SilaConnectionManager[0]
      连接到 SiLA 服务器: 192.168.1.100:50051
      
[2025-10-10 14:23:17] info: SiLA.Connection.SilaConnectionManager[0]
      成功连接到 SiLA 服务器: 192.168.1.100:50051
      
[2025-10-10 14:23:18] info: SiLA.Command.CommandExecutor[0]
      => CommandName: "StartMeasurement", CorrelationId: "a1b2c3d4-..."
      开始执行命令: StartMeasurement
      
[2025-10-10 14:23:18] info: SiLA.Command.CommandExecutor[0]
      => CommandName: "StartMeasurement", CorrelationId: "a1b2c3d4-..."
      可观察命令已启动: StartMeasurement, ExecutionUUID=exec-12345
      
[2025-10-10 14:23:19] dbug: SiLA.Command.CommandExecutor[0]
      => CommandName: "StartMeasurement", CorrelationId: "a1b2c3d4-..."
      命令执行进度: StartMeasurement, Status=Running, Progress=25%
      
[2025-10-10 14:23:25] info: SiLA.Command.CommandExecutor[0]
      => CommandName: "StartMeasurement", CorrelationId: "a1b2c3d4-..."
      可观察命令执行成功: StartMeasurement, ExecutionUUID=exec-12345
      
[2025-10-10 14:23:25] info: SiLA.Command.CommandExecutor[0]
      => CommandName: "StartMeasurement", CorrelationId: "a1b2c3d4-..."
      命令执行成功: StartMeasurement, 耗时=7234ms
```

---

## 18. 测试策略

### 18.1 单元测试

- Protocol Buffer 序列化/反序列化
- 数据类型转换
- FQI 生成和解析
- 错误处理逻辑
- 日志记录功能

### 18.2 集成测试

- 与 SiLA 参考实现服务器通信
- 服务器发现功能（mDNS/DNS-SD）
- 认证和授权流程
- 二进制传输
- 可观察命令和属性

### 18.3 测试工具

- 使用 SiLA GitLab 提供的参考实现
- 模拟 SiLA 服务器（用于 CI/CD）
- 日志分析工具（验证日志输出正确性）

---

## 19. 参考资源

### 19.1 官方资源

- **GitLab**: https://gitlab.com/SiLA2/sila_base
- **官网**: https://sila-standard.com
- **特性定义**: https://gitlab.com/SiLA2/sila_base/-/tree/master/featuredefinitions

### 19.2 技术规范

- **RFC 6762**: mDNS 规范
- **RFC 6763**: DNS-SD 规范
- **RFC 4122**: UUID 规范
- **RFC 1738**: URL 规范
- **gRPC**: https://grpc.io/
- **Protocol Buffers**: https://developers.google.com/protocol-buffers

### 19.3 C# 库

**核心库**：
- **Grpc.Net.Client**: gRPC 客户端
- **Google.Protobuf**: Protocol Buffers 运行时
- **Grpc.Tools**: 包含 protoc 编译器和 gRPC 插件
- **System.Security.Cryptography.X509Certificates**: 证书处理

**服务发现**：
- **Zeroconf**: mDNS/DNS-SD 实现（**推荐**）
  - 跨平台支持
  - 现代异步 API
  - NuGet: `Zeroconf`

**日志记录**：
- **Microsoft.Extensions.Logging**: 日志框架（**推荐**）
- **Microsoft.Extensions.Logging.Console**: 控制台日志输出
- **Microsoft.Extensions.Logging.Debug**: 调试日志输出
- **Microsoft.Extensions.Logging.Configuration**: 配置支持

### 19.4 转换工具资源

- **SiLA Base 仓库**: https://gitlab.com/SiLA2/sila_base
  - `xslt/` - XSLT 转换模板
  - `protobuf/` - SiLA 框架 Proto 定义
  - `schema/` - XML Schema 验证文件
  - `feature_definitions/` - 官方特性定义示例

- **本文档提供的转换器**（第 12 章）:
  - 支持文件转换和内存转换
  - 支持批量转换
  - 集成 XML Schema 验证
  - 自动查找和调用 protoc 编译器
  - 完整的 .NET 8 项目示例

---

## 附录 A: 常见场景代码示例

### A.1 发现并连接到服务器

```csharp
var discovery = new SilaDiscoveryService();
discovery.ServerDiscovered += async (sender, e) =>
{
    Console.WriteLine($"发现服务器: {e.Server.ServerName}");
    
    using var client = new DynamicSilaClient(e.Server.IPAddress, e.Server.Port);
    
    // 获取特性列表
    var features = await client.GetImplementedFeaturesAsync();
    foreach (var feature in features)
    {
        Console.WriteLine($"  - {feature}");
    }
};

discovery.Start();
```

### A.2 动态调用命令

```csharp
var client = new DynamicSilaClient("192.168.1.100", 50051);

// 获取特性定义
var feature = await client.GetFeatureDefinitionAsync(
    "org.silastandard/core/SiLAService/v1");

// 调用命令
var result = await client.InvokeCommandAsync(
    "org.silastandard/core/SiLAService/v1/Command/GetFeatureDefinition",
    new Dictionary<string, object>
    {
        ["QualifiedFeatureIdentifier"] = "com.vendor/device/Control/v1"
    });
```

### A.3 订阅可观察属性

```csharp
var subscription = client.SubscribePropertyAsync(
    "com.vendor/device/TemperatureProvider/v1/Property/Temperature");

subscription.Subscribe(value =>
{
    var temperature = (double)value;
    Console.WriteLine($"温度: {temperature}°C");
});
```

### A.4 使用转换工具生成客户端代码

```csharp
// 示例：从服务器获取特性定义并动态生成客户端

// 1. 连接到 SiLA 服务器
using var client = new DynamicSilaClient("192.168.1.100", 50051);

// 2. 获取特性列表
var features = await client.GetImplementedFeaturesAsync();

// 3. 对每个特性生成强类型客户端
foreach (var featureFqi in features)
{
    // 获取特性定义 XML
    var featureXml = await client.GetFeatureDefinitionXmlAsync(featureFqi);
    
    // 转换为 Proto
    string protoContent = SiLAConverter.ConvertXmlStringToProto(featureXml);
    
    // 保存 Proto 文件
    var featureName = featureFqi.Split('/').Last().Split('.')[0];
    var protoPath = $"generated/{featureName}.proto";
    File.WriteAllText(protoPath, protoContent);
    
    // 编译为 C#
    SiLAConverter.CompileProtoToCSharp(protoPath, $"generated/{featureName}");
    
    Console.WriteLine($"✓ 已生成 {featureName} 客户端代码");
}
```

### A.5 批量转换特性定义

```csharp
// 批量转换项目中的所有 SiLA 特性定义

string featuresDir = "MyDevice/Features";
string outputDir = "Generated";

// 获取所有 .sila.xml 文件
var xmlFiles = Directory.GetFiles(featuresDir, "*.sila.xml", SearchOption.AllDirectories);

Console.WriteLine($"发现 {xmlFiles.Length} 个特性定义");

int successCount = 0;
foreach (var xmlFile in xmlFiles)
{
    try
    {
        var featureName = Path.GetFileNameWithoutExtension(xmlFile);
        var featureOutput = Path.Combine(outputDir, featureName);
        
        // 完整转换流程
        SiLAConverter.ConvertComplete(xmlFile, featureOutput);
        successCount++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ {Path.GetFileName(xmlFile)} 转换失败: {ex.Message}");
    }
}

Console.WriteLine($"\n批量转换完成: {successCount}/{xmlFiles.Length} 成功");
```

### A.6 集成转换器到构建流程

```xml
<!-- 在 .csproj 中集成自动转换 -->
<Project Sdk="Microsoft.NET.Sdk">
  
  <!-- 预构建事件：转换所有特性定义 -->
  <Target Name="ConvertSiLAFeatures" BeforeTargets="BeforeBuild">
    <Exec Command="dotnet run --project ../SiLAConverter/SiLAConverter.csproj -- convert-all Features/ Generated/" />
  </Target>
  
  <!-- 自动编译生成的 Proto 文件 -->
  <ItemGroup>
    <Protobuf Include="Generated/**/*.proto" 
              GrpcServices="Both" 
              AdditionalImportDirs="protobuf" />
    <Protobuf Include="protobuf/SiLAFramework.proto" GrpcServices="None" />
  </ItemGroup>
  
</Project>
```

---

**文档结束**

此实现指南涵盖了开发 SiLA 2 C# 库所需的所有核心技术信息，包括：
- 服务器发现与连接
- 动态客户端实现
- 特性定义转换工具（XML → Proto → C#）
- 静态代码生成
- 完整的测试和部署策略

建议按照模块逐步实现，优先实现服务器发现和基础连接功能，使用第 12 章提供的转换工具简化开发流程，然后逐步添加动态客户端和高级功能。

