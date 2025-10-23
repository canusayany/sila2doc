using System.Collections.Generic;

namespace SilaGeneratorWpf.Models
{
    /// <summary>
    /// XML 文档注释信息
    /// </summary>
    public class XmlDocumentationInfo
    {
        /// <summary>
        /// 摘要
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string? Remarks { get; set; }

        /// <summary>
        /// 返回值说明
        /// </summary>
        public string? Returns { get; set; }

        /// <summary>
        /// 参数说明字典（参数名 -> 说明）
        /// </summary>
        public Dictionary<string, string> Parameters { get; set; } = new();
    }
}


