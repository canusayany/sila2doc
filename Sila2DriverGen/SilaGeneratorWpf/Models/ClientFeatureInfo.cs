using System;
using System.Collections.Generic;

namespace SilaGeneratorWpf.Models
{
    /// <summary>
    /// 客户端特性信息
    /// </summary>
    public class ClientFeatureInfo
    {
        /// <summary>
        /// 接口类型
        /// </summary>
        public Type? InterfaceType { get; set; }

        /// <summary>
        /// 特性名称（如：TemperatureController）
        /// </summary>
        public string FeatureName { get; set; } = string.Empty;

        /// <summary>
        /// 接口名称（如：ITemperatureController）
        /// </summary>
        public string InterfaceName { get; set; } = string.Empty;

        /// <summary>
        /// 客户端类名称（如：TemperatureControllerClient）
        /// </summary>
        public string ClientName { get; set; } = string.Empty;

        /// <summary>
        /// 方法列表
        /// </summary>
        public List<MethodGenerationInfo> Methods { get; set; } = new();
    }
}


