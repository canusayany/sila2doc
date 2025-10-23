using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SilaGeneratorWpf.Services;

namespace Sila2DriverGen.TestConsole
{
    /// <summary>
    /// 测试运行器
    /// </summary>
    public class TestRunner
    {
        private readonly D3DriverOrchestrationService _orchestrationService;
        private string? _lastGeneratedProjectPath;

        public TestRunner()
        {
            _orchestrationService = new D3DriverOrchestrationService();
        }

        public async Task RunAsync()
        {
            bool exit = false;

            while (!exit)
            {
                var choice = ConsoleHelper.ShowMenu(
                    "═══ D3驱动生成功能测试 ═══",
                    new[]
                    {
                        "测试1：从本地XML生成D3项目",
                        "测试2：编译已生成的D3项目",
                        "测试3：完整流程（生成+编译）",
                        "测试4：调整方法分类并重新生成",
                        "测试5：错误处理测试（无效文件）",
                        "测试6：错误处理测试（编译失败）",
                        "测试7：查看测试说明",
                        "退出"
                    });

                switch (choice)
                {
                    case 1:
                        await TestGenerateFromLocalXmlAsync();
                        break;
                    case 2:
                        await TestCompileProjectAsync();
                        break;
                    case 3:
                        await TestCompleteWorkflowAsync();
                        break;
                    case 4:
                        await TestAdjustMethodClassificationsAsync();
                        break;
                    case 5:
                        await TestErrorHandling_InvalidFileAsync();
                        break;
                    case 6:
                        await TestErrorHandling_CompilationFailureAsync();
                        break;
                    case 7:
                        ShowTestInstructions();
                        break;
                    case 8:
                        exit = true;
                        break;
                    default:
                        ConsoleHelper.PrintError("无效的选择，请重试");
                        break;
                }

                if (!exit)
                {
                    Console.WriteLine("\n按任意键继续...");
                    Console.ReadKey();
                    Console.Clear();
                    ConsoleHelper.PrintHeader();
                }
            }
        }

        /// <summary>
        /// 测试1：从本地XML生成D3项目
        /// </summary>
        private async Task TestGenerateFromLocalXmlAsync()
        {
            ConsoleHelper.PrintSection("测试1：从本地XML生成D3项目");
            Console.WriteLine();

            // 查找示例XML文件
            var workspaceRoot = Directory.GetCurrentDirectory();
            var searchPaths = new[]
            {
                Path.Combine(workspaceRoot, "TemperatureController-v1_0.sila.xml"),
                Path.Combine(workspaceRoot, "..", "SilaGeneratorWpf", "TemperatureController-v1_0.sila.xml"),
                Path.Combine(workspaceRoot, "..", "..", "TemperatureController-v1_0.sila.xml"),
                Path.Combine(workspaceRoot, "..", "..", "..", "TemperatureController-v1_0.sila.xml")
            };

            string? xmlPath = null;
            foreach (var path in searchPaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    xmlPath = fullPath;
                    break;
                }
            }

            if (xmlPath == null)
            {
                ConsoleHelper.PrintError("未找到示例XML文件: TemperatureController-v1_0.sila.xml");
                ConsoleHelper.PrintWarning("请将 TemperatureController-v1_0.sila.xml 文件放在项目根目录");
                return;
            }

            ConsoleHelper.PrintInfo($"找到XML文件: {Path.GetFileName(xmlPath)}");
            Console.WriteLine();

            // 创建生成请求
            var request = new D3GenerationRequest
            {
                Brand = "TestBrand",
                Model = "TestModel",
                DeviceType = "TestDevice",
                Developer = "Bioyond",
                IsOnlineSource = false,
                LocalFeatureXmlPaths = new List<string> { xmlPath }
            };

            ConsoleHelper.PrintInfo("开始生成D3项目...");
            ConsoleHelper.PrintInfo($"设备信息: {request.Brand} {request.Model} ({request.DeviceType})");
            Console.WriteLine();

