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

            // 检查是否使用自动化测试模式
            if (args.Length > 0 && args[0].ToLower() == "--auto")
            {
                Console.WriteLine("运行自动化测试模式...");
                Console.WriteLine();
                
                var autoTest = new AutomatedTest();
                var success = await autoTest.RunAllTestsAsync();
                
                Console.WriteLine();
                Console.WriteLine($"自动化测试结果: {(success ? "通过" : "失败")}");
                Console.WriteLine("\n按任意键退出...");
                Console.ReadKey();
                Environment.Exit(success ? 0 : 1);
                return;
            }
            
            var runner = new TestRunner();
            await runner.RunAsync();
            
            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }
    }
}

