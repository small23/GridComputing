namespace HomeServerApp.DTOs
{
    class ReceiveStatusDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.Answer;
        public bool Success;
        public string Msg;
    }
}
