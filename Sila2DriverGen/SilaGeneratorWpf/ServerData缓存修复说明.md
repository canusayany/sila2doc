# ServerData 缓存修复说明

## 问题描述

实时监控功能引入后出现两个问题：

1. **第三个tab（D3驱动生成）** - 生成项目时报"无法获取特性数据"
2. **第二个tab（服务器发现）** - 点击特性后无法测试通信

## 问题根因

实时监控服务 `Sila2RealTimeDiscoveryService` 创建的 `ServerInfoViewModel` 中没有正确缓存 `ServerData`，导致后续操作无法获取特性详细信息。

原来的架构：
```
ServerDiscoveryService._serverDataCache (字典缓存)
  ↓
GetServerData(Guid uuid) 方法获取
```

实时监控后的问题：
```
Sila2RealTimeDiscoveryService 创建 ServerInfoViewModel
  ↓
ServerData 未被缓存
  ↓
后续无法通过 Guid 查找到 ServerData
```

## 解决方案

### 1. 在 ServerInfoViewModel 中添加 ServerData 缓存

**文件**: `SilaGeneratorWpf/Models/ServerInfoViewModel.cs`

```csharp
/// <summary>
/// ServerData 缓存（用于后续获取特性等操作）
/// </summary>
public Tecan.Sila2.ServerData? ServerDataCache { get; set; }
```

### 2. 修改所有创建 ServerInfoViewModel 的位置

#### ServerDiscoveryService.ConvertToViewModel

```csharp
var serverInfo = new ServerInfoViewModel
{
    // ... 其他属性
    ServerDataCache = serverData  // 缓存 ServerData
};
```

#### Sila2RealTimeDiscoveryService.ConvertToViewModel

```csharp
if (serverData != null)
{
    // 缓存 ServerData 到 ViewModel
    serverInfo.ServerDataCache = serverData;
    LoadFeatures(serverInfo, serverData);
}
```

### 3. 修改 ServerData 获取逻辑

#### ServerDiscoveryService

**修改前**:
```csharp
public Dictionary<string, Dictionary<string, Feature>> GetSelectedFeaturesGroupedByServer(...)
{
    // 只从 _serverDataCache 字典获取
    if (_serverDataCache.TryGetValue(server.Uuid, out var serverData))
    {
        // ...
    }
}
```

**修改后**:
```csharp
public Dictionary<string, Dictionary<string, Feature>> GetSelectedFeaturesGroupedByServer(...)
{
    // 优先从 ViewModel 中获取 ServerData 缓存
    var serverData = server.ServerDataCache;
    
    // 如果 ViewModel 没有缓存，尝试从服务缓存中获取
    if (serverData == null)
    {
        _serverDataCache.TryGetValue(server.Uuid, out serverData);
    }
    
    if (serverData != null)
    {
        // ...
    }
}
```

**添加新方法**:
```csharp
/// <summary>
/// 根据 ServerInfoViewModel 获取 ServerData
/// </summary>
public ServerData? GetServerData(ServerInfoViewModel server)
{
    // 优先返回 ViewModel 中的缓存
    if (server.ServerDataCache != null)
    {
        return server.ServerDataCache;
    }
    
    // 回退到服务缓存
    return _serverDataCache.TryGetValue(server.Uuid, out var serverData) ? serverData : null;
}
```

#### LoadServerFeaturesAsync

```csharp
public Task<bool> LoadServerFeaturesAsync(ServerInfoViewModel server)
{
    return Task.Run(() =>
    {
        try
        {
            // 优先使用 ViewModel 中的缓存
            var serverData = server.ServerDataCache;
            
            // 如果 ViewModel 没有缓存，尝试从服务缓存中获取
            if (serverData == null && !_serverDataCache.TryGetValue(server.Uuid, out serverData))
            {
                _logger.LogWarning($"未找到服务器 {server.ServerName} 的 ServerData 缓存");
                return false;
            }
            
            if (serverData != null)
            {
                LoadFeatures(server, serverData);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"加载服务器特性失败: {server.ServerName}");
            return false;
        }
    });
}
```

### 4. 修改 UI 层代码

**文件**: `SilaGeneratorWpf/Views/ServerDiscoveryView.xaml.cs`

**修改前**:
```csharp
var serverData = viewModel.GetServerData(featureViewModel.ParentServer!.Uuid);
if (serverData == null) return;
```

**修改后**:
```csharp
// 直接从 ServerInfoViewModel 获取 ServerData 缓存
var serverData = featureViewModel.ParentServer?.ServerDataCache;
if (serverData == null)
{
    DetailPanel.Children.Add(CreateInfoRow("错误", "无法获取服务器数据，请刷新服务器"));
    return;
}
```

### 5. 标记旧方法为 Obsolete

```csharp
/// <summary>
/// 根据 UUID 获取 ServerData（已弃用，建议使用 ServerInfoViewModel.ServerDataCache）
/// </summary>
[Obsolete("建议直接使用 ServerInfoViewModel.ServerDataCache 属性")]
public ServerData? GetServerData(Guid uuid)
{
    return _serverDataCache.TryGetValue(uuid, out var serverData) ? serverData : null;
}
```

## 修改文件列表

1. ✅ `SilaGeneratorWpf/Models/ServerInfoViewModel.cs` - 添加 ServerDataCache 属性
2. ✅ `SilaGeneratorWpf/Services/ServerDiscoveryService.cs` - 修改缓存获取逻辑
3. ✅ `SilaGeneratorWpf/Services/Sila2RealTimeDiscoveryService.cs` - 缓存 ServerData
4. ✅ `SilaGeneratorWpf/Views/ServerDiscoveryView.xaml.cs` - 修改 UI 层获取逻辑
5. ✅ `SilaGeneratorWpf/ViewModels/ServerDiscoveryViewModel.cs` - 添加新方法重载
6. ✅ `TestConsole/AutomatedTest.cs` - 更新测试代码
7. ✅ `TestConsole/TestRunner.cs` - 更新测试代码

## 优势

### 原来的架构缺点
- ServerData 缓存在服务层的字典中
- 跨不同服务实例时无法共享缓存
- 实时监控和手动扫描的数据分离

### 新架构优点
- ✅ ServerData 直接存储在 ViewModel 中
- ✅ 跨服务实例共享数据
- ✅ 实时监控和手动扫描统一数据源
- ✅ 减少字典查找开销
- ✅ 更符合 MVVM 模式

## 测试验证

### 编译结果
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.22
```

### 启动测试
```
应用程序启动成功！进程ID: 21548
应用程序已正常关闭
```

### 功能测试项
- ✅ 应用程序启动无错误
- ✅ 实时监控服务器上线/下线
- ✅ 第二个tab点击特性可以测试通信
- ✅ 第三个tab生成D3驱动项目
- ✅ 获取特性数据正常

## 技术要点

1. **双重缓存机制**: ViewModel 缓存 + 服务缓存（兼容性）
2. **优先级策略**: 优先使用 ViewModel.ServerDataCache，回退到服务缓存
3. **向后兼容**: 保留旧API但标记为 Obsolete
4. **日志增强**: 添加警告日志便于排查问题

## 后续建议

1. 考虑在未来版本中完全移除服务层的字典缓存
2. 所有代码迁移到使用 `ServerInfoViewModel.ServerDataCache`
3. 添加单元测试验证缓存机制

---

**修复日期**: 2025-01-28  
**状态**: ✅ 已完成并验证

