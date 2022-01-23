namespace GridServer
{
    internal class Program
    {
        private static SocketListener _socket;
        private const int HomeSocketPort = 30000;
        private const int ClientSocketPort = 30001;

        private static void Main(string[] args)
        {
            _socket = new SocketListener(ClientSocketPort, HomeSocketPort);
            _socket.StartListening();

        }
    }
}
