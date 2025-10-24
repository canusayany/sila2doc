using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Tecan.Sila2;
using Tecan.Sila2.Generator;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// SiLA2 客户端代码生成器 - 使用 Generator.dll 直接调用 API
    /// 
    /// 使用说明：
    /// 1. 通过项目引用 Generator.dll
    /// 2. 支持从 Feature XML 文件或 Feature 对象直接生成客户端代码
    /// 3. 生成三个文件：接口（I*.cs）、DTOs（*Dtos.cs）、客户端（*Client.cs）
    /// 
    /// 部署说明：
    /// - Generator.dll 及其依赖项会自动复制到输出目录
    /// </summary>
    public class ClientCodeGenerator
    {
        private readonly bool _generateInterface;
        private readonly GeneratedCodeDeduplicator _deduplicator;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="generateInterface">是否生成接口文件,默认为 true</param>
        public ClientCodeGenerator(bool generateInterface = true)
        {
            _generateInterface = generateInterface;
            _deduplicator = new GeneratedCodeDeduplicator();
        }

        /// <summary>
        /// 从 Feature XML 文件生成客户端代码
        /// </summary>
        public GenerationResult GenerateClientCode(
            IEnumerable<string> featureFiles,
            string outputDirectory,
            string? customNamespace = null,
            Action<string>? progressCallback = null)
        {
            var result = new GenerationResult { Success = true };
            var generatedFiles = new List<string>();

            try
            {
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                foreach (var featureFile in featureFiles)
                {
                    try
                    {
                        progressCallback?.Invoke($"正在处理: {Path.GetFileName(featureFile)}");

                        if (!File.Exists(featureFile))
                        {
                            result.Warnings.Add($"文件不存在: {featureFile}");
                            continue;
                        }

                        // 加载 Feature 以获取标识符
                        var feature = FeatureSerializer.Load(featureFile);
                        
                        // 生成代码
                        var files = GenerateClientFromFile(
                            featureFile,
                            feature.Identifier,
                            outputDirectory,
                            customNamespace ?? "Sila2Client",
                            progressCallback);

                        generatedFiles.AddRange(files);
                        progressCallback?.Invoke($"✓ 完成: {feature.Identifier}");
                    }
                    catch (Exception ex)
                    {
                        var fileName = Path.GetFileName(featureFile);
                        result.Warnings.Add($"处理 {fileName} 时出错: {ex.Message}");
                        progressCallback?.Invoke($"✗ 错误: {fileName} - {ex.Message}");
                    }
                }

                result.GeneratedFiles = generatedFiles;
                result.Message = $"成功生成 {generatedFiles.Count} 个文件";

                // 复制必需的 DLL 到输出目录
                CopyRequiredDllsToClientDirectory(outputDirectory, progressCallback);

                // 去重处理：检查并注释重复的类型定义
                progressCallback?.Invoke("检查重复的类型定义...");
                var dedupResult = _deduplicator.DeduplicateGeneratedCode(outputDirectory, progressCallback);
                if (dedupResult.Success && dedupResult.CommentedTypes.Count > 0)
                {
                    result.Warnings.Add($"已自动注释 {dedupResult.CommentedTypes.Count} 个重复的类型定义");
                    foreach (var commented in dedupResult.CommentedTypes.Take(5)) // 只显示前5个
                    {
                        result.Warnings.Add($"  - {commented}");
                    }
                    if (dedupResult.CommentedTypes.Count > 5)
                    {
                        result.Warnings.Add($"  ... 还有 {dedupResult.CommentedTypes.Count - 5} 个");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"生成失败: {ex.Message}";
                result.ErrorDetails = ex.ToString();
            }

            return result;
        }

        /// <summary>
        /// 从 Feature 对象直接生成客户端代码
        /// </summary>
        public GenerationResult GenerateClientCodeFromFeatures(
            Dictionary<string, Feature> features,
            string outputDirectory,
            string? customNamespace = null,
            Action<string>? progressCallback = null)
        {
            var result = new GenerationResult { Success = true };
            var generatedFiles = new List<string>();

            try
            {
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                // 创建临时目录用于存放 Feature XML 文件
                var tempDir = Path.Combine(Path.GetTempPath(), "SilaGen_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                try
                {
                    foreach (var kvp in features)
                    {
                        var feature = kvp.Value;
                        try
                        {
                            progressCallback?.Invoke($"正在处理: {feature.Identifier}");

                            // 将 Feature 对象保存为临时 XML 文件
                            var tempFeatureFile = Path.Combine(tempDir, $"{feature.Identifier}.sila.xml");
                            FeatureSerializer.Save(feature, tempFeatureFile);

                            // 生成代码
                            var files = GenerateClientFromFile(
                                tempFeatureFile,
                                feature.Identifier,
                                outputDirectory,
                                customNamespace ?? "Sila2Client",
                                progressCallback);

                            generatedFiles.AddRange(files);
                            progressCallback?.Invoke($"✓ 完成: {feature.Identifier}");
                        }
                        catch (Exception ex)
                        {
                            result.Warnings.Add($"处理 {feature.Identifier} 时出错: {ex.Message}");
                            progressCallback?.Invoke($"✗ 错误: {feature.Identifier} - {ex.Message}");
                        }
                    }

                    result.GeneratedFiles = generatedFiles;
                    result.Message = $"成功生成 {generatedFiles.Count} 个文件";

                    // 复制必需的 DLL 到输出目录
                    CopyRequiredDllsToClientDirectory(outputDirectory, progressCallback);

                    // 去重处理：检查并注释重复的类型定义
                    progressCallback?.Invoke("检查重复的类型定义...");
                    var dedupResult = _deduplicator.DeduplicateGeneratedCode(outputDirectory, progressCallback);
                    if (dedupResult.Success && dedupResult.CommentedTypes.Count > 0)
                    {
                        result.Warnings.Add($"已自动注释 {dedupResult.CommentedTypes.Count} 个重复的类型定义");
                        foreach (var commented in dedupResult.CommentedTypes.Take(5)) // 只显示前5个
                        {
                            result.Warnings.Add($"  - {commented}");
                        }
                        if (dedupResult.CommentedTypes.Count > 5)
                        {
                            result.Warnings.Add($"  ... 还有 {dedupResult.CommentedTypes.Count - 5} 个");
                        }
                    }
                }
                finally
                {
                    // 清理临时目录
                    try
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                    }
                    catch { /* 忽略清理错误 */ }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"生成失败: {ex.Message}";
                result.ErrorDetails = ex.ToString();
            }

            return result;
        }

        /// <summary>
        /// 使用 SilaGeneratorApi 从 Feature XML 文件生成客户端代码
        /// </summary>
        private List<string> GenerateClientFromFile(
            string featureFile,
            string featureIdentifier,
            string outputDirectory,
            string ns,
            Action<string>? progressCallback)
        {
            var generatedFiles = new List<string>();

            try
            {
                // 定义输出文件路径（直接在输出目录中）
                var interfacePath = Path.Combine(outputDirectory, $"I{featureIdentifier}.cs");
                var dtoPath = Path.Combine(outputDirectory, $"{featureIdentifier}Dtos.cs");
                var clientPath = Path.Combine(outputDirectory, $"{featureIdentifier}Client.cs");

                progressCallback?.Invoke($"  → 调用 SilaGeneratorApi 生成代码...");
                
                // 使用 SilaGeneratorApi 生成代码
                using (var api = new SilaGeneratorApi())
                {
                    // 1. 生成接口文件（如果启用）
                    if (_generateInterface)
                    {
                        progressCallback?.Invoke($"  → 生成接口...");
                        api.GenerateInterface(
                            featurePath: featureFile,
                            outputPath: interfacePath,
                            ns: ns
                        );
                    }

                    // 2. 生成 DTOs 和 Client
                    progressCallback?.Invoke($"  → 生成 DTOs 和 Client...");
                    api.GenerateProvider(
                        featurePath: featureFile,
                        dtoPath: dtoPath,
                        providerPath: clientPath,
                        clientPath: null,
                        ns: ns,
                        clientOnly: true,
                        serverOnly: false,
                        importedNamespaces: null
                    );
                }

                progressCallback?.Invoke($"  → 代码生成成功");

                // 检查生成的文件
                var fileList = new List<(string filePath, string fileName)>
                {
                    (dtoPath, $"{featureIdentifier}Dtos.cs"),
                    (clientPath, $"{featureIdentifier}Client.cs")
                };

                if (_generateInterface)
                {
                    fileList.Insert(0, (interfacePath, $"I{featureIdentifier}.cs"));
                }

                foreach (var (filePath, fileName) in fileList)
                {
                    if (File.Exists(filePath))
                    {
                        generatedFiles.Add(filePath);
                        progressCallback?.Invoke($"    ✓ {fileName}");
                    }
                    else
                    {
                        progressCallback?.Invoke($"    ⚠ 未找到: {fileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"  ✗ 错误: {ex.Message}");
                throw; // 重新抛出异常以便上层处理
            }

            return generatedFiles;
        }

        /// <summary>
        /// 复制 Tecan.Sila2 必需的 DLL 到目标目录
        /// 这些 DLL 是编译生成的客户端代码所必需的
        /// </summary>
        /// <param name="targetDirectory">目标目录</param>
        /// <param name="progressCallback">进度回调</param>
        private void CopyRequiredDllsToClientDirectory(string targetDirectory, Action<string>? progressCallback = null)
        {
            try
            {
                // 查找当前执行程序集所在目录（Generator.dll所在目录）
                var currentDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(currentDir))
                {
                    progressCallback?.Invoke("  ⚠ 警告: 无法确定 Generator.dll 所在目录");
                    return;
                }

                // 必需的DLL列表
                var requiredDlls = new[]
                {
                    "protobuf-net.dll",
                    "protobuf-net.Core.dll",
                    "Tecan.Sila2.dll",
                    "Tecan.Sila2.Contracts.dll",
                    "Tecan.Sila2.Annotations.dll",
                    "Tecan.Sila2.DynamicClient.dll",  // 支持动态类型（AnyTypeDto等）
                    "Grpc.Core.Api.dll",
                    "Grpc.Core.dll",
                    "Grpc.Net.Client.dll",
                    "Grpc.Net.Common.dll",
                    "Newtonsoft.Json.dll"  // JSON序列化支持（用于[JsonConstructor]特性）
                };

                progressCallback?.Invoke("  → 复制必需的 DLL 到客户端目录...");

                int copiedCount = 0;
                foreach (var dllName in requiredDlls)
                {
                    var sourcePath = Path.Combine(currentDir, dllName);
                    if (File.Exists(sourcePath))
                    {
                        var destPath = Path.Combine(targetDirectory, dllName);
                        try
                        {
                            File.Copy(sourcePath, destPath, overwrite: true);
                            copiedCount++;
                        }
                        catch (Exception ex)
                        {
                            progressCallback?.Invoke($"  ⚠ 复制 {dllName} 失败: {ex.Message}");
                        }
                    }
                }

                if (copiedCount > 0)
                {
                    progressCallback?.Invoke($"  ✓ 已复制 {copiedCount} 个必需的 DLL");
                }
                else
                {
                    progressCallback?.Invoke("  ⚠ 警告: 未找到任何必需的 DLL");
                }
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"  ⚠ 复制 DLL 时出错: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 代码生成结果
    /// </summary>
    public class GenerationResult
    {
        /// <summary>生成是否成功</summary>
        public bool Success { get; set; }

        /// <summary>结果消息</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>已生成的文件列表（完整路径）</summary>
        public List<string> GeneratedFiles { get; set; } = new();

        /// <summary>警告信息列表</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>错误详情（仅在失败时有值）</summary>
        public string? ErrorDetails { get; set; }

        /// <summary>
        /// 获取摘要信息
        /// </summary>
        public string GetSummary()
        {
            var summary = Message;
            if (Warnings.Any())
            {
                summary += $"\n警告: {Warnings.Count} 个";
            }
            return summary;
        }
    }
}
