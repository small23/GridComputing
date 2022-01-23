using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using GridServer.DTO;
using HomeServerApp.DTOs;
using Newtonsoft.Json;

namespace HomeServerApp
{
    internal class SocketClient
    {
        private const int DbSize = 1000;

        // ManualResetEvent instances signal completion.  
        private readonly AutoResetEvent _connectDone =
            new AutoResetEvent(false);

        private readonly AutoResetEvent _receiveDone =
            new AutoResetEvent(false);

        private readonly AutoResetEvent _sendDone =
            new AutoResetEvent(false);

        private Socket _conn;

        private ConnectionDataDto _connectionData;

        private int _result;

        public HomeDBEntities DbContext;
        public AutoResetEvent MainThreadLock = new AutoResetEvent(true);

        public void AddData()
        {
            var text = File.ReadAllText("example.txt");
            var splittedData = text.Split('\n').ToList();
            var cnt = 0;
            foreach (var data in splittedData)
            {
                var temp = new tasks
                {
                    text = Convert.ToBase64String(Encoding.UTF8.GetBytes(data)),
                    done = false,
                    id = Guid.NewGuid(),
                    serverId = Guid.Empty,
                    res = ""
                };
                DbContext.tasks.Add(temp);
                cnt++;
                if (cnt >= DbSize)
                    break;
            }

            DbContext.SaveChanges();
        }

        public void StartClient()
        {
            DbContext = new HomeDBEntities();
            if (DbContext.tasks.Count() < DbSize) AddData();

            if (!File.Exists("loginData.txt"))
            {
                CreateConfigFile();
                Console.Write("No config file detected! Created new one. Please config it!");
                Console.ReadKey();
                return;
            }

            var dta = File.ReadAllText("loginData.txt");
            _connectionData = JsonConvert.DeserializeObject<ConnectionDataDto>(dta);

            if (_connectionData != null)
            {
                IPAddress ipAddress = IPAddress.Parse(_connectionData.Ip);
                var remoteEp = new IPEndPoint(ipAddress, _connectionData.Port);
                _conn = new Socket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);
                _conn.BeginConnect(remoteEp,
                    ConnectCallback, _conn);
                _connectDone.WaitOne();
            }
            else
            {
                Console.Write("Error during creating config file!");
                Console.ReadKey();
                return;
            }

            if (LoginFcn() == false)
            {
                Console.Write("Login not successfull!");
                Console.ReadKey();
                return;
            }

            while (true)
            {
                Receive(_conn);
                _receiveDone.WaitOne();
            }
        }

        private static void CreateConfigFile()
        {
            var singinData = new ConnectionDataDto
            {
                Login = "login",
                Name = "serevrName",
                Description = "Description",
                Pwd = "pwd",
                Ip = "ip-address",
                Port = 9999
            };

            File.WriteAllText("loginData.txt", JsonConvert.SerializeObject(singinData));
        }

        private bool LoginFcn()
        {
            var loginForm = new LoginDto
            {
                Login = _connectionData.Login,
                DataType = JsonType.RequestType.CheckExistence
            };
            SendAndReceive(JsonConvert.SerializeObject(loginForm));

            if (_result == 0)
            {
                loginForm.Login = _connectionData.Login;
                loginForm.DataType = JsonType.RequestType.Login;
                loginForm.Pwd = _connectionData.Pwd;
                SendAndReceive(JsonConvert.SerializeObject(loginForm));
            }
            else
            {
                var registerForm = new RegisterHomeDto
                {
                    Login = _connectionData.Login,
                    Desc = _connectionData.Description,
                    Pwd = _connectionData.Pwd,
                    Name = _connectionData.Name
                };
                SendAndReceive(JsonConvert.SerializeObject(registerForm));
            }

            return _result == 0;
        }

