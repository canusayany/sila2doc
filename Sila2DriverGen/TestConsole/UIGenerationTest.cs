using System;
using System.IO;
using System.Threading.Tasks;

namespace Sila2DriverGen.TestConsole
{
    /// <summary>
    /// UI生成路径测试 - 模拟从WPF界面生成D3驱动
    /// </summary>
    public class UIGenerationTest
    {
        /// <summary>
        /// 测试说明：验证从UI生成的项目文件结构
        /// </summary>
        public void Test_UIGeneration_Instructions()
        {
            ConsoleHelper.PrintSection("═══ UI生成路径测试说明 ═══");
            Console.WriteLine();

            ConsoleHelper.PrintInfo("由于UI生成需要交互（选择服务器、输入设备信息等），");
            ConsoleHelper.PrintInfo("无法完全自动化测试。请按以下步骤手动验证：");
            Console.WriteLine();

            ConsoleHelper.PrintSection("步骤 1：重启WPF应用程序");
            ConsoleHelper.PrintInfo("  1. 关闭当前运行的SilaGeneratorWpf应用（如果有）");
            ConsoleHelper.PrintInfo("  2. 重新启动SilaGeneratorWpf应用，确保加载最新代码");
            Console.WriteLine();

            ConsoleHelper.PrintSection("步骤 2：从UI生成D3驱动");
            ConsoleHelper.PrintInfo("  1. 切换到'🎯 生成D3驱动'标签页");
            ConsoleHelper.PrintInfo("  2. 扫描并选择SiLA2服务器");
            ConsoleHelper.PrintInfo("  3. 点击'生成D3驱动'按钮");
            ConsoleHelper.PrintInfo("  4. 等待生成完成");
            Console.WriteLine();

            ConsoleHelper.PrintSection("步骤 3：验证文件结构");
            ConsoleHelper.PrintInfo("  打开生成的项目目录，检查：");
            Console.WriteLine();
            
            ConsoleHelper.PrintSuccess("  ✓ 应该有：ProjectName/Sila2Client/ 文件夹（包含客户端代码）");
            ConsoleHelper.PrintError("  ✗ 不应该有：GeneratedClient/ 文件夹");
            Console.WriteLine();

            ConsoleHelper.PrintSection("步骤 4：编译验证");
            ConsoleHelper.PrintInfo("  在生成的项目目录中运行：");
            ConsoleHelper.PrintInfo("    dotnet build ProjectName.sln");
            ConsoleHelper.PrintInfo("  确保编译成功");
            Console.WriteLine();

            ConsoleHelper.PrintSection("预期结果");
            Console.WriteLine();
            ConsoleHelper.PrintSuccess("  ✓ Sila2Client文件夹存在且包含客户端代码");
            ConsoleHelper.PrintSuccess("  ✓ GeneratedClient文件夹不存在");
            ConsoleHelper.PrintSuccess("  ✓ 项目可以成功编译");
            Console.WriteLine();

            ConsoleHelper.PrintInfo("如果测试失败，请检查：");
            ConsoleHelper.PrintInfo("  1. 是否重启了WPF应用");
            ConsoleHelper.PrintInfo("  2. 生成日志中是否有错误");
            ConsoleHelper.PrintInfo("  3. 联系开发人员获取支持");
            Console.WriteLine();
        }

        /// <summary>
        /// 自动验证最新生成的项目
        /// </summary>
        public bool Test_VerifyLatestGeneration()
        {
            ConsoleHelper.PrintSection("═══ 验证最新生成的项目 ═══");
            Console.WriteLine();

            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), "Sila2D3Gen");
                if (!Directory.Exists(tempPath))
                {
                    ConsoleHelper.PrintError("✗ 未找到生成目录: Sila2D3Gen");
                    return false;
                }

                // 获取最新的项目
                var directories = Directory.GetDirectories(tempPath);
                if (directories.Length == 0)
                {
                    ConsoleHelper.PrintError("✗ 未找到任何生成的项目");
                    return false;
                }

                var latestDir = "";
                DateTime latestTime = DateTime.MinValue;
                foreach (var dir in directories)
                {
                    var dirInfo = new DirectoryInfo(dir);
                    if (dirInfo.LastWriteTime > latestTime)
                    {
                        latestTime = dirInfo.LastWriteTime;
                        latestDir = dir;
                    }
                }

                ConsoleHelper.PrintInfo($"最新项目: {Path.GetFileName(latestDir)}");
                ConsoleHelper.PrintInfo($"生成时间: {latestTime:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine();

                // 查找项目文件夹
                var projectDirs = Directory.GetDirectories(latestDir);
                string? projectDir = null;
                foreach (var dir in projectDirs)
                {
                    if (!Path.GetFileName(dir).Equals("TestConsole", StringComparison.OrdinalIgnoreCase))
                    {
                        projectDir = dir;
                        break;
                    }
                }

                if (projectDir == null)
                {
                    ConsoleHelper.PrintError("✗ 未找到主项目文件夹");
                    return false;
                }

                ConsoleHelper.PrintInfo($"主项目文件夹: {Path.GetFileName(projectDir)}");
                Console.WriteLine();

                bool allPassed = true;

                // 检查Sila2Client
                var sila2ClientDir = Path.Combine(projectDir, "Sila2Client");
                if (Directory.Exists(sila2ClientDir))
                {
                    var csFiles = Directory.GetFiles(sila2ClientDir, "*.cs");
                    if (csFiles.Length > 0)
                    {
                        ConsoleHelper.PrintSuccess($"✓ Sila2Client文件夹存在，包含 {csFiles.Length} 个.cs文件");
                        foreach (var file in csFiles)
                        {
                            ConsoleHelper.PrintInfo($"    - {Path.GetFileName(file)}");
                        }
                    }
                    else
                    {
                        ConsoleHelper.PrintError("✗ Sila2Client文件夹存在但为空");
                        allPassed = false;
                    }
                }
                else
                {
                    ConsoleHelper.PrintError("✗ Sila2Client文件夹不存在");
                    allPassed = false;
                }

                Console.WriteLine();

                // 检查GeneratedClient
                var generatedClientDir = Path.Combine(latestDir, "GeneratedClient");
                if (Directory.Exists(generatedClientDir))
                {
                    ConsoleHelper.PrintError("✗ GeneratedClient文件夹仍然存在（应该被移除）");
                    var files = Directory.GetFiles(generatedClientDir);
                    ConsoleHelper.PrintError($"    包含 {files.Length} 个文件");
                    allPassed = false;
                }
                else
                {
                    ConsoleHelper.PrintSuccess("✓ GeneratedClient文件夹不存在（正确）");
                }

                Console.WriteLine();
                return allPassed;
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintError($"✗ 验证失败: {ex.Message}");
                return false;
            }
        }
    }
}

