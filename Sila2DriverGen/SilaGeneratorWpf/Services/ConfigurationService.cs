using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// 配置服务 - 管理appsettings.json配置
    /// </summary>
    public class ConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        public ConfigurationService()
        {
            _logger = LoggerService.GetLogger<ConfigurationService>();
            
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            _configuration = builder.Build();
            _logger.LogInformation("配置服务初始化完成");
        }

        /// <summary>
        /// 获取配置值
        /// </summary>
        public string? GetValue(string key)
        {
            return _configuration[key];
        }

        /// <summary>
        /// 获取配置值（带默认值）
        /// </summary>
        public string GetValue(string key, string defaultValue)
        {
            return _configuration[key] ?? defaultValue;
        }

        /// <summary>
        /// 获取日志配置
        /// </summary>
        public LoggingConfig GetLoggingConfig()
        {
            var config = new LoggingConfig();
            _configuration.GetSection("Logging").Bind(config);
            return config;
        }

        /// <summary>
        /// 获取应用配置
        /// </summary>
        public AppConfig GetAppConfig()
        {
            var config = new AppConfig();
            _configuration.GetSection("App").Bind(config);
            return config;
        }
    }

    /// <summary>
    /// 日志配置
    /// </summary>
    public class LoggingConfig
    {
        public string MinimumLevel { get; set; } = "Information";
        public int RetainDays { get; set; } = 30;
        public long MaxFileSizeMB { get; set; } = 1024;
    }

    /// <summary>
    /// 应用配置
    /// </summary>
    public class AppConfig
    {
        public string DefaultOutputPath { get; set; } = "";
        public string DefaultDeveloper { get; set; } = "Bioyond";
        public bool AutoSavePreferences { get; set; } = true;
        public int ServerScanTimeout { get; set; } = 3000;
    }
}

