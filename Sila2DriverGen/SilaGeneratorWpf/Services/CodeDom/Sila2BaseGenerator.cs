using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using Microsoft.CSharp;
using Microsoft.Extensions.Logging;

namespace SilaGeneratorWpf.Services.CodeDom
{
    /// <summary>
    /// Sila2Base 生成器 - 生成 RPC 通信基类
    /// </summary>
    public class Sila2BaseGenerator
    {
        private readonly ILogger _logger;

        public Sila2BaseGenerator()
        {
            _logger = LoggerService.GetLogger<Sila2BaseGenerator>();
        }

        /// <summary>
        /// 生成 Sila2Base.cs
        /// </summary>
  
        public void Generate(string outputPath, string namespaceName, string clientCodeNamespace = "Sila2Client")
        {
            _logger.LogInformation("开始生成 Sila2Base.cs");

            var codeUnit = new CodeCompileUnit();

            // 添加 global usings（C# 10+）
            // 注意：CodeDOM 不直接支持 global using，我们用注释形式添加
            var globalUsings = new CodeSnippetCompileUnit(@$"
global using {namespaceName};
global using BR.ECS.Executor.Device.Domain.Contracts;
global using BR.ECS.Executor.Device.Domain.Share;
global using BR.ECS.Executor.Device.Infrastructure;
global using Newtonsoft.Json;
global using {clientCodeNamespace};");
            codeUnit.Namespaces.Add(new CodeNamespace());  // 添加一个空命名空间以确保编译单元有效

            var codeNamespace = new CodeNamespace(namespaceName);

            // 创建 Sila2Base 类
            var baseClass = new CodeTypeDeclaration("Sila2Base")
            {
                IsClass = true,
                TypeAttributes = System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.Abstract
            };
            baseClass.BaseTypes.Add(new CodeTypeReference("DeviceBase"));

            // 添加字段
            AddFields(baseClass);

            // 添加构造函数
            AddConstructor(baseClass);

            // 添加 Client_IsConnectionChanged 方法
            AddConnectionChangedMethod(baseClass);

            // 添加 UpdateDeviceInfo 方法
            AddUpdateDeviceInfoMethod(baseClass);

            // 添加 Connect 方法
            AddConnectMethod(baseClass);

            // 添加 Disconnect 方法
            AddDisconnectMethod(baseClass);

            codeNamespace.Types.Add(baseClass);

            // 创建 ConnectionInfo 类
            var connectionInfoClass = CreateConnectionInfoClass();
            codeNamespace.Types.Add(connectionInfoClass);

            codeUnit.Namespaces.Add(codeNamespace);

            // 生成代码文件
            GenerateCodeFile(globalUsings, codeUnit, outputPath);

            _logger.LogInformation($"成功生成 Sila2Base.cs: {outputPath}");
        }

        /// <summary>
        /// 添加字段
        /// </summary>
        private void AddFields(CodeTypeDeclaration baseClass)
        {
            baseClass.Members.Add(new CodeMemberField(typeof(bool), "_deviceConnected")
            {
                Attributes = MemberAttributes.Family,
                InitExpression = new CodePrimitiveExpression(false)
            });

            baseClass.Members.Add(new CodeMemberField("AllSila2Client", "_sila2Device")
            {
                Attributes = MemberAttributes.Assembly
            });

            baseClass.Members.Add(new CodeMemberField("ConnectionInfo", "_connectionInfo")
            {
                Attributes = MemberAttributes.Family
            });
        }

        /// <summary>
        /// 添加构造函数
        /// </summary>
        private void AddConstructor(CodeTypeDeclaration baseClass)
        {
            var constructor = new CodeConstructor
            {
                Attributes = MemberAttributes.Public
            };

            // _sila2Device = new AllSila2Client();
            constructor.Statements.Add(new CodeAssignStatement(
                new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "_sila2Device"),
                new CodeObjectCreateExpression("AllSila2Client")));

            // _sila2Device.OnConnectionStatusChanged += Client_IsConnectionChanged;
            // 使用代码片段来实现事件附加
            constructor.Statements.Add(new CodeSnippetStatement(
                "            _sila2Device.OnConnectionStatusChanged += Client_IsConnectionChanged;"));

            baseClass.Members.Add(constructor);
        }

