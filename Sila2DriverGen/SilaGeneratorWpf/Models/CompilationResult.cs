namespace SilaGeneratorWpf.Models
{
    /// <summary>
    /// 编译结果
    /// </summary>
    public class CompilationResult
    {
        /// <summary>
        /// 编译是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 编译消息（成功或错误信息）
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// DLL 输出路径
        /// </summary>
        public string DllPath { get; set; } = string.Empty;

        /// <summary>
        /// 错误数量
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// 警告数量
        /// </summary>
        public int WarningCount { get; set; }
    }
}

