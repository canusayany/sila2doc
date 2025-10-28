using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SilaGeneratorWpf.Models;
using SilaGeneratorWpf.Services;
using SilaGeneratorWpf.Services.CodeDom;

namespace Sila2DriverGen.TestConsole
{
    /// <summary>
    /// 测试AbsorbanceReaderService的类型和注释生成
    /// </summary>
    public class AbsorbanceReaderTest
    {
        public string TestName { get; set; } = "AbsorbanceReaderService类型和注释测试";
        public string TestDescription { get; set; } = "验证生成的AllSila2Client.cs和D3Driver.cs中类型和注释是否正确";
        public bool TestResult { get; set; } = false;

        public void Run()
        {
            PrintTestHeader();

            try
            {
                // 1. 准备测试数据
                Console.WriteLine("========== 步骤1：准备测试数据 ==========");
                var testXmlPath = @"c:\data\Bioyond\Code\D3产品相关代码\技术测试\sila_csharp - 副本\src\sila_base\feature_definitions\com\madisoft\reader\AbsorbanceReaderService-v1_0.sila.xml";
                
                if (!File.Exists(testXmlPath))
                {
                    Console.WriteLine($"❌ 未找到 AbsorbanceReaderService-v1_0.sila.xml 文件: {testXmlPath}");
                    return;
                }
                Console.WriteLine($"✓ 找到测试文件: {testXmlPath}");

                // 2. 生成客户端代码
                Console.WriteLine("\n========== 步骤2：生成客户端代码 ==========");
                var outputDir = Path.Combine(Environment.CurrentDirectory, "TestOutput", $"AbsorbanceTest_{DateTime.Now:yyyyMMdd_HHmmss}");
                var clientCodeDir = Path.Combine(outputDir, "Sila2Client");
                Directory.CreateDirectory(clientCodeDir);
                Console.WriteLine($"输出目录: {clientCodeDir}");

                // 加载Feature对象
                Console.WriteLine("  加载Feature对象...");
                var features = new Dictionary<string, Tecan.Sila2.Feature>();
                var loadedFeature = Tecan.Sila2.FeatureSerializer.Load(testXmlPath);
                features[loadedFeature.Identifier] = loadedFeature;
                Console.WriteLine($"  ✓ 加载特性: {loadedFeature.Identifier}");

                var codeGenerator = new ClientCodeGenerator(generateInterface: true);
                var generationResult = codeGenerator.GenerateClientCodeFromFeatures(
                    features,
                    clientCodeDir,
                    "Sila2Client",
                    message => Console.WriteLine($"  {message}"));

                if (!generationResult.Success)
                {
                    Console.WriteLine($"❌ 客户端代码生成失败: {generationResult.Message}");
                    return;
                }
                Console.WriteLine($"✓ 成功生成 {generationResult.GeneratedFiles.Count} 个文件");

                // 3. 分析客户端代码
                Console.WriteLine("\n========== 步骤3：分析客户端代码 ==========");
                var analyzer = new ClientCodeAnalyzer();
                
                ClientAnalysisResult? analysisResult = null;
                try
                {
                    analysisResult = analyzer.Analyze(clientCodeDir);
                    Console.WriteLine($"✓ 分析完成");
                    Console.WriteLine($"  - 检测到 {analysisResult.Features.Count} 个特性");
                    Console.WriteLine($"  - 总共 {analysisResult.Features.Sum(f => f.Methods.Count)} 个方法");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 分析失败: {ex.Message}");
                    return;
                }

                // 4. 显示分析结果详情
                Console.WriteLine("\n========== 步骤4：分析结果详情 ==========");
                foreach (var feature in analysisResult.Features)
                {
                    Console.WriteLine($"\n特性: {feature.FeatureName}");
                    Console.WriteLine($"  接口名称: {feature.InterfaceName}");
                    
                    foreach (var method in feature.Methods)
                    {
                        var returnTypeInfo = $"{method.ReturnType.Name}";
                        
                        Console.WriteLine($"\n  方法: {method.Name}");
                        Console.WriteLine($"    返回类型: {returnTypeInfo}");
                        Console.WriteLine($"    描述: {method.Description ?? "(无)"}");
                        Console.WriteLine($"    XML文档: {(method.XmlDocumentation?.Summary ?? "(无)")}");
                        
                        if (method.Parameters.Any())
                        {
                            Console.WriteLine($"    参数:");
                            foreach (var param in method.Parameters)
                            {
                                var paramTypeInfo = $"{param.Type.Name}";
                                Console.WriteLine($"      - {param.Name}: {paramTypeInfo}");
                            }
                        }
                    }
                }

                // 5. 生成D3驱动代码
                Console.WriteLine("\n========== 步骤5：生成D3驱动代码 ==========");
                var config = new D3DriverGenerationConfig
                {
                    Namespace = "BR.ECS.DeviceDriver.TestDevice",
                    Brand = "TestBrand",
                    Model = "TestModel",
                    DeviceType = "TestDevice",
                    Developer = "TestDev",
                    OutputPath = outputDir,
                    ClientCodePath = clientCodeDir,
                    Features = analysisResult.Features,
                    IsOnlineSource = false,
                    LocalFeatureXmlPaths = new List<string> { testXmlPath }
                };

                var d3Service = new D3DriverGeneratorService();
                
                var result = d3Service.Generate(
                    config,
                    message => Console.WriteLine($"  {message}"));

                if (!result.Success)
                {
                    Console.WriteLine($"❌ D3驱动生成失败: {result.Message}");
                    return;
                }

                Console.WriteLine($"✓ {result.Message}");

                // 6. 检查生成的文件
                Console.WriteLine("\n========== 步骤6：验证生成的文件 ==========");
                var allSila2ClientPath = Path.Combine(outputDir, "AllSila2Client.cs");
                var d3DriverPath = Path.Combine(outputDir, "D3Driver.cs");

                if (File.Exists(allSila2ClientPath))
                {
                    Console.WriteLine("\nAllSila2Client.cs 内容检查:");
                    var content = File.ReadAllText(allSila2ClientPath);
                    
                    // 检查关键方法签名
                    if (content.Contains("OpticalDensity AbsorbanceReadWell(WellAddress targetWell)"))
                    {
                        Console.WriteLine("  ✓ AbsorbanceReadWell 方法签名正确（保留了自定义类型）");
                    }
                    else if (content.Contains("object AbsorbanceReadWell(object targetWell)"))
                    {
                        Console.WriteLine("  ❌ AbsorbanceReadWell 方法签名错误（类型被替换为object）");
                    }
                    else if (content.Contains("AbsorbanceReadWell"))
                    {
                        // 提取实际的方法签名
                        var lines = content.Split('\n');
                        var methodLine = lines.FirstOrDefault(l => l.Contains("AbsorbanceReadWell"));
                        Console.WriteLine($"  ⚠ AbsorbanceReadWell 方法签名: {methodLine?.Trim()}");
                    }

                    // 检查XML注释
                    if (content.Contains("<summary>") && content.Contains("AbsorbanceReadWell"))
                    {
                        Console.WriteLine("  ✓ 包含XML注释");
                    }
                    else
                    {
                        Console.WriteLine("  ❌ 缺少XML注释");
                    }
                }
                else
                {
                    Console.WriteLine($"❌ 未找到 AllSila2Client.cs");
                }

                if (File.Exists(d3DriverPath))
                {
                    Console.WriteLine("\nD3Driver.cs 内容检查:");
                    var content = File.ReadAllText(d3DriverPath);
                    
                    // 检查XML注释
                    if (content.Contains("<summary>") && content.Contains("AbsorbanceReadWell"))
                    {
                        Console.WriteLine("  ✓ 包含XML注释");
                    }
                    else
                    {
                        Console.WriteLine("  ❌ 缺少XML注释");
                    }
                }
                else
                {
                    Console.WriteLine($"❌ 未找到 D3Driver.cs");
                }

                Console.WriteLine($"\n所有文件已生成到: {outputDir}");
                Console.WriteLine("请手动检查生成的文件内容！");
                
                TestResult = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ 测试失败: {ex.Message}");
                Console.WriteLine($"\n堆栈跟踪:\n{ex.StackTrace}");
                TestResult = false;
            }

            PrintTestFooter();
        }

        private void PrintTestHeader()
        {
            Console.WriteLine("\n========================================");
            Console.WriteLine($"测试: {TestName}");
            Console.WriteLine($"描述: {TestDescription}");
            Console.WriteLine("========================================\n");
        }

        private void PrintTestFooter()
        {
            Console.WriteLine("\n========== 测试完成 ==========");
            Console.WriteLine($"测试结果: {(TestResult ? "✓ 通过" : "❌ 失败")}\n");
        }
    }
}

