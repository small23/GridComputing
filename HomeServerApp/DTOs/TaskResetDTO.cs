using System;

namespace HomeServerApp.DTOs
{
    internal class TaskResetDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.TaskReset;
        public Guid taskid = Guid.Empty;
    }
}
