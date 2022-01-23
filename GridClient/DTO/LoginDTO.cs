namespace GridClient.DTO
{
    internal class LoginDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.Login;
        public string Login;
        public string Pwd;
    }
}
