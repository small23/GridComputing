using System;
using HomeServerApp.DTOs;

namespace GridServer.DTO
{
    class TaskDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.Task;
        public Guid Id;
        public string Text;
    }
}
