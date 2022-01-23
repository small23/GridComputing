namespace GridServer.DTO
{
    internal class ReceiveStatusDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.ReceiveStatus;
        public bool Success;
        public string Msg = "";
    }
}
