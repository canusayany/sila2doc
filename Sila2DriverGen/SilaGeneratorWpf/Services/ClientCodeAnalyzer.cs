using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.Logging;
using SilaGeneratorWpf.Models;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// 客户端代码分析器
    /// </summary>
    public class ClientCodeAnalyzer
    {
        private readonly ILogger _logger;
        private XDocument? _xmlDocumentation;

        public ClientCodeAnalyzer()
        {
            _logger = LoggerService.GetLogger<ClientCodeAnalyzer>();
        }

        /// <summary>
        /// 分析客户端代码目录
        /// </summary>
        public ClientAnalysisResult Analyze(string clientCodePath)
        {
            _logger.LogInformation($"开始分析客户端代码: {clientCodePath}");
            var result = new ClientAnalysisResult();

            try
            {
                // 1. 查找所有 C# 文件
                var csFiles = Directory.GetFiles(clientCodePath, "*.cs", SearchOption.TopDirectoryOnly);
                if (!csFiles.Any())
                {
                    throw new Exception($"在 {clientCodePath} 中未找到任何 .cs 文件");
                }

                _logger.LogInformation($"找到 {csFiles.Length} 个 .cs 文件");

                // 2. 编译成 DLL（含 XML 文档）
                var (assembly, xmlDocPath) = CompileToAssembly(clientCodePath, csFiles);

                // 3. 加载 XML 文档注释
                if (File.Exists(xmlDocPath))
                {
                    _xmlDocumentation = XDocument.Load(xmlDocPath);
                    _logger.LogInformation("成功加载 XML 文档注释");
                }

                // 4. 分析所有接口（查找带有 SilaFeature 特性的接口）
                var interfaceTypes = assembly.GetTypes()
                    .Where(t => t.IsInterface && HasSilaFeatureAttribute(t))
                    .ToList();

                _logger.LogInformation($"找到 {interfaceTypes.Count} 个 SiLA2 特性接口");

                foreach (var interfaceType in interfaceTypes)
                {
                    var featureInfo = AnalyzeFeature(interfaceType);
                    result.Features.Add(featureInfo);
                }

                _logger.LogInformation($"分析完成，共 {result.Features.Count} 个特性");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分析客户端代码失败");
                throw;
            }
        }

        /// <summary>
        /// 检查类型是否有 SilaFeature 特性
        /// </summary>
        private bool HasSilaFeatureAttribute(Type type)
        {
            return type.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name == "SilaFeatureAttribute");
        }

        /// <summary>
        /// 分析单个特性
        /// </summary>
        private ClientFeatureInfo AnalyzeFeature(Type interfaceType)
        {
            _logger.LogInformation($"分析特性接口: {interfaceType.Name}");

            var featureInfo = new ClientFeatureInfo
            {
                InterfaceType = interfaceType,
                InterfaceName = interfaceType.Name,
                FeatureName = ExtractFeatureName(interfaceType),
                ClientName = interfaceType.Name.TrimStart('I') + "Client"
            };

            // 分析属性
            foreach (var property in interfaceType.GetProperties())
            {
                var isObservable = HasObservableAttribute(property);
                
                var method = new MethodGenerationInfo
                {
                    Name = $"Get{property.Name}",
                    OriginalName = property.Name,
                    IsProperty = true,
                    PropertyName = property.Name,
                    ReturnType = property.PropertyType,
                    IsObservable = isObservable,
                    Description = ExtractSummary(property),
                    XmlDocumentation = GetXmlDocumentation(property),
                    FeatureName = featureInfo.FeatureName
                };

                // 检查返回类型是否支持
                method.RequiresJsonReturn = !IsSupportedType(method.ReturnType);

                featureInfo.Methods.Add(method);
                _logger.LogDebug($"  属性: {property.Name} -> {method.Name}");
            }

            // 分析方法
            foreach (var method in interfaceType.GetMethods().Where(m => !m.IsSpecialName))
            {
                var isObservableCommand = IsObservableCommand(method.ReturnType);
                
                var methodInfo = new MethodGenerationInfo
                {
                    Name = method.Name,
                    OriginalName = method.Name,
                    IsProperty = false,
                    ReturnType = method.ReturnType,
                    IsObservableCommand = isObservableCommand,
                    Description = ExtractSummary(method),
                    XmlDocumentation = GetXmlDocumentation(method),
                    FeatureName = featureInfo.FeatureName,
                    IsIncluded = true,  // 默认包含
                    IsOperations = false,  // 默认不是调度方法
                    IsMaintenance = DetermineIfMaintenance(method)  // 根据方法名判断是否为维护方法
                };

                // 处理参数
                foreach (var param in method.GetParameters())
                {
                    var paramInfo = new ParameterGenerationInfo
                    {
                        Name = param.Name ?? "param",
                        Type = param.ParameterType,
                        Description = ExtractParameterDescription(param),
                        XmlDocumentation = GetXmlDocumentation(param)
                    };

                    // 检查参数类型是否支持
                    paramInfo.RequiresJsonParameter = !IsSupportedType(param.ParameterType);

                    methodInfo.Parameters.Add(paramInfo);
                }

                // 检查返回类型是否支持
                methodInfo.RequiresJsonReturn = !IsSupportedType(methodInfo.ReturnType);

                featureInfo.Methods.Add(methodInfo);
                _logger.LogDebug($"  方法: {method.Name}({string.Join(", ", methodInfo.Parameters.Select(p => p.Name))})");
            }

            return featureInfo;
        }

        /// <summary>
        /// 提取特性名称
        /// </summary>
        private string ExtractFeatureName(Type interfaceType)
        {
            // 尝试从 SilaIdentifierAttribute 获取
            var identifierAttr = interfaceType.GetCustomAttributes(false)
                .FirstOrDefault(attr => attr.GetType().Name == "SilaIdentifierAttribute");

            if (identifierAttr != null)
            {
                var identifierProp = identifierAttr.GetType().GetProperty("Identifier");
                if (identifierProp != null)
                {
                    return identifierProp.GetValue(identifierAttr) as string ?? interfaceType.Name.TrimStart('I');
                }
            }

            // 回退：使用接口名称去掉 I 前缀
            return interfaceType.Name.TrimStart('I');
        }

        /// <summary>
        /// 检查成员是否有 Observable 特性
        /// </summary>
        private bool HasObservableAttribute(MemberInfo member)
        {
            return member.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name == "ObservableAttribute");
        }

        /// <summary>
        /// 检查返回类型是否为可观察命令
        /// </summary>
        private bool IsObservableCommand(Type returnType)
        {
            if (returnType.FullName != null && returnType.FullName.Contains("IObservableCommand"))
                return true;

            if (returnType.IsGenericType)
            {
                var genericDef = returnType.GetGenericTypeDefinition();
                if (genericDef.FullName != null && genericDef.FullName.Contains("IObservableCommand"))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 确定方法分类
        /// </summary>
        private bool DetermineIfMaintenance(MethodInfo method)
        {
            // 简化实现：根据方法名称判断
            var methodName = method.Name.ToLower();
            return methodName.Contains("maintenance") || 
                   methodName.Contains("calibrate") || 
                   methodName.Contains("reset") ||
                   methodName.Contains("init") ||
                   methodName.Contains("config");
        }

        /// <summary>
        /// 检查类型是否为支持的类型
        /// </summary>
        private bool IsSupportedType(Type type)
        {
            // 基础类型
            var supportedTypes = new[]
            {
                typeof(int), typeof(byte), typeof(sbyte), typeof(string),
                typeof(DateTime), typeof(double), typeof(float), typeof(bool),
                typeof(byte[]), typeof(long), typeof(short), typeof(ushort),
                typeof(uint), typeof(ulong), typeof(decimal), typeof(char)
            };

            if (supportedTypes.Contains(type))
                return true;

            // void 类型
            if (type == typeof(void))
                return true;

            // 枚举
            if (type.IsEnum)
                return true;

            // 可观察命令（特殊处理）
            if (IsObservableCommand(type))
                return true;

            // 数组或列表（元素必须是基础类型）
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                return elementType != null && supportedTypes.Contains(elementType);
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = type.GetGenericArguments()[0];
                return IsSupportedType(elementType);
            }

            // 简单类/结构（仅包含基础类型，不嵌套）
            if (type.IsClass || type.IsValueType)
            {
                return ValidateSimpleCompositeType(type);
            }

            return false;
        }

        /// <summary>
        /// 验证简单复合类型（不嵌套）
        /// </summary>
        private bool ValidateSimpleCompositeType(Type type)
        {
            try
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    if (!IsPrimitiveOrString(field.FieldType))
                        return false;
                }

                foreach (var prop in properties)
                {
                    if (!IsPrimitiveOrString(prop.PropertyType))
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 判断是否为基础类型或字符串
        /// </summary>
        private bool IsPrimitiveOrString(Type type)
        {
            return type.IsPrimitive || type == typeof(string) || type == typeof(DateTime) || type == typeof(decimal);
        }

        /// <summary>
        /// 提取 XML 文档的摘要
        /// </summary>
        private string ExtractSummary(MemberInfo member)
        {
            var xmlDoc = GetXmlDocumentation(member);
            return xmlDoc?.Summary ?? string.Empty;
        }

        /// <summary>
        /// 提取参数描述
        /// </summary>
        private string ExtractParameterDescription(ParameterInfo parameter)
        {
            if (_xmlDocumentation == null || parameter.Member == null)
                return string.Empty;

            var memberName = GetXmlMemberName(parameter.Member);
            var memberElement = _xmlDocumentation.Descendants("member")
                .FirstOrDefault(m => m.Attribute("name")?.Value == memberName);

            if (memberElement == null)
                return string.Empty;

            var paramElement = memberElement.Elements("param")
                .FirstOrDefault(p => p.Attribute("name")?.Value == parameter.Name);

            return paramElement?.Value.Trim() ?? string.Empty;
        }

        /// <summary>
        /// 从 XML 文档中提取成员的注释
        /// </summary>
        private XmlDocumentationInfo? GetXmlDocumentation(MemberInfo member)
        {
            if (_xmlDocumentation == null)
                return null;

            var memberName = GetXmlMemberName(member);
            var memberElement = _xmlDocumentation.Descendants("member")
                .FirstOrDefault(m => m.Attribute("name")?.Value == memberName);

            if (memberElement == null)
                return null;

            return new XmlDocumentationInfo
            {
                Summary = memberElement.Element("summary")?.Value.Trim(),
                Remarks = memberElement.Element("remarks")?.Value.Trim(),
                Returns = memberElement.Element("returns")?.Value.Trim(),
                Parameters = memberElement.Elements("param")
                    .ToDictionary(
                        p => p.Attribute("name")?.Value ?? string.Empty,
                        p => p.Value.Trim()
                    )
            };
        }

        /// <summary>
        /// 获取参数的 XML 文档（特殊处理）
        /// </summary>
        private XmlDocumentationInfo? GetXmlDocumentation(ParameterInfo parameter)
        {
            // 参数本身没有独立的 XML 文档，这里返回 null
            // 参数的文档包含在方法的 XML 文档中
            return null;
        }

        /// <summary>
        /// 生成 XML 文档的成员名称（如：M:Namespace.Class.Method）
        /// </summary>
        private string GetXmlMemberName(MemberInfo member)
        {
            var prefix = member.MemberType switch
            {
                MemberTypes.Method => "M:",
                MemberTypes.Property => "P:",
                MemberTypes.Field => "F:",
                MemberTypes.TypeInfo => "T:",
                _ => ""
            };

            var declaringType = member.DeclaringType?.FullName ?? "";
            var memberName = member.Name;

            // 处理方法参数
            if (member is MethodInfo method && method.GetParameters().Length > 0)
            {
                var paramTypes = string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName));
                memberName += $"({paramTypes})";
            }

            return $"{prefix}{declaringType}.{memberName}";
        }

        /// <summary>
        /// 编译客户端代码到程序集（同时生成 XML 文档）
        /// </summary>
        private (Assembly assembly, string xmlDocPath) CompileToAssembly(string basePath, string[] sourceFiles)
        {
            _logger.LogInformation("开始编译客户端代码...");

            try
            {
                // 读取所有源代码
                var syntaxTrees = sourceFiles.Select(file =>
                    CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file))
                    .ToList();

                // 添加必要的引用
                var references = new List<MetadataReference>
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
                };

                // 添加 .NET 标准库引用
                try
                {
                    references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.ObjectModel").Location));
                    references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.ComponentModel.Primitives").Location));
                    references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.ComponentModel.Composition").Location));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "添加 System.ComponentModel 引用时出现警告");
                }

                // 尝试添加 Tecan.Sila2 相关的引用（从执行程序集目录，不从Sila2Client文件夹）
                try
                {
                    // 查找当前执行程序集所在目录（Generator.dll所在目录）
                    var currentDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    if (!string.IsNullOrEmpty(currentDir))
                    {
                        // 必需的DLL列表（完整列表，包含所有编译时需要的依赖）
                        var requiredDlls = new[]
                        {
                            "protobuf-net.dll",
                            "protobuf-net.Core.dll",
                            "Tecan.Sila2.dll",
                            "Tecan.Sila2.Contracts.dll",
                            "Tecan.Sila2.Annotations.dll",
                            "Tecan.Sila2.DynamicClient.dll",
                            "Grpc.Core.Api.dll",
                            "Grpc.Net.Client.dll",
                            "Grpc.Net.Common.dll",
                            "Newtonsoft.Json.dll"
                        };

                        foreach (var dllName in requiredDlls)
                        {
                            var requiredDllPath = Path.Combine(currentDir, dllName);
                            if (File.Exists(requiredDllPath))
                            {
                                try
                                {
                                    references.Add(MetadataReference.CreateFromFile(requiredDllPath));
                                    _logger.LogInformation($"添加引用: {dllName}");
                                }
                                catch (Exception dllEx)
                                {
                                    _logger.LogWarning(dllEx, $"无法加载 DLL: {dllName}");
                                }
                            }
                            else
                            {
                                _logger.LogWarning($"未找到必需的DLL: {dllName} at {requiredDllPath}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "添加外部引用时出现警告");
                }

                // 创建编译
                var assemblyName = $"TempClientAnalysis_{Guid.NewGuid():N}";
                var compilation = CSharpCompilation.Create(
                    assemblyName,
                    syntaxTrees,
                    references,
                    new CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary,
                        optimizationLevel: OptimizationLevel.Release
                    )
                );

                // 输出路径
                var tempPath = Path.GetTempPath();
                var dllPath = Path.Combine(tempPath, $"{assemblyName}.dll");
                var xmlDocPath = Path.Combine(tempPath, $"{assemblyName}.xml");

                // 生成 DLL 和 XML 文档
                EmitResult emitResult;
                using (var dllStream = new FileStream(dllPath, FileMode.Create))
                using (var xmlStream = new FileStream(xmlDocPath, FileMode.Create))
                {
                    emitResult = compilation.Emit(
                        dllStream,
                        xmlDocumentationStream: xmlStream);
                }

                if (!emitResult.Success)
                {
                    var errors = string.Join("\n", emitResult.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => $"{d.Id}: {d.GetMessage()} at {d.Location}"));
                    
                    _logger.LogError($"编译失败:\n{errors}");
                    throw new Exception($"编译客户端代码失败：\n{errors}");
                }

                _logger.LogInformation($"编译成功: {dllPath}");

                // 加载程序集（确保文件流已关闭）
                var assembly = Assembly.LoadFrom(dllPath);
                return (assembly, xmlDocPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "编译客户端代码时发生错误");
                throw;
            }
        }

        /// <summary>
        /// 从本地XML文件分析特性
        /// （阶段9.5：支持从本地.sila.xml文件分析）
        /// </summary>
        /// <param name="xmlFilePaths">XML文件路径列表</param>
        /// <returns>分析结果</returns>
        public ClientAnalysisResult AnalyzeFromFeatureXml(List<string> xmlFilePaths)
        {
            _logger.LogInformation($"开始从XML文件分析特性，共 {xmlFilePaths.Count} 个文件");
            var result = new ClientAnalysisResult();

            try
            {
                // TODO: 实现XML解析逻辑
                // 1. 解析每个.sila.xml文件
                // 2. 提取Feature定义
                // 3. 转换为ClientFeatureInfo
                // 4. 这需要使用Tecan的Feature序列化器
                
                _logger.LogWarning("从XML文件分析特性功能暂未完全实现");
                throw new NotImplementedException("从XML文件分析特性功能将在后续版本中完善");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从XML文件分析特性失败");
                throw;
            }
        }
    }
}

