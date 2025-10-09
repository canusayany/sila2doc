# SiLA 2 第(B)部分 - 映射规范

版本 v1.1 - 2022年3月19日

© 2008-2022 实验室自动化标准化联盟协会(SiLA)

http://www.sila-standard.org/

---

## 声明

版权所有 © SiLA 2008-2022。保留所有权利。

本文档及其包含的信息按"原样"提供,SiLA不提供任何明示或暗示的保证,包括但不限于对本文信息使用不侵犯任何所有权或任何适销性或特定用途适用性的暗示保证。

## 许可证

本文档根据"知识共享署名-相同方式共享(CC BY-SA 4.0)"许可证授权。

---

## 摘要

本文档描述SiLA 2的技术映射规范,说明如何将SiLA 2的概念和需求映射到具体的技术实现。

---

# 简介

## 原则和背景

SiLA 2必须映射到一个能够持续相当长时间的基础技术和通信标准。经过充分考虑,选择了以下技术组合:

- **HTTP/2** - 作为基础通信层
- **类似REST的通信范式**
- **Protocol Buffers(协议缓冲区)** - 作为数据结构
- **接口描述语言** - 如RAML、OpenAPI

这些技术完美结合在**gRPC**中,这是一个具有多种语言绑定的微服务架构框架。

**总结**: SiLA 2基于HTTP/2和Protocol Buffers(由gRPC的线格式指定)。

## gRPC简介

gRPC框架是一个开源的通用远程过程调用(RPC)框架,基于HTTP/2进行传输,基于Protocol Buffers进行接口描述。

Protocol Files的基本元素:

1. **Service(服务)**: 使用Protocol Buffers定义RPC的基本元素,包含一组相关的RPC
2. **RPC**: 服务的功能单元,具有请求消息和响应消息
3. **Message(消息)**: 包含信息的主要数据元素,可以包含简单数据类型或嵌套消息

## REST vs. gRPC

### Protobuf vs. JSON

- **REST**: 通常使用JSON作为有效负载格式(文本格式,可压缩但效率较低)
- **gRPC**: 使用Protocol Buffers消息(二进制格式,高效且紧凑)

### HTTP/2 vs. HTTP 1.1

**HTTP 1.1的问题**:
- 过于庞大和复杂(176页规范)
- 对延迟敏感,每个请求需要TCP握手
- 队头阻塞问题,连接数限制(6-8个)

**HTTP/2的改进**:
- 使用多路复用流,单个TCP连接支持多个双向流
- 流可以交错,无需排队
- 支持服务器推送通知

### 消息 vs. 资源和动词

- **REST**: 建立在HTTP之上,使用资源和动词的概念
- **gRPC**: 使用服务、接口和结构化消息的模型,直接对应编程语言概念

### 流式传输

gRPC支持三种流式传输类型:

1. **服务器端流式传输**: 服务器发回响应流
2. **客户端流式传输**: 客户端发送请求流,服务器返回单个响应
3. **双向流式传输**: 客户端和服务器相互发送消息流

### 强类型 vs. 序列化

- **REST/JSON**: 需要序列化和反序列化,可能引入错误
- **gRPC**: 强类型消息自动转换,类型安全

---

# === 规范部分开始 ===

## SiLA 2规范的结构

SiLA 2规范是多部分规范:

- **第(A)部分**: 概述、概念和核心规范(用户需求)
- **第(B)部分**: 映射规范(本文档,描述技术实现)
- **第(C)部分**: 标准特征索引

## 术语和一致性语言

关键词定义(按RFC2119):
- **MUST/MUST NOT**: 强制性要求
- **SHOULD/SHOULD NOT**: 推荐性要求
- **MAY**: 可选要求

## 架构

**核心架构**:
- SiLA 2运行在**HTTP/2**之上
- 使用**Protocol Buffers**序列化有效负载数据
- 依赖**gRPC线格式**实现连接和数据传输

**实现方式**:
- 可以使用gRPC库实现SiLA客户端和服务器
- 也可以从头实现,只要严格遵守gRPC线格式规范

## 客户端-服务器通信

SiLA基于客户端-服务器架构:
- **SiLA服务器**: 提供特征(Features)的系统
- **SiLA客户端**: 使用特征的系统
- **特征(Feature)**: SiLA服务器提供的服务

### 两种连接方法

**1. 客户端发起的连接方法**:
- SiLA客户端连接到SiLA服务器的特定套接字(IP地址和端口)
- 所有服务在该套接字上公开

