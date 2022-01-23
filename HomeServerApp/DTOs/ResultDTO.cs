using System;
using HomeServerApp.DTOs;

namespace GridServer.DTO
{
    class ResultDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.Result;
        public Guid Id;
        public int Symbols;
        public int Spaces;
    }
}
