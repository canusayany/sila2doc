using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using Microsoft.CSharp;
using Microsoft.Extensions.Logging;
using SilaGeneratorWpf.Models;

namespace SilaGeneratorWpf.Services.CodeDom
{
    /// <summary>
    /// 测试控制台生成器 - 生成简单的测试壳子程序
    /// </summary>
    public class TestConsoleGenerator
    {
        private readonly ILogger _logger;

        public TestConsoleGenerator()
        {
            _logger = LoggerService.GetLogger<TestConsoleGenerator>();
        }

        /// <summary>
        /// 生成测试控制台项目
        /// </summary>
        public void Generate(string outputPath, D3DriverGenerationConfig config)
        {
            _logger.LogInformation("开始生成测试控制台项目");

            // 创建测试控制台目录
            var testConsoleDir = Path.Combine(outputPath, "TestConsole");
            Directory.CreateDirectory(testConsoleDir);

            // 生成 Program.cs
            GenerateProgramFile(testConsoleDir, config);

            // 生成项目文件
            GenerateProjectFile(testConsoleDir, config);

            _logger.LogInformation($"成功生成测试控制台项目: {testConsoleDir}");
        }

        /// <summary>
        /// 生成 Program.cs
        /// </summary>
        private void GenerateProgramFile(string testConsoleDir, D3DriverGenerationConfig config)
        {
            var codeUnit = new CodeCompileUnit();
            var codeNamespace = new CodeNamespace("TestConsole");

            // 添加导入命名空间
            codeNamespace.Imports.Add(new CodeNamespaceImport("System"));
            codeNamespace.Imports.Add(new CodeNamespaceImport(config.Namespace));

            // 创建 Program 类
            var programClass = new CodeTypeDeclaration("Program")
            {
                IsClass = true,
                TypeAttributes = System.Reflection.TypeAttributes.NotPublic
            };

            // 添加 Main 方法
            AddMainMethod(programClass, config);

            codeNamespace.Types.Add(programClass);
            codeUnit.Namespaces.Add(codeNamespace);

            // 生成代码文件
            var outputPath = Path.Combine(testConsoleDir, "Program.cs");
            GenerateCodeFile(codeUnit, outputPath);

            _logger.LogInformation($"生成 Program.cs: {outputPath}");
        }

        /// <summary>
        /// 添加 Main 方法
        /// </summary>
        private void AddMainMethod(CodeTypeDeclaration programClass, D3DriverGenerationConfig config)
        {
            var method = new CodeMemberMethod
            {
                Name = "Main",
                Attributes = MemberAttributes.Static | MemberAttributes.Private
            };
            method.Parameters.Add(new CodeParameterDeclarationExpression(typeof(string[]), "args"));

            method.Comments.Add(new CodeCommentStatement("<summary>", true));
            method.Comments.Add(new CodeCommentStatement("测试控制台程序入口", true));
            method.Comments.Add(new CodeCommentStatement("</summary>", true));

            // 方法体
            method.Statements.Add(new CodeSnippetStatement(@$"
            Console.WriteLine(""=== {config.Brand} {config.Model} D3驱动测试控制台 ==="");
            Console.WriteLine(""此为测试壳子程序，请在此基础上添加测试逻辑。"");
            Console.WriteLine();
            
            try
            {{
                // TODO: 添加测试逻辑
                // var driver = new D3Driver();
                // driver.UpdateDeviceInfo();
                // driver.Connect();
                // ... 调用驱动方法 ...
                // driver.Disconnect();
                
                Console.WriteLine(""测试完成。"");
            }}
            catch (Exception ex)
            {{
                Console.WriteLine($""发生错误: {{ex.Message}}"");
                Console.WriteLine(ex.StackTrace);
            }}
            
            Console.WriteLine();
            Console.WriteLine(""按任意键退出..."");
            Console.ReadKey();"));

            programClass.Members.Add(method);
        }

        /// <summary>
        /// 生成项目文件
        /// </summary>
        private void GenerateProjectFile(string testConsoleDir, D3DriverGenerationConfig config)
        {
            var projectFileName = $"{config.Brand}{config.Model}.TestConsole.csproj";
            var projectPath = Path.Combine(testConsoleDir, projectFileName);

            var driverProjectName = $"{config.Brand}{config.Model}.D3Driver.csproj";
            var driverProjectPath = $"..\\{driverProjectName}";

            var projectContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include=""{driverProjectPath}"" />
  </ItemGroup>

</Project>
";

            File.WriteAllText(projectPath, projectContent);
            _logger.LogInformation($"生成项目文件: {projectPath}");
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


