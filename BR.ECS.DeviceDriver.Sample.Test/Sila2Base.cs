
global using BR.ECS.DeviceDriver.Generated;
global using BR.ECS.Executor.Device.Domain.Contracts;
global using BR.ECS.Executor.Device.Domain.Share;
global using BR.ECS.Executor.Device.Infrastructure;
global using Sila2Client;
using BR.ECS.DeviceDriver.Sample.Test;

namespace BR.ECS.DeviceDriver.Generated
{
    /// <summary>
    /// RPC communication base class, containing common RPC connection and communication logic
    /// </summary>
    public abstract class Sila2Base : DeviceBase
    {

        protected bool _deviceConnected = false;
        internal AllSila2Client _sila2Device;
        protected ConnectionInfo _connectionInfo;
        public Sila2Base()
        {
            _sila2Device = new AllSila2Client();
            _sila2Device.OnConnectionStatusChanged += Client_IsConnectionChanged;
        }

        private void Client_IsConnectionChanged(bool obj)
        {

            if (_deviceConnected != obj)
            {
                _deviceConnected = obj;

                if (_deviceStateMachine.CurrentState != DeviceState_Common.IDLE)
                {
                    _deviceStateMachine.HandleEvent(DeviceEvent_Common.Connected);
                }
                if (_deviceStateMachine.CurrentState != DeviceState_Common.DISCONNECTED)
                {
                    _deviceStateMachine.HandleEvent(DeviceEvent_Common.Disconnected);
                }
            }
        }

        public override int UpdateDeviceInfo()
        {
            _connectionInfo = _jsonHelper.DeserializeObject<ConnectionInfo>(
                _jsonHelper.SerializeObject(DeviceCfg.Parameters.Parameter));
            return 0;
        }

        /// <summary>
        /// Connect device, after this step the device can start working
        /// When framework calls initialization, if configuration is set to simulation mode, this method is not called, otherwise it is called
        /// </summary>
        /// <returns></returns>
        public override int Connect()
        {
            return  _sila2Device.Connect(_connectionInfo.IP, _connectionInfo.Port) ? 0 : 1;
        }

        /// <summary>
        /// Disconnect device
        /// </summary>
        /// <returns></returns>
        public override int Disconnect()
        {
            return _sila2Device.Disconnect() ? 0 : 1;
        }


    }

    public class ConnectionInfo
    {
        public string IP { get; set; }
        public int Port { get; set; }
    }


}