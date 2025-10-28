using System;
using Microsoft.Extensions.DependencyInjection;
using SilaGeneratorWpf.Services;

namespace Sila2DriverGen.TestConsole
{
    /// <summary>
    /// 依赖注入系统测试
    /// </summary>
    public static class DependencyInjectionTest
    {
        public static void Run()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("依赖注入系统测试");
            Console.WriteLine("========================================\n");

            try
            {
                // 1. 配置服务
                var services = new ServiceCollection();
                services.AddApplicationServices();
                
                var serviceProvider = services.BuildServiceProvider();
                ServiceLocator.Initialize(serviceProvider);

                Console.WriteLine("✓ 服务容器初始化成功");

                // 2. 测试服务获取
                var configService = ServiceLocator.Current.GetRequiredService<ConfigurationService>();
                Console.WriteLine("✓ ConfigurationService 获取成功");

                var userPrefsService = ServiceLocator.Current.GetRequiredService<UserPreferencesService>();
                Console.WriteLine("✓ UserPreferencesService 获取成功");

                var discoveryService = ServiceLocator.Current.GetRequiredService<ServerDiscoveryService>();
                Console.WriteLine("✓ ServerDiscoveryService 获取成功");

                // 3. 测试瞬时服务
                var generator1 = ServiceLocator.Current.GetRequiredService<ClientCodeGenerator>();
                var generator2 = ServiceLocator.Current.GetRequiredService<ClientCodeGenerator>();
                Console.WriteLine($"✓ 瞬时服务验证: generator1 != generator2 = {!ReferenceEquals(generator1, generator2)}");

                // 4. 测试单例服务
                var config1 = ServiceLocator.Current.GetRequiredService<ConfigurationService>();
                var config2 = ServiceLocator.Current.GetRequiredService<ConfigurationService>();
                Console.WriteLine($"✓ 单例服务验证: config1 == config2 = {ReferenceEquals(config1, config2)}");

                // 5. 测试配置读取
                var appConfig = configService.GetAppConfig();
                Console.WriteLine($"✓ 配置读取: DefaultDeveloper = {appConfig.DefaultDeveloper}");
                Console.WriteLine($"✓ 配置读取: ServerScanTimeout = {appConfig.ServerScanTimeout}ms");

                var loggingConfig = configService.GetLoggingConfig();
                Console.WriteLine($"✓ 日志配置: MinimumLevel = {loggingConfig.MinimumLevel}");
                Console.WriteLine($"✓ 日志配置: RetainDays = {loggingConfig.RetainDays}");

                // 6. 测试用户偏好
                userPrefsService.UpdateLastDeviceInfo("TestBrand", "TestModel", "TestType", "TestDev");
                Console.WriteLine("✓ 用户偏好更新成功");

                var prefs = userPrefsService.Preferences;
                Console.WriteLine($"✓ 用户偏好读取: LastBrand = {prefs.LastBrand}");

                Console.WriteLine("\n========================================");
                Console.WriteLine("✅ 依赖注入系统测试通过");
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

