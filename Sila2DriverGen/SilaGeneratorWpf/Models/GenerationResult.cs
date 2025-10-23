namespace SilaGeneratorWpf.Models
{
    /// <summary>
    /// 生成结果
    /// </summary>
    public class D3DriverGenerationResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 错误详情
        /// </summary>
        public string? ErrorDetails { get; set; }

        /// <summary>
        /// 编译是否成功（如果执行了编译）
        /// </summary>
        public bool? CompileSuccess { get; set; }

        /// <summary>
        /// 编译输出信息
        /// </summary>
        public string? CompileOutput { get; set; }

        /// <summary>
        /// 编译警告数量
        /// </summary>
        public int CompileWarnings { get; set; }

        /// <summary>
        /// 编译错误数量
        /// </summary>
        public int CompileErrors { get; set; }
    }
}
