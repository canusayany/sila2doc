using System;
using System.IO;
using System.Threading.Tasks;
using SilaGeneratorWpf.Services;
using Microsoft.Extensions.Logging;

namespace Sila2DriverGen.TestConsole
{
    /// <summary>
    /// 日志系统测试
    /// </summary>
    public class LoggingTest : TestBase
    {
        /// <summary>
        /// 测试日志系统初始化和写入
        /// </summary>
        public async Task<bool> Test_LoggingSystemAsync()
        {
            Console.WriteLine("========== 测试日志系统 ==========");
            Console.WriteLine();

            try
            {
                // 1. 初始化日志系统
                Console.WriteLine("[步骤 1/5] 初始化日志系统...");
                LoggerService.Initialize(LogLevel.Debug);
                Console.WriteLine("  ✓ 日志系统初始化成功");

                // 2. 测试不同级别的日志
                Console.WriteLine("[步骤 2/5] 测试各级别日志写入...");
                var logger = LoggerService.GetLogger<LoggingTest>();
                
                logger.LogTrace("这是一条跟踪日志");
                logger.LogDebug("这是一条调试日志");
                logger.LogInformation("这是一条信息日志");
                logger.LogWarning("这是一条警告日志");
                logger.LogError("这是一条错误日志");
                logger.LogCritical("这是一条严重错误日志");
                Console.WriteLine("  ✓ 各级别日志写入完成");

                // 3. 测试结构化日志
                Console.WriteLine("[步骤 3/5] 测试结构化日志...");
                logger.LogInformation("测试结构化日志 - 参数1: {Param1}, 参数2: {Param2}", "值1", 123);
                Console.WriteLine("  ✓ 结构化日志写入完成");

                // 4. 测试异常日志
                Console.WriteLine("[步骤 4/5] 测试异常日志...");
                try
                {
                    throw new InvalidOperationException("这是一个测试异常");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "捕获到测试异常");
                }
                Console.WriteLine("  ✓ 异常日志写入完成");

                // 5. 验证日志文件
                Console.WriteLine("[步骤 5/5] 验证日志文件...");
                var logsDir = LoggerService.GetLogsDirectory();
                if (!Directory.Exists(logsDir))
                {
                    Console.WriteLine("  ✗ 日志目录不存在");
                    return false;
                }

                var logFiles = Directory.GetFiles(logsDir, "*.log");
                if (logFiles.Length == 0)
                {
                    Console.WriteLine("  ✗ 未找到日志文件");
                    return false;
                }

                Console.WriteLine($"  ✓ 找到 {logFiles.Length} 个日志文件");
                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    Console.WriteLine($"    - {fileInfo.Name} ({fileInfo.Length} 字节)");
                }

                // 6. 测试日志清理
                Console.WriteLine();
                Console.WriteLine("测试日志清理功能...");
                LoggerService.CleanupOldLogs(30);
                Console.WriteLine("  ✓ 日志清理完成");

                Console.WriteLine();
                Console.WriteLine("========== 日志系统测试通过 ✓ ==========");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 测试失败: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return false;
            }
            finally
            {
                await Task.Delay(100); // 等待日志写入
            }
        }
    }
}

