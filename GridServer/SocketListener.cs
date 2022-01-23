// This is a personal academic project. Dear PVS-Studio, please check it.

// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Timers;
using GridServer.DTO;
using Newtonsoft.Json;
using Timer = System.Timers.Timer;

namespace GridServer
{
    internal class SocketListener
    {
        private const int SecondsToRemoveTask = 60;

        private readonly GridServerDataEntities _dbContext;

        private readonly List<ProcessingIntersocketMessages> _interSocketMsg;
        private readonly int _portClient;
        private readonly int _portHome;
        private ConnectionSettingsDTO _connectionData;

        private readonly List<StateObject> _socketsClient;
        private readonly List<StateObject> _socketsHome;
        private IPEndPoint _localEndPointClient;
        private IPEndPoint _localEndPointHome;
        public AutoResetEvent MainThreadLock = new AutoResetEvent(true);

        public bool Working = true;
        public ManualResetEvent WorkSoketMain = new ManualResetEvent(false);

        public SocketListener(int portClient, int portHome)
        {
            _portClient = portClient;
            _portHome = portHome;
            _dbContext = new GridServerDataEntities();
            _interSocketMsg = new List<ProcessingIntersocketMessages>();
            _socketsClient = new List<StateObject>();
            _socketsHome = new List<StateObject>();
            var checkConnectionTimer = new Timer(1000);
            checkConnectionTimer.Elapsed += CheckConnections;
            checkConnectionTimer.AutoReset = true;
            checkConnectionTimer.Enabled = true;
            var checkTaskTimer = new Timer(60000); //60 sec
            checkTaskTimer.Elapsed += CheckUndoneTasks;
            checkTaskTimer.AutoReset = true;
            checkTaskTimer.Enabled = true;
            var errorHomesDisposeTimer = new Timer(30000); //30 sec
            errorHomesDisposeTimer.Elapsed += CleatErrorHomes;
            errorHomesDisposeTimer.AutoReset = true;
            errorHomesDisposeTimer.Enabled = true;
        }

        private void CleatErrorHomes(object source, ElapsedEventArgs e)
        {
            foreach (StateObject state in _socketsClient) state.ErrorHomeServers.Clear();
        }

        private void CheckUndoneTasks(object source, ElapsedEventArgs e)
        {
            var clientsOffline = _dbContext.Client.Where(t => t.isOnline == false).ToList();
            foreach (Client client in clientsOffline)
            {
                var clientTasks = client.UserTasks.Where(t => t.isDone == false).ToList();
                for (var i = clientTasks.Count - 1; i >= 0; i--)
                {
                    UserTasks task = clientTasks[i];
                    if (task.dateDone == null) continue;
                    if ((DateTime.Now - task.dateDone).Value.TotalSeconds > SecondsToRemoveTask)
                        DeleteTaskFromUser(task);
                }
            }
        }

        private void DeleteTaskFromUser(UserTasks task)
        {
            var taskForRemove = new TaskResetDto {taskid = task.TaskJournal.taskId};
            Guid homeId = task.TaskJournal.homeId;
            foreach (StateObject socketHome in _socketsHome.Where(socketHome => socketHome.UserId == homeId))
            {
                Send(socketHome.WorkSocket, JsonConvert.SerializeObject(taskForRemove));
                break;
            }

            Console.WriteLine(DateTime.Now + " Remove assigned task from user, taskid: {0}" + task.TaskJournal.taskId);
            TaskJournal journal = task.TaskJournal;
            _dbContext.UserTasks.Remove(task);
            _dbContext.TaskJournal.Remove(journal);
            _dbContext.SaveChanges();
        }


