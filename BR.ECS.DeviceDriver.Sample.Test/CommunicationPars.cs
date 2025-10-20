using BR.ECS.Executor.Device.Domain.Contracts;
using BR.ECS.Executor.Device.Domain.Share;
using System.Collections.Generic;

namespace BR.ECS.DeviceDriver.Sample.Test
{
    /// <summary>
    /// 通信参数描述
    /// </summary>
    public class CommunicationPars : IDeviceCommunication
    {
        public List<DeviceCommunicationDto> DeviceCommunications { get; set; } = new List<DeviceCommunicationDto>
        {
            new DeviceCommunicationDto
            {
                DeviceCommunicationKey = DeviceCommunicationType.Customization.ToString(),
                DeviceCommunicationItems = new List<DeviceCommunicationItem>
                {
                    new DeviceCommunicationItem("IP", "192.168.1.201",
                        "IP", "", true, true, true, typeof(string)),
                      new DeviceCommunicationItem("Port", 6002,
                        "Port", "", true, true, true, typeof(int))
                }
            }
        };
    }
}
