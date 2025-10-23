using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using Microsoft.CSharp;
using Microsoft.Extensions.Logging;

namespace SilaGeneratorWpf.Services.CodeDom
{
    /// <summary>
    /// CommunicationPars 生成器 - 生成通信参数配置类
    /// </summary>
    public class CommunicationParsGenerator
    {
        private readonly ILogger _logger;

        public CommunicationParsGenerator()
        {
            _logger = LoggerService.GetLogger<CommunicationParsGenerator>();
        }

        /// <summary>
        /// 生成 CommunicationPars.cs
        /// </summary>
        public void Generate(string outputPath, string namespaceName)
        {
            _logger.LogInformation("开始生成 CommunicationPars.cs");

            var codeUnit = new CodeCompileUnit();
            var codeNamespace = new CodeNamespace(namespaceName);

            // 添加导入命名空间
            codeNamespace.Imports.Add(new CodeNamespaceImport("System.Collections.Generic"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("BR.ECS.Executor.Device.Domain.Contracts"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("BR.ECS.Executor.Device.Domain.Share"));

            // 创建类
            var commParsClass = new CodeTypeDeclaration("CommunicationPars")
            {
                IsClass = true,
                TypeAttributes = System.Reflection.TypeAttributes.Public
            };
            commParsClass.BaseTypes.Add(new CodeTypeReference("IDeviceCommunication"));

            // 添加 XML 注释
            commParsClass.Comments.Add(new CodeCommentStatement("<summary>", true));
            commParsClass.Comments.Add(new CodeCommentStatement("通信参数配置", true));
            commParsClass.Comments.Add(new CodeCommentStatement("</summary>", true));

            // 添加 DeviceCommunications 属性
            AddDeviceCommunicationsProperty(commParsClass);

            codeNamespace.Types.Add(commParsClass);
            codeUnit.Namespaces.Add(codeNamespace);

            // 生成代码文件
            GenerateCodeFile(codeUnit, outputPath);

            _logger.LogInformation($"成功生成 CommunicationPars.cs: {outputPath}");
        }

        /// <summary>
        /// 添加 DeviceCommunications 属性
        /// </summary>
        private void AddDeviceCommunicationsProperty(CodeTypeDeclaration commParsClass)
        {
            // 使用代码片段来生成复杂的初始化逻辑
            var property = new CodeSnippetTypeMember(@"
        public List<DeviceCommunicationDto> DeviceCommunications { get; set; } = new List<DeviceCommunicationDto>
        {
            new DeviceCommunicationDto
            {
                DeviceCommunicationKey = DeviceCommunicationType.Customization.ToString(),
                DeviceCommunicationItems = new List<DeviceCommunicationItem>
                {
                    new DeviceCommunicationItem(""IP"", ""192.168.1.201"",
                        ""IP地址"", """", true, true, true, typeof(string)),
                    new DeviceCommunicationItem(""Port"", 6002,
                        ""端口号"", """", true, true, true, typeof(int))
                }
            }
        };");

            commParsClass.Members.Add(property);
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


