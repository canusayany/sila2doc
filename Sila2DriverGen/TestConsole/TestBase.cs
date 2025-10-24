using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SilaGeneratorWpf.Services;

namespace Sila2DriverGen.TestConsole
{
    /// <summary>
    /// 测试基类，包含通用的辅助方法
    /// </summary>
    public abstract class TestBase
    {
        protected readonly D3DriverOrchestrationService _orchestrationService;
        protected string? _lastGeneratedProjectPath;

        protected TestBase()
        {
            _orchestrationService = new D3DriverOrchestrationService();
        }

        #region XML文件查找

        /// <summary>
        /// 查找单个XML文件（TemperatureController-v1_0.sila.xml）
        /// </summary>
        protected string? FindXmlFile()
        {
            var workspaceRoot = Directory.GetCurrentDirectory();
            var searchPaths = new[]
            {
                Path.Combine(workspaceRoot, "TemperatureController-v1_0.sila.xml"),
                Path.Combine(workspaceRoot, "..", "SilaGeneratorWpf", "TemperatureController-v1_0.sila.xml"),
                Path.Combine(workspaceRoot, "..", "..", "TemperatureController-v1_0.sila.xml"),
                Path.Combine(workspaceRoot, "..", "..", "..", "TemperatureController-v1_0.sila.xml")
            };

            foreach (var path in searchPaths)
            {
                try
                {
                    var fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// 查找所有XML文件
        /// </summary>
        protected List<string> FindAllXmlFiles()
        {
            var foundFiles = new List<string>();
            var workspaceRoot = Directory.GetCurrentDirectory();
            var searchPaths = new[]
            {
                workspaceRoot,
                Path.Combine(workspaceRoot, "..", "SilaGeneratorWpf"),
                Path.Combine(workspaceRoot, "..", ".."),
                Path.Combine(workspaceRoot, "..", "..", "..")
            };

            foreach (var searchPath in searchPaths)
            {
                try
                {
                    var fullPath = Path.GetFullPath(searchPath);
                    if (Directory.Exists(fullPath))
                    {
                        var xmlFiles = Directory.GetFiles(fullPath, "*.sila.xml", SearchOption.TopDirectoryOnly);
                        foreach (var file in xmlFiles)
                        {
                            if (!foundFiles.Contains(file))
                            {
                                foundFiles.Add(file);
                            }
                        }
                    }
                }
                catch { }
            }

            return foundFiles;
        }

        #endregion

        #region 通用测试方法

        /// <summary>
        /// 创建测试用的生成请求
        /// </summary>
        protected D3GenerationRequest CreateTestRequest(string brand, string model, string deviceType, List<string> xmlPaths)
        {
            return new D3GenerationRequest
            {
                Brand = brand,
                Model = model,
                DeviceType = deviceType,
                Developer = "Bioyond",
                IsOnlineSource = false,
                LocalFeatureXmlPaths = xmlPaths
            };
        }

        /// <summary>
        /// 验证项目路径是否存在
        /// </summary>
        protected bool ValidateProjectPath(string? projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
            {
                ConsoleHelper.PrintError("项目路径为空");
                return false;
            }

            if (!Directory.Exists(projectPath))
            {
                ConsoleHelper.PrintError($"项目目录不存在: {projectPath}");
                return false;
            }

            return true;
        }

        #endregion
    }
}

