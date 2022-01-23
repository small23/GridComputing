using System;
using HomeServerApp.DTOs;

namespace GridServer.DTO
{
    class AnswerDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.Answer;
        public Guid TaskId = Guid.Empty;
        public string Result;
    }
}
