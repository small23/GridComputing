using System.Net.Sockets;
using System.Text;

namespace HomeServerApp
{
    // State object for receiving data from remote device.  
    public class StateObject
    {
        // Client socket.  
        public Socket WorkSocket = null;

        // Size of receive buffer.  
        public const int BufferSize = 256;

        // Receive buffer.  
        public byte[] Buffer = new byte[BufferSize];

        // Received data string.  
        public StringBuilder Sb = new StringBuilder();

        public int Read = 0;
    }
}
