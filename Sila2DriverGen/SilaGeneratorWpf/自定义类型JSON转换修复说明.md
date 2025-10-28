# 自定义类型JSON转换修复说明

## 修改日期
2025-10-28

## 修改背景

在生成D3Driver.cs时，方法入参不支持自定义类或struct，即使这些类/结构内部都是基础类型也不行。所有自定义类型的参数都必须转换为JSON string，在使用时再反序列化。

## 修改内容

### 文件: `ClientCodeAnalyzer.cs`

**修改位置**: `IsSupportedType()` 方法

**修改前的逻辑**:
- 支持基础类型（int, string, DateTime等）
- 支持枚举
- 支持数组和List（元素为基础类型）
- **支持简单的自定义类/结构**（只要内部都是基础类型）

**修改后的逻辑**:
- 支持基础类型（int, string, DateTime等）
- 支持枚举
- 支持Nullable<T>（如 int?, DateTime?等，但T必须是支持的类型）
- 支持数组和List（元素为基础类型）
- **不支持任何自定义类和结构**（即使内部都是基础类型）

### 关键代码变更

#### 1. 添加了 Nullable<T> 支持

```csharp
// Nullable<T> 类型，检查底层类型是否支持
if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
{
    var underlyingType = Nullable.GetUnderlyingType(type);
    return underlyingType != null && IsSupportedType(underlyingType);
}
```

这确保了 `int?`、`DateTime?`、`double?` 等可空类型仍然被支持。

#### 2. 移除了自定义类/结构的支持

**修改前**:
```csharp
// 简单类/结构（仅包含基础类型，不嵌套）
if (type.IsClass || type.IsValueType)
{
    return ValidateSimpleCompositeType(type);  // 会检查内部是否都是基础类型
}
```

**修改后**:
```csharp
// 自定义类和结构不支持，必须转换为JSON string
// 即使内部都是基础类型也不行
if (type.IsClass || type.IsValueType)
{
    // 检查是否为系统内置的值类型（如DateTime已经在supportedTypes中）
    // 所有其他自定义类和结构都不支持
    return false;
}
```

## 支持的类型列表

### ✅ 直接支持的类型（无需JSON转换）

1. **基础值类型**:
   - `int`, `byte`, `sbyte`, `short`, `ushort`
   - `long`, `ulong`, `uint`
   - `float`, `double`, `decimal`
   - `bool`, `char`

2. **系统类型**:
   - `string`
   - `DateTime`
   - `byte[]`

3. **可空类型**:
   - `int?`, `double?`, `DateTime?` 等（任何支持类型的可空版本）

4. **枚举**:
   - 任何枚举类型

5. **集合类型**:
   - `T[]` - 数组（T必须是基础类型）
   - `List<T>` - 列表（T必须是支持的类型）

6. **特殊类型**:
   - `void` - 返回值可以是void
   - `IObservableCommand<T>` - SiLA2的可观察命令

### ❌ 不支持的类型（必须使用JSON转换）

1. **自定义类**:
   ```csharp
   public class MyConfig {
       public string Name { get; set; }
       public int Value { get; set; }
   }
   ```

2. **自定义结构**:
   ```csharp
   public struct Point {
       public int X;
       public int Y;
   }
   ```

3. **复杂泛型**:
   ```csharp
   Dictionary<string, int>
   Tuple<int, string>
   ```

4. **Stream类型**:
   - `Stream`, `Stream?` - 这些类型的方法已在预览中被过滤

## 生成的代码示例

### 示例1: 基础类型参数（无需转换）

**原始方法**:
```csharp
public void SetTemperature(double temperature, int duration);
```

**生成的D3Driver方法**:
```csharp
public void SetTemperature(double temperature, int duration)
{
    _sila2Device.SetTemperature(temperature, duration);
}
```

### 示例2: 自定义类型参数（需要JSON转换）

**原始方法**:
```csharp
public void Configure(MyConfig config);

public class MyConfig {
    public string Name { get; set; }
    public int Value { get; set; }
}
```

**生成的D3Driver方法**:
```csharp
/// <param name="configJsonString">配置对象 (JSON格式)</param>
public void Configure(string configJsonString)
{
    var config = Newtonsoft.Json.JsonConvert.DeserializeObject<MyNamespace.MyConfig>(configJsonString);
    _sila2Device.Configure(config);
}
```

### 示例3: 自定义返回值（需要JSON转换）

