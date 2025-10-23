using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CSharp;
using Microsoft.Extensions.Logging;
using SilaGeneratorWpf.Models;

namespace SilaGeneratorWpf.Services.CodeDom
{
    /// <summary>
    /// AllSila2Client 生成器 - 整合所有特性的中间封装类
    /// </summary>
    public class AllSila2ClientGenerator
    {
        private readonly ILogger _logger;

        public AllSila2ClientGenerator()
        {
            _logger = LoggerService.GetLogger<AllSila2ClientGenerator>();
        }

        /// <summary>
        /// 生成 AllSila2Client.cs
        /// </summary>
        public void Generate(
            List<ClientFeatureInfo> features,
            string outputPath,
            string namespaceName)
        {
            _logger.LogInformation($"开始生成 AllSila2Client.cs，共 {features.Count} 个特性");

            var codeUnit = new CodeCompileUnit();
            var codeNamespace = new CodeNamespace(namespaceName);

            // 添加导入命名空间
            AddImports(codeNamespace);

            // 创建类
            var clientClass = new CodeTypeDeclaration("AllSila2Client")
            {
                IsClass = true,
                TypeAttributes = System.Reflection.TypeAttributes.Public
            };

            // 添加字段
            AddFields(clientClass, features);

            // 添加构造函数
            AddConstructor(clientClass);

            // 添加 Connect 方法
            AddConnectMethod(clientClass, features);

            // 添加 Disconnect 方法
            AddDisconnectMethod(clientClass);

            // 添加连接状态事件
            AddConnectionStatusEvent(clientClass);

            // 添加 DiscoverFactories 方法
            AddDiscoverFactoriesMethod(clientClass);

            // 添加所有平铺方法
            AddFlattenedMethods(clientClass, features);

            codeNamespace.Types.Add(clientClass);
            codeUnit.Namespaces.Add(codeNamespace);

            // 生成代码文件
            GenerateCodeFile(codeUnit, outputPath);

            _logger.LogInformation($"成功生成 AllSila2Client.cs: {outputPath}");
        }

        /// <summary>
        /// 添加导入命名空间
        /// </summary>
        private void AddImports(CodeNamespace codeNamespace)
        {
            var imports = new[]
            {
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "System.Reflection",
                "Tecan.Sila2.Client",
                "Tecan.Sila2.Client.ExecutionManagement",
                "Tecan.Sila2.Discovery",
                "Tecan.Sila2.Locking",
                "BR.PC.Device.Sila2Discovery",
                "Newtonsoft.Json"
            };

            foreach (var import in imports)
            {
                codeNamespace.Imports.Add(new CodeNamespaceImport(import));
            }
        }

        /// <summary>
        /// 添加字段
        /// </summary>
        private void AddFields(CodeTypeDeclaration clientClass, List<ClientFeatureInfo> features)
        {
            // 为每个特性添加客户端字段
            foreach (var feature in features)
            {
                var field = new CodeMemberField(
                    feature.InterfaceName,
                    ToCamelCase(feature.InterfaceName))
                {
                    Attributes = MemberAttributes.Private
                };
                clientClass.Members.Add(field);
            }

            // 添加连接相关字段
            clientClass.Members.Add(new CodeMemberField("Tecan.Sila2.Discovery.ServerConnector", "_connector")
            {
                Attributes = MemberAttributes.Private
            });
            
            clientClass.Members.Add(new CodeMemberField("ExecutionManagerFactory", "executionManagerFactory")
            {
                Attributes = MemberAttributes.Private
            });
            
            clientClass.Members.Add(new CodeMemberField("IEnumerable<Tecan.Sila2.ServerData>", "_servers")
            {
                Attributes = MemberAttributes.Private
            });
            
            clientClass.Members.Add(new CodeMemberField("Tecan.Sila2.ServerData", "_server")
            {
                Attributes = MemberAttributes.Private
            });
        }

