using System.Collections.Generic;

namespace GridServer.DTO
{
    internal class HomeListDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.HomeList;
        public List<HomeElementDto> Homes = new List<HomeElementDto>();

    }
}