**原始方法**:
```csharp
public MyStatus GetStatus();

public class MyStatus {
    public string State { get; set; }
    public double Temperature { get; set; }
}
```

**生成的D3Driver方法**:
```csharp
/// <returns>设备状态 (返回JSON格式字符串)</returns>
public string GetStatus()
{
    var result = _sila2Device.GetStatus();
    return Newtonsoft.Json.JsonConvert.SerializeObject(result);
}
```

## 工作流程

### 分析阶段 (ClientCodeAnalyzer)
1. 分析客户端代码的所有方法
2. 对每个方法的参数和返回值调用 `IsSupportedType()` 检查
3. 如果不支持，设置标记：
   - 参数: `RequiresJsonParameter = true`
   - 返回值: `RequiresJsonReturn = true`

### 代码生成阶段 (D3DriverGenerator)
1. **参数处理**:
   - 如果 `RequiresJsonParameter == true`:
     - 参数类型改为 `string`
     - 参数名改为 `{原名}JsonString`
     - 在方法体开头添加反序列化代码

2. **返回值处理**:
   - 如果 `RequiresJsonReturn == true`:
     - 返回类型改为 `string`
     - 调用方法后序列化结果为JSON
     - 返回JSON字符串

## 测试建议

### 测试场景1: 基础类型参数
```csharp
// 原始
void SetValue(int value);

// 应生成
public void SetValue(int value) {
    _sila2Device.SetValue(value);
}
```

### 测试场景2: 自定义类参数
```csharp
// 原始
void SetConfig(MyConfig config);

// 应生成
public void SetConfig(string configJsonString) {
    var config = Newtonsoft.Json.JsonConvert.DeserializeObject<MyNamespace.MyConfig>(configJsonString);
    _sila2Device.SetConfig(config);
}
```

### 测试场景3: 可空类型参数
```csharp
// 原始
void SetOptionalValue(int? value);

// 应生成
public void SetOptionalValue(int? value) {
    _sila2Device.SetOptionalValue(value);
}
```

### 测试场景4: 自定义返回值
```csharp
// 原始
MyStatus GetStatus();

// 应生成
public string GetStatus() {
    var result = _sila2Device.GetStatus();
    return Newtonsoft.Json.JsonConvert.SerializeObject(result);
}
```

## 影响范围

### 直接影响
- 客户端代码分析 (ClientCodeAnalyzer)
- D3Driver.cs 代码生成 (D3DriverGenerator)
- 方法预览窗口显示的参数类型

### 行为变化
- **更严格的类型检查**: 所有自定义类/结构都必须使用JSON
- **更安全的调用**: 避免了复杂类型的序列化问题
- **更清晰的接口**: D3调用者知道哪些参数需要传JSON字符串

### 向后兼容性
- ⚠️ 可能破坏性变化：
  - 之前支持的简单自定义类/结构现在需要JSON格式
  - 需要重新生成所有D3Driver.cs文件
  - 已有的D3调用代码可能需要调整

## 相关文件

- `Sila2DriverGen/SilaGeneratorWpf/Services/ClientCodeAnalyzer.cs` - 类型检查逻辑
- `Sila2DriverGen/SilaGeneratorWpf/Services/CodeDom/D3DriverGenerator.cs` - 代码生成逻辑
- `Sila2DriverGen/SilaGeneratorWpf/Models/ParameterGenerationInfo.cs` - 参数信息模型
- `Sila2DriverGen/Build/Generator/net8.0/` - 更新的可执行文件

## 编译和部署

已完成：
1. ✅ 修改类型检查逻辑
2. ✅ 添加Nullable<T>支持
3. ✅ 编译项目 (Release模式)
4. ✅ 更新Build文件夹

构建命令：
```bash
cd Sila2DriverGen
dotnet build -c Release
```

## 注意事项

1. **JSON序列化要求**:
   - 自定义类必须有无参构造函数
   - 属性必须有getter和setter
   - 使用Newtonsoft.Json进行序列化

2. **性能考虑**:
   - JSON序列化/反序列化有性能开销
   - 对于频繁调用的方法，考虑优化数据结构

3. **调试建议**:
   - 使用JSON格式化工具验证传入的JSON字符串
   - 检查生成的D3Driver.cs中的类型名称是否正确
   - 确保命名空间完整（如 `MyNamespace.MyConfig`）

4. **未来改进**:
   - 可以考虑添加白名单机制，允许特定的自定义类直接支持
   - 可以添加JSON验证，在反序列化前检查格式

