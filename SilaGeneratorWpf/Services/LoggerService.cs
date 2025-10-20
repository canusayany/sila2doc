using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// 日志服务 - 集中管理所有日志操作
    /// </summary>
    public static class LoggerService
    {
        private static ILoggerFactory? _loggerFactory;
        private static Serilog.Core.Logger? _serilogLogger;

        /// <summary>
        /// 初始化日志系统
        /// </summary>
        public static void Initialize()
        {
            if (_loggerFactory != null)
                return;

            try
            {
                // 创建日志文件目录
                var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(logsDir);

                var logFilePath = Path.Combine(logsDir, "SilaGenerator_{Date:yyyy-MM-dd}.log");

                // 配置 Serilog
                _serilogLogger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(
                        logFilePath,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30)
                    .CreateLogger();

                // 创建 ILoggerFactory
                _loggerFactory = LoggerFactory.Create(builder =>
                    builder.AddSerilog(_serilogLogger));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize logger: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定名称的日志记录器
        /// </summary>
        public static Microsoft.Extensions.Logging.ILogger GetLogger(string categoryName)
        {
            if (_loggerFactory == null)
                Initialize();

            return _loggerFactory?.CreateLogger(categoryName) 
                ?? new NullLogger();
        }

        /// <summary>
        /// 获取指定类型的日志记录器
        /// </summary>
        public static Microsoft.Extensions.Logging.ILogger GetLogger<T>()
        {
            return GetLogger(typeof(T).FullName ?? typeof(T).Name);
        }

        /// <summary>
        /// 关闭日志系统
        /// </summary>
        public static void Shutdown()
        {
            _serilogLogger?.Dispose();
            _loggerFactory?.Dispose();
            _loggerFactory = null;
            _serilogLogger = null;
        }

        /// <summary>
        /// 获取日志文件目录
        /// </summary>
        public static string GetLogsDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        }
    }

    /// <summary>
    /// 空日志记录器 - 当日志系统初始化失败时使用
    /// </summary>
    internal class NullLogger : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => new NullDisposable();

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, 
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        private class NullDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
