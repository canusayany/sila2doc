using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SilaGeneratorWpf.Services;

namespace Sila2DriverGen.TestConsole
{
    /// <summary>
    /// 文件结构测试 - 验证生成的D3驱动文件结构是否正确
    /// </summary>
    public class FileStructureTest
    {
        private readonly D3DriverOrchestrationService _orchestrationService;
        private readonly string _testDataPath;

        public FileStructureTest()
        {
            _orchestrationService = new D3DriverOrchestrationService();
            
            // 获取测试数据路径（SilaGeneratorWpf项目中的示例XML）
            var currentDir = Directory.GetCurrentDirectory();
            var solutionDir = Path.GetFullPath(Path.Combine(currentDir, ".."));
            _testDataPath = Path.Combine(solutionDir, "SilaGeneratorWpf");
        }

        /// <summary>
        /// 测试：验证生成的文件结构是否符合预期
        /// </summary>
        public async Task<bool> Test_FileStructureAsync()
        {
            ConsoleHelper.PrintSection("═══ 文件结构验证测试 ═══");
            Console.WriteLine();

            try
            {
                // 1. 准备测试数据
                ConsoleHelper.PrintInfo("步骤 1：准备测试数据");
                var xmlFile = Path.Combine(_testDataPath, "TemperatureController-v1_0.sila.xml");
                
                if (!File.Exists(xmlFile))
                {
                    ConsoleHelper.PrintError($"✗ 测试文件不存在: {xmlFile}");
                    return false;
                }
                ConsoleHelper.PrintSuccess($"✓ 找到测试文件: {Path.GetFileName(xmlFile)}");

                // 2. 生成D3驱动项目
                ConsoleHelper.PrintInfo("\n步骤 2：生成D3驱动项目");
                var request = new D3GenerationRequest
                {
                    Brand = "TestBrand",
                    Model = "TestModel",
                    DeviceType = "SiLA2IntegrationTestServer",
                    Developer = "Bioyond",
                    IsOnlineSource = false,
                    LocalFeatureXmlPaths = new List<string> { xmlFile }
                };

                var result = await _orchestrationService.GenerateD3ProjectAsync(
                    request,
                    message => Console.WriteLine($"  {message}"));

                if (!result.Success)
                {
                    ConsoleHelper.PrintError($"✗ 生成失败: {result.Message}");
                    return false;
                }
                ConsoleHelper.PrintSuccess($"✓ 生成成功: {result.ProjectPath}");

                // 3. 验证文件结构
                ConsoleHelper.PrintInfo("\n步骤 3：验证文件结构");
                if (!VerifyFileStructure(result.ProjectPath!))
                {
                    ConsoleHelper.PrintError("✗ 文件结构验证失败");
                    return false;
                }
                ConsoleHelper.PrintSuccess("✓ 文件结构验证通过");

                // 4. 验证项目可以编译
                ConsoleHelper.PrintInfo("\n步骤 4：验证项目编译");
                var compileResult = await _orchestrationService.CompileD3ProjectAsync(
                    result.ProjectPath!,
                    message => Console.WriteLine($"  {message}"));

                if (!compileResult.Success)
                {
                    ConsoleHelper.PrintError($"✗ 编译失败: {compileResult.Message}");
                    ConsoleHelper.PrintInfo("\n编译输出：");
                    Console.WriteLine(compileResult.Message);
                    return false;
                }
                ConsoleHelper.PrintSuccess($"✓ 编译成功");

                // 5. 清理测试文件（可选）
                ConsoleHelper.PrintInfo("\n步骤 5：保留生成的文件供检查");
                ConsoleHelper.PrintInfo($"  生成路径: {result.ProjectPath}");

                return true;
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintError($"✗ 测试异常: {ex.Message}");
                ConsoleHelper.PrintError($"  堆栈: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 验证文件结构
        /// </summary>
        private bool VerifyFileStructure(string projectPath)
        {
            bool allValid = true;

            Console.WriteLine();
            ConsoleHelper.PrintInfo("验证文件结构：");

            // 预期的命名空间
            var expectedNamespace = "BR.ECS.DeviceDriver.SiLA2IntegrationTestServer.TestBrand_TestModel";
            
            // 1. 验证根目录存在
            if (!Directory.Exists(projectPath))
            {
                ConsoleHelper.PrintError($"  ✗ 根目录不存在: {projectPath}");
                return false;
            }
            ConsoleHelper.PrintSuccess($"  ✓ 根目录存在");

            // 2. 验证解决方案文件
            var slnFile = Path.Combine(projectPath, $"{expectedNamespace}.sln");
            if (!File.Exists(slnFile))
            {
                ConsoleHelper.PrintError($"  ✗ 解决方案文件不存在: {Path.GetFileName(slnFile)}");
                allValid = false;
            }
            else
            {
                ConsoleHelper.PrintSuccess($"  ✓ 解决方案文件: {Path.GetFileName(slnFile)}");
            }

            // 3. 验证主项目文件夹
            var projectDir = Path.Combine(projectPath, expectedNamespace);
            if (!Directory.Exists(projectDir))
            {
                ConsoleHelper.PrintError($"  ✗ 主项目文件夹不存在: {expectedNamespace}");
                ConsoleHelper.PrintInfo($"    实际存在的文件夹:");
                foreach (var dir in Directory.GetDirectories(projectPath))
                {
                    ConsoleHelper.PrintInfo($"      - {Path.GetFileName(dir)}");
                }
                allValid = false;
            }
            else
            {
                ConsoleHelper.PrintSuccess($"  ✓ 主项目文件夹: {expectedNamespace}");

                // 4. 验证主项目文件
                var csprojFile = Path.Combine(projectDir, $"{expectedNamespace}.csproj");
                if (!File.Exists(csprojFile))
                {
                    ConsoleHelper.PrintError($"  ✗ 项目文件不存在: {Path.GetFileName(csprojFile)}");
                    allValid = false;
                }
                else
                {
                    ConsoleHelper.PrintSuccess($"  ✓ 项目文件: {Path.GetFileName(csprojFile)}");
                }

                // 5. 验证核心文件
                var coreFiles = new[] { "AllSila2Client.cs", "D3Driver.cs", "Sila2Base.cs", "CommunicationPars.cs" };
                foreach (var file in coreFiles)
                {
                    var filePath = Path.Combine(projectDir, file);
                    if (!File.Exists(filePath))
                    {
                        ConsoleHelper.PrintError($"  ✗ 核心文件不存在: {file}");
                        allValid = false;
                    }
                    else
                    {
                        ConsoleHelper.PrintSuccess($"  ✓ 核心文件: {file}");
                    }
                }

                // 6. 验证Sila2Client文件夹（关键！）
                var sila2ClientDir = Path.Combine(projectDir, "Sila2Client");
                if (!Directory.Exists(sila2ClientDir))
                {
                    ConsoleHelper.PrintError($"  ✗ Sila2Client文件夹不存在");
                    ConsoleHelper.PrintInfo($"    项目文件夹内容:");
                    foreach (var item in Directory.GetFileSystemEntries(projectDir))
                    {
                        ConsoleHelper.PrintInfo($"      - {Path.GetFileName(item)}");
                    }
                    allValid = false;
                }
                else
                {
                    ConsoleHelper.PrintSuccess($"  ✓ Sila2Client文件夹存在");

                    // 验证客户端代码文件
                    var clientFiles = Directory.GetFiles(sila2ClientDir, "*.cs");
                    if (clientFiles.Length == 0)
                    {
                        ConsoleHelper.PrintError($"  ✗ Sila2Client文件夹为空");
                        allValid = false;
                    }
                    else
                    {
                        ConsoleHelper.PrintSuccess($"  ✓ Sila2Client包含 {clientFiles.Length} 个文件");
                        foreach (var file in clientFiles)
                        {
                            ConsoleHelper.PrintInfo($"      - {Path.GetFileName(file)}");
                        }
                    }
                }

                // 7. 验证lib文件夹
                var libDir = Path.Combine(projectDir, "lib");
                if (!Directory.Exists(libDir))
                {
                    ConsoleHelper.PrintWarning($"  ⚠ lib文件夹不存在（依赖库可能未复制）");
                }
                else
                {
                    var libFiles = Directory.GetFiles(libDir, "*.dll");
                    ConsoleHelper.PrintSuccess($"  ✓ lib文件夹包含 {libFiles.Length} 个DLL");
                }
            }

            // 8. 验证TestConsole文件夹
            var testConsoleDir = Path.Combine(projectPath, "TestConsole");
            if (!Directory.Exists(testConsoleDir))
            {
                ConsoleHelper.PrintError($"  ✗ TestConsole文件夹不存在");
                allValid = false;
            }
            else
            {
                ConsoleHelper.PrintSuccess($"  ✓ TestConsole文件夹存在");

                var testCsproj = Path.Combine(testConsoleDir, $"{expectedNamespace}.Test.csproj");
                if (!File.Exists(testCsproj))
                {
                    ConsoleHelper.PrintError($"  ✗ 测试项目文件不存在");
                    allValid = false;
                }
                else
                {
                    ConsoleHelper.PrintSuccess($"  ✓ 测试项目文件存在");
                }
            }

            // 9. 验证不应该存在的GeneratedClient文件夹
            var generatedClientDir = Path.Combine(projectPath, "GeneratedClient");
            if (Directory.Exists(generatedClientDir))
            {
                ConsoleHelper.PrintWarning($"  ⚠ GeneratedClient文件夹仍然存在（应该已废弃）");
            }
            else
            {
                ConsoleHelper.PrintSuccess($"  ✓ GeneratedClient文件夹不存在（正确）");
            }

            return allValid;
        }

        /// <summary>
        /// 辅助方法：打印目录树
        /// </summary>
        private void PrintDirectoryTree(string path, string indent = "")
        {
            if (!Directory.Exists(path))
                return;

            var dirName = Path.GetFileName(path);
            Console.WriteLine($"{indent}📁 {dirName}/");

            var files = Directory.GetFiles(path);
            var dirs = Directory.GetDirectories(path);

            foreach (var file in files)
            {
                Console.WriteLine($"{indent}  📄 {Path.GetFileName(file)}");
            }

            foreach (var dir in dirs)
            {
                PrintDirectoryTree(dir, indent + "  ");
            }
        }
    }
}

