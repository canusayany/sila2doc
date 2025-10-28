using System;
using System.IO;
using SilaGeneratorWpf.Services;

namespace Sila2DriverGen.TestConsole
{
    /// <summary>
    /// 用户偏好系统测试
    /// </summary>
    public static class UserPreferencesTest
    {
        public static void Run()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("用户偏好系统测试");
            Console.WriteLine("========================================\n");

            try
            {
                var prefsService = new UserPreferencesService();

                // 1. 测试默认偏好
                Console.WriteLine("1. 测试默认偏好:");
                var prefs = prefsService.Preferences;
                Console.WriteLine($"   窗口宽度: {prefs.WindowWidth}");
                Console.WriteLine($"   窗口高度: {prefs.WindowHeight}");
                Console.WriteLine($"   侧边栏可见: {prefs.SidebarVisible}");
                Console.WriteLine($"   侧边栏宽度: {prefs.SidebarWidth}");

                // 2. 测试设备信息更新
                Console.WriteLine("\n2. 测试设备信息更新:");
                prefsService.UpdateLastDeviceInfo("TestBrand", "TestModel", "Dispenser", "Bioyond");
                prefs = prefsService.Preferences;
                Console.WriteLine($"   最后品牌: {prefs.LastBrand}");
                Console.WriteLine($"   最后型号: {prefs.LastModel}");
                Console.WriteLine($"   最后类型: {prefs.LastDeviceType}");
                Console.WriteLine($"   最后开发者: {prefs.LastDeveloper}");

                // 3. 测试窗口状态更新
                Console.WriteLine("\n3. 测试窗口状态更新:");
                prefsService.UpdateWindowState(100, 200, 1400, 800, false);
                prefs = prefsService.Preferences;
                Console.WriteLine($"   窗口位置: ({prefs.WindowLeft}, {prefs.WindowTop})");
                Console.WriteLine($"   窗口大小: {prefs.WindowWidth}x{prefs.WindowHeight}");
                Console.WriteLine($"   最大化: {prefs.WindowMaximized}");

                // 4. 测试侧边栏状态更新
                Console.WriteLine("\n4. 测试侧边栏状态更新:");
                prefsService.UpdateSidebarState(false, 350);
                prefs = prefsService.Preferences;
                Console.WriteLine($"   侧边栏可见: {prefs.SidebarVisible}");
                Console.WriteLine($"   侧边栏宽度: {prefs.SidebarWidth}");

                // 5. 测试偏好文件
                Console.WriteLine("\n5. 测试偏好文件:");
                var prefsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SilaGenerator",
                    "preferences.json");
                Console.WriteLine($"   偏好文件路径: {prefsPath}");
                
                var exists = File.Exists(prefsPath);
                Console.WriteLine($"   文件存在: {(exists ? "✓ 是" : "✗ 否")}");

                if (exists)
                {
                    var fileInfo = new FileInfo(prefsPath);
                    Console.WriteLine($"   文件大小: {fileInfo.Length} bytes");
                    Console.WriteLine($"   最后修改: {fileInfo.LastWriteTime}");
                    
                    // 读取并显示内容
                    var content = File.ReadAllText(prefsPath);
                    Console.WriteLine("\n   文件内容预览:");
                    var lines = content.Split('\n');
                    for (int i = 0; i < Math.Min(lines.Length, 10); i++)
                    {
                        Console.WriteLine($"     {lines[i]}");
                    }
                }

                Console.WriteLine("\n========================================");
                Console.WriteLine("✅ 用户偏好系统测试通过");
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

