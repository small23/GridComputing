using System;

namespace GridServer.DTO
{
    internal class TaskRequestDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.TaskRequest;
        public Guid Id;
        public Guid RequestId;
        public Guid ServerId;
        public string Text;
    }
}
