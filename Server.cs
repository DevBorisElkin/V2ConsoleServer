using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace V2ConsoleServer
{
    public class Server
    {
        public string ip;
        public int port = 8384;
        public Dictionary<int, ClientHandler> clients;

        public bool serverActive;

        Socket handler;
        Socket listenSocket;

        #region Delegates
        public delegate void OnServerStartedDelegate();
        public event OnServerStartedDelegate OnServerStartedEvent;

        public delegate void OnServerShutDownDelegate();
        public event OnServerShutDownDelegate OnServerShutDownEvent;

        public delegate void OnClientConnectedDelegate(ClientHandler client);
        public event OnClientConnectedDelegate OnClientConnectedEvent;

        public delegate void OnClientDisconnectedDelegate(ClientHandler client, string error);
        public event OnClientDisconnectedDelegate OnClientDisconnectedEvent;

        public delegate void OnMessageReceivedDelegate(string message, ClientHandler client);
        public event OnMessageReceivedDelegate OnMessageReceivedEvent;


        void OnServerStarted() { OnServerStartedEvent?.Invoke(); }
        void OnServerShutDown() { OnServerShutDownEvent?.Invoke(); }
        public void OnClientConnected(ClientHandler client) { OnClientConnectedEvent?.Invoke(client); }
        public void OnClientDisconnected(ClientHandler client, string error) { OnClientDisconnectedEvent?.Invoke(client, error); }
        public void OnMessageReceived(string message, ClientHandler client) { OnMessageReceivedEvent?.Invoke(message, client); }
        #endregion

        // [START SERVER]
        public void StartServer(int port)
        {
            this.port = port;
            ip = GetIpOfServer().ToString();

            clients = new Dictionary<int, ClientHandler>();

            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, port);
            listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listenSocket.Bind(ipEndPoint);
            listenSocket.Listen(5);
            serverActive = true;

            OnServerStarted();

            Task listenToConnectionsTask = new Task(ListenToNewConnections);
            listenToConnectionsTask.Start();
        }
        // [LISTEN TO CONNECTIONS]
        void ListenToNewConnections()
        {
            try
            {
                while (serverActive)
                {
                    handler = listenSocket.Accept();
                    int clientId = GetFirstFreeId();
                    ClientHandler client = new ClientHandler(this, handler, clientId);
                    AddClient(client, clientId);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString() + "\n Error 1");
            }
            finally
            {
                if (handler != null)
                {
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                }
            }
        }
        // [SHUT DOWN SERVER]
        public void ShutDownServer()
        {
            serverActive = false;
            if (handler != null)
            {
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            DisposeAllClients();
            OnServerShutDown();
        }
        // [ADD CLIENT]
        void AddClient(ClientHandler client, int id)
        {
            OnClientConnected(client);
            clients[id] = client;
        }
        // [REMOVE CLIENT]
        public void DisconnectClient(ClientHandler client)
        {
            client.ShutDownClient();
        }

        #region Util
        int GetFirstFreeId()
        {
            ClientHandler util;
            for (int i = 1; i < 10000; i++)
            {
                if (!clients.TryGetValue(i, out util))
                {
                    return i;
                }
            }
            Console.WriteLine("Error getting first free id!");
            return -1;
        }
        public ClientHandler TryToGetClientWithId(int id)
        {
            ClientHandler util;
            if (clients.TryGetValue(id, out util)) { return util; }
            else Console.WriteLine($"Error getting client with id {id}");
            return null;
        }
        public ClientHandler TryToGetClientWithIp(string ip)
        {
            ClientHandler util;
            for (int i = 1; i <= clients.Count; i++)
            {
                if (clients.TryGetValue(i, out util))
                {
                    if (util.ip.Equals(ip))
                    {
                        return util;
                    }
                }
            }
            return null;
        }

        void DisposeAllClients()
        {
            for (int i = 1; i <= clients.Count; i++)
            {
                clients[i].ShutDownClient(0, false);
            }
            clients = null;
        }

        // [SEND MESSAGE]
        public void SendMessageToAllClients(string message)
        {
            for (int i = 1; i <= clients.Count; i++)
            {
                clients[i].SendMessage(message);
            }
        }
        public void SendMessageToClient(string message, string ip)
        {
            ClientHandler clientHandler = TryToGetClientWithIp(ip);
            if (clientHandler != null) clientHandler.SendMessage(message);
        }
        public void SendMessageToClient(string message, int id)
        {
            ClientHandler clientHandler = TryToGetClientWithId(id);
            if (clientHandler != null) clientHandler.SendMessage(message);
        }
        public void SendMessageToClient(string message, ClientHandler client)
        {
            client.SendMessage(message);
        }


        IPAddress GetIpOfServer()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[1];
            return ipAddress;
        }
        #endregion
    }
    public class ClientHandler
    {
        Server server;
        public Socket handler;

        public int id;
        public string ip;

        Task taskListener;
        public ClientHandler(Server server, Socket handler, int id)
        {
            this.server = server;
            this.handler = handler;
            this.id = id;
            ip = this.GetRemoteIp();

            taskListener = new Task(ListenToMessages);
            taskListener.Start();
        }

        int errorMessages = 0;
        // [LISTEN TO MESSAGES]
        void ListenToMessages()
        {
            byte[] bytes = new byte[1024];
            string str;

            while (true)
            {
                try
                {
                    str = ReadLine2(handler, bytes);
                    if (!str.Equals(""))
                    {
                        server.OnMessageReceived(str, this);
                    }
                    else
                    {
                        errorMessages++;
                        if (errorMessages > 100)
                        {
                            ShutDownClient(1);
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    ShutDownClient(2);
                    break;
                }
            }
        }

        string ReadLine(Socket reciever, byte[] buffer)
        {
            int bytesRec = reciever.Receive(buffer);
            string data = Encoding.ASCII.GetString(buffer, 0, bytesRec);
            return data;
        }

        string ReadLine2(Socket reciever, byte[] buffer)
        {
            StringBuilder builder = new StringBuilder();
            int bytes = 0; // amount of received bytes
            do
            {
                bytes = reciever.Receive(buffer);
                builder.Append(Encoding.Unicode.GetString(buffer, 0, bytes));
            }
            while (handler.Available > 0);

            return builder.ToString();
        }
        public void SendMessage(string message)
        {
            byte[] dataToSend = Encoding.Unicode.GetBytes(message);
            handler.Send(dataToSend);
        }

        public void ShutDownClient(int error = 0, bool removeFromClientsList = true)
        {
            server.OnClientDisconnected(this, error.ToString());
            handler.Dispose();
            if(removeFromClientsList) server.clients.Remove(this.id);
            taskListener.Dispose();
        }
    }

    public static class ClientHandlerExtensions
    {
        public static bool SocketSimpleConnected(this ClientHandler ch)
        {
            return !((ch.handler.Poll(1000, SelectMode.SelectRead) && (ch.handler.Available == 0)) || !ch.handler.Connected);
        }

        public static string GetRemoteIp(this ClientHandler ch)
        {
            string rawRemoteIP = ch.handler.RemoteEndPoint.ToString();
            int dotsIndex = rawRemoteIP.LastIndexOf(":");
            string remoteIP = rawRemoteIP.Substring(0, dotsIndex);
            return remoteIP;
        }
        public static string GetRemoteIpAndPort(this ClientHandler ch)
        {
            return ch.handler.RemoteEndPoint.ToString();
        }
    }
}
