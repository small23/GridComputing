using System;

namespace GridServer.DTO
{
    internal class TaskResetDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.TaskReset;
        public Guid taskid = Guid.Empty;
    }
}
