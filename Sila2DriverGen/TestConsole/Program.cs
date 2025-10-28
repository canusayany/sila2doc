using System;
using System.Threading.Tasks;

namespace Sila2DriverGen.TestConsole
{
    /// <summary>
    /// SiLA2 D3驱动生成工具功能测试控制台
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            ConsoleHelper.PrintHeader();

            // 检查是否使用文件结构测试模式
            if (args.Length > 0 && args[0].ToLower() == "--filestructure")
            {
                Console.WriteLine("运行文件结构测试...");
                Console.WriteLine();
                
                var fileStructureTest = new FileStructureTest();
                var success = await fileStructureTest.Test_FileStructureAsync();
                
                Console.WriteLine();
                Console.WriteLine($"文件结构测试结果: {(success ? "✓ 通过" : "✗ 失败")}");
                Environment.Exit(success ? 0 : 1);
                return;
            }
            
            // 检查是否验证UI生成
            if (args.Length > 0 && args[0].ToLower() == "--verifyui")
            {
                Console.WriteLine("验证最新的UI生成项目...");
                Console.WriteLine();
                
                var uiTest = new UIGenerationTest();
                var success = uiTest.Test_VerifyLatestGeneration();
                
                Console.WriteLine();
                Console.WriteLine($"验证结果: {(success ? "✓ 通过" : "✗ 失败")}");
                
                if (!success)
                {
                    Console.WriteLine();
                    Console.WriteLine("可能的原因：");
                    Console.WriteLine("  1. 项目是在代码修改前生成的");
                    Console.WriteLine("  2. WPF应用未重启，仍在使用旧代码");
                    Console.WriteLine();
                    Console.WriteLine("请重启WPF应用后重新生成项目");
                }
                
                Environment.Exit(success ? 0 : 1);
                return;
            }
            
            // 显示UI测试说明
            if (args.Length > 0 && args[0].ToLower() == "--uihelp")
            {
                var uiTest = new UIGenerationTest();
                uiTest.Test_UIGeneration_Instructions();
                return;
            }
            
            // 检查是否使用自动化测试模式
            if (args.Length > 0 && args[0].ToLower() == "--auto")
            {
                Console.WriteLine("运行自动化测试模式...");
                Console.WriteLine();
                
                var autoTest = new AutomatedTest();
                var success = await autoTest.RunAllTestsAsync();
                
                Console.WriteLine();
                Console.WriteLine($"自动化测试结果: {(success ? "通过" : "失败")}");
                //Console.WriteLine("\n按任意键退出...");
                //Console.ReadKey();
                Environment.Exit(success ? 0 : 1);
                return;
            }
            
            // 检查是否运行性能测试
            if (args.Length > 0 && args[0].ToLower() == "--performance")
            {
                Console.WriteLine("运行性能优化测试...");
                Console.WriteLine();
                
                var perfTest = new PerformanceTest();
                var success = await perfTest.RunAllPerformanceTestsAsync();
                
                Console.WriteLine();
                Console.WriteLine($"性能测试结果: {(success ? "✓ 通过" : "✗ 失败")}");
                Environment.Exit(success ? 0 : 1);
                return;
            }
            
            // 检查是否运行日志测试
            if (args.Length > 0 && args[0].ToLower() == "--logging")
            {
                Console.WriteLine("运行日志系统测试...");
                Console.WriteLine();
                
                var loggingTest = new LoggingTest();
                var success = await loggingTest.Test_LoggingSystemAsync();
                
                Console.WriteLine();
                Console.WriteLine($"日志测试结果: {(success ? "✓ 通过" : "✗ 失败")}");
                Environment.Exit(success ? 0 : 1);
                return;
            }
            
            // 检查是否运行代码清理测试
            if (args.Length > 0 && args[0].ToLower() == "--cleanup")
            {
                Console.WriteLine("运行代码清理验证测试...");
                Console.WriteLine();
                
                var cleanupTest = new CodeCleanupTest();
                var success = await cleanupTest.Test_CodeCleanupAsync();
                
                Console.WriteLine();
                Console.WriteLine($"代码清理测试结果: {(success ? "✓ 通过" : "✗ 失败")}");
                Environment.Exit(success ? 0 : 1);
                return;
            }
            
            // 检查是否运行依赖注入测试
            if (args.Length > 0 && args[0].ToLower() == "--di")
            {
                DependencyInjectionTest.Run();
                return;
            }
            
            // 检查是否运行配置系统测试
            if (args.Length > 0 && args[0].ToLower() == "--config")
            {
                ConfigurationTest.Run();
                return;
            }
            
            // 检查是否运行用户偏好测试
            if (args.Length > 0 && args[0].ToLower() == "--prefs")
            {
                UserPreferencesTest.Run();
                return;
            }
            
            var runner = new TestRunner();
            await runner.RunAsync();
            
           // Console.WriteLine("\n按任意键退出...");
           // Console.ReadKey();
        }
    }
}