        private void CheckConnections(object source, ElapsedEventArgs e)
        {
            for (var i = _socketsClient.Count - 1; i >= 0; i--)
                try
                {
                    _ = _socketsClient[i].WorkSocket.Available;
                }
                catch
                {
                    var cnt = 0;
                    Console.WriteLine(DateTime.Now + " Disconect client!");
                    Guid id = _socketsClient[i].UserId;
                    if (id != Guid.Empty)
                    {
                        _socketsClient[i].WorkSocket.Close();
                        Client client = _dbContext.Client.Single(t => t.id == id);
                        var unDonetsks = client.UserTasks.ToList();
                        foreach (UserTasks task in unDonetsks.Where(task => !task.isDone))
                        {
                            task.dateDone = DateTime.Now;
                            cnt++;
                        }

                        client.isOnline = false;
                        Console.WriteLine("Total unfinished tasks: {0}", cnt);
                        _dbContext.SaveChanges();
                    }

                    _socketsClient.RemoveAt(i);
                }

            for (var i = _socketsHome.Count - 1; i >= 0; i--)
                try
                {
                    _ = _socketsHome[i].WorkSocket.Available;
                }
                catch
                {
                    Console.WriteLine(DateTime.Now + " Disconect home!");
                    _socketsHome[i].WorkSocket.Close();
                    _socketsHome.RemoveAt(i);
                }
        }

        public void StartListening()
        {
            if (!File.Exists("loginData.txt"))
            {
                CreateConfigFile();
                Console.Write("No config file detected! Created new one. Please config it!");
                Console.ReadKey();
                return;
            }

            var dta = File.ReadAllText("loginData.txt");
            _connectionData = JsonConvert.DeserializeObject<ConnectionSettingsDTO>(dta);

            IPAddress ipAddress = IPAddress.Parse(_connectionData.Ip);
            _localEndPointClient = new IPEndPoint(ipAddress, _portClient);
            _localEndPointHome = new IPEndPoint(ipAddress, _portHome);

            var listenerHome = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);
            var listenerClient = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);
            Console.WriteLine(DateTime.Now + " Socket started!");

