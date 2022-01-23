using System.Net.Sockets;
using System.Text;

namespace GridClient
{
    public class StateObject
    {
        // Client socket.  
        public Socket WorkSocket = null;

        // Size of receive buffer.  
        public const int BufferSize = 65536;

        // Receive buffer.  
        public byte[] Buffer = new byte[BufferSize];

        // Received data string.  
        public StringBuilder Sb = new StringBuilder();

        public int Read = 0;
    }
}
