using System;

namespace SilaGeneratorWpf.Models
{
    /// <summary>
    /// 参数生成信息
    /// </summary>
    public class ParameterGenerationInfo
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 参数类型
        /// </summary>
        public Type Type { get; set; } = typeof(object);

        /// <summary>
        /// 参数描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// XML 文档注释信息
        /// </summary>
        public XmlDocumentationInfo? XmlDocumentation { get; set; }

        /// <summary>
        /// 是否需要额外的 JSON 字符串参数
        /// </summary>
        public bool RequiresJsonParameter { get; set; }
    }
}