**2. 服务器发起的连接方法**:
- SiLA服务器建立到SiLA客户端的连接
- 所有服务通过双向gRPC流提供
- 也称为"云连接"或"反向连接"

### 连接

**连接特性**:
- 可以由客户端或服务器关闭
- 双方必须完全容忍连接随时关闭和断开
- **必须始终通过加密(TLS)保护**

#### 客户端发起的连接方法

通过SiLA客户端调用SiLA服务器上的gRPC服务建立连接。

#### 服务器发起的连接方法

通过在SiLA客户端上调用`ConnectSiLAServer` gRPC服务建立连接:

```protobuf
service CloudClientEndpoint { 
    rpc ConnectSiLAServer (stream SILAServerMessage) 
        returns (stream SILAClientMessage) {} 
}
```

此服务打开双向读写流,双方发送SiLA客户端消息和SiLA服务器消息序列。

## SiLA服务器消息

**定义**: 通过双向流从SiLA服务器发送到SiLA客户端的所有gRPC消息。

**消息结构**:
- `requestUUID`: 唯一ID,用于将响应映射到请求
- `<oneof message>`: 具体的消息类型

**主要消息类型**:
- `CommandResponse`: 不可观察命令的结果
- `ObservableCommandConfirmation`: 可观察命令启动确认
- `ObservableCommandExecutionInfo`: 命令执行信息
- `ObservableCommandIntermediateResponse`: 中间响应
- `ObservableCommandResponse`: 最终响应
- `PropertyValue`: 属性值
- `ObservablePropertyValue`: 可观察属性值
- `BinaryTransferError`: 二进制传输错误
- `SiLAError`: SiLA错误

## SiLA客户端消息

**定义**: 通过双向流从SiLA客户端发送到SiLA服务器的所有gRPC消息。

**消息结构**:
- `requestUUID`: 唯一ID(由客户端创建)
- `<oneof message>`: 具体的消息类型

**主要消息类型**:
- `CommandExecution`: 不可观察命令执行
- `CommandInitiation`: 可观察命令启动
- `CommandExecutionInfoSubscription`: 订阅执行信息
- `CommandIntermediateResponseSubscription`: 订阅中间响应
- `CommandGetResponse`: 获取命令结果
- `PropertyRead`: 读取属性
- `PropertySubscription`: 订阅属性
- 二进制传输相关消息

## 订阅机制

**特点**:
- 点对点订阅,无需代理
- 使用服务器端流式RPC实现
- 只能由SiLA客户端取消
- 服务器尽最大努力发送数据变化
- 初始订阅时发送当前值

## 高级映射概述

### 客户端发起连接方法的映射

| SiLA概念 | gRPC映射 |
|---------|---------|
| 特征 | gRPC服务 |
| 命令 | 单个或多个RPC(取决于是否可观察) |
| 属性 | 单个RPC(不可观察)或流式RPC(可观察) |
| 数据类型 | Protocol Buffer消息 |
| 错误 | SiLA错误Protocol Buffer消息 |
| 元数据 | Protocol Buffer消息和RPC |

### 服务器发起连接方法的映射

| SiLA概念 | gRPC映射 |
|---------|---------|
| 特征 | 无直接表示,通过完全限定标识符访问 |
| 命令 | 单个或多个SiLA客户端消息 |
| 属性 | 单个或多个SiLA客户端消息 |
| 错误 | 通过SiLA服务器消息发送 |

## 命名规则

**协议缓冲区定义**:
- 使用proto3语法
- 定义以`protobuf`作为块代码标题

**术语替换**:
- `<命令标识符>` → 实际命令名称
- 例如: `<命令标识符>_Parameters` → `MyCommand_Parameters`

**多值处理**:
- 使用整数索引(从1开始): `<参数1>`, `<参数2>`, ..., `<参数N>`

## 特征实现

### Proto文件

**语法声明**:
```protobuf
syntax = "proto3";
```

**框架定义**:
- 建议导入SiLA在GitLab提供的协议缓冲区文件
- 使用proto_path解析依赖关系

### 特征标识符

**映射规则**:
- 特征 → gRPC服务
- 特征标识符 → 服务名称和文件名
- 文件命名: `{特征标识符}.proto`

**示例**:
```protobuf
service LockController { ... }
```

### Protobuf包

**包名称组成**:
```
<SiLA代>.<发起者>.<类别>.<特征标识符>.<版本>
```

