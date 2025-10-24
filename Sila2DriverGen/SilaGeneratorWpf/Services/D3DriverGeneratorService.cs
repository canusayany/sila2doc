using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using SilaGeneratorWpf.Models;
using SilaGeneratorWpf.Services.CodeDom;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// D3驱动生成服务（核心服务）
    /// </summary>
    public class D3DriverGeneratorService
    {
        private readonly ILogger _logger;

        public D3DriverGeneratorService()
        {
            _logger = LoggerService.GetLogger<D3DriverGeneratorService>();
        }

        /// <summary>
        /// 生成D3驱动
        /// </summary>
        public D3DriverGenerationResult Generate(
            D3DriverGenerationConfig config,
            Action<string>? progressCallback = null)
        {
            try
            {
                _logger.LogInformation("开始生成D3驱动");
                progressCallback?.Invoke("开始生成...");

                // 1. 创建输出目录结构
                progressCallback?.Invoke("创建输出目录结构...");
                CreateOutputDirectories(config);

                // 2. 复制客户端代码文件
                progressCallback?.Invoke("复制客户端代码文件...");
                CopyClientCode(config);

                // 3. 生成 AllSila2Client.cs
                progressCallback?.Invoke("生成 AllSila2Client.cs...");
                GenerateAllSila2Client(config);

                // 4. 生成 Sila2Base.cs
                progressCallback?.Invoke("生成 Sila2Base.cs...");
                GenerateSila2Base(config);

                // 5. 生成 CommunicationPars.cs
                progressCallback?.Invoke("生成 CommunicationPars.cs...");
                GenerateCommunicationPars(config);

                // 6. 生成 D3Driver.cs
                progressCallback?.Invoke("生成 D3Driver.cs...");
                GenerateD3Driver(config);

                // 7. 生成项目文件
                progressCallback?.Invoke("生成项目文件...");
                GenerateProjectFiles(config);

                // 8. 复制依赖库
                progressCallback?.Invoke("复制依赖库...");
                CopyDependencyLibraries(config);

                // 9. 生成解决方案文件
                progressCallback?.Invoke("生成解决方案文件...");
                GenerateSolutionFile(config);

                progressCallback?.Invoke("生成完成！");
                _logger.LogInformation("D3驱动代码生成成功");

                return new D3DriverGenerationResult
                {
                    Success = true,
                    Message = $"成功生成 D3 驱动（{config.Features.Count} 个特性，共 {config.Features.Sum(f => f.Methods.Count)} 个方法）"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成D3驱动失败");
                return new D3DriverGenerationResult
                {
                    Success = false,
                    Message = ex.Message,
                    ErrorDetails = ex.StackTrace
                };
            }
        }

        /// <summary>
        /// 创建输出目录结构
        /// </summary>
        private void CreateOutputDirectories(D3DriverGenerationConfig config)
        {
            _logger.LogInformation($"创建输出目录: {config.OutputPath}");

            Directory.CreateDirectory(config.OutputPath);
            Directory.CreateDirectory(Path.Combine(config.OutputPath, "Sila2Client"));
            Directory.CreateDirectory(Path.Combine(config.OutputPath, "lib"));
        }

        /// <summary>
        /// 复制客户端代码文件
        /// </summary>
        private void CopyClientCode(D3DriverGenerationConfig config)
        {
            _logger.LogInformation("复制客户端代码文件");

            var clientCodeDir = Path.Combine(config.OutputPath, "Sila2Client");
            var sourceFiles = Directory.GetFiles(config.ClientCodePath, "*.cs", SearchOption.TopDirectoryOnly);

            foreach (var sourceFile in sourceFiles)
            {
                var fileName = Path.GetFileName(sourceFile);
                var destFile = Path.Combine(clientCodeDir, fileName);
                File.Copy(sourceFile, destFile, overwrite: true);
                _logger.LogDebug($"复制文件: {fileName}");
            }

            _logger.LogInformation($"复制了 {sourceFiles.Length} 个客户端代码文件");
        }

        /// <summary>
        /// 生成 AllSila2Client.cs
        /// </summary>
        private void GenerateAllSila2Client(D3DriverGenerationConfig config)
        {
            _logger.LogInformation("生成 AllSila2Client.cs");

            var generator = new AllSila2ClientGenerator();
            var outputPath = Path.Combine(config.OutputPath, "AllSila2Client.cs");
            generator.Generate(config.Features, outputPath, config.Namespace);
        }

        /// <summary>
        /// 生成 Sila2Base.cs
        /// </summary>
        private void GenerateSila2Base(D3DriverGenerationConfig config)
        {
            _logger.LogInformation("生成 Sila2Base.cs");

            var generator = new Sila2BaseGenerator();
            var outputPath = Path.Combine(config.OutputPath, "Sila2Base.cs");
            generator.Generate(outputPath, config.Namespace, "Sila2Client");
        }

        /// <summary>
        /// 生成 CommunicationPars.cs
        /// </summary>
        private void GenerateCommunicationPars(D3DriverGenerationConfig config)
        {
            _logger.LogInformation("生成 CommunicationPars.cs");

            var generator = new CommunicationParsGenerator();
            var outputPath = Path.Combine(config.OutputPath, "CommunicationPars.cs");
            generator.Generate(outputPath, config.Namespace);
        }

        /// <summary>
        /// 生成 D3Driver.cs
        /// </summary>
        private void GenerateD3Driver(D3DriverGenerationConfig config)
        {
            _logger.LogInformation("生成 D3Driver.cs");

            // 收集所有方法信息
            var methods = CollectAllMethods(config.Features);

            var generator = new D3DriverGenerator();
            var outputPath = Path.Combine(config.OutputPath, "D3Driver.cs");
            generator.Generate(config, methods, outputPath);
        }

        /// <summary>
        /// 收集所有方法（处理命名冲突）
        /// </summary>
        private List<MethodGenerationInfo> CollectAllMethods(List<ClientFeatureInfo> features)
        {
            var methods = new List<MethodGenerationInfo>();
            var methodNameCount = new Dictionary<string, int>();

            // 统计方法名出现次数
            foreach (var feature in features)
            {
                foreach (var method in feature.Methods)
                {
                    if (!methodNameCount.ContainsKey(method.Name))
                        methodNameCount[method.Name] = 0;
                    methodNameCount[method.Name]++;
                }
            }

            // 生成方法信息
            foreach (var feature in features)
            {
                foreach (var method in feature.Methods)
                {
                    var finalName = method.Name;

                    // 处理命名冲突
                    if (methodNameCount[method.Name] > 1)
                    {
                        finalName = $"{feature.FeatureName}_{method.Name}";
                    }

                    // 创建方法副本
                    var methodInfo = new MethodGenerationInfo
                    {
                        Name = finalName,
                        OriginalName = method.OriginalName,
                        ReturnType = method.ReturnType,
                        Parameters = method.Parameters,
                        Description = method.Description,
                        IsIncluded = method.IsIncluded,
                        IsOperations = method.IsOperations,
                        IsMaintenance = method.IsMaintenance,
                        IsProperty = method.IsProperty,
                        PropertyName = method.PropertyName,
                        IsObservableCommand = method.IsObservableCommand,
                        IsObservable = method.IsObservable,
                        FeatureName = feature.FeatureName,
                        XmlDocumentation = method.XmlDocumentation,
                        RequiresJsonReturn = method.RequiresJsonReturn
                    };

                    methods.Add(methodInfo);
                }
            }

            return methods;
        }

        /// <summary>
        /// 生成项目文件
        /// </summary>
        private void GenerateProjectFiles(D3DriverGenerationConfig config)
        {
            _logger.LogInformation("生成项目文件");

            var projectName = $"{config.Brand}{config.Model}.D3Driver";
            var projectPath = Path.Combine(config.OutputPath, $"{projectName}.csproj");

            var projectContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Tecan.Sila2.Client.NetCore"" Version=""4.4.1"" />
    <PackageReference Include=""Tecan.Sila2.Features.Locking.Client"" Version=""4.4.1"" />
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.3"" />
  </ItemGroup>

  <ItemGroup>
    <Reference Include=""BR.ECS.Executor.Device.Domain.Contracts"">
      <HintPath>lib\BR.ECS.Executor.Device.Domain.Contracts.dll</HintPath>
    </Reference>
    <Reference Include=""BR.ECS.Executor.Device.Domain.Share"">
      <HintPath>lib\BR.ECS.Executor.Device.Domain.Share.dll</HintPath>
    </Reference>
    <Reference Include=""BR.ECS.Executor.Device.Infrastructure"">
      <HintPath>lib\BR.ECS.Executor.Device.Infrastructure.dll</HintPath>
    </Reference>
    <Reference Include=""BR.PC.Device.Sila2Discovery"">
      <HintPath>lib\BR.PC.Device.Sila2Discovery.dll</HintPath>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <Compile Remove=""GeneratedClient\**"" />
    <EmbeddedResource Remove=""GeneratedClient\**"" />
    <None Remove=""GeneratedClient\**"" />
  </ItemGroup>

</Project>
";

            File.WriteAllText(projectPath, projectContent);
            _logger.LogInformation($"生成项目文件: {projectPath}");
        }

        /// <summary>
        /// 复制依赖库
        /// </summary>
        private void CopyDependencyLibraries(D3DriverGenerationConfig config)
        {
            _logger.LogInformation("复制依赖库");

            var libDir = Path.Combine(config.OutputPath, "lib");
            
            // 从示例项目复制依赖库
            var sampleLibDir = Path.Combine(
                Path.GetDirectoryName(config.ClientCodePath) ?? "",
                "..",
                "BR.ECS.DeviceDriver.Sample.Test",
                "lib");

            // 如果示例 lib 目录不存在，尝试从当前目录查找
            if (!Directory.Exists(sampleLibDir))
            {
                // 从当前工作目录向上查找
                var currentDir = Directory.GetCurrentDirectory();
                var searchDir = currentDir;
                for (int i = 0; i < 5; i++)
                {
                    var testPath = Path.Combine(searchDir, "BR.ECS.DeviceDriver.Sample.Test", "lib");
                    if (Directory.Exists(testPath))
                    {
                        sampleLibDir = testPath;
                        break;
                    }
                    var parent = Directory.GetParent(searchDir);
                    if (parent == null) break;
                    searchDir = parent.FullName;
                }
            }

            if (Directory.Exists(sampleLibDir))
            {
                var dllFiles = Directory.GetFiles(sampleLibDir, "*.dll", SearchOption.TopDirectoryOnly);
                foreach (var dllFile in dllFiles)
                {
                    var fileName = Path.GetFileName(dllFile);
                    var destFile = Path.Combine(libDir, fileName);
                    File.Copy(dllFile, destFile, overwrite: true);
                    _logger.LogDebug($"复制库文件: {fileName}");
                }
                _logger.LogInformation($"复制了 {dllFiles.Length} 个依赖库文件");
            }
            else
            {
                _logger.LogWarning($"未找到依赖库目录: {sampleLibDir}，请手动复制依赖库");
            }
        }

        /// <summary>
        /// 生成测试控制台
        /// </summary>
        private void GenerateTestConsole(D3DriverGenerationConfig config)
        {
            _logger.LogInformation("生成测试控制台");

            var generator = new TestConsoleGenerator();
            generator.Generate(config.OutputPath, config);
        }

        /// <summary>
        /// 生成解决方案文件
        /// </summary>
        private void GenerateSolutionFile(D3DriverGenerationConfig config)
        {
            _logger.LogInformation("生成解决方案文件");

            var solutionName = $"{config.Brand}{config.Model}.D3Driver";
            var solutionPath = Path.Combine(config.OutputPath, $"{solutionName}.sln");

            var driverProjectName = $"{config.Brand}{config.Model}.D3Driver";
            var driverProjectPath = $"{driverProjectName}.csproj";
            var driverProjectGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();

            var testProjectName = $"{config.Brand}{config.Model}.TestConsole";
            var testProjectPath = $"TestConsole\\{testProjectName}.csproj";
            var testProjectGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();

            var solutionContent = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
";

            // 添加驱动项目
            solutionContent += $@"Project(""{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}"") = ""{driverProjectName}"", ""{driverProjectPath}"", ""{driverProjectGuid}""
EndProject
";

            // 添加 Global 配置
            solutionContent += @"Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
";

            // 驱动项目配置
            solutionContent += $@"		{driverProjectGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{driverProjectGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{driverProjectGuid}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{driverProjectGuid}.Release|Any CPU.Build.0 = Release|Any CPU
";

            solutionContent += @"	EndGlobalSection
EndGlobal
";

            File.WriteAllText(solutionPath, solutionContent);
            _logger.LogInformation($"生成解决方案文件: {solutionPath}");
        }

        /// <summary>
        /// 编译指定的项目文件（公共方法，用于独立编译）
        /// </summary>
        /// <param name="projectPath">项目文件路径（.csproj 或 .sln）</param>
        /// <param name="progressCallback">进度回调</param>
        /// <returns>编译结果</returns>
        public CompilationResult CompileProject(string projectPath, Action<string>? progressCallback = null)
        {
            _logger.LogInformation($"开始编译项目: {projectPath}");
            progressCallback?.Invoke("正在编译项目...");

            try
            {
                if (!File.Exists(projectPath))
                {
                    return new CompilationResult
                    {
                        Success = false,
                        Message = "项目文件不存在",
                        ErrorCount = 1
                    };
                }

                var workingDirectory = Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory();
                var compileResult = CompileProjectInternal(projectPath, workingDirectory, progressCallback);

                // 确定 DLL 路径
                var projectDir = Path.GetDirectoryName(projectPath);
                var dllPath = Path.Combine(projectDir!, "bin", "Release");

                return new CompilationResult
                {
                    Success = compileResult.Success,
                    Message = compileResult.Output,
                    DllPath = dllPath,
                    ErrorCount = compileResult.Errors,
                    WarningCount = compileResult.Warnings
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "编译项目失败");
                return new CompilationResult
                {
                    Success = false,
                    Message = $"编译失败: {ex.Message}\n{ex.StackTrace}",
                    ErrorCount = 1
                };
            }
        }

        /// <summary>
        /// 编译生成的项目（内部方法，从配置调用）
        /// </summary>
        private (bool Success, string Output, int Warnings, int Errors) CompileProject(D3DriverGenerationConfig config)
        {
            var projectName = $"{config.Brand}{config.Model}.D3Driver";
            var projectPath = Path.Combine(config.OutputPath, $"{projectName}.csproj");
            return CompileProjectInternal(projectPath, config.OutputPath, null);
        }

        /// <summary>
        /// 编译项目文件的核心实现
        /// </summary>
        private (bool Success, string Output, int Warnings, int Errors) CompileProjectInternal(
            string projectPath, 
            string workingDirectory,
            Action<string>? progressCallback = null)
        {
            _logger.LogInformation($"编译项目: {projectPath}");
            
            try
            {
                if (!File.Exists(projectPath))
                {
                    _logger.LogWarning($"项目文件不存在: {projectPath}");
                    return (false, "项目文件不存在", 0, 1);
                }
                
                // 先执行 dotnet restore
                _logger.LogInformation("开始还原NuGet包...");
                progressCallback?.Invoke("还原NuGet包...");
                
                var restoreProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"restore \"{projectPath}\"",
                        WorkingDirectory = workingDirectory,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                restoreProcess.Start();
                restoreProcess.WaitForExit(60000); // 等待最多60秒
                
                if (restoreProcess.ExitCode != 0)
                {
                    _logger.LogWarning("NuGet包还原失败，尝试继续编译...");
                }
                
                // 使用 dotnet build 编译
                _logger.LogInformation("开始编译项目...");
                progressCallback?.Invoke("编译项目...");
                
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"build \"{projectPath}\" -c Release",
                        WorkingDirectory = workingDirectory,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                var output = new System.Text.StringBuilder();
                var errors = new System.Text.StringBuilder();
                
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                    }
                };
                
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errors.AppendLine(e.Data);
                    }
                };
                
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                // 等待最多 120 秒
                bool finished = process.WaitForExit(120000);
                
                if (!finished)
                {
                    process.Kill();
                    _logger.LogError("编译超时");
                    return (false, "编译超时（超过 120 秒）", 0, 1);
                }
                
                var fullOutput = output.ToString() + errors.ToString();
                var exitCode = process.ExitCode;
                
                // 解析编译结果
                var warnings = System.Text.RegularExpressions.Regex.Matches(fullOutput, @"warning\s+CS\d+").Count;
                var compileErrors = System.Text.RegularExpressions.Regex.Matches(fullOutput, @"error\s+CS\d+").Count;
                
                if (exitCode == 0)
                {
                    _logger.LogInformation($"编译成功 (警告: {warnings})");
                    return (true, fullOutput, warnings, 0);
                }
                else
                {
                    _logger.LogError($"编译失败 (错误: {compileErrors}, 警告: {warnings})");
                    return (false, fullOutput, warnings, compileErrors);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "编译项目时发生异常");
                return (false, $"编译异常: {ex.Message}", 0, 1);
            }
        }

        /// <summary>
        /// 重新生成D3Driver.cs文件（用于调整方法特性后重新生成）
        /// </summary>
        /// <param name="config">生成配置</param>
        /// <param name="progressCallback">进度回调</param>
        /// <returns>生成结果</returns>
        public D3DriverGenerationResult RegenerateD3Driver(
            D3DriverGenerationConfig config,
            Action<string>? progressCallback = null)
        {
            try
            {
                progressCallback?.Invoke("重新生成 D3Driver.cs...");
                _logger.LogInformation("重新生成 D3Driver.cs");

                // 生成 D3Driver.cs
                GenerateD3Driver(config);

                progressCallback?.Invoke("✓ D3Driver.cs 已更新");
                _logger.LogInformation("D3Driver.cs 重新生成成功");

                return new D3DriverGenerationResult
                {
                    Success = true,
                    Message = "D3Driver.cs 已成功更新"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重新生成 D3Driver.cs 失败");
                return new D3DriverGenerationResult
                {
                    Success = false,
                    Message = ex.Message,
                    ErrorDetails = ex.StackTrace
                };
            }
        }
    }
}