        private void SendAndReceive(string data)
        {
            _receiveDone.Reset();
            _sendDone.Reset();
            Send(_conn, data);
            _sendDone.WaitOne();
            Receive(_conn);
            _receiveDone.WaitOne();
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                var client = (Socket) ar.AsyncState;
                client.EndConnect(ar);
                Console.WriteLine("Socket connected to {0}",
                    client.RemoteEndPoint);
                _connectDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void Receive(Socket client)
        {
            try
            {
                var state = new StateObject {WorkSocket = client};
                client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0,
                    ReceiveCallback, state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            MainThreadLock.WaitOne();
            var state = (StateObject) ar.AsyncState;
            Socket client = state.WorkSocket;

            var bytesRead = client.EndReceive(ar);
            state.Read = bytesRead;
            state.Sb.Append(Encoding.ASCII.GetString(state.Buffer, 0, state.Read));
            var contentList = state.Sb.ToString().Split('\n');

            foreach (var content in contentList)
            {
                if (content == "")
                    continue;
                Console.WriteLine($"Read {content.Length} bytes from socket.\n Data : {content}");
                var reqType = JsonConvert.DeserializeObject<CommonDto>(content);

                Debug.Assert(reqType != null, nameof(reqType) + " != null");
                switch (reqType.DataType)
                {
                    case JsonType.RequestType.ReceiveStatus:
                        var res = JsonConvert.DeserializeObject<ReceiveStatusDto>(content);
                        Debug.Assert(res != null, nameof(res) + " != null");
                        _result = res.Success ? 0 : 1;
                        break;
                    case JsonType.RequestType.TaskRequest:
                        ProceedTaskRequest(content, client);
                        break;
                    case JsonType.RequestType.Answer:
                        ProceedAnswer(content);
                        break;
                    case JsonType.RequestType.Connection:
                        _sendDone.Reset();
                        Send(state.WorkSocket, content);
                        _sendDone.WaitOne();
                        break;
                    case JsonType.RequestType.TaskReset:
                        var taskForReset = JsonConvert.DeserializeObject<TaskResetDto>(content);
                        if (taskForReset == null)
                            throw new ArgumentException();
                        tasks dbTaskForReset = DbContext.tasks.Single(t => t.id == taskForReset.taskid);
                        dbTaskForReset.serverId = Guid.Empty;
                        DbContext.SaveChanges();
                        break;
                    case JsonType.RequestType.HomeList:
                        throw new InvalidOperationException();
                    case JsonType.RequestType.Login:
                        throw new InvalidOperationException();
                    case JsonType.RequestType.RegisterClient:
                        throw new InvalidOperationException();
                    case JsonType.RequestType.RegisterHome:
                        throw new InvalidOperationException();
                    case JsonType.RequestType.Result:
                        throw new InvalidOperationException();
                    case JsonType.RequestType.Subscribe:
                        throw new InvalidOperationException();
                    case JsonType.RequestType.Task:
                        throw new InvalidOperationException();
                    case JsonType.RequestType.SubscribeList:
                        throw new InvalidOperationException();
                    case JsonType.RequestType.CheckExistence:
                        throw new InvalidOperationException();
                    default:
                        throw new InvalidOperationException();
                }
            }

            _receiveDone.Set();
            MainThreadLock.Set();
        }

        private void ProceedAnswer(string content)
        {
            var ans = JsonConvert.DeserializeObject<AnswerDto>(content);
            if (ans == null)
                throw new ArgumentException();
            tasks taskAns = DbContext.tasks.Single(t => t.id == ans.TaskId);
            taskAns.done = true;
            taskAns.res = ans.Result;
            DbContext.SaveChanges();
        }

        private void ProceedTaskRequest(string content, Socket client)
        {
            var task = JsonConvert.DeserializeObject<TaskRequestDto>(content);

            if (task == null)
                throw new ArgumentException();

            tasks dbTask = task.Id == Guid.Empty
                ? DbContext.tasks.FirstOrDefault(t => t.serverId == Guid.Empty)
                : DbContext.tasks.FirstOrDefault(t => t.id == task.Id);

            if (dbTask != null)
            {
                task.Text = dbTask.text;
                dbTask.serverId = task.ServerId;
                task.Id = dbTask.id;
                DbContext.SaveChanges();
            }
            else
            {
                task.ServerId = Guid.Empty;
                task.Id = Guid.Empty;
            }

            _sendDone.Reset();
            Send(client, JsonConvert.SerializeObject(task));
            _sendDone.WaitOne();
        }

        private void Send(Socket client, string data)
        {
            var byteData = Encoding.ASCII.GetBytes(data + "\n");
            client.BeginSend(byteData, 0, byteData.Length, 0,
                SendCallback, client);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                var client = (Socket) ar.AsyncState;
                var bytesSent = client.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to server.", bytesSent);

                _sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}