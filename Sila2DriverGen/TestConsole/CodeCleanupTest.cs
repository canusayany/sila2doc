using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sila2DriverGen.TestConsole
{
    /// <summary>
    /// 代码清理测试 - 验证废弃代码已被删除
    /// </summary>
    public class CodeCleanupTest : TestBase
    {
        /// <summary>
        /// 测试代码清理是否完整
        /// </summary>
        public async Task<bool> Test_CodeCleanupAsync()
        {
            Console.WriteLine("========== 测试代码清理 ==========");
            Console.WriteLine();

            try
            {
                Console.WriteLine("[步骤 1/3] 检查Obsolete标记...");
                
                // 查找项目根目录
                var projectRoot = FindProjectRoot();
                if (string.IsNullOrEmpty(projectRoot))
                {
                    Console.WriteLine("  ✗ 未找到项目根目录");
                    return false;
                }

                // 检查关键文件
                var silaGeneratorWpfPath = Path.Combine(projectRoot, "SilaGeneratorWpf");
                if (!Directory.Exists(silaGeneratorWpfPath))
                {
                    Console.WriteLine("  ✗ SilaGeneratorWpf目录不存在");
                    return false;
                }

                Console.WriteLine($"  项目路径: {silaGeneratorWpfPath}");

                // 检查MethodGenerationInfo.cs
                Console.WriteLine();
                Console.WriteLine("[步骤 2/3] 验证 MethodGenerationInfo.cs...");
                var methodGenInfoFile = Path.Combine(silaGeneratorWpfPath, "Models", "MethodGenerationInfo.cs");
                if (!File.Exists(methodGenInfoFile))
                {
                    Console.WriteLine("  ✗ 文件不存在");
                    return false;
                }

                var content = File.ReadAllText(methodGenInfoFile);
                
                // 检查是否仍有Obsolete标记
                if (content.Contains("[Obsolete"))
                {
                    Console.WriteLine("  ✗ 仍包含Obsolete标记");
                    return false;
                }

                // 检查是否仍有Category属性
                if (content.Contains("public MethodCategory Category"))
                {
                    Console.WriteLine("  ✗ 仍包含废弃的Category属性");
                    return false;
                }

                // 检查是否仍有MethodCategory枚举
                if (content.Contains("public enum MethodCategory"))
                {
                    Console.WriteLine("  ✗ 仍包含废弃的MethodCategory枚举");
                    return false;
                }

                Console.WriteLine("  ✓ MethodGenerationInfo.cs 清理完成");

                // 检查ClientCodeGenerator.cs
                Console.WriteLine();
                Console.WriteLine("[步骤 3/3] 验证 ClientCodeGenerator.cs...");
                var codeGenFile = Path.Combine(silaGeneratorWpfPath, "Services", "ClientCodeGenerator.cs");
                if (!File.Exists(codeGenFile))
                {
                    Console.WriteLine("  ✗ 文件不存在");
                    return false;
                }

                content = File.ReadAllText(codeGenFile);
                
                // 检查是否仍有Obsolete的CopyRequiredDllsToClientDirectory方法
                if (content.Contains("CopyRequiredDllsToClientDirectory"))
                {
                    Console.WriteLine("  ✗ 仍包含废弃的CopyRequiredDllsToClientDirectory方法");
                    return false;
                }

                Console.WriteLine("  ✓ ClientCodeGenerator.cs 清理完成");

                Console.WriteLine();
                Console.WriteLine("========== 代码清理测试通过 ✓ ==========");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 测试失败: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return false;
            }
        }

        private string? FindProjectRoot()
        {
            var currentDir = Directory.GetCurrentDirectory();
            
            // 向上查找，直到找到.sln文件
            for (int i = 0; i < 10; i++)
            {
                if (Directory.GetFiles(currentDir, "*.sln").Any())
                {
                    return currentDir;
                }

                var parent = Directory.GetParent(currentDir);
                if (parent == null)
                    break;

                currentDir = parent.FullName;
            }

            return null;
        }
    }
}

