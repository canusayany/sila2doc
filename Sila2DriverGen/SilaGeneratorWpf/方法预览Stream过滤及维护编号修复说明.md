# 方法预览Stream过滤及维护编号修复说明

## 修改日期
2025-10-28

## 修改内容

### 1. 方法预览弹窗过滤Stream类型方法

**文件**: `Sila2DriverGen/SilaGeneratorWpf/Models/ClientAnalysisResult.cs`

**修改描述**:
- 在方法预览弹窗中过滤掉入参或返回值包含 `Stream` 或 `Stream?` 类型的方法
- 这些方法将不会在预览弹窗中显示，因为它们无法被D3调用

**实现细节**:
1. 在 `GetMethodPreviewData()` 方法中添加了过滤逻辑
2. 新增 `HasStreamType()` 方法检查方法是否包含Stream类型
3. 新增 `IsStreamType()` 方法检查类型是否为Stream或Stream?
4. 检查包括：
   - 返回值类型
   - 所有参数类型
   - Nullable<Stream> (即 Stream?)

**代码示例**:
```csharp
// 过滤掉包含 Stream 或 Stream? 的方法
if (HasStreamType(method))
{
    continue;
}
```

### 2. 维护方法编号跳过100

**文件**: `Sila2DriverGen/SilaGeneratorWpf/Services/CodeDom/D3DriverGenerator.cs`

**修改描述**:
- 维护方法序号自动分配时，跳过编号100
- 编号顺序：1, 2, 3, ..., 99, 101, 102, ...

**实现细节**:
在 `AddMethods()` 方法中的维护方法编号分配逻辑中添加跳过100的判断：

```csharp
int maintenanceIndex = 1;
foreach (var method in maintenanceMethods)
{
    // 跳过编号 100
    if (maintenanceIndex == 100)
    {
        maintenanceIndex++;
    }
    maintenanceIndexMap[method.Name] = maintenanceIndex++;
}
```

**原因说明**:
编号100可能有特殊用途，因此在自动分配维护方法序号时跳过该编号。

## 测试建议

### 测试场景1: Stream类型方法过滤
1. 使用包含Stream类型方法的SiLA2特性生成客户端代码
2. 打开方法预览弹窗
3. 验证：包含Stream参数或返回值的方法不应出现在列表中
4. 验证：日志中应显示正确的方法总数（不包括被过滤的方法）

### 测试场景2: 维护方法编号跳过100
1. 创建超过100个维护方法的驱动
2. 生成D3Driver.cs文件
3. 验证：
   - 第99个方法的 `[MethodMaintenance(99)]`
   - 第100个方法的 `[MethodMaintenance(101)]`
   - 不应存在 `[MethodMaintenance(100)]`

## 影响范围

### 直接影响
- 方法预览窗口（MethodPreviewWindow）
- D3Driver.cs 代码生成

### 间接影响
- 生成的驱动类不再包含无法调用的Stream类型方法
- 维护方法编号更加规范，避免使用保留编号

## 编译和部署

已完成以下操作：
1. ✅ 修改源代码
2. ✅ 添加必要的using指令 (`using System;`)
3. ✅ 编译项目 (Release模式)
4. ✅ 更新Build文件夹

构建命令：
```bash
cd Sila2DriverGen
dotnet build -c Release
```

部署路径：
- `Build/Generator/net8.0/` - 已更新

## 注意事项

1. **Stream类型检测逻辑**：
   - 通过类型名称 `"Stream"` 和完全限定名 `"System.IO.Stream"` 进行匹配
   - 支持检测 Nullable<Stream> 类型
   - 不影响其他类型的正常显示

2. **维护方法编号**：
   - 只在自动分配编号时跳过100
   - 如果手动指定编号为100，不会被阻止（需要注意）
   - 编号从1开始，连续分配

3. **向后兼容性**：
   - 现有代码不受影响
   - 只影响新生成的代码
   - 不影响已生成的D3Driver.cs文件

## 相关文件

- `Sila2DriverGen/SilaGeneratorWpf/Models/ClientAnalysisResult.cs` - Stream过滤逻辑
- `Sila2DriverGen/SilaGeneratorWpf/Services/CodeDom/D3DriverGenerator.cs` - 维护编号逻辑
- `Sila2DriverGen/Build/Generator/net8.0/` - 更新的可执行文件

