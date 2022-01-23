namespace HomeServerApp
{
    class Program
    {
        static void Main(string[] args)
        {
            SocketClient client = new SocketClient();
            client.StartClient();
        }
    }
}