**规则**:
- SiLA代: 固定为"sila2"
- 发起者: 原样使用
- 类别: 原样使用
- 特征标识符: 转换为全小写
- 版本: "v" + 主版本号

**示例**:
```protobuf
package sila2.org.silastandard.core.silaservice.v1;
```

## 命令

### 命令类型

1. **不可观察命令**: 不需要观察执行进度
2. **可观察命令**: 可以观察执行进度和状态

### 参数和响应

**命令参数映射**(客户端发起):
```protobuf
message <命令标识符>_Parameters { 
    <参数类型1> <参数标识符1> = 1; 
    <参数类型2> <参数标识符2> = 2; 
    ...
}
```

**命令响应映射**(客户端发起):
```protobuf
message <命令标识符>_Responses { 
    <响应类型1> <响应标识符1> = 1; 
    <响应类型2> <响应标识符2> = 2; 
    ...
}
```

### 不可观察命令

**客户端发起连接**:
```protobuf
rpc <命令标识符> (<命令标识符>_Parameters) 
    returns (<命令标识符>_Responses) {}
```

**服务器发起连接**:
- 发送: `CommandExecution`消息(包含完全限定命令ID和参数)
- 接收: `CommandResponse`消息(包含结果字节)

**示例**:
```protobuf
service RobotMoveController {
    rpc MoveSample (MoveSample_Parameters) 
        returns (MoveSample_Responses) {}
}
message MoveSample_Parameters {
    Integer PlateSiteA = 1;
    Integer PlateSiteB = 2;
}
message MoveSample_Responses {
    Real Precision = 1;
}
```

### 可观察命令

**需要的RPC**(客户端发起):
1. **命令启动**: `rpc <命令> (...) returns (CommandConfirmation) {}`
2. **执行信息**: `rpc <命令>_Info (...) returns (stream ExecutionInfo) {}`
3. **中间响应**(可选): `rpc <命令>_Intermediate (...) returns (stream ...) {}`
4. **结果获取**: `rpc <命令>_Result (...) returns (...) {}`

**执行流程**:
1. 客户端调用命令RPC,获得`CommandExecutionUUID`
2. 使用UUID订阅执行信息和中间响应
3. 命令完成后使用UUID获取最终结果

**命令执行状态**:
- `waiting`: 等待执行
- `running`: 正在执行
- `finishedSuccessfully`: 成功完成
- `finishedWithError`: 执行出错

**框架定义**:
```protobuf
message CommandExecutionUUID { 
    string value = 1; 
}

message CommandConfirmation { 
    CommandExecutionUUID commandExecutionUUID = 1; 
    Duration lifetimeOfExecution = 2;  // 可选
}

message ExecutionInfo { 
    enum CommandStatus { 
        waiting = 0; 
        running = 1; 
        finishedSuccessfully = 2; 
        finishedWithError = 3; 
    } 
    CommandStatus commandStatus = 1; 
    Real progressInfo = 2;  // 0.0-1.0
    Duration estimatedRemainingTime = 3; 
    Duration updatedLifetimeOfExecution = 4;
}
```

## 属性

### 不可观察属性

**客户端发起连接**:
```protobuf
rpc Get_<属性标识符> (Get_<属性标识符>_Parameters) 
    returns (Get_<属性标识符>_Responses) {}

message Get_<属性标识符>_Parameters {}

message Get_<属性标识符>_Responses { 
    <属性数据类型> <属性标识符> = 1; 
}
```

**服务器发起连接**:
- 发送: `PropertyRead`消息
- 接收: `PropertyValue`消息

**示例**:
```protobuf
service DeviceProvider {
    rpc Get_DeviceName (Get_DeviceName_Parameters) 
        returns (Get_DeviceName_Responses) {}
}
message Get_DeviceName_Parameters {}
message Get_DeviceName_Responses {
    String DeviceName = 1;
}
```

### 可观察属性

**客户端发起连接**:
```protobuf
rpc Subscribe_<属性标识符> (Subscribe_<属性标识符>_Parameters) 
    returns (stream Subscribe_<属性标识符>_Responses) {}

message Subscribe_<属性标识符>_Parameters {}

message Subscribe_<属性标识符>_Responses {
    <属性数据类型> <属性标识符> = 1;
}
```

**特点**:
- 使用服务器端流
- 客户端可以只读取一次后取消流
- 客户端通过取消gRPC流来关闭订阅

**服务器发起连接**:
- 订阅: 发送`PropertySubscription`消息
- 接收: 一个或多个`PropertyValue`消息
- 取消: 发送`CancelPropertySubscription`消息

