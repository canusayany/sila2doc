using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CSharp;
using Microsoft.Extensions.Logging;
using SilaGeneratorWpf.Models;

namespace SilaGeneratorWpf.Services.CodeDom
{
    /// <summary>
    /// D3Driver 生成器 - 生成 D3 驱动类
    /// </summary>
    public class D3DriverGenerator
    {
        private readonly ILogger _logger;

        public D3DriverGenerator()
        {
            _logger = LoggerService.GetLogger<D3DriverGenerator>();
        }

        /// <summary>
        /// 生成 D3Driver.cs
        /// </summary>
        public void Generate(
            D3DriverGenerationConfig config,
            List<MethodGenerationInfo> methods,
            string outputPath)
        {
            _logger.LogInformation($"开始生成 D3Driver.cs，共 {methods.Count} 个方法");

            var codeUnit = new CodeCompileUnit();
            var codeNamespace = new CodeNamespace(config.Namespace);

            // 创建类
            var driverClass = new CodeTypeDeclaration("D3Driver")
            {
                IsClass = true,
                TypeAttributes = System.Reflection.TypeAttributes.Public
            };
            driverClass.BaseTypes.Add(new CodeTypeReference("Sila2Base"));

            // 添加 DeviceClass 特性
            AddDeviceClassAttribute(driverClass, config);

            // 添加类级别的 XML 注释
            AddClassComments(driverClass);

            // 添加所有方法
            AddMethods(driverClass, methods);

            codeNamespace.Types.Add(driverClass);
            codeUnit.Namespaces.Add(codeNamespace);

            // 生成代码文件
            GenerateCodeFile(codeUnit, outputPath);

            _logger.LogInformation($"成功生成 D3Driver.cs: {outputPath}");
        }

        /// <summary>
        /// 添加 DeviceClass 特性
        /// </summary>
        private void AddDeviceClassAttribute(CodeTypeDeclaration driverClass, D3DriverGenerationConfig config)
        {
            var brand = config.Brand;
            var model = config.Model;
            var key = $"{brand}{model}";
            var deviceType = string.IsNullOrWhiteSpace(config.DeviceType) ? "Robot" : config.DeviceType;
            var developer = string.IsNullOrWhiteSpace(config.Developer) ? "Developer" : config.Developer;

            var attribute = new CodeAttributeDeclaration("DeviceClass",
                new CodeAttributeArgument(new CodePrimitiveExpression(brand)),
                new CodeAttributeArgument(new CodePrimitiveExpression(model)),
                new CodeAttributeArgument(new CodePrimitiveExpression(key)),
                new CodeAttributeArgument(new CodePrimitiveExpression(deviceType)),
                new CodeAttributeArgument(new CodePrimitiveExpression(developer)));

            driverClass.CustomAttributes.Add(attribute);
        }

        /// <summary>
        /// 添加类级别注释
        /// </summary>
        private void AddClassComments(CodeTypeDeclaration driverClass)
        {
            driverClass.Comments.Add(new CodeCommentStatement("<summary>", true));
            driverClass.Comments.Add(new CodeCommentStatement("设备驱动实现类", true));
            driverClass.Comments.Add(new CodeCommentStatement("</summary>", true));
            driverClass.Comments.Add(new CodeCommentStatement("<remarks>", true));
            driverClass.Comments.Add(new CodeCommentStatement("能被D3调用的方法必须是同步的。", true));
            driverClass.Comments.Add(new CodeCommentStatement("带有MethodOperations特性的方法为调度方法，带有MethodMaintenance特性的为维护方法。", true));
            driverClass.Comments.Add(new CodeCommentStatement("方法可以同时标记为调度和维护方法。", true));
            driverClass.Comments.Add(new CodeCommentStatement("只有带特性标记的方法才会被生成到D3Driver中。", true));
            driverClass.Comments.Add(new CodeCommentStatement("</remarks>", true));
        }

        /// <summary>
        /// 添加所有方法
        /// </summary>
        private void AddMethods(CodeTypeDeclaration driverClass, List<MethodGenerationInfo> methods)
        {
            // 只包含标记为 IsIncluded 且有特性标记的方法
            // 方法必须至少有一个特性标记（IsOperations 或 IsMaintenance）才会被生成
            var includedMethods = methods
                .Where(m => m.IsIncluded && (m.IsOperations || m.IsMaintenance))
                .ToList();
            
            _logger.LogInformation($"共 {methods.Count} 个方法，其中 {includedMethods.Count} 个被包含在D3Driver中（有特性标记）");

            // 先统计需要维护序号的方法
            var maintenanceMethods = includedMethods.Where(m => m.IsMaintenance).ToList();
            var maintenanceIndexMap = new Dictionary<string, int>();
            int maintenanceIndex = 1;
            foreach (var method in maintenanceMethods)
            {
                maintenanceIndexMap[method.Name] = maintenanceIndex++;
            }

            // 添加所有方法
            foreach (var method in includedMethods)
            {
                int? maintenanceOrder = method.IsMaintenance && maintenanceIndexMap.ContainsKey(method.Name) 
                    ? maintenanceIndexMap[method.Name] 
                    : null;
                AddMethod(driverClass, method, maintenanceOrder);
            }
        }

