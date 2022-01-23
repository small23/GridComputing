namespace GridServer.DTO
{
    internal class RegisterClientDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.RegisterClient;
        public string Email;
        public string Login;
        public string Pwd;
    }
}
