using System;

namespace GridServer.DTO
{
    internal class AnswerDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.Answer;
        public Guid TaskId = Guid.Empty;
        public string Result;
    }
}