        /// <summary>
        /// 添加单个方法
        /// </summary>
        private void AddMethod(
            CodeTypeDeclaration driverClass,
            MethodGenerationInfo method,
            int? maintenanceOrder = null)
        {
            var codeMethod = new CodeMemberMethod
            {
                Name = method.Name,
                Attributes = MemberAttributes.Public
            };

            // 添加 MethodOperations 特性（如果标记了）
            if (method.IsOperations)
            {
                var operationsAttribute = new CodeAttributeDeclaration("MethodOperations");
                codeMethod.CustomAttributes.Add(operationsAttribute);
            }

            // 添加 MethodMaintenance 特性（如果标记了）
            if (method.IsMaintenance && maintenanceOrder.HasValue)
            {
                var maintenanceAttribute = new CodeAttributeDeclaration("MethodMaintenance",
                    new CodeAttributeArgument(new CodePrimitiveExpression(maintenanceOrder.Value)));
                codeMethod.CustomAttributes.Add(maintenanceAttribute);
            }

            // 添加 XML 注释
            AddMethodComments(codeMethod, method);

            // 设置返回类型
            Type returnType = method.ReturnType;
            if (method.IsObservableCommand)
            {
                // IObservableCommand<T> -> T，IObservableCommand -> void
                if (method.ReturnType.IsGenericType)
                {
                    var genericArgs = method.ReturnType.GetGenericArguments();
                    if (genericArgs.Length > 0)
                    {
                        returnType = genericArgs[0];
                    }
                    else
                    {
                        returnType = typeof(void);
                    }
                }
                else
                {
                    returnType = typeof(void);
                }
            }
            
            // 如果返回类型不支持，改为 JSON 字符串
            if (method.RequiresJsonReturn && returnType != typeof(void))
            {
                codeMethod.ReturnType = new CodeTypeReference(typeof(string));
            }
            else
            {
                codeMethod.ReturnType = new CodeTypeReference(returnType);
            }

            // 添加参数
            foreach (var param in method.Parameters)
            {
                // 如果类型不支持，直接使用 JSON 字符串类型
                if (param.RequiresJsonParameter)
                {
                    codeMethod.Parameters.Add(new CodeParameterDeclarationExpression(
                        typeof(string), $"{param.Name}JsonString"));
                }
                else
                {
                    codeMethod.Parameters.Add(new CodeParameterDeclarationExpression(
                        param.Type, param.Name));
                }
            }

            // 添加方法体
            AddMethodBody(codeMethod, method);

            driverClass.Members.Add(codeMethod);
        }

        /// <summary>
        /// 添加方法注释
        /// </summary>
        private void AddMethodComments(CodeMemberMethod codeMethod, MethodGenerationInfo method)
        {
            if (method.XmlDocumentation != null)
            {
                var xmlDoc = method.XmlDocumentation;

                // Summary
                if (!string.IsNullOrEmpty(xmlDoc.Summary))
                {
                    codeMethod.Comments.Add(new CodeCommentStatement(
                        $"<summary>{xmlDoc.Summary}</summary>", true));
                }

                // Parameters
                foreach (var param in method.Parameters)
                {
                    if (xmlDoc.Parameters != null &&
                        xmlDoc.Parameters.TryGetValue(param.Name, out var paramDoc))
                    {
                        // 如果参数需要JSON，修改参数名称和说明
                        if (param.RequiresJsonParameter)
                        {
                            codeMethod.Comments.Add(new CodeCommentStatement(
                                $"<param name=\"{param.Name}JsonString\">{paramDoc} (JSON格式)</param>", true));
                        }
                        else
                        {
                            codeMethod.Comments.Add(new CodeCommentStatement(
                                $"<param name=\"{param.Name}\">{paramDoc}</param>", true));
                        }
                    }
                }

                // Returns
                if (!string.IsNullOrEmpty(xmlDoc.Returns))
                {
                    var returnsDoc = xmlDoc.Returns;

                    // 如果返回类型不支持，修改说明
                    if (method.RequiresJsonReturn)
                    {
                        returnsDoc += " (返回JSON格式字符串)";
                    }

                    codeMethod.Comments.Add(new CodeCommentStatement(
                        $"<returns>{returnsDoc}</returns>", true));
                }
            }
            else if (!string.IsNullOrEmpty(method.Description))
            {
                // 回退：使用简单描述
                codeMethod.Comments.Add(new CodeCommentStatement(
                    $"<summary>{method.Description}</summary>", true));
            }
        }

