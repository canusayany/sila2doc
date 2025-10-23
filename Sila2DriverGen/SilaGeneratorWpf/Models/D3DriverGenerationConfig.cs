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
        /// 是否为在线服务器来源（true=在线服务器，false=本地特性文件）
        /// </summary>
        public bool IsOnlineSource { get; set; }

        /// <summary>
        /// 在线服务器 UUID
        /// </summary>
        public string? ServerUuid { get; set; }

        /// <summary>
        /// 本地特性 XML 文件路径列表
        /// </summary>
        public List<string>? LocalFeatureXmlPaths { get; set; }

        /// <summary>
        /// 服务器 IP 地址（从在线扫描获取或使用默认值）
        /// </summary>
        public string? ServerIp { get; set; }

        /// <summary>
        /// 服务器端口（从在线扫描获取或使用默认值）
        /// </summary>
        public int? ServerPort { get; set; }
    }
}


