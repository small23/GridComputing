namespace GridServer.DTO
{
    internal class JsonType
    {
        public enum RequestType : int
        {
            Answer,
            HomeList,
            Login,
            RegisterClient,
            RegisterHome,
            Result,
            Subscribe,
            Task,
            SubscribeList,
            Connection,
            ReceiveStatus,
            CheckExistence,
            TaskRequest,
            TaskReset
        }
    }
}
