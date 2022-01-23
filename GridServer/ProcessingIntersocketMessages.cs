using System;

namespace GridServer
{
    internal class ProcessingIntersocketMessages
    {
        public StateObject Requester = null;

        public RequestType RequestReason = RequestType.RequestNull;

        public Guid RequestId = Guid.NewGuid();
        public enum RequestType : int
        {
            RequestNull,
            TaskRequest,
            StatusRequest
        }
    }
}
