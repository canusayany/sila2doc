using System.Windows;
using System;
using System.IO;
using System.Windows.Threading;

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
            }
            else
            {
                message += "发生未知错误";
            }

            // 写入日志文件
            try
            {
                var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(logDir);
                var logFile = Path.Combine(logDir, $"error_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(logFile, message);
            }
            catch { /* 忽略日志错误 */ }

            // 显示错误对话框
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}

