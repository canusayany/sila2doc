# SiLA 2 第(C)部分 - 标准功能索引

发布版本 v1.0 - 2019年9月30日

---

## 摘要

**请注意：** 本文档是所有 SiLA 功能的索引。它不包含规范内容，仅作为索引，链接到具体的功能规范。

本文档最终将被 SiLA 网站上的功能注册、评论和投票平台所取代。

核心功能的技术规范可以在 https://gitlab.com/SiLA2/sila_base 找到。

---

## SiLA 2 规范的结构

SiLA 2 规范是一个多部分规范：

- **第(A)部分 - 概述、概念和核心规范**：包含 SiLA 2 的用户需求规范，描述 SiLA 希望实现的目标。
  - 详细描述了 SiLA 2 的核心，包括功能框架，但不映射到具体实现。包含：
    - 设计目标概述
    - SiLA 2 功能规范
    - SiLA 2 功能设计规则
    - SiLA 2 功能开发和投票流程
    - 错误处理和 SiLA 数据类型
    - 安全性和认证
    - SiLA 服务器发现和 SiLA 功能发现

- **第(B)部分 - 映射规范**：描述如何实现用户需求。映射规范文档描述了特定技术的具体映射和实际实现。

- **第(C)部分 - 标准功能索引（本文档）**：标准功能索引文档是已标准化或正在讨论标准化的功能的索引。

---

## SiLA 标准功能索引

### SiLA 服务（SiLA Service）

每个 SiLA 服务器**必须**实现的功能。它是 SiLA 服务器的入口点，帮助发现服务器实现的功能。

**定义：** SiLAService

---

## 认证与授权

### 认证服务（Authentication Service）

在 SiLA 2 中，认证是由 SiLA 服务器执行的实际确认 SiLA 客户端身份的过程。

此功能基于标识和密码或其他认证方式为 SiLA 客户端提供访问令牌。访问令牌在特定上下文中颁发。也就是说，在进行认证时，SiLA 客户端**必须**指定需要授予访问权限的服务器以及需要访问的功能。如果请求的功能列表为空，则视为通配符请求，即相当于请求访问服务器提供的所有功能。

SiLA 服务器**必须**响应认证失败的错误消息，或响应 SiLA 客户端可用于授权后续请求的访问令牌。

**推荐**：如果使用用户名/密码认证，SiLA 服务器应始终通过实现"AuthenticationService"功能来认证 SiLA 客户端的身份。

**定义：** AuthenticationService

---

### 授权服务（Authorization Service）

在 SiLA 2 中，授权是访问控制功能，由 SiLA 服务器执行，以授予或拒绝 SiLA 客户端对 SiLA 服务器的访问。

此功能为访问令牌指定 SiLA 客户端元数据，该令牌例如由 AuthenticationService 授予。

**推荐**：SiLA 服务器应始终通过实现"AuthorizationService"功能来授权 SiLA 客户端的访问。

**定义：** AuthorizationService

---

### 授权提供者服务（Authorization Provider Service）

此功能为 SiLA 客户端提供一个函数，用于检查给定的访问令牌对于给定的服务器和功能是否有效。没有自己的用户管理或需要集成到现有用户管理中的 SiLA 服务器可以使用此功能来检查提供的访问令牌是否有效。

---

### 授权配置服务（Authorization Configuration Service）

此功能为 SiLA 客户端提供一个函数，用于检查给定 SiLA 服务器使用的授权提供者（如果 SiLA 服务器使用集成到现有用户管理系统中的用户管理）。此外，该功能允许更改 SiLA 服务器使用的授权提供者。此功能可从 SiLA 客户端使用，将新的 SiLA 服务器集成到现有的用户管理基础设施中。

---

### 认证和授权示例场景

#### 1. 本地认证和授权

最简单的认证/授权场景是 SiLA 客户端直接向 SiLA 服务器进行认证。为此，SiLA 服务器必须实现 AuthenticationService 功能。然后客户端使用 Login 命令获取访问令牌。

流程：
1. SiLA 客户端登录到 SiLA 服务器
2. 服务器生成具有给定生命周期的访问令牌
3. 将访问令牌和生命周期返回给客户端
4. 客户端可以发出请求
5. 服务器验证访问令牌并执行命令
6. 客户端从服务器注销

