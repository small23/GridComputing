namespace GridServer.DTO
{
    internal class SubscribeDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.Subscribe;
        public string Name;
        public RequestType Request = RequestType.RequestNull;
        public enum RequestType : int
        {
            RequestNull,
            SubRequest,
            UnsubRequest
        }
    }
}
