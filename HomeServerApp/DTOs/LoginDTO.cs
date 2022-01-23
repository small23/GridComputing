using HomeServerApp.DTOs;

namespace GridServer.DTO
{
    class LoginDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.Login;
        public string Login;
        public string Pwd;
    }
}
