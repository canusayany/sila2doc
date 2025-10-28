using System;

namespace Sila2DriverGen.TestConsole
{
    /// <summary>
    /// 测试项枚举
    /// </summary>
    public enum TestItem
    {
        /// <summary>基础测试：从本地XML生成D3项目</summary>
        GenerateFromLocalXml = 1,
        
        /// <summary>基础测试：编译已生成的D3项目</summary>
        CompileProject = 2,
        
        /// <summary>集成测试：完整流程（生成+编译）</summary>
        CompleteWorkflow = 3,
        
        /// <summary>集成测试：调整方法分类并重新生成</summary>
        AdjustMethodClassifications = 4,
        
        /// <summary>集成测试：多特性完整流程</summary>
        MultipleFeatures = 5,
        
        /// <summary>错误处理测试：无效文件</summary>
        ErrorHandling_InvalidFile = 6,
        
        /// <summary>错误处理测试：编译失败</summary>
        ErrorHandling_CompilationFailure = 7,
        
        /// <summary>在线服务器测试：完整流程</summary>
        OnlineServer = 8,
        
        /// <summary>在线服务器测试：从模拟Feature对象生成</summary>
        OnlineServerGeneration = 9,
        
        /// <summary>性能测试：并行处理优化</summary>
        PerformanceOptimization = 10
    }

    /// <summary>
    /// 测试类别
    /// </summary>
    public enum TestCategory
    {
        /// <summary>基础功能测试</summary>
        Basic,
        
        /// <summary>集成测试</summary>
        Integration,
        
        /// <summary>错误处理测试</summary>
        ErrorHandling,
        
        /// <summary>在线服务器测试</summary>
        OnlineServer,
        
        /// <summary>性能测试</summary>
        Performance
    }

    /// <summary>
    /// 测试信息
    /// </summary>
    public class TestInfo
    {
        public TestItem Item { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public TestCategory Category { get; set; }
        public bool RequiresPrerequisite { get; set; }
        public TestItem? Prerequisite { get; set; }

        /// <summary>
        /// 获取所有测试项的定义
        /// </summary>
        public static TestInfo[] GetAllTests()
        {
            return new[]
            {
                new TestInfo
                {
                    Item = TestItem.GenerateFromLocalXml,
                    Name = "从本地XML生成D3项目",
                    Description = "使用本地.sila.xml文件生成D3驱动项目",
                    Category = TestCategory.Basic,
                    RequiresPrerequisite = false
                },
                new TestInfo
                {
                    Item = TestItem.CompileProject,
                    Name = "编译已生成的D3项目",
                    Description = "编译已生成的D3驱动项目并输出DLL",
                    Category = TestCategory.Basic,
                    RequiresPrerequisite = true,
                    Prerequisite = TestItem.GenerateFromLocalXml
                },
                new TestInfo
                {
                    Item = TestItem.CompleteWorkflow,
                    Name = "完整流程（生成+编译）",
                    Description = "完整测试从生成到编译的整个流程",
                    Category = TestCategory.Integration,
                    RequiresPrerequisite = false
                },
                new TestInfo
                {
                    Item = TestItem.AdjustMethodClassifications,
                    Name = "调整方法分类并重新生成",
                    Description = "调整方法的分类（操作/维护）并重新生成D3Driver",
                    Category = TestCategory.Integration,
                    RequiresPrerequisite = true,
                    Prerequisite = TestItem.GenerateFromLocalXml
                },
                new TestInfo
                {
                    Item = TestItem.MultipleFeatures,
                    Name = "多特性完整流程",
                    Description = "使用多个特性文件测试完整流程",
                    Category = TestCategory.Integration,
                    RequiresPrerequisite = false
                },
                new TestInfo
                {
                    Item = TestItem.ErrorHandling_InvalidFile,
                    Name = "错误处理：无效文件",
                    Description = "测试处理不存在的文件的错误处理",
                    Category = TestCategory.ErrorHandling,
                    RequiresPrerequisite = false
                },
                new TestInfo
                {
                    Item = TestItem.ErrorHandling_CompilationFailure,
                    Name = "错误处理：编译失败",
                    Description = "测试处理编译失败的错误处理",
                    Category = TestCategory.ErrorHandling,
                    RequiresPrerequisite = false
                },
                new TestInfo
                {
                    Item = TestItem.OnlineServer,
                    Name = "在线服务器完整流程",
                    Description = "扫描在线SiLA2服务器并生成D3驱动（如无服务器则跳过）",
                    Category = TestCategory.OnlineServer,
                    RequiresPrerequisite = false
                },
                new TestInfo
                {
                    Item = TestItem.OnlineServerGeneration,
                    Name = "在线服务器生成测试（模拟）",
                    Description = "测试从模拟的Feature对象生成代码（不需要真实服务器）",
                    Category = TestCategory.OnlineServer,
                    RequiresPrerequisite = false
                },
                new TestInfo
                {
                    Item = TestItem.PerformanceOptimization,
                    Name = "性能优化测试",
                    Description = "测试并行处理、文件验证和性能监控功能",
                    Category = TestCategory.Performance,
                    RequiresPrerequisite = false
                }
            };
        }

        /// <summary>
        /// 获取测试项的显示名称
        /// </summary>
        public static string GetDisplayName(TestItem item)
        {
            var tests = GetAllTests();
            var testInfo = Array.Find(tests, t => t.Item == item);
            return testInfo != null ? $"测试{(int)item}：{testInfo.Name}" : item.ToString();
        }
    }
}


