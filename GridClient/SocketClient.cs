using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GridClient.DTO;
using Newtonsoft.Json;
using ShellProgressBar;

namespace GridClient
{
    internal class SocketClient
    {
        private const int ScreenUpdateDelay = 100;

        // ManualResetEvent instances signal completion.  
        private readonly AutoResetEvent _connectDone =
            new AutoResetEvent(false);

        private readonly AutoResetEvent _receiveDone =
            new AutoResetEvent(false);

        private readonly AutoResetEvent _sendDone =
            new AutoResetEvent(false);

        private Socket _conn;

        private ConnectionDataDto _connectionData;

        private string _receiveBuffer;
        private int _result;
        private Thread _taskThread;

        public void StartClient()
        {
            if (!File.Exists("userData.txt"))
            {
                CreateConfigFile();
                Console.Write("No config file detected! Created new one. Please config it!");
                Console.ReadKey();
                return;
            }

            var dta = File.ReadAllText("userData.txt");
            _connectionData = JsonConvert.DeserializeObject<ConnectionDataDto>(dta);

            if (_connectionData != null)
            {
                IPAddress ipAddress = IPAddress.Parse(_connectionData.Ip);
                var remoteEp = new IPEndPoint(ipAddress, _connectionData.Port);

                // Create a TCP/IP socket.  
                _conn = new Socket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
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
                Console.Clear();
                Thread.Sleep(ScreenUpdateDelay);
                Console.Write("1) Manage you subscribes\n2) Start working\n3) Quit\nYour selection: ");
                var selectionLine = Console.ReadLine();
                var selec = 0;
                try
                {
                    selec = Convert.ToInt32(selectionLine);
                }
                catch
                {
                    // ignored
                }

                switch (selec)
                {
                    case 1:
                        ShowSubs();
                        break;
                    case 2:
                        StartWorking();
                        break;
                    case 3:
                        _conn.Shutdown(SocketShutdown.Both);
                        _conn.Close();
                        return;
                }
            }
        }

