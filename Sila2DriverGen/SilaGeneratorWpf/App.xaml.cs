using System.Windows;
using System;
using System.IO;
using System.Windows.Threading;
using SilaGeneratorWpf.Services;
using Microsoft.Extensions.Logging;

namespace SilaGeneratorWpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // 捕获所有未处理的异常
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 初始化日志系统 (Debug模式下使用Debug级别,Release使用Information级别)
#if DEBUG
            LoggerService.Initialize(LogLevel.Debug);
#else
            LoggerService.Initialize(LogLevel.Information);
#endif
            
            // 清理30天前的日志
            LoggerService.CleanupOldLogs(30);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LoggerService.Shutdown();
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