        /// <summary>
        /// 添加构造函数
        /// </summary>
        private void AddConstructor(CodeTypeDeclaration clientClass)
        {
            var constructor = new CodeConstructor
            {
                Attributes = MemberAttributes.Public
            };

            // _connector = new ServerConnector(new DiscoveryExecutionManager());
            constructor.Statements.Add(new CodeAssignStatement(
                new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "_connector"),
                new CodeObjectCreateExpression("ServerConnector",
                    new CodeObjectCreateExpression("DiscoveryExecutionManager"))));

            // executionManagerFactory = new ExecutionManagerFactory(...);
            constructor.Statements.Add(new CodeAssignStatement(
                new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "executionManagerFactory"),
                new CodeObjectCreateExpression("ExecutionManagerFactory",
                    new CodeArrayCreateExpression("IClientRequestInterceptor",
                        new CodeObjectCreateExpression("LockingInterceptor",
                            new CodePrimitiveExpression(null))))));

            // Sila2Discovery.StartRealTimeMonitoring();
            constructor.Statements.Add(new CodeMethodInvokeExpression(
                new CodeTypeReferenceExpression("Sila2Discovery"),
                "StartRealTimeMonitoring"));

            clientClass.Members.Add(constructor);
        }

        /// <summary>
        /// 添加 Connect 方法
        /// </summary>
        private void AddConnectMethod(CodeTypeDeclaration clientClass, List<ClientFeatureInfo> features)
        {
            var method = new CodeMemberMethod
            {
                Name = "Connect",
                Attributes = MemberAttributes.Public,
                ReturnType = new CodeTypeReference(typeof(bool))
            };
            method.Parameters.Add(new CodeParameterDeclarationExpression(typeof(string), "ip"));
            method.Parameters.Add(new CodeParameterDeclarationExpression(typeof(int), "port"));

            // var info = Sila2Discovery.GetServer(ip, port, TimeSpan.FromSeconds(5));
            method.Statements.Add(new CodeVariableDeclarationStatement("var", "info",
                new CodeMethodInvokeExpression(
                    new CodeTypeReferenceExpression("Sila2Discovery"),
                    "GetServer",
                    new CodeArgumentReferenceExpression("ip"),
                    new CodeArgumentReferenceExpression("port"),
                    new CodeMethodInvokeExpression(
                        new CodeTypeReferenceExpression(typeof(TimeSpan)),
                        "FromSeconds",
                        new CodePrimitiveExpression(5)))));

            // if (info == null) return false;
            method.Statements.Add(new CodeConditionStatement(
                new CodeBinaryOperatorExpression(
                    new CodeVariableReferenceExpression("info"),
                    CodeBinaryOperatorType.IdentityEquality,
                    new CodePrimitiveExpression(null)),
                new CodeMethodReturnStatement(new CodePrimitiveExpression(false))));

            // _server = _connector.Connect(...);
            method.Statements.Add(new CodeAssignStatement(
                new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "_server"),
                new CodeMethodInvokeExpression(
                    new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "_connector"),
                    "Connect",
                    new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("info"), "IPAddress"),
                    new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("info"), "Port"),
                    new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("info"), "Uuid"),
                    new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("info"), "TxtRecords"))));

            // ClientProvider clientProvider = new ClientProvider(...);
            method.Statements.Add(new CodeVariableDeclarationStatement("ClientProvider", "clientProvider",
                new CodeObjectCreateExpression("ClientProvider",
                    new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "executionManagerFactory"),
                    new CodeMethodInvokeExpression(null, "DiscoverFactories"))));

            // 为每个特性创建客户端
            foreach (var feature in features)
            {
                var fieldName = ToCamelCase(feature.InterfaceName);
                
                // clientProvider.TryCreateClient<IFeature>(_server, out feature);
                method.Statements.Add(new CodeMethodInvokeExpression(
                    new CodeVariableReferenceExpression("clientProvider"),
                    "TryCreateClient",
                    new CodeTypeOfExpression(feature.InterfaceName),
                    new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "_server"),
                    new CodeDirectionExpression(FieldDirection.Out,
                        new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), fieldName))));
            }

            // TODO: 添加连接状态事件处理
            // 这部分比较复杂，暂时简化

            // return true;
            method.Statements.Add(new CodeMethodReturnStatement(new CodePrimitiveExpression(true)));

            clientClass.Members.Add(method);
        }

        /// <summary>
        /// 添加 Disconnect 方法
        /// </summary>
        private void AddDisconnectMethod(CodeTypeDeclaration clientClass)
        {
            var method = new CodeMemberMethod
            {
                Name = "Disconnect",
                Attributes = MemberAttributes.Public,
                ReturnType = new CodeTypeReference(typeof(bool))
            };

            // _server.Channel.Dispose();
            method.Statements.Add(new CodeMethodInvokeExpression(
                new CodePropertyReferenceExpression(
                    new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "_server"),
                    "Channel"),
                "Dispose"));

            // return true;
            method.Statements.Add(new CodeMethodReturnStatement(new CodePrimitiveExpression(true)));

            clientClass.Members.Add(method);
        }

        /// <summary>
        /// 添加连接状态事件
        /// </summary>
        private void AddConnectionStatusEvent(CodeTypeDeclaration clientClass)
        {
            var eventField = new CodeMemberField("Action<bool>", "OnConnectionStatusChanged")
            {
                Attributes = MemberAttributes.Public
            };
            clientClass.Members.Add(eventField);
        }

        /// <summary>
        /// 添加 DiscoverFactories 方法
        /// </summary>
        private void AddDiscoverFactoriesMethod(CodeTypeDeclaration clientClass)
        {
            var method = new CodeMemberMethod
            {
                Name = "DiscoverFactories",
                Attributes = MemberAttributes.Public | MemberAttributes.Static,
                ReturnType = new CodeTypeReference("List<IClientFactory>")
            };

            // 添加方法体（简化实现，生成完整的反射代码）
            method.Statements.Add(new CodeVariableDeclarationStatement(
                "List<IClientFactory>", "factories",
                new CodeObjectCreateExpression("List<IClientFactory>")));

            // var assembly = Assembly.GetExecutingAssembly();
            method.Statements.Add(new CodeVariableDeclarationStatement("var", "assembly",
                new CodeMethodInvokeExpression(
                    new CodeTypeReferenceExpression(typeof(Assembly)),
                    "GetExecutingAssembly")));

            // var factoryTypes = assembly.GetTypes().Where(...)...
            method.Statements.Add(new CodeVariableDeclarationStatement("var", "factoryTypes",
                new CodeSnippetExpression(
                    "assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract).Where(t => typeof(IClientFactory).IsAssignableFrom(t)).ToList()")));

            // foreach (var type in factoryTypes)
            method.Statements.Add(new CodeSnippetStatement(@"
            foreach (var type in factoryTypes)
            {
                try
                {
                    if (Activator.CreateInstance(type) is IClientFactory factory)
                    {
                        factories.Add(factory);
                    }
                }
                catch { }
            }"));

            // return factories;
            method.Statements.Add(new CodeMethodReturnStatement(
                new CodeVariableReferenceExpression("factories")));

            clientClass.Members.Add(method);
        }

        /// <summary>
        /// 添加所有平铺方法
        /// </summary>
        private void AddFlattenedMethods(CodeTypeDeclaration clientClass, List<ClientFeatureInfo> features)
        {
            // 检测命名冲突
            var methodNames = new Dictionary<string, List<(ClientFeatureInfo Feature, MethodGenerationInfo Method)>>();

            foreach (var feature in features)
            {
                foreach (var method in feature.Methods)
                {
                    if (!methodNames.ContainsKey(method.Name))
                        methodNames[method.Name] = new();
                    methodNames[method.Name].Add((feature, method));
                }
            }

            // 生成方法
            foreach (var feature in features)
            {
                foreach (var method in feature.Methods)
                {
                    var finalName = method.Name;
                    if (methodNames[method.Name].Count > 1)
                    {
                        // 命名冲突，添加前缀
                        finalName = $"{feature.FeatureName}_{method.Name}";
                    }

                    GenerateMethod(clientClass, feature, method, finalName);
                }
            }
        }

        /// <summary>
        /// 生成单个方法
        /// </summary>
        private void GenerateMethod(
            CodeTypeDeclaration clientClass,
            ClientFeatureInfo feature,
            MethodGenerationInfo method,
            string finalName)
        {
            var codeMethod = new CodeMemberMethod
            {
                Name = finalName,
                Attributes = MemberAttributes.Public
            };

            // 添加 XML 注释
            AddXmlComments(codeMethod, method);

            // 确定返回类型
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
            if (method.IsProperty)
            {
                // 属性 getter：return _client.Property;
                GeneratePropertyGetBody(codeMethod, feature, method);
            }
            else if (method.IsObservableCommand)
            {
                // 可观察命令：阻塞等待
                GenerateObservableCommandBody(codeMethod, feature, method);
            }
            else
            {
                // 普通方法
                GenerateNormalMethodBody(codeMethod, feature, method);
            }

            clientClass.Members.Add(codeMethod);
        }

        /// <summary>
        /// 添加 XML 注释
        /// </summary>
        private void AddXmlComments(CodeMemberMethod codeMethod, MethodGenerationInfo method)
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

                // Remarks
                if (!string.IsNullOrEmpty(xmlDoc.Remarks))
                {
                    codeMethod.Comments.Add(new CodeCommentStatement(
                        $"<remarks>{xmlDoc.Remarks}</remarks>", true));
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
        /// 生成属性 Get 方法体
        /// </summary>
        private void GeneratePropertyGetBody(
            CodeMemberMethod codeMethod,
            ClientFeatureInfo feature,
            MethodGenerationInfo method)
        {
            // return _client.Property;
            codeMethod.Statements.Add(new CodeMethodReturnStatement(
                new CodePropertyReferenceExpression(
                    new CodeFieldReferenceExpression(new CodeThisReferenceExpression(),
                        ToCamelCase(feature.InterfaceName)),
                    method.PropertyName)));
        }

        /// <summary>
        /// 生成可观察命令方法体
        /// </summary>
        private void GenerateObservableCommandBody(
            CodeMemberMethod codeMethod,
            ClientFeatureInfo feature,
            MethodGenerationInfo method)
        {
            // 构建参数列表
            var arguments = method.Parameters.Select(p => 
                new CodeArgumentReferenceExpression(p.Name)).ToArray();

            // var command = _client.Method(...);
            var invokeExpression = new CodeMethodInvokeExpression(
                new CodeFieldReferenceExpression(null, ToCamelCase(feature.InterfaceName)),
                method.OriginalName,
                arguments);

            var commandVar = new CodeVariableDeclarationStatement("var", "command", invokeExpression);
            codeMethod.Statements.Add(commandVar);

            // 阻塞等待：command.Response.GetAwaiter().GetResult()
            var awaitExpression = new CodeMethodInvokeExpression(
                new CodeMethodInvokeExpression(
                    new CodePropertyReferenceExpression(
                        new CodeVariableReferenceExpression("command"),
                        "Response"),
                    "GetAwaiter"),
                "GetResult");

            if (codeMethod.ReturnType.BaseType == "System.Void")
            {
                codeMethod.Statements.Add(new CodeExpressionStatement(awaitExpression));
            }
            else
            {
                codeMethod.Statements.Add(new CodeMethodReturnStatement(awaitExpression));
            }
        }

        /// <summary>
        /// 生成普通方法体
        /// </summary>
        private void GenerateNormalMethodBody(
            CodeMemberMethod codeMethod,
            ClientFeatureInfo feature,
            MethodGenerationInfo method)
        {
            // 构建参数列表
            var arguments = method.Parameters.Select(p =>
                new CodeArgumentReferenceExpression(p.Name)).ToArray();

            // _client.Method(...);
            var invokeExpression = new CodeMethodInvokeExpression(
                new CodeFieldReferenceExpression(new CodeThisReferenceExpression(),
                    ToCamelCase(feature.InterfaceName)),
                method.OriginalName,
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

        /// <summary>
        /// 转换为驼峰命名
        /// </summary>
        private string ToCamelCase(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            if (str.StartsWith("I") && str.Length > 1 && char.IsUpper(str[1]))
            {
                // ITemperatureController -> temperatureController
                return char.ToLowerInvariant(str[1]) + str.Substring(2);
            }
            return char.ToLowerInvariant(str[0]) + str.Substring(1);
        }
    }
}

