using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// 日志服务 - 集中管理所有日志操作
    /// </summary>
    public static class LoggerService
    {
        private static ILoggerFactory? _loggerFactory;
        private static Serilog.Core.Logger? _serilogLogger;
        private static LogLevel _minimumLevel = LogLevel.Information;

        /// <summary>
        /// 初始化日志系统
        /// </summary>
        /// <param name="minimumLevel">最小日志级别（默认：Information）</param>
        public static void Initialize(LogLevel minimumLevel = LogLevel.Information)
        {
            if (_loggerFactory != null)
                return;

            _minimumLevel = minimumLevel;

            try
            {
                var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(logsDir);
                Console.WriteLine($"日志目录: {logsDir}");

                // 使用正确的文件名模板（注意：这里不用占位符，Serilog会根据rollingInterval自动添加日期）
                var logFilePath = Path.Combine(logsDir, "SilaGenerator.log");
                Console.WriteLine($"日志文件路径: {logFilePath}");

                var serilogLevel = ConvertToSerilogLevel(minimumLevel);
                
                // 配置 Serilog：结构化日志、按日滚动、保留30天、单文件限制1GB
                _serilogLogger = new LoggerConfiguration()
                    .MinimumLevel.Is(serilogLevel)
                    .WriteTo.File(
                        logFilePath,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        fileSizeLimitBytes: 1_073_741_824, // 1GB
                        rollOnFileSizeLimit: true,
                        shared: true,
                        flushToDiskInterval: TimeSpan.FromSeconds(1))
                    .CreateLogger();

                // 将 Serilog 与 Microsoft.Extensions.Logging 集成
                _loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.ClearProviders();
                    builder.AddSerilog(_serilogLogger, dispose: false);
                });
                
                Log.Logger = _serilogLogger;
                
                _serilogLogger.Information("日志系统初始化成功 - 最小级别: {MinimumLevel}, 日志目录: {LogsDir}", minimumLevel, logsDir);
                Console.WriteLine("日志系统初始化成功");
            }
            catch (Exception ex)
            {
                var errorMsg = $"日志系统初始化失败: {ex.Message}\n堆栈跟踪:\n{ex.StackTrace}";
                System.Diagnostics.Debug.WriteLine(errorMsg);
                Console.WriteLine(errorMsg);
                throw; // 重新抛出异常，让调用者知道初始化失败
            }
        }
        
        /// <summary>
        /// 转换LogLevel到Serilog LogEventLevel
        /// </summary>
        private static LogEventLevel ConvertToSerilogLevel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => LogEventLevel.Verbose,
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Information => LogEventLevel.Information,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Error => LogEventLevel.Error,
                LogLevel.Critical => LogEventLevel.Fatal,
                _ => LogEventLevel.Information
            };
        }

        /// <summary>
        /// 获取指定名称的日志记录器
        /// </summary>
        public static Microsoft.Extensions.Logging.ILogger GetLogger(string categoryName)
        {
            if (_loggerFactory == null)
                Initialize();

            return _loggerFactory?.CreateLogger(categoryName) ?? new NullLogger();
        }

        /// <summary>
        /// 获取指定类型的日志记录器
        /// </summary>
        public static Microsoft.Extensions.Logging.ILogger GetLogger<T>()
        {
            return GetLogger(typeof(T).FullName ?? typeof(T).Name);
        }

        /// <summary>
        /// 设置最小日志级别
        /// </summary>
        public static void SetMinimumLevel(LogLevel level)
        {
            _minimumLevel = level;
            _serilogLogger?.Information("日志级别已更改为: {Level}", level);
        }

        /// <summary>
        /// 获取当前最小日志级别
        /// </summary>
        public static LogLevel GetMinimumLevel() => _minimumLevel;

        /// <summary>
        /// 关闭日志系统
        /// </summary>
        public static void Shutdown()
        {
            _serilogLogger?.Information("日志系统关闭");
            _serilogLogger?.Dispose();
            _loggerFactory?.Dispose();
            _loggerFactory = null;
            _serilogLogger = null;
            Log.CloseAndFlush();
        }

        /// <summary>
        /// 获取日志文件目录
        /// </summary>
        public static string GetLogsDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        }
        
        /// <summary>
        /// 清理过期日志文件
        /// </summary>
        /// <param name="daysToKeep">保留天数</param>
        public static void CleanupOldLogs(int daysToKeep = 30)
        {
            try
            {
                var logsDir = GetLogsDirectory();
                if (!Directory.Exists(logsDir))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var logFiles = Directory.GetFiles(logsDir, "*.log");
                int deletedCount = 0;

                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        try
                        {
                            File.Delete(logFile);
                            deletedCount++;
                        }
                        catch
                        {
                            // 忽略删除失败的文件
                        }
                    }
                }

                if (deletedCount > 0)
                {
                    _serilogLogger?.Information("清理了 {Count} 个过期日志文件", deletedCount);
                }
            }
            catch (Exception ex)
            {
                _serilogLogger?.Warning(ex, "清理日志文件时出错");
            }
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
