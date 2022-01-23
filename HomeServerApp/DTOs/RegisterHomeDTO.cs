using HomeServerApp.DTOs;

namespace GridServer.DTO
{
    class RegisterHomeDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.RegisterHome;
        public string Login;
        public string Pwd;
        public string Name;
        public string Desc;
    }
}