        private static void CreateConfigFile()
        {
            var singinData = new ConnectionDataDto
            {
                Login = "login",
                Email = "email",
                Pwd = "pwd",
                Ip = "ip-address",
                Port = 9999
            };

            File.WriteAllText("userData.txt", JsonConvert.SerializeObject(singinData));
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
                var registerForm = new RegisterClientDto
                {
                    Login = _connectionData.Login,
                    Email = _connectionData.Email,
                    Pwd = _connectionData.Pwd
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

        private void ShowSubs()
        {
            while (true)
            {
                Console.Clear();
                Thread.Sleep(ScreenUpdateDelay);
                var request = new CommonDto {DataType = JsonType.RequestType.SubscribeList};
                SendAndReceive(JsonConvert.SerializeObject(request));
                var res = JsonConvert.DeserializeObject<HomeListDto>(_receiveBuffer);
                var cnt = 1;
                Console.WriteLine("Current homes:");
                Debug.Assert(res != null, nameof(res) + " != null");
                foreach (HomeElementDto home in res.Homes) Console.WriteLine(cnt++ + ") " + home.Name);
                Console.Write(
                    "Available actions:\n1) Subscribe to...\n2) Unsubscribe from...\n3) Return\nYour selection: ");
                var selectionLine = Console.ReadLine();
                var selec = 0;
                try
                {
                    selec = Convert.ToInt32(selectionLine);
                }
                catch
                {
                    selec = 0;
                }

                switch (selec)
                {
                    case 1:
                        SubscribeToHome();
                        break;
                    case 2:
                        UnsubFromHome(res);
                        break;
                    case 3:
                        return;
                }
            }
        }

        private void UnsubFromHome(HomeListDto res)
        {
            var cnt = 1;
            Console.Clear();
            Thread.Sleep(ScreenUpdateDelay);
            foreach (HomeElementDto home in res.Homes) Console.WriteLine(cnt++ + ") " + home.Name);

            Console.WriteLine(cnt + ") Return");
            Console.Write("Subscribe to: ");
            var selectionLine = Console.ReadLine();
            var selec = 0;
            try
            {
                selec = Convert.ToInt32(selectionLine);
            }
            catch
            {
                selec = 0;
            }

            if (selec >= cnt || selec < 1)
                return;
            var unsubDo = new SubscribeDto
            {
                Request = SubscribeDto.RequestType.UnsubRequest, Name = res.Homes[selec - 1].Name
            };
            SendAndReceive(JsonConvert.SerializeObject(unsubDo));
            Console.WriteLine(_result != 0 ? "Error!" : "Success!");
            Thread.Sleep(2000);
        }

        private void SubscribeToHome()
        {
            Console.Clear();
            Thread.Sleep(ScreenUpdateDelay);
            var request = new CommonDto {DataType = JsonType.RequestType.HomeList};
            SendAndReceive(JsonConvert.SerializeObject(request));
            var res = JsonConvert.DeserializeObject<HomeListDto>(_receiveBuffer);
            var cnt = 1;
            Console.WriteLine("Available homes: {0}", res.Homes.Count);
            Debug.Assert(res != null, nameof(res) + " != null");
            foreach (HomeElementDto home in res.Homes)
            {
                Console.WriteLine(cnt + ") " + home.Name);
                cnt++;
            }

            Console.WriteLine(cnt + ") Return");
            Console.Write("Subscribe to: ");
            var selectionLine = Console.ReadLine();
            var selec = 0;
            try
            {
                selec = Convert.ToInt32(selectionLine);
            }
            catch
            {
                selec = 0;
            }

            if (selec >= cnt || selec < 1)
                return;
            HomeElementDto choosenHome = res.Homes[selec - 1];
            var subDo = new SubscribeDto {Request = SubscribeDto.RequestType.SubRequest, Name = choosenHome.Name};
            SendAndReceive(JsonConvert.SerializeObject(subDo));
            Console.WriteLine(_result != 0 ? "Error!" : "Success!");
            Thread.Sleep(2000);
        }

        private void StartWorking()
        {
            var exit = false;
            var tokenSource2 = new CancellationTokenSource();
            CancellationToken ct = tokenSource2.Token;

            Task tsk = Task.Factory.StartNew(() =>
            {
                while (Console.ReadKey().Key != ConsoleKey.Q)
                    if (ct.IsCancellationRequested)
                        break;

                exit = true;
            }, tokenSource2.Token);

            while (!exit)
            {
                Console.Clear();
                Console.WriteLine("Requesting task...");
                var request = new CommonDto {DataType = JsonType.RequestType.Task};

                SendAndReceive(JsonConvert.SerializeObject(request));

                if (_result != 0)
                {
                    Console.WriteLine("Error, no task provided!");
                    tokenSource2.Cancel();
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine("Success!");

                var taskData = JsonConvert.DeserializeObject<TaskDto>(_receiveBuffer);
                var task = new TaskSolver();
                Debug.Assert(taskData != null, nameof(taskData) + " != null");
                Console.WriteLine("Solving task id: " + taskData.Id + "\nPress 'Q' to stop!");
                task.TaskData = taskData.Text;
                _taskThread = new Thread(task.Run);
                _taskThread.Start();

                using (var bar = new ProgressBar(10000, "Solving task..."))
                {
                    var progress = bar.AsProgress<float>();
                    while (task.IsDone == false && exit == false)
                    {
                        progress.Report((float) task.Prorgess);
                        Thread.Sleep(250);
                    }
                }

                if (exit)
                {
                    task.Interrupt = true;
                    return;
                }

                Console.WriteLine("Sending results...");

                var resultAns = new AnswerDto {Result = task.Result, TaskId = taskData.Id};
                SendAndReceive(JsonConvert.SerializeObject(resultAns));

                if (_result != 0)
                {
                    Console.WriteLine("Error, answer not sended!!");
                    tokenSource2.Cancel();
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine("Success!");
            }
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                var client = (Socket) ar.AsyncState;
                client.EndConnect(ar);
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
            try
            {
                var state = (StateObject) ar.AsyncState;
                Socket client = state.WorkSocket;

                var bytesRead = client.EndReceive(ar);

                state.Read = bytesRead;

                state.Sb.Append(Encoding.ASCII.GetString(state.Buffer, 0, state.Read));
                var content = state.Sb.ToString();
                var reqType = JsonConvert.DeserializeObject<CommonDto>(content);
                Debug.Assert(reqType != null, nameof(reqType) + " != null");
                switch (reqType.DataType)
                {
                    case JsonType.RequestType.ReceiveStatus:
                        var status = JsonConvert.DeserializeObject<ReceiveStatusDto>(content);
                        _result = status != null && status.Success ? 0 : 1;
                        _receiveDone.Set();
                        break;
                    case JsonType.RequestType.SubscribeList:
                        _receiveBuffer = content;
                        _receiveDone.Set();
                        _result = 0;
                        break;
                    case JsonType.RequestType.HomeList:
                        _receiveBuffer = content;
                        _receiveDone.Set();
                        _result = 0;
                        break;
                    case JsonType.RequestType.Task:
                        _receiveBuffer = content;
                        _receiveDone.Set();
                        _result = 0;
                        break;
                    case JsonType.RequestType.Connection:
                        _sendDone.Reset();
                        Send(_conn, content);
                        _sendDone.WaitOne();
                        //_result = 0;
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                Receive(client);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
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
                client.EndSend(ar);
                _sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}