using System;

namespace GridServer.DTO
{
    internal class TaskDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.Task;
        public Guid Id;
        public string Text;
    }
}
