using System;
using System.Collections.Generic;
using System.IO;

namespace SiLAConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════╗");
            Console.WriteLine("║     SiLA 2 特性转换器测试示例 (.NET 8)       ║");
            Console.WriteLine("╚════════════════════════════════════════════════╝\n");

            // ========== 示例 1: 从文件转换 ==========
            Console.WriteLine("【示例 1】从文件转换单个特性");
            SiLAConverter.ConvertComplete(
                "examples/GreetingProvider-v1_0.sila.xml",
                "output/example1"
            );

            // ========== 示例 2: 从内存字符串转换（单个）==========
            Console.WriteLine("\n\n【示例 2】从内存字符串转换");
            try
            {
                string xmlContent = File.ReadAllText("examples/SiLAService-v1_0.sila.xml");
                string protoResult = SiLAConverter.ConvertXmlStringToProto(xmlContent);
                
                Console.WriteLine("✓ 内存转换成功");
                Console.WriteLine($"  Proto 长度: {protoResult.Length} 字符");
                
                // 保存结果
                string outputPath = "output/example2/SiLAService.proto";
                Directory.CreateDirectory("output/example2");
                File.WriteAllText(outputPath, protoResult);
                Console.WriteLine($"  已保存到: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 示例 2 失败: {ex.Message}");
            }

            // ========== 示例 3: 批量从内存转换 ==========
            Console.WriteLine("\n\n【示例 3】批量从内存字符串转换");
            try
            {
                var xmlContents = new Dictionary<string, string>
                {
                    ["GreetingProvider"] = File.ReadAllText("examples/GreetingProvider-v1_0.sila.xml"),
                    ["LockController"] = File.ReadAllText("examples/LockController-v1_0.sila.xml"),
                    ["SiLAService"] = File.ReadAllText("examples/SiLAService-v1_0.sila.xml")
                };

                var results = SiLAConverter.ConvertXmlStringsToProto(xmlContents);
                
                Console.WriteLine($"\n批量转换完成: {results.Count} 个特性");
                
                // 保存所有结果
                Directory.CreateDirectory("output/example3");
                foreach (var kvp in results)
                {
                    if (kvp.Value != null)
                    {
                        string path = $"output/example3/{kvp.Key}.proto";
                        File.WriteAllText(path, kvp.Value);
                        Console.WriteLine($"  ✓ 已保存: {path}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 示例 3 失败: {ex.Message}");
            }

            // ========== 示例 4: 完整的内存转换流程（含 C# 生成）==========
            Console.WriteLine("\n\n【示例 4】内存转换完整流程（XML → Proto → C#）");
            try
            {
                string xmlContent = File.ReadAllText("examples/GreetingProvider-v1_0.sila.xml");
                SiLAConverter.ConvertXmlStringComplete(
                    xmlContent,
                    "GreetingProvider",
                    "output/example4"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 示例 4 失败: {ex.Message}");
            }

            // ========== 示例 5: 仅转换为 Proto（不生成 C#）==========
            Console.WriteLine("\n\n【示例 5】仅转换 XML → Proto");
            SiLAConverter.ConvertXmlToProto(
                "examples/LockController-v1_0.sila.xml",
                "output/example5/LockController.proto"
            );

            // ========== 总结 ==========
            Console.WriteLine("\n\n" + new string('=', 60));
            Console.WriteLine("所有示例执行完成！");
            Console.WriteLine("输出目录: output/");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }
    }
}

