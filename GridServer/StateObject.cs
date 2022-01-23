using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace GridServer
{
    internal class StateObject
    {
        // Size of receive buffer.  
        public const int BufferSize = 64*1024;

        // Receive buffer.  
        public byte[] Buffer = new byte[BufferSize];

        // readed
        public int Read = 0;

        // Received data string.
        public StringBuilder Sb = new StringBuilder();

        // Client socket.
        public Socket WorkSocket = null;

        public Guid UserId = Guid.Empty;

        public bool MarkedForClose = false;

        public List<Guid> ErrorHomeServers = new List<Guid>();

        public List<Guid> PreviousHomeTaskGiver = new List<Guid>();
    }
}