            try
            {
                listenerClient.Bind(_localEndPointClient);
                listenerClient.Listen(100);
                listenerHome.Bind(_localEndPointHome);
                listenerHome.Listen(100);

                while (Working)
                {
                    // Set the event to nonsignaled state.  
                    WorkSoketMain.Reset();

                    // Start an asynchronous socket to listen for connections.  
                    listenerHome.BeginAccept(
                        AcceptCallback,
                        listenerHome);
                    listenerClient.BeginAccept(
                        AcceptCallback,
                        listenerClient);

                    // Wait until a connection is made before continuing.  
                    WorkSoketMain.WaitOne();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine(DateTime.Now + " Socket stopped work!");
        }

        private static void CreateConfigFile()
        {
            var singinData = new ConnectionSettingsDTO()
            {
                Ip = "ip-address",
            };

            File.WriteAllText("loginData.txt", JsonConvert.SerializeObject(singinData));
        }
        private void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.  
            WorkSoketMain.Set();

            // Get the socket that handles the client request.  
            var listener = (Socket) ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // Create the state object.  
            var state = new StateObject {WorkSocket = handler};
            handler.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0,
                ReadCallback, state);
            if (listener.LocalEndPoint.Equals(_localEndPointClient))
            {
                Console.WriteLine(DateTime.Now + " Socket: client connected!");
                _socketsClient.Add(state);
            }
            else
            {
                Console.WriteLine(DateTime.Now + " Socket: Home server connected!");
                _socketsHome.Add(state);
            }
        }

        public void ReadCallback(IAsyncResult ar)
        {
            var state = (StateObject) ar.AsyncState;
            Socket handler = state.WorkSocket;
            var read = 0;
            // Read data from the client socket.  
            try
            {
                read = handler.EndReceive(ar);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
            }

            if (read > 0)
            {
                MainThreadLock.WaitOne();
                state.Read = read;
                handler.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0,
                    ReadCallback, state);
                if (handler.LocalEndPoint.Equals(_localEndPointClient))
                    ProcessClient(state);
                else
                    ProcessHome(state);
                MainThreadLock.Set();
            }
            else
            {
                handler.Close();
            }
        }

        private void ProcessClient(StateObject state)
        {
            state.Sb.Append(Encoding.ASCII.GetString(state.Buffer, 0, state.Read));
            var contentList = state.Sb.ToString().Split('\n');
            foreach (var content in contentList)
            {
                if (content == "")
                    continue;
                var reqType = JsonConvert.DeserializeObject<CommonDto>(content);

                Debug.Assert(reqType != null, nameof(reqType) + " != null");
                switch (reqType.DataType)
                {
                    case JsonType.RequestType.RegisterClient: //Register new user
                        RegisterClient(state, content);
                        break;
                    case JsonType.RequestType.Login: //Login user
                        LoginClient(state, content);
                        break;
                    case JsonType.RequestType.Task: //Get task
                        GetTaskClientRequest(state);
                        break;
                    case JsonType.RequestType.Answer: //Sended answer to task
                        ReceiveAnswerFromClient(state, content);
                        break;
                    case JsonType.RequestType.HomeList: //Get all avail homes
                        var homeList = _dbContext.Home.ToList();
                        var userHomeList = _dbContext.Client.Single(t => t.id == state.UserId).Home.ToList();
                        var missedHomeList = homeList.Except(userHomeList)
                            .Select(t => new HomeElementDto {Name = t.name, Desc = t.description}).ToList();

                        var homesSend = new HomeListDto();
                        homesSend.Homes = missedHomeList;
                        Send(state.WorkSocket, JsonConvert.SerializeObject(homesSend));
                        break;
                    case JsonType.RequestType.SubscribeList: //Get homes subsribes
                        Client subs = _dbContext.Client.Single(t => t.id == state.UserId);
                        var test1 = subs.Home.
                            Select(t => new HomeElementDto {Name = t.name, Desc = t.description}).ToList();
                        var homesSendL = new HomeListDto
                        {
                            Homes = test1
                        };
                        Send(state.WorkSocket, JsonConvert.SerializeObject(homesSendL));
                        break;
                    case JsonType.RequestType.Subscribe: //Change subscribe
                        SubscribeManageClient(state, content);
                        break;
                    case JsonType.RequestType.Connection: // Connection still exist
                        state.MarkedForClose = false;
                        break;
                    case JsonType.RequestType.CheckExistence:
                        var login = JsonConvert.DeserializeObject<LoginDto>(content)?.Login;
                        Client userContainer = _dbContext.Client.SingleOrDefault(t => t.user_login == login);
                        if (userContainer != null)
                        {
                            SendOkStatus(state);
                        }
                        else
                        {
                            var res = new ReceiveStatusDto {Success = false, Msg = "Not exist"};
                            Send(state.WorkSocket, JsonConvert.SerializeObject(res));
                        }

                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            state.Sb.Clear();
        }

        private void SubscribeManageClient(StateObject state, string content)
        {
            var sub = JsonConvert.DeserializeObject<SubscribeDto>(content);
            if (sub == null) return;
            switch (sub.Request)
            {
                case SubscribeDto.RequestType.SubRequest:
                {
                    Home homeSub = _dbContext.Home.Single(t => t.name == sub.Name);
                    Client clie = _dbContext.Client.Single(t => t.id == state.UserId);
                    clie.Home.Add(homeSub);
                    _dbContext.SaveChanges();
                    SendOkStatus(state);
                    break;
                }
                case SubscribeDto.RequestType.UnsubRequest:
                {
                    Home homeSub = _dbContext.Home.SingleOrDefault(t => t.name == sub.Name);
                    Client clie = _dbContext.Client.SingleOrDefault(t => t.id == state.UserId);
                    if (clie != null && homeSub != null)
                    {
                        clie.Home.Remove(homeSub);
                        var userTasksPerHome = clie.UserTasks.Where(t => t.TaskJournal.homeId == homeSub.id)
                            .ToList();
                        foreach (UserTasks temp in userTasksPerHome) DeleteTaskFromUser(temp);
                    }

                    _dbContext.SaveChanges();
                    SendOkStatus(state);
                    break;
                }
                case SubscribeDto.RequestType.RequestNull:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void SendOkStatus(StateObject state)
        {
            var res = new ReceiveStatusDto
            {
                Success = true,
                Msg = "OK!"
            };
            Send(state.WorkSocket, JsonConvert.SerializeObject(res));
        }

        private void ReceiveAnswerFromClient(StateObject state, string content)
        {
            var ans = JsonConvert.DeserializeObject<AnswerDto>(content);
            UserTasks userTask =
                _dbContext.UserTasks.Single(t => t.userId == state.UserId && t.TaskJournal.taskId == ans.TaskId);
            TaskJournal tsk = userTask.TaskJournal;

            Guid serverGuid = tsk.Home.id;
            var flagSended = false;
            StateObject homeSocket = _socketsHome.SingleOrDefault(socket => socket.UserId == serverGuid);
            if (homeSocket != null)
                try
                {
                    _ = homeSocket.WorkSocket.Available;
                    Send(homeSocket.WorkSocket, content);
                    flagSended = true;
                }
                catch
                {
                    _socketsHome.Remove(homeSocket);
                }

            if (flagSended)
            {
                var taskl = tsk.UserTasks.ToList();
                foreach (UserTasks variable in taskl) _dbContext.UserTasks.Remove(variable);

                _dbContext.TaskJournal.Remove(tsk);
            }
            else
            {
                userTask.dateDone = DateTime.Now;
                Debug.Assert(ans != null, nameof(ans) + " != null");
                userTask.resilt = ans.Result;
                userTask.isDone = true;
            }

            SendOkStatus(state);
            _dbContext.SaveChanges();
        }

        private void GetTaskClientRequest(StateObject state)
        {
            var msg = new ProcessingIntersocketMessages
            {
                RequestId = Guid.NewGuid(),
                RequestReason = ProcessingIntersocketMessages.RequestType.TaskRequest,
                Requester = state
            };

            Client client = _dbContext.Client.Single(t => t.id == state.UserId);
            var homeSubs = client.Home.ToList();
            var doneWork = client.UserTasks.Where(t => t.isDone == false).ToList();
            if (doneWork.Count > 0)
            {
                var cnt = 0;
                while (cnt < doneWork.Count)
                {
                    UserTasks userTask = doneWork[cnt++];
                    StateObject socketHome = _socketsHome.SingleOrDefault(t => t.UserId == userTask.TaskJournal.homeId);
                    if (socketHome == null) continue;
                    var taskDto = new TaskRequestDto
                    {
                        RequestId = msg.RequestId,
                        ServerId = msg.RequestId,
                        Id = userTask.TaskJournal.taskId
                    };
                    try
                    {
                        _ = socketHome.WorkSocket.Available;
                        _interSocketMsg.Add(msg);
                        Send(socketHome.WorkSocket, JsonConvert.SerializeObject(taskDto));
                        return;
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (FindNewTaskForClient(state, homeSubs, msg)) return;


            Console.WriteLine(DateTime.Now + " Error during sending task to client!");
            var res = new ReceiveStatusDto
            {
                Success = false,
                Msg = "No task!"
            };
            Send(state.WorkSocket, JsonConvert.SerializeObject(res));
        }

        private bool FindNewTaskForClient(StateObject state, List<Home> homeSubs, ProcessingIntersocketMessages msg)
        {
            if (homeSubs.Count == 0)
            {
                var res = new ReceiveStatusDto
                {
                    Success = false,
                    Msg = "No homes avail for task!"
                };
                Send(state.WorkSocket, JsonConvert.SerializeObject(res));
                return true;
            }

            Home target;
            var availHomes = _socketsHome.Select(t => t.UserId).ToList();
            homeSubs = homeSubs.Where(t => availHomes.Contains(t.id) && !state.ErrorHomeServers.Contains(t.id))
                .ToList();
            while (homeSubs.Count > 0)
            {
                if (homeSubs.Count > 1)
                {
                    if (state.PreviousHomeTaskGiver.Count > 0)
                    {
                        var homeIds = homeSubs.Select(t => t.id).ToList();
                        var anotherHomes = homeIds.Except(state.PreviousHomeTaskGiver).Intersect(availHomes).ToList();
                        if (anotherHomes.Count == 0)
                        {
                            state.PreviousHomeTaskGiver.Clear();
                            continue;
                        }

                        target = homeSubs.First(t => t.id == anotherHomes[0]);
                        state.PreviousHomeTaskGiver.Add(target.id);
                    }
                    else
                    {
                        target = homeSubs[0];
                        state.PreviousHomeTaskGiver.Add(target.id);
                    }
                }
                else
                {
                    target = homeSubs[0];
                }

                StateObject socketHome = _socketsHome.SingleOrDefault(t => t.UserId == target.id);

                if (socketHome != null)
                {
                    var taskDto = new TaskRequestDto
                    {
                        RequestId = msg.RequestId,
                        ServerId = msg.RequestId,
                        Id = Guid.Empty
                    };
                    try
                    {
                        _ = socketHome.WorkSocket.Available;
                        _interSocketMsg.Add(msg);
                        Send(socketHome.WorkSocket, JsonConvert.SerializeObject(taskDto));
                        return true;
                    }
                    catch
                    {
                        _socketsHome.Remove(socketHome);
                        availHomes = _socketsHome.Select(t => t.UserId).ToList();
                        homeSubs = homeSubs.Where(t => availHomes.Contains(t.id)).ToList();
                    }
                }
            }

            return false;
        }

        private void LoginClient(StateObject state, string content)
        {
            var logForm = JsonConvert.DeserializeObject<LoginDto>(content);
            Client user = _dbContext.Client.SingleOrDefault(t => t.user_login == logForm.Login);
            foreach (StateObject variable in _socketsClient.Where(variable =>
            {
                Debug.Assert(user != null, nameof(user) + " != null");
                return variable.UserId == user.id;
            }))
            {
                _socketsClient.Remove(variable);
                break;
            }

            Debug.Assert(logForm != null, nameof(logForm) + " != null");
            Debug.Assert(user != null, nameof(user) + " != null");
            var pbkdf2 = new Rfc2898DeriveBytes(logForm.Pwd, Convert.FromBase64String(user.salt), 100000);
            var hashed = Convert.ToBase64String(pbkdf2.GetBytes(64));
            if (_dbContext.Client.SingleOrDefault(t => t.pwd == Convert.ToBase64String(pbkdf2.GetBytes(64)) && 
                                                       t.user_login == logForm.Login) != null)
            {
                state.UserId = user.id;
                user.isOnline = true;
                SendOkStatus(state);
                Console.WriteLine(DateTime.Now + " User logged in: {0}", user.user_login);
            }
            else
            {
                var res = new ReceiveStatusDto {Success = false, Msg = "Error during logining"};
                Send(state.WorkSocket, JsonConvert.SerializeObject(res));
                Console.WriteLine(DateTime.Now + " Error while loggining user: {0}", user.user_login);
            }

            _dbContext.SaveChanges();
        }

        private void RegisterClient(StateObject state, string content)
        {
            var regForm = JsonConvert.DeserializeObject<RegisterClientDto>(content);
            Debug.Assert(regForm != null, nameof(regForm) + " != null");
            byte[] salt = new byte[128 / 8];
            using (var rngCsp = new RNGCryptoServiceProvider())
            {
                rngCsp.GetNonZeroBytes(salt);
            }
            var pbkdf2 = new Rfc2898DeriveBytes(regForm.Pwd, salt, 100000);
            var hashed= Convert.ToBase64String(pbkdf2.GetBytes(64));
    

            var test = new Client
            {
                email = regForm.Email,
                pwd = hashed,
                salt = Convert.ToBase64String(salt),
                isOnline = true,
                user_login = regForm.Login
            };
            _dbContext.Client.Add(test);
            try
            {
                _dbContext.SaveChanges();

                state.UserId = test.id;
                SendOkStatus(state);
                Console.WriteLine(DateTime.Now + " New user registered: {0}", test.user_login);
            }
            catch (Exception e)
            {
                Console.WriteLine(DateTime.Now + " Error while registering new user: {0}", test.user_login);
                Console.WriteLine(e.ToString());
                var res = new ReceiveStatusDto
                {
                    Success = false,
                    Msg = "Error during registering new user"
                };
                Send(state.WorkSocket, JsonConvert.SerializeObject(res));
            }
        }

        private void ProcessHome(StateObject state)
        {
            state.Sb.Append(Encoding.ASCII.GetString(state.Buffer, 0, state.Read));
            var contentList = state.Sb.ToString().Split('\n');
            foreach (var content in contentList)
            {
                if (content == "")
                    continue;

                var reqType = JsonConvert.DeserializeObject<CommonDto>(content);

                Debug.Assert(reqType != null, nameof(reqType) + " != null");
                switch (reqType.DataType)
                {
                    case JsonType.RequestType.RegisterHome: //Register new user
                        RegisterHome(state, content);
                        break;
                    case JsonType.RequestType.Login: //Login user
                        LoginHome(state, content);
                        break;
                    case JsonType.RequestType.TaskRequest: //Send task
                        RequestTaskAnsFromHome(state, content);
                        break;
                    case JsonType.RequestType.CheckExistence:
                        var login = JsonConvert.DeserializeObject<LoginDto>(content)?.Login;
                        Home home = _dbContext.Home.FirstOrDefault(t => t.home_login == login);
                        if (home != null)
                        {
                            SendOkStatus(state);
                        }
                        else
                        {
                            var res = new ReceiveStatusDto {Success = false, Msg = "Not exist"};
                            Send(state.WorkSocket, JsonConvert.SerializeObject(res));
                        }

                        break;
                    case JsonType.RequestType.Connection: // Connection still exist
                        state.MarkedForClose = false;
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            state.Sb.Clear();
        }

        private void RequestTaskAnsFromHome(StateObject state, string content)
        {
            var taskData = JsonConvert.DeserializeObject<TaskRequestDto>(content);
            if (taskData == null)
                throw new ArgumentException();
            ProcessingIntersocketMessages intersocketCom =
                _interSocketMsg.SingleOrDefault(t => t.RequestId == taskData.RequestId);

            if (taskData.ServerId == Guid.Empty || intersocketCom == null)
            {
                if (taskData.ServerId == Guid.Empty && intersocketCom != null)
                {
                    intersocketCom.Requester.ErrorHomeServers.Add(state.UserId);
                    GetTaskClientRequest(intersocketCom.Requester);
                    _interSocketMsg.Remove(intersocketCom);
                    return;
                }

                Console.WriteLine(DateTime.Now + "No intersocket comm data! Internal server error!");
                return;
            }

            var taskBody = new TaskDto {Id = taskData.Id, Text = taskData.Text};
            UserTasks userTask =
                _dbContext.UserTasks.SingleOrDefault(t => t.userId == intersocketCom.Requester.UserId
                                                          && t.TaskJournal.taskId == taskData.Id);
            if (userTask == null)
            {
                var journalEntity = new TaskJournal
                {
                    homeId = state.UserId,
                    date = DateTime.Now,
                    taskId = taskBody.Id
                };
                _dbContext.TaskJournal.Add(journalEntity);
                Client usr = _dbContext.Client.Single(t => t.id == intersocketCom.Requester.UserId);
                var usrTsk = new UserTasks
                {
                    TaskJournal = journalEntity,
                    dateStart = DateTime.Now,
                    userId = usr.id,
                    isDone = false
                };
                usr.UserTasks.Add(usrTsk);
                _dbContext.UserTasks.Add(usrTsk);
                _dbContext.SaveChanges();
            }
            else
            {
                Console.WriteLine(
                    DateTime.Now + " Send already existed task to user: " + intersocketCom.Requester.UserId,
                    ", Task: " + taskData.Id);
                userTask.dateDone = null;
                _dbContext.SaveChanges();
            }

            Send(intersocketCom.Requester.WorkSocket, JsonConvert.SerializeObject(taskBody));
            _interSocketMsg.Remove(intersocketCom);
        }

        private void LoginHome(StateObject state, string content)
        {
            var logForm = JsonConvert.DeserializeObject<LoginDto>(content);
            Home user = _dbContext.Home.SingleOrDefault(t => t.home_login == logForm.Login);
            var pbkdf2 = new Rfc2898DeriveBytes(logForm.Pwd, Convert.FromBase64String(user.salt), 100000);
            var hashed = Convert.ToBase64String(pbkdf2.GetBytes(64));
            if (_dbContext.Home.SingleOrDefault(t => t.pwd == Convert.ToBase64String(pbkdf2.GetBytes(64)) &&
                                                       t.home_login == logForm.Login) != null)
            {
                foreach (StateObject variable in _socketsHome.Where(variable => variable.UserId == user.id))
                {
                    _socketsHome.Remove(variable);
                    break;
                }

                state.UserId = user.id;
                SendOkStatus(state);
                Console.WriteLine(DateTime.Now + " User logged in: {0}", user.home_login);

                var taskReadyList = _dbContext.UserTasks.Where(t => t.isDone && t.TaskJournal.homeId == state.UserId)
                    .ToList();
                foreach (UserTasks task in taskReadyList)
                {
                    var ans = new AnswerDto {Result = task.resilt, TaskId = task.TaskJournal.taskId};
                    Send(state.WorkSocket, JsonConvert.SerializeObject(ans));
                    TaskJournal taskJ = task.TaskJournal;
                    var taskl = taskJ.UserTasks.ToList();
                    foreach (UserTasks variable in taskl) _dbContext.UserTasks.Remove(variable);

                    _dbContext.TaskJournal.Remove(taskJ);
                }

                _dbContext.SaveChanges();
            }
            else
            {
                var res = new ReceiveStatusDto {Success = false, Msg = "Error during logining"};
                Send(state.WorkSocket, JsonConvert.SerializeObject(res));
                Debug.Assert(user != null, nameof(user) + " != null");
                Console.WriteLine(DateTime.Now + " Error while loggining user: {0}", user.home_login);
            }
        }

        private void RegisterHome(StateObject state, string content)
        {
            var regForm = JsonConvert.DeserializeObject<RegisterHomeDto>(content);
            Debug.Assert(regForm != null, nameof(regForm) + " != null");
            byte[] salt = new byte[128 / 8];
            using (var rngCsp = new RNGCryptoServiceProvider())
            {
                rngCsp.GetNonZeroBytes(salt);
            }
            var pbkdf2 = new Rfc2898DeriveBytes(regForm.Pwd, salt, 100000);
            var hashed = Convert.ToBase64String(pbkdf2.GetBytes(64));
            var test = new Home
            {
                pwd = hashed,
                salt = Convert.ToBase64String(salt),
                name = regForm.Name,
                description = regForm.Desc,
                home_login = regForm.Login
            };
            _dbContext.Home.Add(test);
            try
            {
                _dbContext.SaveChanges();

                state.UserId = test.id;
                SendOkStatus(state);
                Console.WriteLine(DateTime.Now + " New user registered: {0}", test.home_login);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine(DateTime.Now + " Error while registering new user: {0}", test.home_login);
                var res = new ReceiveStatusDto {Success = false, Msg = "Error"};
                Send(state.WorkSocket, JsonConvert.SerializeObject(res));
            }
        }

        private void Send(Socket handler, string data)
        {
            var byteData = Encoding.ASCII.GetBytes(data + "\n");
            try
            {
                handler.BeginSend(byteData, 0, byteData.Length, 0,
                    SendCallback, handler);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                var handler = (Socket) ar.AsyncState;
                handler.EndSend(ar);
                //Console.WriteLine("Sent {0} bytes to client.", bytesSent);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}