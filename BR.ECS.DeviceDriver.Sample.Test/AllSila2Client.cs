using BR.PC.Device.Sila2Discovery;
using System.Reflection;
using Tecan.Sila2.Client;
using Tecan.Sila2.Client.ExecutionManagement;
using Tecan.Sila2.Discovery;
using Tecan.Sila2.Locking;

namespace BR.ECS.DeviceDriver.Sample.Test;

internal class AllSila2Client
{
    ITemperatureController temperatureController;//如果有多个特性就定义多个
    Tecan.Sila2.Discovery.ServerConnector _connector = new ServerConnector(new DiscoveryExecutionManager());
    ExecutionManagerFactory executionManagerFactory = new ExecutionManagerFactory(new IClientRequestInterceptor[] { new LockingInterceptor(null) });
    //ServerDiscovery _discovery;
    IEnumerable<Tecan.Sila2.ServerData> _servers;
    Tecan.Sila2.ServerData _server;
    public AllSila2Client()
    {
        _connector = new ServerConnector(new DiscoveryExecutionManager());
        // _discovery = new ServerDiscovery(_connector);
        Sila2Discovery.StartRealTimeMonitoring();
    }
    public bool Connect(string ip, int port)
    {
        var info = Sila2Discovery.GetServer(ip, port, TimeSpan.FromSeconds(5));
        if (info == null)
        {
            return false;
        }
        _server = _connector.Connect(info.IPAddress, info.Port, info.Uuid, info.TxtRecords);
        ClientProvider clientProvider = new ClientProvider(executionManagerFactory, DiscoverFactories());
        Sila2Discovery.OnServerOffline += (s) =>
        {
            if (s.IPAddress == ip && s.Port == port)
            {
                OnConnectionStatusChanged?.Invoke(false);
            }
        };
        Sila2Discovery.OnServerOnline += (s) =>
        {
            if (s.IPAddress == ip && s.Port == port)
            {
                OnConnectionStatusChanged?.Invoke(true);
            }
        };

        clientProvider.TryCreateClient<ITemperatureController>(_server, out temperatureController);//如果有多个特性就将所有特性拿出来
        return true;
    }
    public bool Disconnect()
    {
        _server.Channel.Dispose();
        return true;
    }
    //属性改为方法,规则为Get+属性名
    //如果方法是可观察方法,改为阻塞模式
    #region 平铺ITemperatureController的所有方法以及属性
    public double GetCurrentTemperature()
    {
        return temperatureController.CurrentTemperature;
    }
    public void ControlTemperature(double targetTemperature)
    {
        var command = temperatureController.ControlTemperature(targetTemperature);
        command.Response.GetAwaiter().GetResult();
    }
    public bool GetDeviceState()
    {
        return temperatureController.DeviceState;
    }
    public void SwitchDeviceState(bool isOn)
    {
        temperatureController.SwitchDeviceState(isOn);
    }
    #endregion
    public Action<bool> OnConnectionStatusChanged;
    /// <summary>
    /// 自动发现并创建所有 IClientFactory 实例
    /// </summary>
    public static List<IClientFactory> DiscoverFactories()
    {
        var factories = new List<IClientFactory>();

        // 获取当前程序集中所有类型
        var assembly = Assembly.GetExecutingAssembly();

        var factoryTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)  // 是类且不是抽象类
            .Where(t => typeof(IClientFactory).IsAssignableFrom(t))  // 实现了 IClientFactory
            .ToList();

        foreach (var type in factoryTypes)
        {
            try
            {
                // 尝试创建实例（需要无参构造函数）
                if (Activator.CreateInstance(type) is IClientFactory factory)
                {
                    factories.Add(factory);
                    Console.WriteLine($"✓ 发现特性工厂: {type.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ 无法创建工厂 {type.Name}: {ex.Message}");
            }
        }

        return factories;
    }
}
