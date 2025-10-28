using System;
using System.IO;
using SilaGeneratorWpf.Services;

namespace Sila2DriverGen.TestConsole
{
    /// <summary>
    /// 配置系统测试
    /// </summary>
    public static class ConfigurationTest
    {
        public static void Run()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("配置系统测试");
            Console.WriteLine("========================================\n");

            try
            {
                var configService = new ConfigurationService();

                // 1. 测试日志配置
                Console.WriteLine("1. 测试日志配置:");
                var loggingConfig = configService.GetLoggingConfig();
                Console.WriteLine($"   最小日志级别: {loggingConfig.MinimumLevel}");
                Console.WriteLine($"   日志保留天数: {loggingConfig.RetainDays}");
                Console.WriteLine($"   最大文件大小: {loggingConfig.MaxFileSizeMB}MB");

                // 2. 测试应用配置
                Console.WriteLine("\n2. 测试应用配置:");
                var appConfig = configService.GetAppConfig();
                Console.WriteLine($"   默认输出路径: {(string.IsNullOrEmpty(appConfig.DefaultOutputPath) ? "(未设置)" : appConfig.DefaultOutputPath)}");
                Console.WriteLine($"   默认开发者: {appConfig.DefaultDeveloper}");
                Console.WriteLine($"   自动保存偏好: {appConfig.AutoSavePreferences}");
                Console.WriteLine($"   扫描超时: {appConfig.ServerScanTimeout}ms");

                // 3. 测试单独配置值
                Console.WriteLine("\n3. 测试单独配置值:");
                var developer = configService.GetValue("App:DefaultDeveloper", "Unknown");
                Console.WriteLine($"   App:DefaultDeveloper = {developer}");

                var timeout = configService.GetValue("App:ServerScanTimeout", "5000");
                Console.WriteLine($"   App:ServerScanTimeout = {timeout}");

                // 4. 测试配置文件存在性
                Console.WriteLine("\n4. 测试配置文件:");
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                var exists = File.Exists(configPath);
                Console.WriteLine($"   配置文件路径: {configPath}");
                Console.WriteLine($"   文件存在: {(exists ? "✓ 是" : "✗ 否")}");

                if (exists)
                {
                    var fileInfo = new FileInfo(configPath);
                    Console.WriteLine($"   文件大小: {fileInfo.Length} bytes");
                    Console.WriteLine($"   最后修改: {fileInfo.LastWriteTime}");
                }

                Console.WriteLine("\n========================================");
                Console.WriteLine("✅ 配置系统测试通过");
                Console.WriteLine("========================================\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ 测试失败: {ex.Message}");
                Console.WriteLine($"详细信息: {ex}");
            }
        }
    }
}