        /// <summary>
        /// 添加连接状态变化处理方法
        /// </summary>
        private void AddConnectionChangedMethod(CodeTypeDeclaration baseClass)
        {
            var method = new CodeMemberMethod
            {
                Name = "Client_IsConnectionChanged",
                Attributes = MemberAttributes.Private
            };
            method.Parameters.Add(new CodeParameterDeclarationExpression(typeof(bool), "obj"));

            // 方法体使用代码片段（因为包含状态机逻辑）
            method.Statements.Add(new CodeSnippetStatement(@"
            if (_deviceConnected != obj)
            {
                _deviceConnected = obj;

                if (_deviceStateMachine.CurrentState != DeviceState_Common.IDLE)
                {
                    _deviceStateMachine.HandleEvent(DeviceEvent_Common.Connected);
                }
                if (_deviceStateMachine.CurrentState != DeviceState_Common.DISCONNECTED)
                {
                    _deviceStateMachine.HandleEvent(DeviceEvent_Common.Disconnected);
                }
            }"));

            baseClass.Members.Add(method);
        }

        /// <summary>
        /// 添加 UpdateDeviceInfo 方法
        /// </summary>
        private void AddUpdateDeviceInfoMethod(CodeTypeDeclaration baseClass)
        {
            var method = new CodeMemberMethod
            {
                Name = "UpdateDeviceInfo",
                Attributes = MemberAttributes.Public | MemberAttributes.Override,
                ReturnType = new CodeTypeReference(typeof(int))
            };

            method.Comments.Add(new CodeCommentStatement("<summary>", true));
            method.Comments.Add(new CodeCommentStatement("更新设备信息", true));
            method.Comments.Add(new CodeCommentStatement("</summary>", true));

            // 方法体
            method.Statements.Add(new CodeSnippetStatement(@"
            _connectionInfo = _jsonHelper.DeserializeObject<ConnectionInfo>(
                _jsonHelper.SerializeObject(DeviceCfg.Parameters.Parameter));
            return 0;"));

            baseClass.Members.Add(method);
        }

        /// <summary>
        /// 添加 Connect 方法
        /// </summary>
        private void AddConnectMethod(CodeTypeDeclaration baseClass)
        {
            var method = new CodeMemberMethod
            {
                Name = "Connect",
                Attributes = MemberAttributes.Public | MemberAttributes.Override,
                ReturnType = new CodeTypeReference(typeof(int))
            };

            method.Comments.Add(new CodeCommentStatement("<summary>", true));
            method.Comments.Add(new CodeCommentStatement("连接设备", true));
            method.Comments.Add(new CodeCommentStatement("</summary>", true));

            // return _sila2Device.Connect(_connectionInfo.IP, _connectionInfo.Port) ? 0 : 1;
            // 使用代码片段来实现三元运算符
            method.Statements.Add(new CodeSnippetStatement(
                "            return _sila2Device.Connect(_connectionInfo.IP, _connectionInfo.Port) ? 0 : 1;"));

            baseClass.Members.Add(method);
        }

        /// <summary>
        /// 添加 Disconnect 方法
        /// </summary>
        private void AddDisconnectMethod(CodeTypeDeclaration baseClass)
        {
            var method = new CodeMemberMethod
            {
                Name = "Disconnect",
                Attributes = MemberAttributes.Public | MemberAttributes.Override,
                ReturnType = new CodeTypeReference(typeof(int))
            };

            method.Comments.Add(new CodeCommentStatement("<summary>", true));
            method.Comments.Add(new CodeCommentStatement("断开设备连接", true));
            method.Comments.Add(new CodeCommentStatement("</summary>", true));

            // return _sila2Device.Disconnect() ? 0 : 1;
            // 使用代码片段来实现三元运算符
            method.Statements.Add(new CodeSnippetStatement(
                "            return _sila2Device.Disconnect() ? 0 : 1;"));

            baseClass.Members.Add(method);
        }

        /// <summary>
        /// 创建 ConnectionInfo 类
        /// </summary>
        private CodeTypeDeclaration CreateConnectionInfoClass()
        {
            var connectionInfoClass = new CodeTypeDeclaration("ConnectionInfo")
            {
                IsClass = true,
                TypeAttributes = System.Reflection.TypeAttributes.Public
            };

            // 使用CodeSnippetTypeMember直接生成自动属性
            var propertiesSnippet = new CodeSnippetTypeMember(@"
        public string IP { get; set; }
        public int Port { get; set; }");
            
            connectionInfoClass.Members.Add(propertiesSnippet);

            return connectionInfoClass;
        }

        /// <summary>
        /// 生成代码文件
        /// </summary>
        private void GenerateCodeFile(CodeSnippetCompileUnit globalUsings, CodeCompileUnit codeUnit, string outputPath)
        {
            using var writer = new StreamWriter(outputPath);
            var provider = new CSharpCodeProvider();
            var options = new CodeGeneratorOptions
            {
                BracingStyle = "C",
                IndentString = "    ",
                BlankLinesBetweenMembers = true
            };

            // 先写 global usings
            provider.GenerateCodeFromCompileUnit(globalUsings, writer, options);
            
            // 再写主代码
            provider.GenerateCodeFromCompileUnit(codeUnit, writer, options);
        }
    }
}

