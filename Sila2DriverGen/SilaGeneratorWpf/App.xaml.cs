using System.Windows;
using System;
using System.IO;
using System.Windows.Threading;
using SilaGeneratorWpf.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace SilaGeneratorWpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;

        public App()
        {
            // 捕获所有未处理的异常
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            try
            {
                // 配置依赖注入
                var services = new ServiceCollection();
                ConfigureServices(services);
                _serviceProvider = services.BuildServiceProvider();
                
                // 初始化服务定位器
                ServiceLocator.Initialize(_serviceProvider);
                
                // 加载配置
                var configService = _serviceProvider.GetRequiredService<ConfigurationService>();
                var loggingConfig = configService.GetLoggingConfig();
                
                // 初始化日志系统
                var logLevel = ParseLogLevel(loggingConfig.MinimumLevel);
                LoggerService.Initialize(logLevel);
                
                var logger = LoggerService.GetLogger<App>();
                logger.LogInformation("应用程序启动");
                logger.LogInformation("日志文件位置: {LogPath}", LoggerService.GetLogsDirectory());
                
                // 清理过期日志
                LoggerService.CleanupOldLogs(loggingConfig.RetainDays);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用程序启动失败:\n{ex.Message}\n\n详细信息:\n{ex.StackTrace}", 
                    "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // 注册应用服务
            services.AddApplicationServices();
            
            // 注册ViewModels
            services.AddViewModels();
        }

        private LogLevel ParseLogLevel(string level)
        {
            return level?.ToLower() switch
            {
                "debug" => LogLevel.Debug,
                "information" => LogLevel.Information,
                "warning" => LogLevel.Warning,
                "error" => LogLevel.Error,
                "critical" => LogLevel.Critical,
                _ => LogLevel.Information
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var logger = LoggerService.GetLogger<App>();
            logger.LogInformation("应用程序退出");
            
            LoggerService.Shutdown();
            
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            
            base.OnExit(e);
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            ShowErrorAndExit("应用程序启动错误", ex);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            ShowErrorAndExit("应用程序运行时错误", e.Exception);
        }

        private void ShowErrorAndExit(string title, Exception? ex)
        {
            string message = $"{title}\n\n";
            if (ex != null)
            {
                message += $"错误信息: {ex.Message}\n";
                message += $"堆栈跟踪: {ex.StackTrace}";
                
                // 使用日志服务记录错误
                var logger = LoggerService.GetLogger("App");
                logger.LogCritical(ex, "{Title}", title);
            }
            else
            {
                message += "发生未知错误";
            }

            // 显示错误对话框
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}