        /// <summary>
        /// 添加方法体
        /// </summary>
        private void AddMethodBody(CodeMemberMethod codeMethod, MethodGenerationInfo method)
        {
            // 1. 对需要JSON的参数进行反序列化
            var hasJsonParams = method.Parameters.Any(p => p.RequiresJsonParameter);
            if (hasJsonParams)
            {
                foreach (var param in method.Parameters.Where(p => p.RequiresJsonParameter))
                {
                    // var paramName = JsonConvert.DeserializeObject<ParamType>(paramNameJsonString);
                    var friendlyTypeName = GetFriendlyTypeName(param.Type);
                    var deserializeStatement = new CodeSnippetStatement(
                        $"            var {param.Name} = Newtonsoft.Json.JsonConvert.DeserializeObject<{friendlyTypeName}>({param.Name}JsonString);");
                    codeMethod.Statements.Add(deserializeStatement);
                }
            }

            // 2. 构建参数列表（使用反序列化后的变量）
            var arguments = method.Parameters.Select(p =>
                new CodeArgumentReferenceExpression(p.Name)).ToArray();

            // 3. 调用 _sila2Device.Method(...)
            var invokeExpression = new CodeMethodInvokeExpression(
                new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "_sila2Device"),
                method.Name,
                arguments);

            // 4. 处理返回值
            if (codeMethod.ReturnType.BaseType == "System.Void")
            {
                // void 方法：直接调用
                codeMethod.Statements.Add(new CodeExpressionStatement(invokeExpression));
            }
            else if (method.RequiresJsonReturn)
            {
                // 返回值需要JSON：调用后序列化
                // var result = _sila2Device.Method(...);
                codeMethod.Statements.Add(new CodeVariableDeclarationStatement("var", "result", invokeExpression));
                // return JsonConvert.SerializeObject(result);
                var serializeStatement = new CodeSnippetStatement(
                    "            return Newtonsoft.Json.JsonConvert.SerializeObject(result);");
                codeMethod.Statements.Add(serializeStatement);
            }
            else
            {
                // 普通返回值：直接返回
                codeMethod.Statements.Add(new CodeMethodReturnStatement(invokeExpression));
            }
        }

        /// <summary>
        /// 获取友好的类型名称（用于代码生成）
        /// </summary>
        /// <remarks>
        /// 处理泛型类型，避免生成带程序集信息的完整限定名称
        /// 例如：ICollection`1[[Stream, ...]] -> ICollection&lt;Stream&gt;
        /// </remarks>
        private string GetFriendlyTypeName(Type type)
        {
            if (type == null)
                return "object";

            // 处理泛型类型
            if (type.IsGenericType)
            {
                var typeName = type.GetGenericTypeDefinition().FullName;
                if (string.IsNullOrEmpty(typeName))
                    return type.Name;
                
                // 移除泛型参数数量标记（如 `1, `2 等）
                var backtickIndex = typeName.IndexOf('`');
                if (backtickIndex > 0)
                {
                    typeName = typeName.Substring(0, backtickIndex);
                }

                // 获取泛型参数的友好名称
                var genericArgs = type.GetGenericArguments();
                var genericArgNames = genericArgs.Select(GetFriendlyTypeName);
                
                return $"{typeName}<{string.Join(", ", genericArgNames)}>";
            }

            // 处理数组类型
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType == null)
                    return type.Name;
                    
                var elementTypeName = GetFriendlyTypeName(elementType);
                return $"{elementTypeName}[]";
            }

            // 处理普通类型，返回命名空间+类型名
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                return $"{type.Namespace}.{type.Name}";
            }

            return type.Name;
        }

        /// <summary>
        /// 生成代码文件
        /// </summary>
        private void GenerateCodeFile(CodeCompileUnit codeUnit, string outputPath)
        {
            using var writer = new StreamWriter(outputPath);
            var provider = new CSharpCodeProvider();
            var options = new CodeGeneratorOptions
            {
                BracingStyle = "C",
                IndentString = "    ",
                BlankLinesBetweenMembers = true
            };
            provider.GenerateCodeFromCompileUnit(codeUnit, writer, options);
        }
    }
}