## SiLA客户端元数据

**定义**: SiLA服务器期望从客户端接收的信息,用于执行命令或访问属性。

**客户端发起连接**:
- 作为gRPC自定义元数据发送(二进制头格式)
- 键名格式: `sila-<完全限定元数据ID>-bin`
- 值: Base64编码的序列化Protocol Buffer消息
- 提供RPC返回受影响的特征/命令/属性列表

**服务器发起连接**:
- 作为消息字段包含在相关请求中
- 使用`Metadata`消息结构

## SiLA数据类型映射

### 基本类型

| SiLA类型 | Protocol Buffer映射 |
|---------|-------------------|
| String | `message String { string value = 1; }` |
| Integer | `message Integer { int64 value = 1; }` |
| Real | `message Real { double value = 1; }` |
| Boolean | `message Boolean { bool value = 1; }` |
| Binary | `message Binary { oneof union { bytes value = 1; string binaryTransferUUID = 2; } }` |
| Date | `message Date { uint32 day/month/year = 1/2/3; Timezone timezone = 4; }` |
| Time | `message Time { uint32 second/minute/hour = 1/2/3; Timezone timezone = 4; }` |
| Timestamp | `message Timestamp { uint32 second/minute/hour/day/month/year; Timezone timezone = 7; }` |

### 派生类型

**1. 列表类型**:
```protobuf
repeated <数据类型> <标识符> = N;
```

**2. 结构类型**:
```protobuf
message <标识符>_Struct {
    <元素类型1> <元素标识符1> = 1;
    <元素类型2> <元素标识符2> = 2;
    ...
}
<标识符>_Struct <标识符> = N;
```

**3. 约束类型**:
- 在Protocol Buffer级别无映射
- 服务器必须验证约束并在违反时抛出验证错误

**示例 - 列表类型**:
```protobuf
message MoveTrajectory_Parameters {
    message Points_Struct {
        Real X = 1;
        Real Y = 2;
    }
    repeated Points_Struct Points = 1;
}
```

### Any类型

**用途**: 发送设计时未知的任意SiLA数据类型。

**结构**:
```protobuf
message Any { 
    string type = 1;      // XML描述的数据类型
    bytes payload = 2;    // 序列化的数据
}
```

### Void类型

通过长度约束为0的String类型实现:
```xml
<DataType>
    <Constrained>
        <DataType><Basic>String</Basic></DataType>
        <Constraints><Length>0</Length></Constraints>
    </Constrained>
</DataType>
```

### 自定义数据类型

```protobuf
message DataType_<自定义类型标识符> {
    <数据类型> <自定义类型标识符> = 1;
}
```

## 二进制传输

**定义**: 传输大于2 MiB的二进制数据的机制。

**二进制块**: 数据被分割成多个块,每块不超过2 MiB。

**二进制传输UUID**: 引用特定二进制传输的UUID。

**生命周期**: 二进制传输UUID有效的持续时间。

### 二进制上传(客户端发起连接)

**三步流程**:
1. **创建存储**: `CreateBinary` - 分配空间,获得UUID
2. **上传块**: `UploadChunk` - 上传各个二进制块
3. **使用数据**: 在命令参数中使用UUID

**可选**: 使用后通过`DeleteBinary`删除数据

**Protocol Buffer定义**:
```protobuf
service BinaryUpload {
    rpc CreateBinary (CreateBinaryRequest) 
        returns (CreateBinaryResponse) {}
    rpc UploadChunk (stream UploadChunkRequest) 
        returns (stream UploadChunkResponse) {}
    rpc DeleteBinary (DeleteBinaryRequest) 
        returns (DeleteBinaryResponse) {}
}
```

### 二进制下载(客户端发起连接)

**两步流程**:
1. **检查信息**: `GetBinaryInfo` - 获取大小和生命周期
2. **下载数据**: `GetChunk` - 按偏移量和长度下载块

**Protocol Buffer定义**:
```protobuf
service BinaryDownload {
    rpc GetBinaryInfo (GetBinaryInfoRequest) 
        returns (GetBinaryInfoResponse) {}
    rpc GetChunk (stream GetChunkRequest) 
        returns (stream GetChunkResponse) {}
    rpc DeleteBinary (DeleteBinaryRequest) 
        returns (DeleteBinaryResponse) {}
}
```

### 二进制传输错误