#### 2. 授权提供者

在许多情况下，希望将 SiLA 服务器集成到现有的认证/授权基础设施中。为此，必须有另一个称为 SiLA 授权提供者的服务器，该服务器必须实现 AuthenticationService 和 AuthorizationProvider 功能。

流程：
1. SiLA 客户端首先配置 SiLA 服务器要使用的授权提供者（服务器必须实现 AuthorizationConfiguration 功能）
2. 客户端登录到授权提供者，传递应为哪个服务器创建访问令牌的信息
3. 授权提供者生成访问令牌并响应客户端
4. 客户端可以使用此访问令牌向 SiLA 服务器发出请求
5. SiLA 服务器将请求转发给授权提供者进行验证

#### 3. SiLA 客户端作为授权提供者

SiLA 客户端和 SiLA 授权提供者可以是同一个应用程序。在这种情况下，这两个参与者之间的任何调用都可能合并。

#### 4. 本地认证和授权提供者的组合

**推荐**：SiLA 服务器应同时实现 AuthenticationService 功能和 AuthorizationConfiguration 功能。SiLA 服务器可能附带默认的管理员用户名和密码组合。任何想要访问服务器的 SiLA 客户端可以继续使用 SiLA 服务器提供的用户名/密码进行本地认证，或选择将 SiLA 服务器集成到现有的认证/授权基础设施中。

---

## 核心功能和控制器

### 参数约束提供者（Parameter Constraints Provider）

允许查找给定命令的给定参数的约束（最小值、最大值、字符串的最小长度、最大长度等）；也可以依赖于其他参数或状态。

**推荐**：SiLA 服务器应实现此功能。

**定义：** ParameterConstraintsProvider

---

### 锁控制器（Lock Controller）

此功能允许 SiLA 客户端锁定 SiLA 服务器以供独占使用，防止其他 SiLA 客户端在锁定时使用服务器。

**工作原理：**
- 使用 'LockServer' 命令设置锁标识符来锁定 SiLA 服务器
- 必须在每个（受锁保护的）请求中发送此标识符才能使用服务器功能
- 使用 SiLA 客户端元数据 'LockIdentifier' 发送锁标识符
- 可以指定超时时间，定义在未收到有效锁标识符的情况下自动解锁服务器的时间
- 超时过期或显式解锁后，不再需要发送锁标识符

**定义：** LockController

---

### 模拟控制器（Simulation Controller）

允许实现模拟模式的功能（如 SiLA 1.x 中的模拟模式）。

**定义：** SimulationController

---

### 可观察命令控制器（Observable Command Controller）

允许暂停、恢复或停止当前运行的可观察命令。

**定义：** ObservableCommandController

---

## 其他服务（待完善）

以下服务的详细信息请参考第(C)部分的当前版本：

- **国际化服务**（Internationalization Service）
- **审计跟踪服务**（Audit Trail Service）
- **参数默认值提供者**（Parameter Defaults Provider）
- **服务器详细信息提供者**（Server Detail Provider）
- **初始化控制器**（Initialization Controller）
- **持续时间提供者**（Duration Provider）
- **服务器监控服务 / 告警提供者 / 日志服务**（Server Monitoring Service / Alarm Provider / Logging Service）
- **错误恢复服务**（Error Recovery Service）
- **时间标准提供者 / 时间同步提供者**（Time Normal Provider / Time Sync Provider）
- **心跳提供者 / 保活服务**（Heart Beat Provider / Keep Alive Service）
- **发现服务 / 服务器注册**（Discovery Service / Server Registry）
- **代理服务 / 后期绑定服务**（Broker Service / Late Binding Service）
- **许可证服务**（License Service）

---

## 基于功能定义的架构概念

- **色谱数据系统服务**（Chromatography Data System Services - CDS Services）
- **编排服务**（Orchestration Services）

详细信息请参考第(C)部分的当前版本。

---

**注：** 本文档为索引文档，具体功能的详细技术规范请访问 https://gitlab.com/SiLA2/sila_base