            try
            {
                var result = await _orchestrationService.GenerateD3ProjectAsync(
                    request,
                    message => Console.WriteLine(message));

                Console.WriteLine();
                if (result.Success)
                {
                    ConsoleHelper.PrintSuccess("✓ D3项目生成成功！");
                    ConsoleHelper.PrintInfo($"项目路径: {result.ProjectPath}");
                    
                    _lastGeneratedProjectPath = result.ProjectPath;

                    if (result.AnalysisResult != null)
                    {
                        Console.WriteLine();
                        ConsoleHelper.PrintInfo($"检测到 {result.AnalysisResult.Features.Count} 个特性");
                        ConsoleHelper.PrintInfo($"检测到 {result.AnalysisResult.Features.Sum(f => f.Methods.Count)} 个方法");
                    }
                }
                else
                {
                    ConsoleHelper.PrintError($"✗ 生成失败: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintError($"✗ 发生异常: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// 测试2：编译已生成的D3项目
        /// </summary>
        private async Task TestCompileProjectAsync()
        {
            ConsoleHelper.PrintSection("测试2：编译已生成的D3项目");
            Console.WriteLine();

            if (string.IsNullOrEmpty(_lastGeneratedProjectPath))
            {
                ConsoleHelper.PrintWarning("尚未生成项目，请先运行测试1");
                Console.WriteLine();
                Console.Write("或者输入项目路径（留空取消）: ");
                var input = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(input))
                {
                    ConsoleHelper.PrintInfo("已取消");
                    return;
                }

                _lastGeneratedProjectPath = input;
            }

            if (!Directory.Exists(_lastGeneratedProjectPath))
            {
                ConsoleHelper.PrintError($"项目目录不存在: {_lastGeneratedProjectPath}");
                _lastGeneratedProjectPath = null;
                return;
            }

            ConsoleHelper.PrintInfo($"项目路径: {_lastGeneratedProjectPath}");
            Console.WriteLine();

            try
            {
                ConsoleHelper.PrintInfo("开始编译...");
                var result = await _orchestrationService.CompileD3ProjectAsync(
                    _lastGeneratedProjectPath,
                    message => Console.WriteLine(message));

                Console.WriteLine();
                if (result.Success)
                {
                    ConsoleHelper.PrintSuccess("✓ 编译成功！");
                    ConsoleHelper.PrintInfo($"DLL路径: {result.DllPath}");
                    ConsoleHelper.PrintInfo($"警告数: {result.WarningCount}");
                }
                else
                {
                    ConsoleHelper.PrintError($"✗ 编译失败（{result.ErrorCount} 个错误）");
                    if (!string.IsNullOrEmpty(result.Message))
                    {
                        Console.WriteLine();
                        Console.WriteLine(result.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintError($"✗ 发生异常: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// 测试3：完整流程（生成+编译）
        /// </summary>
        private async Task TestCompleteWorkflowAsync()
        {
            ConsoleHelper.PrintSection("测试3：完整流程（生成+编译）");
            Console.WriteLine();

            // 生成
            await TestGenerateFromLocalXmlAsync();

            if (string.IsNullOrEmpty(_lastGeneratedProjectPath))
            {
                ConsoleHelper.PrintError("生成失败，无法继续编译");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine();

            // 编译
            await TestCompileProjectAsync();
        }

        /// <summary>
        /// 测试4：调整方法分类并重新生成
        /// </summary>
        private async Task TestAdjustMethodClassificationsAsync()
        {
            ConsoleHelper.PrintSection("测试4：调整方法分类并重新生成");
            Console.WriteLine();

            if (string.IsNullOrEmpty(_lastGeneratedProjectPath))
            {
                ConsoleHelper.PrintWarning("尚未生成项目，请先运行测试1或测试3");
                Console.WriteLine();
                Console.Write("或者输入项目路径（留空取消）: ");
                var input = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(input))
                {
                    ConsoleHelper.PrintInfo("已取消");
                    return;
                }

                _lastGeneratedProjectPath = input;
            }

            if (!Directory.Exists(_lastGeneratedProjectPath))
            {
                ConsoleHelper.PrintError($"项目目录不存在: {_lastGeneratedProjectPath}");
                _lastGeneratedProjectPath = null;
                return;
            }

            ConsoleHelper.PrintInfo($"项目路径: {_lastGeneratedProjectPath}");
            Console.WriteLine();

            try
            {
                // 创建示例方法分类（将部分方法标记为维护方法）
                ConsoleHelper.PrintInfo("创建示例方法分类...");
                var methodClassifications = new Dictionary<string, bool>
                {
                    // 假设TemperatureController有这些方法
                    { "TemperatureController.GetTemperature", false },  // Operations
                    { "TemperatureController.SetTemperature", false },  // Operations
                    { "TemperatureController.Reset", true },            // Maintenance
                    { "TemperatureController.Calibrate", true }         // Maintenance
                };

                ConsoleHelper.PrintInfo($"将调整 {methodClassifications.Count} 个方法的分类:");
                foreach (var (methodName, isMaintenance) in methodClassifications)
                {
                    var category = isMaintenance ? "维护" : "操作";
                    ConsoleHelper.PrintInfo($"  - {methodName} → {category}");
                }
                Console.WriteLine();

                ConsoleHelper.PrintInfo("开始调整方法分类...");
                var result = await _orchestrationService.AdjustMethodClassificationsAsync(
                    _lastGeneratedProjectPath,
                    methodClassifications,
                    message => Console.WriteLine(message));

                Console.WriteLine();
                if (result.Success)
                {
                    ConsoleHelper.PrintSuccess("✓ 方法分类调整成功！");
                    ConsoleHelper.PrintInfo(result.Message);
                    
                    if (result.AnalysisResult != null)
                    {
                        Console.WriteLine();
                        ConsoleHelper.PrintInfo($"重新分析了 {result.AnalysisResult.Features.Count} 个特性");
                        ConsoleHelper.PrintInfo($"包含 {result.AnalysisResult.Features.Sum(f => f.Methods.Count)} 个方法");
                    }
                }
                else
                {
                    ConsoleHelper.PrintError($"✗ 调整失败: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintError($"✗ 发生异常: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// 测试5：错误处理测试（无效文件）
        /// </summary>
        private async Task TestErrorHandling_InvalidFileAsync()
        {
            ConsoleHelper.PrintSection("测试5：错误处理测试（无效文件）");
            Console.WriteLine();

            ConsoleHelper.PrintInfo("测试场景：使用不存在的XML文件");
            Console.WriteLine();

            var request = new D3GenerationRequest
            {
                Brand = "ErrorTest",
                Model = "InvalidFile",
                DeviceType = "TestDevice",
                Developer = "Bioyond",
                IsOnlineSource = false,
                LocalFeatureXmlPaths = new List<string> { "NonExistentFile.sila.xml" }
            };

            try
            {
                ConsoleHelper.PrintInfo("尝试生成D3项目（预期失败）...");
                var result = await _orchestrationService.GenerateD3ProjectAsync(
                    request,
                    message => Console.WriteLine(message));

                Console.WriteLine();
                if (!result.Success)
                {
                    ConsoleHelper.PrintSuccess("✓ 错误处理正确：成功捕获并报告了无效文件错误");
                    ConsoleHelper.PrintInfo($"错误信息: {result.Message}");
                }
                else
                {
                    ConsoleHelper.PrintError("✗ 错误处理失败：应该报告错误但却成功了");
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintSuccess("✓ 错误处理正确：抛出了异常");
                ConsoleHelper.PrintInfo($"异常信息: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试6：错误处理测试（编译失败场景）
        /// </summary>
        private async Task TestErrorHandling_CompilationFailureAsync()
        {
            ConsoleHelper.PrintSection("测试6：错误处理测试（编译失败）");
            Console.WriteLine();

            ConsoleHelper.PrintInfo("测试场景：尝试编译不存在的项目");
            Console.WriteLine();

            var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistentProject_" + Guid.NewGuid());

            try
            {
                ConsoleHelper.PrintInfo($"尝试编译不存在的项目: {nonExistentPath}");
                var result = await _orchestrationService.CompileD3ProjectAsync(
                    nonExistentPath,
                    message => Console.WriteLine(message));

                Console.WriteLine();
                if (!result.Success)
                {
                    ConsoleHelper.PrintSuccess("✓ 错误处理正确：成功捕获并报告了编译错误");
                    ConsoleHelper.PrintInfo($"错误信息: {result.Message}");
                }
                else
                {
                    ConsoleHelper.PrintError("✗ 错误处理失败：应该报告错误但却成功了");
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintSuccess("✓ 错误处理正确：抛出了异常");
                ConsoleHelper.PrintInfo($"异常信息: {ex.Message}");
            }
        }

        private void ShowTestInstructions()
        {
            ConsoleHelper.PrintSection("测试说明");
            
            Console.WriteLine("本控制台用于测试 D3DriverOrchestrationService 的功能。");
            Console.WriteLine();
            
            Console.WriteLine("【测试1】从本地XML生成D3项目");
            ConsoleHelper.PrintInfo("  - 自动查找 TemperatureController-v1_0.sila.xml 文件");
            ConsoleHelper.PrintInfo("  - 调用 GenerateD3ProjectAsync 生成项目");
            ConsoleHelper.PrintInfo("  - 显示生成结果和项目路径");
            
            Console.WriteLine();
            Console.WriteLine("【测试2】编译已生成的D3项目");
            ConsoleHelper.PrintInfo("  - 使用测试1生成的项目路径");
            ConsoleHelper.PrintInfo("  - 或手动输入项目路径");
            ConsoleHelper.PrintInfo("  - 调用 CompileD3ProjectAsync 编译");
            ConsoleHelper.PrintInfo("  - 显示编译结果和DLL路径");
            
            Console.WriteLine();
            Console.WriteLine("【测试3】完整流程");
            ConsoleHelper.PrintInfo("  - 依次执行测试1和测试2");
            ConsoleHelper.PrintInfo("  - 验证完整的生成到编译流程");
            
            Console.WriteLine();
            Console.WriteLine("【测试4】调整方法分类并重新生成");
            ConsoleHelper.PrintInfo("  - 使用已生成的项目");
            ConsoleHelper.PrintInfo("  - 创建示例方法分类配置");
            ConsoleHelper.PrintInfo("  - 调用 AdjustMethodClassificationsAsync");
            ConsoleHelper.PrintInfo("  - 验证D3Driver.cs文件已更新");
            
            Console.WriteLine();
            Console.WriteLine("【测试5】错误处理测试（无效文件）");
            ConsoleHelper.PrintInfo("  - 使用不存在的XML文件");
            ConsoleHelper.PrintInfo("  - 验证错误捕获和报告机制");
            
            Console.WriteLine();
            Console.WriteLine("【测试6】错误处理测试（编译失败）");
            ConsoleHelper.PrintInfo("  - 尝试编译不存在的项目");
            ConsoleHelper.PrintInfo("  - 验证编译错误处理");
            
            Console.WriteLine();
            Console.WriteLine("【前置条件】");
            ConsoleHelper.PrintWarning("  - 需要 TemperatureController-v1_0.sila.xml 文件");
            ConsoleHelper.PrintWarning("  - 需要安装 .NET 8.0 SDK");
            ConsoleHelper.PrintWarning("  - 需要网络连接（NuGet包还原）");
            
            Console.WriteLine();
            Console.WriteLine("【验证内容】");
            ConsoleHelper.PrintInfo("  ✓ D3DriverOrchestrationService 无UI依赖");
            ConsoleHelper.PrintInfo("  ✓ 客户端代码生成功能");
            ConsoleHelper.PrintInfo("  ✓ 代码分析功能");
            ConsoleHelper.PrintInfo("  ✓ D3驱动代码生成功能");
            ConsoleHelper.PrintInfo("  ✓ 项目编译功能");
            ConsoleHelper.PrintInfo("  ✓ 方法分类调整功能");
            ConsoleHelper.PrintInfo("  ✓ 错误处理和进度回调");
            ConsoleHelper.PrintInfo("  ✓ 边界条件测试");
        }
    }
}
