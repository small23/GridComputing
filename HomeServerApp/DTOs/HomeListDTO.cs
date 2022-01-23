using HomeServerApp.DTOs;

namespace GridServer.DTO
{
    class HomeListDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.HomeList;
        public string Name;
        public string Desc;
    }
}
