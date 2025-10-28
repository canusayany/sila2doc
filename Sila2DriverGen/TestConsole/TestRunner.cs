using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sila2DriverGen.TestConsole
{
    /// <summary>
    /// 交互式测试运行器
    /// </summary>
    public class TestRunner : TestBase
    {
        public async Task RunAsync()
        {
            bool exit = false;

            while (!exit)
            {
                var menuItems = TestInfo.GetAllTests()
                    .Select(t => $"{TestInfo.GetDisplayName(t.Item)}")
                    .Concat(new[] { "查看测试说明", "退出" })
                    .ToArray();

                var choice = ConsoleHelper.ShowMenu("═══ D3驱动生成功能测试 ═══", menuItems);

                if (choice == menuItems.Length)
                {
                    exit = true;
                }
                else if (choice == menuItems.Length - 1)
                {
                    ShowTestInstructions();
                }
                else if (choice >= 1 && choice <= 8)
                {
                    await RunTestAsync((TestItem)choice);
                }
                else
                {
                    ConsoleHelper.PrintError("无效的选择，请重试");
                }

                if (!exit)
                {
                    Console.Clear();
                    ConsoleHelper.PrintHeader();
                }
            }
        }

        private async Task RunTestAsync(TestItem testItem)
        {
            var testName = TestInfo.GetDisplayName(testItem);
            ConsoleHelper.PrintSection(testName);
            Console.WriteLine();

            try
            {
                await (testItem switch
                {
                    TestItem.GenerateFromLocalXml => Test_GenerateFromLocalXmlAsync(),
                    TestItem.CompileProject => Test_CompileProjectAsync(),
                    TestItem.CompleteWorkflow => Test_CompleteWorkflowAsync(),
                    TestItem.AdjustMethodClassifications => Test_AdjustMethodClassificationsAsync(),
                    TestItem.MultipleFeatures => Test_MultipleFeaturesAsync(),
                    TestItem.ErrorHandling_InvalidFile => Test_ErrorHandling_InvalidFileAsync(),
                    TestItem.ErrorHandling_CompilationFailure => Test_ErrorHandling_CompilationFailureAsync(),
                    TestItem.OnlineServer => Test_OnlineServerAsync(),
                    _ => throw new NotImplementedException($"未实现的测试项: {testItem}")
                });
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintError($"✗ 发生异常: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        #region 测试实现

        private async Task Test_GenerateFromLocalXmlAsync()
        {
            var xmlPath = FindXmlFile();
            if (xmlPath == null)
            {
                ConsoleHelper.PrintError("未找到示例XML文件: TemperatureController-v1_0.sila.xml");
                ConsoleHelper.PrintWarning("请将 TemperatureController-v1_0.sila.xml 文件放在项目根目录");
                return;
            }

            ConsoleHelper.PrintInfo($"找到XML文件: {Path.GetFileName(xmlPath)}");
            Console.WriteLine();

            var request = CreateTestRequest("TestBrand", "TestModel", "TestDevice", new List<string> { xmlPath });

            ConsoleHelper.PrintInfo("开始生成D3项目...");
            ConsoleHelper.PrintInfo($"设备信息: {request.Brand} {request.Model} ({request.DeviceType})");
            Console.WriteLine();

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

        private async Task Test_CompileProjectAsync()
        {
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

            if (!ValidateProjectPath(_lastGeneratedProjectPath))
            {
                _lastGeneratedProjectPath = null;
                return;
            }

            ConsoleHelper.PrintInfo($"项目路径: {_lastGeneratedProjectPath}");
            Console.WriteLine();

            ConsoleHelper.PrintInfo("开始编译...");
            var result = await _orchestrationService.CompileD3ProjectAsync(
                _lastGeneratedProjectPath!,
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

        private async Task Test_CompleteWorkflowAsync()
        {
            await Test_GenerateFromLocalXmlAsync();

            if (string.IsNullOrEmpty(_lastGeneratedProjectPath))
            {
                ConsoleHelper.PrintError("生成失败，无法继续编译");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine();

            await Test_CompileProjectAsync();
        }

        private async Task Test_AdjustMethodClassificationsAsync()
        {
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

            if (!ValidateProjectPath(_lastGeneratedProjectPath))
            {
                _lastGeneratedProjectPath = null;
                return;
            }

            ConsoleHelper.PrintInfo($"项目路径: {_lastGeneratedProjectPath}");
            Console.WriteLine();

            ConsoleHelper.PrintInfo("创建示例方法分类...");
            var methodClassifications = new Dictionary<string, bool>
            {
                { "TemperatureController.GetTemperature", false },
                { "TemperatureController.SetTemperature", false },
                { "TemperatureController.Reset", true },
                { "TemperatureController.Calibrate", true }
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
                _lastGeneratedProjectPath!,
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

        private async Task Test_MultipleFeaturesAsync()
        {
            var foundXmlFiles = FindAllXmlFiles();

            if (!foundXmlFiles.Any())
            {
                ConsoleHelper.PrintError("未找到任何.sila.xml文件");
                return;
            }

            ConsoleHelper.PrintInfo($"找到 {foundXmlFiles.Count} 个XML文件：");
            for (int i = 0; i < foundXmlFiles.Count && i < 5; i++)
            {
                ConsoleHelper.PrintInfo($"  {i + 1}. {Path.GetFileName(foundXmlFiles[i])}");
            }
            
            var selectedFiles = foundXmlFiles.Take(Math.Min(2, foundXmlFiles.Count)).ToList();
            Console.WriteLine();
            ConsoleHelper.PrintInfo($"使用 {selectedFiles.Count} 个特性进行测试");

            var request = CreateTestRequest("MultiTest", "Device01", "MultiFeature", selectedFiles);

            ConsoleHelper.PrintInfo("开始完整流程测试...");
            ConsoleHelper.PrintInfo($"设备信息: {request.Brand} {request.Model} ({request.DeviceType})");
            ConsoleHelper.PrintInfo($"特性数量: {selectedFiles.Count}");
            Console.WriteLine();

            ConsoleHelper.PrintInfo("[1/2] 生成D3项目...");
            var result = await _orchestrationService.GenerateD3ProjectAsync(
                request,
                message => Console.WriteLine($"  {message}"));

            Console.WriteLine();
            if (!result.Success)
            {
                ConsoleHelper.PrintError($"✗ 生成失败: {result.Message}");
                return;
            }

            ConsoleHelper.PrintSuccess("✓ D3项目生成成功！");
            ConsoleHelper.PrintInfo($"项目路径: {result.ProjectPath}");
            _lastGeneratedProjectPath = result.ProjectPath;

            if (result.AnalysisResult != null)
            {
                Console.WriteLine();
                ConsoleHelper.PrintInfo($"检测到 {result.AnalysisResult.Features.Count} 个特性");
                ConsoleHelper.PrintInfo($"检测到 {result.AnalysisResult.Features.Sum(f => f.Methods.Count)} 个方法");
                
                foreach (var feature in result.AnalysisResult.Features)
                {
                    ConsoleHelper.PrintInfo($"  特性: {feature.FeatureName}");
                    ConsoleHelper.PrintInfo($"    方法数: {feature.Methods.Count}");
                }
            }

            Console.WriteLine();
            ConsoleHelper.PrintInfo("[2/2] 编译项目...");
            var compileResult = await _orchestrationService.CompileD3ProjectAsync(
                result.ProjectPath!,
                message => Console.WriteLine($"  {message}"));

            Console.WriteLine();
            if (compileResult.Success)
            {
                ConsoleHelper.PrintSuccess("✓ 编译成功！");
                ConsoleHelper.PrintInfo($"DLL路径: {compileResult.DllPath}");
                ConsoleHelper.PrintInfo($"警告数: {compileResult.WarningCount}");
                
                Console.WriteLine();
                ConsoleHelper.PrintSuccess("══════════════════════════════");
                ConsoleHelper.PrintSuccess("  ✓ 完整流程测试全部通过！  ");
                ConsoleHelper.PrintSuccess("══════════════════════════════");
            }
            else
            {
                ConsoleHelper.PrintError($"✗ 编译失败（{compileResult.ErrorCount} 个错误）");
                if (!string.IsNullOrEmpty(compileResult.Message))
                {
                    Console.WriteLine();
                    Console.WriteLine(compileResult.Message);
                }
            }
        }

        private async Task Test_ErrorHandling_InvalidFileAsync()
        {
            ConsoleHelper.PrintInfo("测试场景：使用不存在的XML文件");
            Console.WriteLine();

            var request = CreateTestRequest("ErrorTest", "InvalidFile", "TestDevice", 
                new List<string> { "NonExistentFile.sila.xml" });

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

        private async Task Test_ErrorHandling_CompilationFailureAsync()
        {
            ConsoleHelper.PrintInfo("测试场景：尝试编译不存在的项目");
            Console.WriteLine();

            var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistentProject_" + Guid.NewGuid());

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

        private async Task Test_OnlineServerAsync()
        {
            ConsoleHelper.PrintInfo("测试在线服务器完整流程...");
            ConsoleHelper.PrintInfo("正在扫描SiLA2服务器（3秒超时）...");
            Console.WriteLine();

            try
            {
                var discoveryService = new SilaGeneratorWpf.Services.ServerDiscoveryService();
                var servers = await discoveryService.ScanServersAsync(TimeSpan.FromSeconds(3));

                if (servers == null || servers.Count == 0)
                {
                    ConsoleHelper.PrintWarning("未发现任何SiLA2服务器");
                    Console.WriteLine();
                    ConsoleHelper.PrintInfo("提示：如果需要测试在线服务器功能，请确保：");
                    ConsoleHelper.PrintInfo("  1. 有SiLA2服务器正在运行");
                    ConsoleHelper.PrintInfo("  2. 服务器在同一网络内");
                    ConsoleHelper.PrintInfo("  3. mDNS服务已启用");
                    return;
                }

                ConsoleHelper.PrintSuccess($"✓ 发现 {servers.Count} 个服务器");
                Console.WriteLine();

                var server = servers[0];
                ConsoleHelper.PrintInfo($"使用服务器: {server.ServerName}");
                ConsoleHelper.PrintInfo($"地址: {server.IPAddress}:{server.Port}");
                ConsoleHelper.PrintInfo($"类型: {server.ServerType ?? "Unknown"}");
                Console.WriteLine();

                if (server.Features == null || server.Features.Count == 0)
                {
                    ConsoleHelper.PrintWarning("服务器没有任何特性");
                    return;
                }

                ConsoleHelper.PrintInfo($"服务器包含 {server.Features.Count} 个特性：");
                foreach (var feature in server.Features.Take(10))
                {
                    ConsoleHelper.PrintInfo($"  - {feature.DisplayName}");
                }
                if (server.Features.Count > 10)
                {
                    ConsoleHelper.PrintInfo($"  ... 还有 {server.Features.Count - 10} 个特性");
                }
                Console.WriteLine();

                // 优先从 ViewModel 缓存获取 ServerData
                var serverData = server.ServerDataCache ?? discoveryService.GetServerData(server);
                if (serverData == null)
                {
                    ConsoleHelper.PrintError("无法获取服务器数据");
                    return;
                }

                var features = new Dictionary<string, Tecan.Sila2.Feature>();
                foreach (var feature in serverData.Features)
                {
                    features[feature.Identifier] = feature;
                }

                var request = new SilaGeneratorWpf.Services.D3GenerationRequest
                {
                    Brand = "OnlineTest",
                    Model = server.ServerName.Replace(" ", "_").Replace("-", "_"),
                    DeviceType = server.ServerType ?? "SilaDevice",
                    Developer = "Bioyond",
                    IsOnlineSource = true,
                    ServerUuid = server.Uuid.ToString(),
                    ServerIp = server.IPAddress,
                    ServerPort = server.Port,
                    Features = features
                };

                ConsoleHelper.PrintInfo("[1/2] 开始生成D3项目...");
                ConsoleHelper.PrintInfo($"  品牌: {request.Brand}");
                ConsoleHelper.PrintInfo($"  型号: {request.Model}");
                ConsoleHelper.PrintInfo($"  设备类型: {request.DeviceType}");
                ConsoleHelper.PrintInfo($"  特性数量: {features.Count}");
                Console.WriteLine();

                var result = await _orchestrationService.GenerateD3ProjectAsync(
                    request,
                    message => Console.WriteLine($"  {message}"));

                Console.WriteLine();
                if (!result.Success)
                {
                    ConsoleHelper.PrintError($"✗ 生成失败: {result.Message}");
                    return;
                }

                ConsoleHelper.PrintSuccess("✓ D3项目生成成功！");
                ConsoleHelper.PrintInfo($"项目路径: {result.ProjectPath}");
                _lastGeneratedProjectPath = result.ProjectPath;

                if (result.AnalysisResult != null)
                {
                    Console.WriteLine();
                    ConsoleHelper.PrintInfo($"检测到 {result.AnalysisResult.Features.Count} 个特性");
                }

                Console.WriteLine();
                ConsoleHelper.PrintInfo("[2/2] 编译项目...");
                var compileResult = await _orchestrationService.CompileD3ProjectAsync(
                    result.ProjectPath!,
                    message => Console.WriteLine($"  {message}"));

                Console.WriteLine();
                if (compileResult.Success)
                {
                    ConsoleHelper.PrintSuccess("✓ 编译成功！");
                    ConsoleHelper.PrintInfo($"DLL路径: {compileResult.DllPath}");
                    
                    Console.WriteLine();
                    ConsoleHelper.PrintSuccess("══════════════════════════════");
                    ConsoleHelper.PrintSuccess("  ✓ 在线服务器测试全部通过！  ");
                    ConsoleHelper.PrintSuccess("══════════════════════════════");
                }
                else
                {
                    ConsoleHelper.PrintWarning($"编译失败（{compileResult.ErrorCount} 个错误）");
                    ConsoleHelper.PrintWarning("这可能是生成的客户端代码的问题，但项目生成本身成功");
                    ConsoleHelper.PrintInfo($"项目路径: {result.ProjectPath}");
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintError($"✗ 发生异常: {ex.Message}");
            }
        }

        #endregion

        private void ShowTestInstructions()
        {
            ConsoleHelper.PrintSection("测试说明");
            Console.WriteLine();
            
            Console.WriteLine("本控制台用于测试 D3DriverOrchestrationService 的功能。");
            Console.WriteLine();

            var testsByCategory = TestInfo.GetAllTests().GroupBy(t => t.Category);

            foreach (var category in testsByCategory)
            {
                Console.WriteLine($"【{GetCategoryName(category.Key)}】");
                foreach (var test in category)
                {
                    Console.WriteLine($"  {TestInfo.GetDisplayName(test.Item)}");
                    ConsoleHelper.PrintInfo($"    {test.Description}");
                    if (test.RequiresPrerequisite && test.Prerequisite.HasValue)
                    {
                        ConsoleHelper.PrintWarning($"    前置条件：需要先运行 {TestInfo.GetDisplayName(test.Prerequisite.Value)}");
                    }
                }
                Console.WriteLine();
            }
            
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
            
            Console.WriteLine();
        }

        private string GetCategoryName(TestCategory category)
        {
            return category switch
            {
                TestCategory.Basic => "基础功能测试",
                TestCategory.Integration => "集成测试",
                TestCategory.ErrorHandling => "错误处理测试",
                TestCategory.OnlineServer => "在线服务器测试",
                _ => category.ToString()
            };
        }
    }
}
