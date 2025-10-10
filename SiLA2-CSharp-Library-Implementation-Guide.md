# SiLA 2 C# 库实现指南

版本 1.0 | 基于 SiLA 2 标准 v1.1

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

```csharp
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

// 发现管理器
public class SilaDiscoveryManager
{
    // 开始监听 mDNS 服务
    public void StartDiscovery();
    
    // 停止监听
    public void StopDiscovery();
    
    // 获取所有已发现的服务器
    public IEnumerable<SilaServerInfo> GetDiscoveredServers();
    
    // 事件：服务器上线
    public event EventHandler<ServerDiscoveredEventArgs> ServerDiscovered;
    
    // 事件：服务器下线
    public event EventHandler<ServerLostEventArgs> ServerLost;
}
```

**缓存策略**：
- 维护服务器列表，包含最后见到时间
- 监听 mDNS goodbye 消息（TTL=0）
- 超时检测：如果 TTL 过期未更新，标记为下线

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

## 12. 静态代码生成

### 12.1 Protocol Buffer 文件结构

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

## 13. 特征定义解析

### 13.1 特征定义语言

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

## 14. 实现检查清单

### 14.1 必须实现（MUST）

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

## 15. 库架构建议

### 15.1 模块划分

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

## 16. 测试策略

### 16.1 单元测试

- Protocol Buffer 序列化/反序列化
- 数据类型转换
- FQI 生成和解析
- 错误处理逻辑

### 16.2 集成测试

- 与 SiLA 参考实现服务器通信
- 服务器发现功能
- 认证和授权流程
- 二进制传输

### 16.3 测试工具

- 使用 SiLA GitLab 提供的参考实现
- 模拟 SiLA 服务器（用于 CI/CD）

---

## 17. 参考资源

### 17.1 官方资源

- **GitLab**: https://gitlab.com/SiLA2/sila_base
- **官网**: https://sila-standard.com
- **特性定义**: https://gitlab.com/SiLA2/sila_base/-/tree/master/featuredefinitions

### 17.2 技术规范

- **RFC 6762**: mDNS 规范
- **RFC 6763**: DNS-SD 规范
- **RFC 4122**: UUID 规范
- **RFC 1738**: URL 规范
- **gRPC**: https://grpc.io/
- **Protocol Buffers**: https://developers.google.com/protocol-buffers

### 17.3 C# 库

- **Grpc.Net.Client**: gRPC 客户端
- **Google.Protobuf**: Protocol Buffers 运行时
- **Makaretu.Dns**: mDNS/DNS-SD 实现（推荐）
- **System.Security.Cryptography.X509Certificates**: 证书处理

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

---

**文档结束**

此实现指南涵盖了开发 SiLA 2 C# 库所需的所有核心技术信息。建议按照模块逐步实现，优先实现服务器发现和基础连接功能，然后逐步添加动态客户端和代码生成功能。

