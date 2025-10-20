using System;
using SilaGeneratorWpf.Models;

namespace SilaGeneratorWpf.ViewModels
{
    /// <summary>
    /// 设计时数据，用于在XAML设计器中预览
    /// </summary>
    public class DesignTimeData
    {
        public static FileGenerationViewModel CreateFileGenerationViewModel()
        {
            var vm = new FileGenerationViewModel();
            vm.FeatureFiles.Add(@"C:\Features\TemperatureController-v1_0.sila.xml");
            vm.FeatureFiles.Add(@"C:\Features\PressureSensor-v2_1.sila.xml");
            vm.FeatureFiles.Add(@"C:\Features\PumpControl-v1_5.sila.xml");
            vm.OutputDirectory = @"C:\Output\SilaGeneratedClients\20250120_143022";
            vm.Namespace = "Sila2Client";
            vm.StatusMessage = "已添加 3 个文件";
            return vm;
        }

        public static ServerDiscoveryViewModel CreateServerDiscoveryViewModel()
        {
            var vm = new ServerDiscoveryViewModel();
            
            // 添加示例服务器1
            var server1 = new ServerInfoViewModel
            {
                ServerName = "TemperatureControllerServer",
                IPAddress = "192.168.1.100",
                Port = 50051,
                Uuid = Guid.Parse("12345678-1234-1234-1234-123456789012"),
                ServerType = "SiLA2 Server",
                Description = "温度控制器服务器",
                LastSeen = DateTime.Now.AddMinutes(-5),
                IsExpanded = true
            };
            
            server1.Features.Add(new FeatureInfoViewModel
            {
                Identifier = "TemperatureController",
                DisplayName = "Temperature Controller",
                Version = "1.0",
                Namespace = "org.silastandard",
                Description = "Controls temperature settings",
                ParentServer = server1
            });
            
            server1.Features.Add(new FeatureInfoViewModel
            {
                Identifier = "DataLogger",
                DisplayName = "Data Logger",
                Version = "2.1",
                Namespace = "org.silastandard",
                Description = "Logs measurement data",
                ParentServer = server1
            });

            // 添加示例服务器2
            var server2 = new ServerInfoViewModel
            {
                ServerName = "PressureSensorServer",
                IPAddress = "192.168.1.101",
                Port = 50052,
                Uuid = Guid.Parse("87654321-4321-4321-4321-210987654321"),
                ServerType = "SiLA2 Server",
                Description = "压力传感器服务器",
                LastSeen = DateTime.Now.AddMinutes(-2)
            };
            
            server2.Features.Add(new FeatureInfoViewModel
            {
                Identifier = "PressureSensor",
                DisplayName = "Pressure Sensor",
                Version = "1.5",
                Namespace = "com.tecan",
                Description = "Reads pressure values",
                ParentServer = server2
            });

            vm.Servers.Add(server1);
            vm.Servers.Add(server2);
            vm.DiscoveryStatus = "发现 2 个服务器";
            vm.OutputDirectory = @"C:\Output\SilaDiscoveredServers\20250120_143022";
            
            return vm;
        }

        public static MainViewModel CreateMainViewModel()
        {
            return new MainViewModel();
        }
    }
}

