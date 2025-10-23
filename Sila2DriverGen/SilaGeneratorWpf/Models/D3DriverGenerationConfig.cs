using System.Collections.Generic;

namespace SilaGeneratorWpf.Models
{
    /// <summary>
    /// D3 驱动生成配置
    /// </summary>
    public class D3DriverGenerationConfig
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
        /// 开发者名称
        /// </summary>
        public string Developer { get; set; } = string.Empty;

        /// <summary>
        /// 命名空间
        /// </summary>
        public string Namespace { get; set; } = "BR.ECS.DeviceDriver.Generated";

        /// <summary>
        /// 输出路径
        /// </summary>
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>
        /// 客户端代码路径
        /// </summary>
        public string ClientCodePath { get; set; } = string.Empty;

        /// <summary>
        /// 特性列表
        /// </summary>
        public List<ClientFeatureInfo> Features { get; set; } = new();

        /// <summary>
        /// 是否生成测试控制台
        /// </summary>
        public bool GenerateTestConsole { get; set; } = true;

        /// <summary>
        /// 是否自动编译生成的项目
        /// </summary>
        public bool AutoCompile { get; set; } = true;
    }
}


