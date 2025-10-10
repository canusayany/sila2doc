# SiLA 2 特性转换器

将 SiLA 2 特性定义（XML）转换为 Protocol Buffer 和 C# 代码的工具。

## 快速开始

### 1. 环境准备

- .NET 8.0 SDK
- 必要的 NuGet 包（项目已配置，自动安装）

### 2. 编译运行

```bash
cd sila_csharp_converter/csharpExamp
dotnet build
dotnet run
```

运行后会执行 5 个转换示例，输出到 `output/` 目录。

### 3. 使用示例

#### 从文件转换

```csharp
// 完整流程：XML → Proto → C#
SiLAConverter.ConvertComplete("input.sila.xml", "output/");

// 仅转换为 Proto
SiLAConverter.ConvertXmlToProto("input.sila.xml", "output.proto");
```

#### 从内存转换（适用于动态场景）

```csharp
// 单个 XML 字符串转换为 Proto
string xmlContent = File.ReadAllText("feature.sila.xml");
string protoResult = SiLAConverter.ConvertXmlStringToProto(xmlContent);

// 批量转换
var xmlDict = new Dictionary<string, string>
{
    ["Feature1"] = xml1Content,
    ["Feature2"] = xml2Content
};
var results = SiLAConverter.ConvertXmlStringsToProto(xmlDict);

// 完整流程（含 C# 生成）
SiLAConverter.ConvertXmlStringComplete(xmlContent, "FeatureName", "output/");
```

#### 批量转换所有示例

```csharp
var xmlFiles = Directory.GetFiles("examples", "*.sila.xml");
foreach (var xmlFile in xmlFiles)
{
    var name = Path.GetFileNameWithoutExtension(xmlFile);
    SiLAConverter.ConvertComplete(xmlFile, $"output/{name}");
}
```

## API 参考

| 方法 | 说明 |
|------|------|
| `ConvertXmlToProto(xmlPath, protoPath)` | 文件转换：XML → Proto |
| `ConvertXmlStringToProto(xmlString)` | 内存转换：XML 字符串 → Proto 字符串 |
| `ConvertXmlStringsToProto(xmlDict)` | 批量内存转换 |
| `ConvertXmlStringToProtoFile(xmlString, protoPath)` | 内存 XML → Proto 文件 |
| `ConvertXmlStringComplete(xmlString, name, outputDir)` | 内存完整流程 |
| `CompileProtoToCSharp(protoPath, outputDir)` | Proto → C# |
| `ConvertComplete(xmlPath, outputDir)` | 文件完整流程 |
| `ValidateXml(xmlPath)` | XML Schema 验证 |

## 文件结构

```
sila_csharp_converter/
├── csharpExamp/              # C# 转换器项目
│   ├── Program.cs            # 测试示例程序
│   ├── SiLAConverter.cs      # 转换器核心类
│   └── SiLAConverter.csproj  # .NET 8 项目文件
├── examples/                 # 示例 SiLA 特性 XML
│   ├── GreetingProvider-v1_0.sila.xml  # 简单示例
│   ├── LockController-v1_0.sila.xml    # 锁控制器（复杂示例）
│   └── SiLAService-v1_0.sila.xml       # SiLA 核心服务
├── protobuf/                 # SiLA 框架 Proto 定义
│   ├── SiLAFramework.proto   # 核心类型（必需）
│   ├── SiLABinaryTransfer.proto    # 二进制传输
│   └── SiLACloudConnector.proto    # 云连接
├── schema/                   # XML Schema 验证文件
│   ├── FeatureDefinition.xsd # 特性定义主 Schema
│   ├── DataTypes.xsd         # 数据类型定义
│   ├── Constraints.xsd       # 约束定义
│   └── AnyTypeDataType.xsd   # Any 类型定义
├── xslt/                     # XML → Proto 转换器
│   ├── fdl2proto.xsl         # 主转换文件
│   ├── fdl2proto-messages.xsl    # 消息生成
│   ├── fdl2proto-service.xsl     # 服务生成
│   └── fdl-validation.xsl    # XML 验证
├── README.md                 # 本文档（主文档）
└── 使用指南.md               # 详细 API 参考
```

### 核心文件说明

**代码文件**：
- `SiLAConverter.cs` - 转换器核心类，包含所有转换方法
- `Program.cs` - 测试示例程序，演示 5 种转换场景
- `SiLAConverter.csproj` - .NET 8 项目配置文件

**转换文件**：
- `xslt/fdl2proto.xsl` - 主转换文件，协调整个转换过程
- `xslt/fdl2proto-messages.xsl` - 生成 Protocol Buffer 消息定义
- `xslt/fdl2proto-service.xsl` - 生成 gRPC 服务定义
- `xslt/fdl-validation.xsl` - 验证 XML 是否符合 SiLA 规范
- `protobuf/SiLAFramework.proto` - SiLA 框架类型定义（**必需**）

**示例文件**：
- `examples/GreetingProvider-v1_0.sila.xml` - 最简单的示例特性
- `examples/SiLAService-v1_0.sila.xml` - SiLA 核心服务特性
- `examples/LockController-v1_0.sila.xml` - 包含元数据和错误定义的复杂示例

### 最小依赖

如果只需要基本转换功能，必需文件为：
- `xslt/fdl2proto*.xsl`（4个文件）
- `protobuf/SiLAFramework.proto`
- `SiLAConverter.cs`
- `SiLAConverter.csproj`

总大小约 50KB。

## 运行测试

运行 `Program.cs` 将执行 5 个示例：

1. 从文件转换（完整流程）
2. 从内存字符串转换
3. 批量内存转换
4. 内存完整流程（含 C# 生成）
5. 仅转换为 Proto

输出位于 `output/` 目录。

## 环境要求

- .NET 8.0 SDK
- NuGet 包（自动安装）:
  - Grpc.Net.Client 2.59.0
  - Grpc.Tools 2.59.0
  - Google.Protobuf 3.25.1

## 参考资源

- [详细使用指南](使用指南.md)
- [文件说明](文件说明.md)
- [SiLA 标准官网](https://sila-standard.com/)
- [SiLA GitLab](https://gitlab.com/SiLA2/sila_base)

