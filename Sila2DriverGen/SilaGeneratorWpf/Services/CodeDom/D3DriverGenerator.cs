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
            driverClass.Comments.Add(new CodeCommentStatement("方法可以同时标记为调度和维护方法，或两者都不标记。", true));
            driverClass.Comments.Add(new CodeCommentStatement("</remarks>", true));
        }

        /// <summary>
        /// 添加所有方法
        /// </summary>
        private void AddMethods(CodeTypeDeclaration driverClass, List<MethodGenerationInfo> methods)
        {
            // 只包含标记为 IsIncluded 的方法
            var includedMethods = methods.Where(m => m.IsIncluded).ToList();
            
            _logger.LogInformation($"共 {methods.Count} 个方法，其中 {includedMethods.Count} 个被包含在D3Driver中");

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
            codeMethod.ReturnType = new CodeTypeReference(returnType);

            // 添加参数
            foreach (var param in method.Parameters)
            {
                codeMethod.Parameters.Add(new CodeParameterDeclarationExpression(
                    param.Type, param.Name));

                // 如果类型不支持，添加额外的 JSON 字符串参数
                if (param.RequiresJsonParameter)
                {
                    codeMethod.Parameters.Add(new CodeParameterDeclarationExpression(
                        typeof(string), $"{param.Name}JsonString"));
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
                        codeMethod.Comments.Add(new CodeCommentStatement(
                            $"<param name=\"{param.Name}\">{paramDoc}</param>", true));
                    }

                    // 如果需要 JSON 参数，添加额外的参数注释
                    if (param.RequiresJsonParameter)
                    {
                        codeMethod.Comments.Add(new CodeCommentStatement(
                            $"<param name=\"{param.Name}JsonString\">JSON 字符串格式的 {param.Name}（可选，优先使用）</param>",
                            true));
                    }
                }

                // Returns
                if (!string.IsNullOrEmpty(xmlDoc.Returns))
                {
                    var returnsDoc = xmlDoc.Returns;

                    // 如果返回类型不支持，添加提示
                    if (method.RequiresJsonReturn)
                    {
                        returnsDoc += " [注意：返回类型为复杂对象，建议使用 JSON 序列化]";
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
            // 构建参数列表
            var arguments = method.Parameters.Select(p =>
                new CodeArgumentReferenceExpression(p.Name)).ToArray();

            // _sila2Device.Method(...);
            var invokeExpression = new CodeMethodInvokeExpression(
                new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "_sila2Device"),
                method.Name,
                arguments);

            if (codeMethod.ReturnType.BaseType == "System.Void")
            {
                codeMethod.Statements.Add(new CodeExpressionStatement(invokeExpression));
            }
            else
            {
                codeMethod.Statements.Add(new CodeMethodReturnStatement(invokeExpression));
            }
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


