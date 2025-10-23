## 项目介绍
本项目是根据Sila2特性文件生成的D3驱动最终项目，编译后的dll和注释文件即为最终产物。

## 文件说明
- **客户端代码**: `ITemperatureController.cs`、`TemperatureControllerClient.cs`、`TemperatureControllerDtos.cs` 由Tecan生成器从特性文件生成
- **D3驱动**: `D3Driver.cs`、`AllSila2Client.cs` 等由D3驱动生成器自动生成
- **通信参数**: `CommunicationPars.cs` 配置服务器连接信息（IP、端口）
- **依赖库**: `lib/` 文件夹包含项目依赖

## 生成方式
使用 `SilaGeneratorWpf` 工具，可从在线服务器或本地特性文件生成D3驱动项目，支持多特性聚合、方法分类调整等功能。