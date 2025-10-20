
namespace BR.ECS.DeviceDriver.Sample.Test;

//DeviceClass 第一个参数是品牌 第二个参数是型号,第三个参数是默认注入key通常采用品牌+型号,第四个参数是设备类型,第五个参数是开发者名称.第一第二第四个参数必须使用英文或者下划线或者数字或者这三者的组合
/// <summary>
/// 设备功能实现
/// </summary>
[DeviceClass("Bioyond", "MD", "BioyondMD", "Robot", "Name")]
public class D3Driver : Sila2Base
{

    //能被D3调用的方法必须是同步的
    //被D3调用的方法有Reset、EStop、SafeEnter,bool Prepare(),bool GStop(),void Dispose(),bool PrepareRetry(),ConnectDevice、DisconnectDevice,以及带有MethodOperations与MethodMaintenance特性的方法
    //当出现Reset、EStop、SafeEnter,bool Prepare(),bool GStop(),void Dispose(),bool PrepareRetry(),ConnectDevice、DisconnectDevice,这几个方法是时,要使用重写override关键字
    //被D3调用的方法不能有重载,即方法名相同,参数不同的方法

    //带有MethodOperations与MethodMaintenance特性的都要添加注释
    //如果是调度方法打上标签MethodOperations
    /// <summary>
    /// The current temperature as measured by the controller.
    /// </summary>
    /// <returns>The current temperature as measured by the controller.</returns>
    [MethodOperations]
    public double GetCurrentTemperature()
    {
        return _sila2Device.GetCurrentTemperature();
    }
    /// <summary>
    /// Control the temperature gradually to a set target.      It is RECOMMENDED to use an oscillation free control system.
    /// </summary>
    /// <param name="targetTemperature">The target temperature that the server will try to reach.        Note that the command might be completed at a temperature that it evaluates to be close enough.        If the temperature cannot be reached, a 'Temperature Not Reachable' error will be thrown.</param>
    [MethodOperations]
    public void ControlTemperature(double targetTemperature)
    {
        _sila2Device.ControlTemperature(targetTemperature);
    }
    /// <summary>
    /// The current state of the device, either On or Off.
    /// </summary>
    /// <returns>The current state of the device, either On or Off./returns>
    [MethodMaintenance(1)]//MethodMaintenance后的参数为该方法出现的顺序,从1开始,不能重复
    public bool GetDeviceState()
    {
        return _sila2Device.GetDeviceState();
    }
    /// <summary>
    /// Switch device state from On to Off, or from Off to On.
    /// </summary>
    /// <param name="isOn">Switch device state to isOn state.</param>
    [MethodMaintenance(2)]
    public void SwitchDeviceState(bool isOn)
    {
        _sila2Device.SwitchDeviceState(isOn);
    }
}
