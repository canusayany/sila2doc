using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using SilaGeneratorWpf.Services;

namespace SilaGeneratorWpf.ViewModels
{
    /// <summary>
    /// 主窗口的ViewModel
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly ILogger _logger;

        public FileGenerationViewModel FileGenerationViewModel { get; }
        public ServerDiscoveryViewModel ServerDiscoveryViewModel { get; }

        public MainViewModel()
        {
            _logger = LoggerService.GetLogger<MainViewModel>();
            _logger.LogInformation("应用程序启动");

            FileGenerationViewModel = new FileGenerationViewModel();
            ServerDiscoveryViewModel = new ServerDiscoveryViewModel();
        }

        public void OnClosing()
        {
            _logger.LogInformation("应用程序关闭");
            _logger.LogInformation($"日志文件位置: {LoggerService.GetLogsDirectory()}");
            LoggerService.Shutdown();
        }
    }
}