**错误类型**:
- `INVALID_BINARY_TRANSFER_UUID`: 无效的UUID
- `BINARY_UPLOAD_FAILED`: 上传失败
- `BINARY_DOWNLOAD_FAILED`: 下载失败

## 错误处理

### gRPC错误机制

**错误分类**:
1. **一般错误**
2. **网络故障**
3. **协议错误**

以上都被归类为SiLA连接错误。

### SiLA错误机制

**客户端发起连接**:
- 使用gRPC状态码`ABORTED`
- 错误信息序列化为SiLA错误Protocol Buffer消息
- Base64编码到gRPC状态消息中

**SiLA错误消息结构**:
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

**服务器发起连接**:
- 直接在SiLA服务器消息中发送错误

### 错误类型

**1. 验证错误**:
- 参数验证失败时发生
- 在命令执行之前

**2. 执行错误**:
- **已定义执行错误**: 在特征定义中预定义的错误
- **未定义执行错误**: 其他所有执行错误

**3. 框架错误**:
- 违反SiLA 2规范时发生
- 包含错误信息和解决建议

**4. 连接错误**:
- 由底层基础设施发出
- 通常表现为超时(DEADLINE_EXCEEDED)

## 加密

**强制要求**:
- SiLA客户端和服务器**必须始终使用TLS加密**
- 推荐服务器使用受信任的证书
- 客户端应只静默接受受信任的证书
- 客户端不得静默接受不受信任的证书

**不受信任证书的要求**:
- 通用名称(CN)设置为"SiLA2"
- 推荐包含OID "1.3.6.1.4.1.58583"扩展,包含SiLA服务器UUID

**私有IP地址的例外**:
- IPv4: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16
- IPv6: 唯一本地地址(ULA)

## SiLA服务器发现

**目的**: 在IP网络上自动发现SiLA服务器,无需配置IP地址或DNS服务器。

**实现技术**:
- **mDNS**(多播DNS) - RFC6762
- **DNS-SD**(基于DNS的服务发现) - RFC6763

**服务命名**:
```
<SiLA服务器UUID>._sila._tcp.local.
```

**示例**:
```
25597b36-e9bf-11e8-aeb5-f2801f1b9fd1._sila._tcp.local.
```

**服务器属性**(通过DNS-SD TXT记录):
```
version=<SiLA 2版本>
server_name=<SiLA服务器名称>
description=<SiLA服务器描述>
```

**不受信任证书的额外要求**:
- 必须在TXT记录中发送PEM编码的证书颁发机构
- 键名格式: `ca<行号>=<证书行内容>`

**要求**:
- 服务器必须实现mDNS和DNS-SD
- 默认启用发现功能,不得禁用
- 地址必须表示HTTP服务器的套接字

---

# === 规范部分结束 ===

## 未来考虑

### 可观察属性过滤器

可用于减少从SiLA服务器发送到SiLA客户端的数据量,例如:
- 死区订阅: 只在值变化超过阈值时发送
- 时间过滤: 按特定时间间隔发送

### SiLA客户端标识符

未来版本可能会包含SiLA客户端UUID的概念,用于:
- 标识客户端
- 跟踪客户端会话
- 实现访问控制

### 设备发现标准化

可能的改进:
- 链路本地地址处理
- 混合网络互操作性
- 跨子网发现

### 内置Web服务器

为SiLA服务器添加内置Web服务器功能,用于:
- 监控和管理
- 提供用户界面
- 简化配置

---

## 总结

SiLA 2第(B)部分定义了如何将SiLA 2的抽象概念映射到具体的技术实现:

**核心技术栈**:
- HTTP/2作为传输层
- Protocol Buffers作为数据序列化格式
- gRPC作为RPC框架

**两种连接方法**:
- 客户端发起: 传统的客户端连接服务器模式
- 服务器发起: 服务器连接客户端(云连接)

**主要映射**:
- 特征 → gRPC服务
- 命令 → RPC(单个或多个)
- 属性 → RPC(单个或流式)
- 数据类型 → Protocol Buffer消息
- 错误 → 特定的错误消息结构

**关键特性**:
- 支持可观察和不可观察的命令/属性
- 提供完整的错误处理机制
- 支持二进制大文件传输
- 强制TLS加密
- 自动服务器发现(mDNS/DNS-SD)

本规范确保了SiLA 2实现的互操作性和标准化,为实验室自动化提供了坚实的技术基础。

---

**文档版本**: v1.1  
**发布日期**: 2022年3月19日  
**状态**: 规范性文档
