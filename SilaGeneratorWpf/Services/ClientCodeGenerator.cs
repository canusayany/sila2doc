using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Tecan.Sila2;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// SiLA2 客户端代码生成器 - 使用预编译的 SilaGen.exe
    /// 
    /// 使用说明：
    /// 1. SilaGen.exe 已包含在输出目录中 (reflib\SilaGen.exe)
    /// 2. 支持从 Feature XML 文件或 Feature 对象直接生成客户端代码
    /// 3. 生成三个文件：接口（I*.cs）、DTOs（*Dtos.cs）、客户端（*Client.cs）
    /// 
    /// 部署说明：
    /// - SilaGen.exe 会自动复制到输出目录
    /// - 发布时需要将 SilaGen.exe 与主应用程序一起部署
    /// </summary>
    public class ClientCodeGenerator
    {
        private string? _silaGenPath;

        public ClientCodeGenerator()
        {
            // 查找 SilaGen.exe - 应该在应用程序所在目录或 reflib 子目录
            var possiblePaths = new List<string>
            {
                // 优先查找应用目录
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SilaGen.exe"),
                // 其次查找 reflib 子目录（调试时的位置）
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reflib", "SilaGen.exe"),
                // 发布后单文件的情况（与主exe同目录）
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SilaGen.exe"),
            };

            _silaGenPath = null;
            
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    _silaGenPath = path;
                    break;
                }
            }

            // 如果在标准位置找不到，尝试从应用目录提取或创建符号链接
            if (string.IsNullOrEmpty(_silaGenPath) || !File.Exists(_silaGenPath))
            {
                // 记录找不到的情况，但不抛出异常（延迟到实际使用时）
                var searchedPaths = string.Join("\n  ", possiblePaths);
                System.Diagnostics.Debug.WriteLine($"警告: 找不到 SilaGen.exe。已尝试的路径:\n  {searchedPaths}");
                
                // 如果所有查找都失败，使用第一个预期路径作为备用
                _silaGenPath = possiblePaths[0];
            }
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
                if (string.IsNullOrEmpty(_silaGenPath) || !File.Exists(_silaGenPath))
                {
                    result.Success = false;
                    result.Message = $"错误: 找不到 SilaGen.exe";
                    return result;
                }

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
                if (string.IsNullOrEmpty(_silaGenPath) || !File.Exists(_silaGenPath))
                {
                    result.Success = false;
                    result.Message = $"错误: 找不到 SilaGen.exe";
                    return result;
                }

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
        /// 使用 SilaGen.exe 从 Feature XML 文件生成客户端代码
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
                if (string.IsNullOrEmpty(_silaGenPath) || !File.Exists(_silaGenPath))
                {
                    throw new InvalidOperationException($"SilaGen.exe 未找到: {_silaGenPath}");
                }

                // 定义输出文件路径（直接在输出目录中）
                var interfacePath = Path.Combine(outputDirectory, $"I{featureIdentifier}.cs");
                var dtoPath = Path.Combine(outputDirectory, $"{featureIdentifier}Dtos.cs");
                var clientPath = Path.Combine(outputDirectory, $"{featureIdentifier}Client.cs");

                progressCallback?.Invoke($"  → 调用 SilaGen.exe 生成代码...");
                
                // 使用 SilaGen.exe 直接生成所有必需的文件
                // 参数: generate-provider <featureFile> <dtoOutputPath> <clientOutputPath> --namespace <namespace> --client-only
                var args = $"generate-provider \"{featureFile}\" \"{dtoPath}\" \"{clientPath}\" " +
                          $"--namespace \"{ns}\" --client-only";

                if (!ExecuteGenerator(args, progressCallback))
                {
                    throw new InvalidOperationException("SilaGen 执行失败");
                }

                progressCallback?.Invoke($"  → SilaGen.exe 执行成功");

                // 检查生成的文件
                var fileList = new[] 
                { 
                    (dtoPath, $"{featureIdentifier}Dtos.cs"),
                    (clientPath, $"{featureIdentifier}Client.cs")
                };

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
            }

            return generatedFiles;
        }

        /// <summary>
        /// 执行 SilaGen.exe 命令并检查结果
        /// </summary>
        private bool ExecuteGenerator(string arguments, Action<string>? progressCallback)
        {
            try
            {
                var processInfo = new ProcessStartInfo(_silaGenPath!, arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("无法启动 SilaGen.exe 进程");
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var errorMsg = string.IsNullOrEmpty(error) ? output : error;
                    progressCallback?.Invoke($"  ✗ 执行失败 (代码 {process.ExitCode}): {errorMsg}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"  ✗ 执行异常: {ex.Message}");
                return false;
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
