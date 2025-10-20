namespace BR.Sila2
{
    public class Sila2Device
    {
        public Sila2Device()
        {
        }
        public bool Connect(string ip, int port)
        {
            return true;
        }
        public bool Disconnect()
        {
            return true;
        }
        public Action<bool> OnConnectionStatusChanged;
    }
}
