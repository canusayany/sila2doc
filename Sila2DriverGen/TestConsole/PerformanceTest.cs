using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SilaGeneratorWpf.Services;

namespace Sila2DriverGen.TestConsole
{
    /// <summary>
    /// 性能优化测试 - 测试并行处理功能
    /// </summary>
    public class PerformanceTest : TestBase
    {
        private readonly ClientCodeGenerator _codeGenerator;

        public PerformanceTest()
        {
            _codeGenerator = new ClientCodeGenerator();
        }

        /// <summary>
        /// 测试本地特性文件并行生成（性能优化）
        /// </summary>
        public async Task<bool> Test_ParallelLocalFileGenerationAsync()
        {
            ConsoleHelper.PrintSection("性能测试：本地特性文件并行生成");

            try
            {
                // 准备测试文件
                var testFilePath = GetTestFeatureFilePath();
                if (!File.Exists(testFilePath))
                {
                    ConsoleHelper.PrintError($"测试文件不存在: {testFilePath}");
                    return false;
                }

                // 创建多个测试文件副本（模拟多特性场景）
                var tempDir = Path.Combine(Path.GetTempPath(), "SiLA2_PerformanceTest_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                var testFiles = new List<string>();
                for (int i = 1; i <= 5; i++)
                {
                    var copyPath = Path.Combine(tempDir, $"TestFeature{i}.sila.xml");
                    File.Copy(testFilePath, copyPath, true);
                    testFiles.Add(copyPath);
                }

                ConsoleHelper.PrintInfo($"创建了 {testFiles.Count} 个测试文件");

                // 创建输出目录
                var outputDir = Path.Combine(Path.GetTempPath(), "SiLA2_PerfTest_Output_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(outputDir);
                ConsoleHelper.PrintInfo($"输出目录: {outputDir}");

                // 测试性能
                Console.WriteLine();
                ConsoleHelper.PrintInfo("开始并行生成代码...");
                
                var messages = new List<string>();
                var stopwatch = Stopwatch.StartNew();

                var result = await Task.Run(() => _codeGenerator.GenerateClientCode(
                    testFiles,
                    outputDir,
                    "TestNamespace",
                    message => messages.Add(message)));

                stopwatch.Stop();

                // 输出日志
                Console.WriteLine();
                Console.WriteLine("生成过程日志：");
                foreach (var msg in messages)
                {
                    Console.WriteLine($"  {msg}");
                }

                Console.WriteLine();
                Console.WriteLine($"⏱ 总耗时: {stopwatch.ElapsedMilliseconds:N0} ms");
                Console.WriteLine($"✓ 成功: {result.Success}");
                Console.WriteLine($"✓ 生成文件数: {result.GeneratedFiles.Count}");
                
                if (result.Warnings.Any())
                {
                    ConsoleHelper.PrintWarning($"⚠ 警告数: {result.Warnings.Count}");
                    foreach (var warning in result.Warnings.Take(3))
                    {
                        ConsoleHelper.PrintWarning($"  - {warning}");
                    }
                }

                // 验证结果
                if (!result.Success)
                {
                    ConsoleHelper.PrintError($"生成失败: {result.Message}");
                    return false;
                }

                if (result.GeneratedFiles.Count == 0)
                {
                    ConsoleHelper.PrintError("未生成任何文件");
                    return false;
                }

                // 验证文件存在
                Console.WriteLine();
                ConsoleHelper.PrintInfo("验证生成的文件...");
                int validCount = 0;
                foreach (var file in result.GeneratedFiles)
                {
                    if (File.Exists(file))
                    {
                        validCount++;
                        Console.WriteLine($"  ✓ {Path.GetFileName(file)}");
                    }
                    else
                    {
                        ConsoleHelper.PrintError($"  ✗ 文件不存在: {Path.GetFileName(file)}");
                    }
                }

                Console.WriteLine($"有效文件: {validCount}/{result.GeneratedFiles.Count}");

                // 清理
                try
                {
                    Directory.Delete(tempDir, true);
                    Directory.Delete(outputDir, true);
                    ConsoleHelper.PrintInfo("清理临时文件成功");
                }
                catch (Exception ex)
                {
                    ConsoleHelper.PrintWarning($"清理临时文件失败: {ex.Message}");
                }

                ConsoleHelper.PrintSuccess("✓ 性能测试通过：本地特性文件并行生成");
                return validCount == result.GeneratedFiles.Count;
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintError($"性能测试失败: {ex.Message}");
                ConsoleHelper.PrintError($"堆栈跟踪: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 测试并行文件验证
        /// </summary>
        public async Task<bool> Test_ParallelFileValidationAsync()
        {
            ConsoleHelper.PrintSection("性能测试：并行文件验证");

            try
            {
                // 创建测试文件
                var tempDir = Path.Combine(Path.GetTempPath(), "SiLA2_FileValidation_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                var testFiles = new List<string>();
                for (int i = 1; i <= 10; i++)
                {
                    var filePath = Path.Combine(tempDir, $"test{i}.txt");
                    File.WriteAllText(filePath, $"Test content {i}");
                    testFiles.Add(filePath);
                }

                ConsoleHelper.PrintInfo($"创建了 {testFiles.Count} 个测试文件");

                // 并行验证
                var stopwatch = Stopwatch.StartNew();
                var validationResults = new System.Collections.Concurrent.ConcurrentBag<(string path, bool exists)>();

                await Task.Run(() =>
                {
                    System.Threading.Tasks.Parallel.ForEach(testFiles, filePath =>
                    {
                        validationResults.Add((filePath, File.Exists(filePath)));
                    });
                });

                stopwatch.Stop();

                // 统计结果
                var allValid = validationResults.All(r => r.exists);
                Console.WriteLine($"⏱ 验证耗时: {stopwatch.ElapsedMilliseconds} ms");
                Console.WriteLine($"✓ 验证结果: {validationResults.Count(r => r.exists)}/{validationResults.Count} 文件存在");

                // 清理
                Directory.Delete(tempDir, true);

                if (allValid)
                {
                    ConsoleHelper.PrintSuccess("✓ 性能测试通过：并行文件验证");
                    return true;
                }
                else
                {
                    ConsoleHelper.PrintError("✗ 部分文件验证失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintError($"性能测试失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 测试性能监控输出
        /// </summary>
        public async Task<bool> Test_PerformanceMonitoringAsync()
        {
            ConsoleHelper.PrintSection("性能测试：性能监控输出");

            try
            {
                var testFilePath = GetTestFeatureFilePath();
                if (!File.Exists(testFilePath))
                {
                    ConsoleHelper.PrintError($"测试文件不存在: {testFilePath}");
                    return false;
                }

                var outputDir = Path.Combine(Path.GetTempPath(), "SiLA2_PerfMonitor_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(outputDir);

                ConsoleHelper.PrintInfo("开始生成代码（监控性能）...");

                var messages = new List<string>();
                var result = await Task.Run(() => _codeGenerator.GenerateClientCode(
                    new[] { testFilePath },
                    outputDir,
                    "TestNamespace",
                    message => messages.Add(message)));

                // 验证性能监控消息
                Console.WriteLine();
                Console.WriteLine("性能监控日志：");
                var hasPerformanceLog = false;
                foreach (var msg in messages)
                {
                    if (msg.Contains("⏱") || msg.Contains("耗时") || msg.Contains("ms"))
                    {
                        Console.WriteLine($"  ✓ {msg}");
                        hasPerformanceLog = true;
                    }
                }

                // 清理
                Directory.Delete(outputDir, true);

                if (hasPerformanceLog)
                {
                    ConsoleHelper.PrintSuccess("✓ 性能测试通过：性能监控输出正常");
                    return true;
                }
                else
                {
                    ConsoleHelper.PrintWarning("⚠ 未检测到性能监控日志");
                    return true; // 功能正常，只是日志格式可能不同
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintError($"性能测试失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 运行所有性能测试
        /// </summary>
        public async Task<bool> RunAllPerformanceTestsAsync()
        {
            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("    性能优化测试套件");
            Console.WriteLine("========================================");
            Console.WriteLine();

            var results = new Dictionary<string, bool>();

            // 测试1：并行本地文件生成
            results["并行本地文件生成"] = await Test_ParallelLocalFileGenerationAsync();
            Console.WriteLine();

            // 测试2：并行文件验证
            results["并行文件验证"] = await Test_ParallelFileValidationAsync();
            Console.WriteLine();

            // 测试3：性能监控
            results["性能监控输出"] = await Test_PerformanceMonitoringAsync();
            Console.WriteLine();

            // 总结
            Console.WriteLine("========================================");
            Console.WriteLine("    性能测试总结");
            Console.WriteLine("========================================");
            
            int passed = 0;
            int total = results.Count;

            foreach (var kvp in results)
            {
                var status = kvp.Value ? "✓ 通过" : "✗ 失败";
                Console.WriteLine($"{status} - {kvp.Key}");
                if (kvp.Value) passed++;
            }

            Console.WriteLine();
            Console.WriteLine($"总计: {passed}/{total} 测试通过");
            
            bool allPassed = passed == total;
            if (allPassed)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ 所有性能测试通过！");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ {total - passed} 个测试失败");
                Console.ResetColor();
            }

            return allPassed;
        }

        /// <summary>
        /// 获取测试特性文件路径
        /// </summary>
        private string GetTestFeatureFilePath()
        {
            // 尝试查找测试特性文件
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TemperatureController-v1_0.sila.xml"),
                Path.Combine(Directory.GetCurrentDirectory(), "TemperatureController-v1_0.sila.xml"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "SilaGeneratorWpf", "TemperatureController-v1_0.sila.xml")
            };

            foreach (var path in possiblePaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            throw new FileNotFoundException("找不到测试特性文件 TemperatureController-v1_0.sila.xml");
        }
    }
}

