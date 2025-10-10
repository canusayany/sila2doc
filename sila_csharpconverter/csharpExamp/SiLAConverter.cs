using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Xsl;

namespace SiLAConverter
{
    /// <summary>
    /// SiLA 2 特性转换器
    /// 支持从文件或内存字符串转换 XML → Proto → C#
    /// </summary>
    public static class SiLAConverter
    {
        private static readonly string XsltPath = "xslt/fdl2proto.xsl";
        
        #region 文件转换方法
        
        /// <summary>
        /// 将 SiLA XML 文件转换为 Proto 文件
        /// </summary>
        public static bool ConvertXmlToProto(string xmlPath, string protoPath)
        {
            try
            {
                Console.WriteLine($"转换: {Path.GetFileName(xmlPath)} → {Path.GetFileName(protoPath)}");
                
                if (!File.Exists(xmlPath))
                {
                    Console.WriteLine($"✗ 错误: 文件不存在 - {xmlPath}");
                    return false;
                }
                
                var xslt = new XslCompiledTransform();
                var settings = new XsltSettings(enableDocumentFunction: true, enableScript: true);
                xslt.Load(XsltPath, settings, new XmlUrlResolver());
                
                Directory.CreateDirectory(Path.GetDirectoryName(protoPath) ?? ".");
                
                using (var writer = new StreamWriter(protoPath))
                {
                    xslt.Transform(xmlPath, null, writer);
                }
                
                Console.WriteLine($"✓ 成功生成 Proto 文件");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 转换失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 验证 XML 文件是否符合 SiLA Schema
        /// </summary>
        public static bool ValidateXml(string xmlPath)
        {
            try
            {
                var settings = new XmlReaderSettings();
                
                if (File.Exists("schema/FeatureDefinition.xsd"))
                {
                    settings.Schemas.Add(null, "schema/FeatureDefinition.xsd");
                    settings.ValidationType = ValidationType.Schema;
                    settings.ValidationEventHandler += (sender, e) =>
                    {
                        Console.WriteLine($"  验证警告: {e.Message}");
                    };
                }
                
                using (var reader = XmlReader.Create(xmlPath, settings))
                {
                    while (reader.Read()) { }
                }
                
                Console.WriteLine("✓ XML 验证通过");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ XML 验证失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 使用 protoc 编译 Proto 文件生成 C# 代码
        /// </summary>
        public static bool CompileProtoToCSharp(string protoPath, string outputDir)
        {
            try
            {
                Console.WriteLine($"编译 Proto → C#: {Path.GetFileName(protoPath)}");
                
                var protocPath = FindProtocPath();
                if (string.IsNullOrEmpty(protocPath))
                {
                    Console.WriteLine("✗ 错误: 找不到 protoc.exe");
                    Console.WriteLine("  请安装 Grpc.Tools NuGet 包");
                    return false;
                }
                
                Directory.CreateDirectory(outputDir);
                
                var grpcPluginPath = FindGrpcPluginPath();
                var args = $"--csharp_out={outputDir} -I . -I protobuf {protoPath}";
                
                if (!string.IsNullOrEmpty(grpcPluginPath))
                {
                    args += $" --grpc_out={outputDir} --plugin=protoc-gen-grpc={grpcPluginPath}";
                }
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = protocPath,
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"✓ 成功生成 C# 代码");
                    return true;
                }
                else
                {
                    Console.WriteLine($"✗ protoc 编译失败: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 编译失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 完整转换流程：XML → Proto → C#
        /// </summary>
        public static void ConvertComplete(string xmlPath, string outputDir)
        {
            Console.WriteLine($"\n{'=',60}");
            Console.WriteLine($"完整转换: {Path.GetFileName(xmlPath)}");
            Console.WriteLine($"{'=',60}");
            
            Directory.CreateDirectory(outputDir);
            var protoDir = Path.Combine(outputDir, "proto");
            var csharpDir = Path.Combine(outputDir, "csharp");
            Directory.CreateDirectory(protoDir);
            
            if (!ValidateXml(xmlPath))
            {
                Console.WriteLine("\n转换已中止");
                return;
            }
            
            var fileName = Path.GetFileNameWithoutExtension(xmlPath);
            var protoPath = Path.Combine(protoDir, $"{fileName}.proto");
            
            if (!ConvertXmlToProto(xmlPath, protoPath))
            {
                Console.WriteLine("\n转换已中止");
                return;
            }
            
            CompileProtoToCSharp(protoPath, csharpDir);
            
            Console.WriteLine($"\n{'=',60}");
            Console.WriteLine("转换完成！");
            Console.WriteLine($"Proto: {protoPath}");
            Console.WriteLine($"C#: {csharpDir}");
            Console.WriteLine($"{'=',60}");
        }
        
        #endregion
        
        #region 内存字符串转换方法（新增）
        
        /// <summary>
        /// 从 XML 字符串转换为 Proto 字符串（内存操作）
        /// </summary>
        public static string ConvertXmlStringToProto(string xmlContent)
        {
            try
            {
                var xslt = new XslCompiledTransform();
                var settings = new XsltSettings(enableDocumentFunction: true, enableScript: true);
                xslt.Load(XsltPath, settings, new XmlUrlResolver());
                
                using (var stringReader = new StringReader(xmlContent))
                using (var xmlReader = XmlReader.Create(stringReader))
                using (var stringWriter = new StringWriter())
                {
                    xslt.Transform(xmlReader, null, stringWriter);
                    return stringWriter.ToString();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"内存转换失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 批量从 XML 字符串转换为 Proto 字符串
        /// </summary>
        /// <param name="xmlContents">键为名称，值为 XML 内容的字典</param>
        /// <returns>键为名称，值为 Proto 内容的字典</returns>
        public static Dictionary<string, string> ConvertXmlStringsToProto(Dictionary<string, string> xmlContents)
        {
            var results = new Dictionary<string, string>();
            
            foreach (var kvp in xmlContents)
            {
                try
                {
                    Console.WriteLine($"转换内存 XML: {kvp.Key}");
                    results[kvp.Key] = ConvertXmlStringToProto(kvp.Value);
                    Console.WriteLine($"✓ {kvp.Key} 转换成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ {kvp.Key} 转换失败: {ex.Message}");
                    results[kvp.Key] = string.Empty;
                }
            }
            
            return results;
        }
        
        /// <summary>
        /// 从内存 XML 转换并保存到文件
        /// </summary>
        public static bool ConvertXmlStringToProtoFile(string xmlContent, string outputProtoPath)
        {
            try
            {
                string protoContent = ConvertXmlStringToProto(xmlContent);
                Directory.CreateDirectory(Path.GetDirectoryName(outputProtoPath) ?? ".");
                File.WriteAllText(outputProtoPath, protoContent);
                Console.WriteLine($"✓ 成功保存 Proto 文件: {outputProtoPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 保存失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 完整的内存转换流程：XML 字符串 → Proto 文件 → C# 代码
        /// </summary>
        public static void ConvertXmlStringComplete(string xmlContent, string featureName, string outputDir)
        {
            Console.WriteLine($"\n内存完整转换: {featureName}");
            
            Directory.CreateDirectory(outputDir);
            var protoDir = Path.Combine(outputDir, "proto");
            var csharpDir = Path.Combine(outputDir, "csharp");
            Directory.CreateDirectory(protoDir);
            
            var protoPath = Path.Combine(protoDir, $"{featureName}.proto");
            
            if (!ConvertXmlStringToProtoFile(xmlContent, protoPath))
            {
                Console.WriteLine("\n转换已中止");
                return;
            }
            
            CompileProtoToCSharp(protoPath, csharpDir);
            Console.WriteLine($"✓ 完成: {featureName}");
        }
        
        #endregion
        
        #region 辅助方法
        
        private static string? FindProtocPath()
        {
            var nugetPackages = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget", "packages", "grpc.tools"
            );
            
            if (Directory.Exists(nugetPackages))
            {
                var versions = Directory.GetDirectories(nugetPackages);
                if (versions.Length > 0)
                {
                    Array.Sort(versions);
                    var latestVersion = versions[^1];
                    var platform = Environment.Is64BitProcess ? "windows_x64" : "windows_x86";
                    var protocPath = Path.Combine(latestVersion, "tools", platform, "protoc.exe");
                    
                    if (File.Exists(protocPath))
                        return protocPath;
                }
            }
            
            return null;
        }
        
        private static string? FindGrpcPluginPath()
        {
            var nugetPackages = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget", "packages", "grpc.tools"
            );
            
            if (Directory.Exists(nugetPackages))
            {
                var versions = Directory.GetDirectories(nugetPackages);
                if (versions.Length > 0)
                {
                    Array.Sort(versions);
                    var latestVersion = versions[^1];
                    var platform = Environment.Is64BitProcess ? "windows_x64" : "windows_x86";
                    var pluginPath = Path.Combine(latestVersion, "tools", platform, "grpc_csharp_plugin.exe");
                    
                    if (File.Exists(pluginPath))
                        return pluginPath;
                }
            }
            
            return null;
        }
        
        #endregion
    }
}

