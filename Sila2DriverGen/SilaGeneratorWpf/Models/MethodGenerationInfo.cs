using System;
using System.Collections.Generic;

namespace SilaGeneratorWpf.Models
{
    /// <summary>
    /// 方法生成信息
    /// </summary>
    public class MethodGenerationInfo
    {
        /// <summary>
        /// 方法名称（可能被重命名以避免冲突）
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 原始方法名称
        /// </summary>
        public string OriginalName { get; set; } = string.Empty;

        /// <summary>
        /// 返回类型
        /// </summary>
        public Type ReturnType { get; set; } = typeof(void);

        /// <summary>
        /// 参数列表
        /// </summary>
        public List<ParameterGenerationInfo> Parameters { get; set; } = new();

        /// <summary>
        /// 方法描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 是否包含在D3Driver中（只有勾选此项的方法才会生成）
        /// </summary>
        public bool IsIncluded { get; set; } = true;

        /// <summary>
        /// 是否为调度方法（生成 [MethodOperations] 特性）
        /// </summary>
        public bool IsOperations { get; set; } = false;

        /// <summary>
        /// 是否为维护方法（生成 [MethodMaintenance] 特性）
        /// </summary>
        public bool IsMaintenance { get; set; } = false;

        /// <summary>
        /// 是否为属性
        /// </summary>
        public bool IsProperty { get; set; }

        /// <summary>
        /// 属性名称（如果是属性）
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>
        /// 是否为可观察命令
        /// </summary>
        public bool IsObservableCommand { get; set; }

        /// <summary>
        /// 是否为可观察属性
        /// </summary>
        public bool IsObservable { get; set; }

        /// <summary>
        /// 所属特性名称
        /// </summary>
        public string FeatureName { get; set; } = string.Empty;

        /// <summary>
        /// XML 文档注释信息
        /// </summary>
        public XmlDocumentationInfo? XmlDocumentation { get; set; }

        /// <summary>
        /// 返回值是否需要 JSON 处理
        /// </summary>
        public bool RequiresJsonReturn { get; set; }
    }
}

