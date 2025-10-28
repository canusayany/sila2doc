using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SilaGeneratorWpf.Models;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// D3驱动编排服务 - 封装完整的生成和编译流程（无UI依赖）
    /// </summary>
    public class D3DriverOrchestrationService
    {
        private readonly ILogger _logger;
        private readonly ClientCodeGenerator _codeGenerator;
        private readonly D3DriverGeneratorService _generatorService;

        public D3DriverOrchestrationService()
        {
            _logger = LoggerService.GetLogger<D3DriverOrchestrationService>();
            _codeGenerator = new ClientCodeGenerator();
            _generatorService = new D3DriverGeneratorService();
        }

        /// <summary>
        /// 完整的生成流程（从特性ID到D3项目）
        /// </summary>
        public async Task<OrchestrationResult> GenerateD3ProjectAsync(
            D3GenerationRequest request,
            Action<string>? progressCallback = null)
        {
            try
            {
                _logger.LogInformation($"开始生成D3项目: {request.Brand}_{request.Model}");
                progressCallback?.Invoke("========== 开始生成D3项目 ==========");

                // 1. 生成命名空间和输出目录
                progressCallback?.Invoke("[1/6] 生成命名空间和输出目录...");
                var namespaceName = $"BR.ECS.DeviceDriver.{request.DeviceType}.{request.Brand}_{request.Model}";
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var outputPath = Path.Combine(
                    Path.GetTempPath(),
                    "Sila2D3Gen",
                    $"{request.Brand}_{request.Model}_{timestamp}");

                progressCallback?.Invoke($"  命名空间: {namespaceName}");
                progressCallback?.Invoke($"  输出目录: {outputPath}");

                // 2. 生成客户端代码
                progressCallback?.Invoke("[2/6] 生成客户端代码...");
                string? clientCodePath = null;

                if (request.IsOnlineSource)
                {
                    // 在线服务器模式
                    if (request.Features != null && request.Features.Any())
                    {
                        // 优先使用 Feature 对象
                        progressCallback?.Invoke($"  从 Feature 对象生成: {request.Features.Count} 个特性");
                        clientCodePath = await GenerateClientCodeFromFeaturesAsync(
                            outputPath,
                            namespaceName,
                            request.Features,
                            progressCallback);
                    }
                    else if (request.FeatureIds != null && request.FeatureIds.Any())
                    {
                        // 备用：使用 FeatureIds（需要服务器连接）
                        progressCallback?.Invoke($"  ⚠ 警告：缺少 Feature 对象，请传入 Features 参数");
                        return OrchestrationResult.Failure("在线模式需要提供 Feature 对象（而不是 FeatureIds）");
                    }
                    else
                    {
                        return OrchestrationResult.Failure("在线模式需要提供 Features 或 FeatureIds");
                    }

                    if (clientCodePath == null)
                    {
                        return OrchestrationResult.Failure("客户端代码生成失败");
                    }
                }
                else
                {
                    // 本地XML文件模式
                    if (request.LocalFeatureXmlPaths == null || !request.LocalFeatureXmlPaths.Any())
                    {
                        return OrchestrationResult.Failure("未提供本地特性文件路径");
                    }

                    progressCallback?.Invoke($"  从本地XML生成: {request.LocalFeatureXmlPaths.Count} 个文件");
                    clientCodePath = await GenerateClientCodeFromLocalXmlAsync(
                        outputPath,
                        namespaceName,
                        request.LocalFeatureXmlPaths,
                        progressCallback);

                    if (clientCodePath == null)
                    {
                        return OrchestrationResult.Failure("客户端代码生成失败");
                    }
                }

                progressCallback?.Invoke("  ✓ 客户端代码生成完成");

                // 3. 分析客户端代码
                progressCallback?.Invoke("[3/6] 分析客户端代码...");
                var analyzer = new ClientCodeAnalyzer();
                var analysisResult = analyzer.Analyze(clientCodePath);
                progressCallback?.Invoke($"  检测到 {analysisResult.Features.Count} 个特性");
                progressCallback?.Invoke($"  检测到 {analysisResult.Features.Sum(f => f.Methods.Count)} 个方法");

                // 4. 应用方法分类（如果提供）
                if (request.MethodClassifications != null && request.MethodClassifications.Any())
                {
                    progressCallback?.Invoke("[4/6] 应用方法分类...");
                    ApplyMethodClassifications(analysisResult.Features, request.MethodClassifications);
                    progressCallback?.Invoke($"  应用了 {request.MethodClassifications.Count} 个方法分类");
                }
                else
                {
                    progressCallback?.Invoke("[4/6] 跳过方法分类（使用默认分类）");
                }

                // 5. 生成D3驱动代码
                progressCallback?.Invoke("[5/6] 生成D3驱动代码...");
                var config = new D3DriverGenerationConfig
                {
                    Brand = request.Brand.Trim(),
                    Model = request.Model.Trim(),
                    DeviceType = request.DeviceType.Trim(),
                    Developer = request.Developer.Trim(),
                    Namespace = namespaceName,
                    OutputPath = outputPath,
                    ClientCodePath = clientCodePath,
                    Features = analysisResult.Features,
                    IsOnlineSource = request.IsOnlineSource,
                    ServerUuid = request.ServerUuid,
                    LocalFeatureXmlPaths = request.LocalFeatureXmlPaths,
                    ServerIp = request.ServerIp,
                    ServerPort = request.ServerPort
                };

                var generationResult = _generatorService.Generate(
                    config,
                    message => progressCallback?.Invoke($"  {message}"));

                if (!generationResult.Success)
                {
                    return OrchestrationResult.Failure($"D3驱动生成失败: {generationResult.Message}");
                }

                progressCallback?.Invoke("  ✓ D3驱动代码生成完成");

                // 6. 返回结果
                progressCallback?.Invoke("[6/6] 生成完成！");
                progressCallback?.Invoke($"========== 项目已生成: {outputPath} ==========");

                return new OrchestrationResult
                {
                    Success = true,
                    Message = "D3项目生成成功",
                    ProjectPath = outputPath,
                    AnalysisResult = analysisResult,
                    GenerationConfig = config
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成D3项目时发生错误");
                return OrchestrationResult.Failure($"发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 编译D3项目
        /// </summary>
        public async Task<CompilationResult> CompileD3ProjectAsync(
            string projectPath,
            Action<string>? progressCallback = null)
        {
            try
            {
                _logger.LogInformation($"开始编译D3项目: {projectPath}");
                progressCallback?.Invoke("========== 开始编译D3项目 ==========");

                // 优先编译.sln文件（包含主项目和测试项目）
                var slnFiles = Directory.GetFiles(projectPath, "*.sln", SearchOption.TopDirectoryOnly);
                if (slnFiles.Any())
                {
                    var slnFile = slnFiles.First();
                    progressCallback?.Invoke($"  解决方案文件: {Path.GetFileName(slnFile)}");
                    progressCallback?.Invoke("  执行 dotnet build...");
                    
                    var compileResult = await Task.Run(() => 
                        _generatorService.CompileProject(
                            slnFile,
                            message => progressCallback?.Invoke($"    {message}")));

                    if (compileResult.Success)
                    {
                        progressCallback?.Invoke("  ✓ 编译成功");
                        progressCallback?.Invoke($"  DLL路径: {compileResult.DllPath}");
                        progressCallback?.Invoke($"========== 编译完成 ==========");
                    }
                    else
                    {
                        progressCallback?.Invoke("  ✗ 编译失败");
                        progressCallback?.Invoke($"========== 编译失败 ==========");
                    }

                    return compileResult;
                }

                // 备选：在子文件夹中查找.csproj文件
                var csprojFiles = Directory.GetFiles(projectPath, "*.csproj", SearchOption.AllDirectories);
                if (!csprojFiles.Any())
                {
                    return new CompilationResult
                    {
                        Success = false,
                        Message = "未找到.sln或.csproj文件"
                    };
                }

                // 排除TestConsole项目，只编译主项目
                var mainProjectFile = csprojFiles.FirstOrDefault(f => !f.Contains("TestConsole"));
                if (mainProjectFile == null)
                {
                    mainProjectFile = csprojFiles.First();
                }

                progressCallback?.Invoke($"  项目文件: {Path.GetFileName(mainProjectFile)}");
                progressCallback?.Invoke("  执行 dotnet build...");
                
                var result = await Task.Run(() => 
                    _generatorService.CompileProject(
                        mainProjectFile,
                        message => progressCallback?.Invoke($"    {message}")));

                if (result.Success)
                {
                    progressCallback?.Invoke("  ✓ 编译成功");
                    progressCallback?.Invoke($"  DLL路径: {result.DllPath}");
                    progressCallback?.Invoke($"========== 编译完成 ==========");
                }
                else
                {
                    progressCallback?.Invoke("  ✗ 编译失败");
                    progressCallback?.Invoke($"========== 编译失败 ==========");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "编译D3项目时发生错误");
                return new CompilationResult
                {
                    Success = false,
                    Message = $"发生异常: {ex.Message}"
                };
            }
        }

        #region 私有辅助方法

        /// <summary>
        /// 从在线服务器生成客户端代码（使用Feature对象）
        /// </summary>
        private async Task<string?> GenerateClientCodeFromOnlineServerAsync(
            string outputPath,
            string serverIp,
            int serverPort,
            List<string> featureIds,
            Action<string>? progressCallback)
        {
            try
            {
                // 注意：实际实现需要从 ViewModel 传入 Feature 对象
                // 这里是简化版本，假设已经有 Feature 对象
                progressCallback?.Invoke("    ⚠ 在线服务器模式需要传入 Feature 对象");
                progressCallback?.Invoke("    ⚠ 请使用重载方法 GenerateClientCodeFromFeatures");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从在线服务器生成客户端代码失败");
                progressCallback?.Invoke($"    ✗ 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从Feature对象生成客户端代码
        /// </summary>
        private async Task<string?> GenerateClientCodeFromFeaturesAsync(
            string outputPath,
            string namespaceName,
            Dictionary<string, Tecan.Sila2.Feature> features,
            Action<string>? progressCallback)
        {
            try
            {
                // 直接生成到最终项目的Sila2Client文件夹，避免GeneratedClient临时文件夹
                var projectDir = Path.Combine(outputPath, namespaceName);
                var clientCodePath = Path.Combine(projectDir, "Sila2Client");
                Directory.CreateDirectory(clientCodePath);

                var result = await Task.Run(() => _codeGenerator.GenerateClientCodeFromFeatures(
                    features,
                    clientCodePath,
                    "Sila2Client",
                    message => progressCallback?.Invoke($"    {message}")));

                if (!result.Success)
                {
                    progressCallback?.Invoke($"    ✗ 失败: {result.Message}");
                    return null;
                }

                return clientCodePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从Feature对象生成客户端代码失败");
                progressCallback?.Invoke($"    ✗ 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从本地XML文件生成客户端代码
        /// </summary>
        private async Task<string?> GenerateClientCodeFromLocalXmlAsync(
            string outputPath,
            string namespaceName,
            List<string> xmlPaths,
            Action<string>? progressCallback)
        {
            try
            {
                // 直接生成到最终项目的Sila2Client文件夹，避免GeneratedClient临时文件夹
                var projectDir = Path.Combine(outputPath, namespaceName);
                var clientCodePath = Path.Combine(projectDir, "Sila2Client");
                Directory.CreateDirectory(clientCodePath);

                var result = await Task.Run(() => _codeGenerator.GenerateClientCode(
                    xmlPaths,
                    clientCodePath,
                    "Sila2Client",
                    message => progressCallback?.Invoke($"    {message}")));

                if (!result.Success)
                {
                    progressCallback?.Invoke($"    ✗ 失败: {result.Message}");
                    return null;
                }

                return clientCodePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从本地XML生成客户端代码失败");
                progressCallback?.Invoke($"    ✗ 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 应用方法分类
        /// </summary>
        private void ApplyMethodClassifications(
            List<ClientFeatureInfo> features,
            Dictionary<string, bool> classifications)
        {
            foreach (var feature in features)
            {
                foreach (var method in feature.Methods)
                {
                    var methodKey = $"{feature.FeatureName}.{method.Name}";
                    if (classifications.TryGetValue(methodKey, out var isMaintenance))
                    {
                        // 使用新的字段
                        method.IsMaintenance = isMaintenance;
                        method.IsOperations = !isMaintenance;
                    }
                }
            }
        }

        #endregion

        #region 方法分类调整

        /// <summary>
        /// 调整方法分类并重新生成D3Driver.cs
        /// </summary>
        /// <param name="projectPath">项目路径</param>
        /// <param name="methodClassifications">方法分类字典（键：FeatureName.MethodName，值：是否为维护方法）</param>
        /// <param name="progressCallback">进度回调</param>
        /// <returns>操作结果</returns>
        public async Task<OrchestrationResult> AdjustMethodClassificationsAsync(
            string projectPath,
            Dictionary<string, bool> methodClassifications,
            Action<string>? progressCallback = null)
        {
            try
            {
                _logger.LogInformation($"调整方法分类: {projectPath}");
                progressCallback?.Invoke("========== 开始调整方法分类 ==========");

                // 1. 查找生成的客户端代码目录
                progressCallback?.Invoke("[1/3] 查找客户端代码...");
                
                // 尝试新的路径结构：ProjectName/Sila2Client
                var projectDirs = Directory.GetDirectories(projectPath);
                string? clientCodePath = null;
                
                foreach (var dir in projectDirs)
                {
                    var sila2ClientPath = Path.Combine(dir, "Sila2Client");
                    if (Directory.Exists(sila2ClientPath))
                    {
                        clientCodePath = sila2ClientPath;
                        break;
                    }
                }
                
                // 兼容旧路径：GeneratedClient（已废弃）
                if (clientCodePath == null)
                {
                    var legacyPath = Path.Combine(projectPath, "GeneratedClient");
                    if (Directory.Exists(legacyPath))
                    {
                        clientCodePath = legacyPath;
                        progressCallback?.Invoke("  ⚠ 使用旧版GeneratedClient路径（已废弃）");
                    }
                }
                
                if (clientCodePath == null)
                {
                    return OrchestrationResult.Failure("未找到客户端代码目录（Sila2Client）");
                }

                // 2. 重新分析客户端代码
                progressCallback?.Invoke("[2/3] 分析客户端代码...");
                var analyzer = new ClientCodeAnalyzer();
                var analysisResult = analyzer.Analyze(clientCodePath);
                progressCallback?.Invoke($"  检测到 {analysisResult.Features.Count} 个特性");

                // 3. 应用方法分类
                progressCallback?.Invoke($"  应用 {methodClassifications.Count} 个方法分类...");
                ApplyMethodClassifications(analysisResult.Features, methodClassifications);

                // 4. 查找现有的配置信息（从现有文件推断）
                progressCallback?.Invoke("[3/3] 重新生成D3Driver.cs...");
                var config = InferConfigFromProject(projectPath, analysisResult.Features);
                if (config == null)
                {
                    return OrchestrationResult.Failure("无法推断项目配置");
                }

                // 5. 重新生成D3Driver.cs
                var result = await Task.Run(() => _generatorService.RegenerateD3Driver(
                    config,
                    message => progressCallback?.Invoke($"  {message}")));

                if (!result.Success)
                {
                    return OrchestrationResult.Failure($"重新生成失败: {result.Message}");
                }

                progressCallback?.Invoke("  ✓ D3Driver.cs已更新");
                progressCallback?.Invoke("========== 方法分类调整完成 ==========");

                return new OrchestrationResult
                {
                    Success = true,
                    Message = "方法分类已调整，D3Driver.cs已更新",
                    ProjectPath = projectPath,
                    AnalysisResult = analysisResult,
                    GenerationConfig = config
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调整方法分类时发生错误");
                return OrchestrationResult.Failure($"发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 从项目目录推断生成配置
        /// </summary>
        private D3DriverGenerationConfig? InferConfigFromProject(string projectPath, List<ClientFeatureInfo> features)
        {
            try
            {
                // 查找.csproj文件
                var csprojFiles = Directory.GetFiles(projectPath, "*.csproj", SearchOption.TopDirectoryOnly);
                if (!csprojFiles.Any())
                {
                    _logger.LogWarning("未找到.csproj文件");
                    return null;
                }

                var projectFileName = Path.GetFileNameWithoutExtension(csprojFiles.First());
                // 项目名格式：{Brand}{Model}.D3Driver
                var projectName = projectFileName.Replace(".D3Driver", "");

                // 从现有文件读取命名空间
                var d3DriverFile = Path.Combine(projectPath, "D3Driver.cs");
                if (!File.Exists(d3DriverFile))
                {
                    _logger.LogWarning("未找到D3Driver.cs文件");
                    return null;
                }

                var fileContent = File.ReadAllText(d3DriverFile);
                var namespaceMatch = System.Text.RegularExpressions.Regex.Match(fileContent, @"namespace\s+([\w\.]+)");
                var namespaceName = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : $"BR.ECS.DeviceDriver.Unknown.{projectName}";

                // 从命名空间推断DeviceType, Brand, Model
                // 格式：BR.ECS.DeviceDriver.{DeviceType}.{Brand}_{Model}
                var parts = namespaceName.Split('.');
                var deviceType = parts.Length > 3 ? parts[3] : "Unknown";
                var brandModel = parts.Length > 4 ? parts[4].Split('_') : new[] { projectName, "Unknown" };
                var brand = brandModel.Length > 0 ? brandModel[0] : projectName;
                var model = brandModel.Length > 1 ? brandModel[1] : "Unknown";

                // 查找实际的ClientCodePath
                string? actualClientCodePath = null;
                var projectDirs = Directory.GetDirectories(projectPath);
                foreach (var dir in projectDirs)
                {
                    var sila2ClientPath = Path.Combine(dir, "Sila2Client");
                    if (Directory.Exists(sila2ClientPath))
                    {
                        actualClientCodePath = sila2ClientPath;
                        break;
                    }
                }
                
                // 兼容旧路径
                if (actualClientCodePath == null)
                {
                    actualClientCodePath = Path.Combine(projectPath, "GeneratedClient");
                }
                
                return new D3DriverGenerationConfig
                {
                    Brand = brand,
                    Model = model,
                    DeviceType = deviceType,
                    Developer = "Bioyond",
                    Namespace = namespaceName,
                    OutputPath = projectPath,
                    ClientCodePath = actualClientCodePath,
                    Features = features
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "推断项目配置失败");
                return null;
            }
        }

        #endregion
    }

    #region 请求和结果模型

    /// <summary>
    /// D3生成请求
    /// </summary>
    public class D3GenerationRequest
    {
        /// <summary>
        /// 设备品牌
        /// </summary>
        public string Brand { get; set; } = string.Empty;

        /// <summary>
        /// 设备型号
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// 设备类型
        /// </summary>
        public string DeviceType { get; set; } = string.Empty;

        /// <summary>
        /// 开发者
        /// </summary>
        public string Developer { get; set; } = "Bioyond";

        /// <summary>
        /// 是否从在线服务器生成
        /// </summary>
        public bool IsOnlineSource { get; set; }

        /// <summary>
        /// 服务器IP（在线模式）
        /// </summary>
        public string? ServerIp { get; set; }

        /// <summary>
        /// 服务器端口（在线模式）
        /// </summary>
        public int? ServerPort { get; set; }

        /// <summary>
        /// 服务器UUID（在线模式）
        /// </summary>
        public string? ServerUuid { get; set; }

        /// <summary>
        /// Feature对象（在线模式优先使用此项）
        /// </summary>
        public Dictionary<string, Tecan.Sila2.Feature>? Features { get; set; }

        /// <summary>
        /// 特性ID列表（在线模式，如果Features为空则使用）
        /// </summary>
        public List<string>? FeatureIds { get; set; }

        /// <summary>
        /// 本地特性XML文件路径列表（本地模式）
        /// </summary>
        public List<string>? LocalFeatureXmlPaths { get; set; }

        /// <summary>
        /// 方法分类（键：FeatureName.MethodName，值：是否为维护方法）
        /// </summary>
        public Dictionary<string, bool>? MethodClassifications { get; set; }
    }

    /// <summary>
    /// 编排结果
    /// </summary>
    public class OrchestrationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ProjectPath { get; set; }
        public ClientAnalysisResult? AnalysisResult { get; set; }
        public D3DriverGenerationConfig? GenerationConfig { get; set; }

        public static OrchestrationResult Failure(string message) =>
            new OrchestrationResult { Success = false, Message = message };
    }

    #endregion
}

