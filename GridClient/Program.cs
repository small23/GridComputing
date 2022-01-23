namespace GridClient
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            SocketClient client = new SocketClient();
            client.StartClient();
        }
    }
}
