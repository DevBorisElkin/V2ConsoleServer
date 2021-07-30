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

        public delegate void OnMessageReceivedDelegate(string message, int id, string ip, MessageProtocol mp);
        public event OnMessageReceivedDelegate OnMessageReceivedEvent;

        public enum MessageProtocol { TCP, UDP }


        void OnServerStarted() { OnServerStartedEvent?.Invoke(); }
        void OnServerShutDown() { OnServerShutDownEvent?.Invoke(); }
        public void OnClientConnected(ClientHandler client) { OnClientConnectedEvent?.Invoke(client); }
        public void OnClientDisconnected(ClientHandler client, string error) { OnClientDisconnectedEvent?.Invoke(client, error); }
        public void OnMessageReceived(string message, int id, string ip, MessageProtocol mp) { OnMessageReceivedEvent?.Invoke(message, id, ip, mp); }
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
                    if (!this.AlreadyHasThisClient(handler))
                    {
                        int clientId = GetFirstFreeId();
                        ClientHandler client = new ClientHandler(this, handler, clientId);
                        AddClient(client, clientId);
                    }
                    else
                    {
                        Console.WriteLine($"[SERVER_MESSAGE] reject repetetive connection from {ConnectionExtensions.GetRemoteIp(handler)}");
                        handler.Shutdown(SocketShutdown.Both);
                        handler.Dispose();
                    }
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
        public void SendMessageToAllClients(string message, MessageProtocol mp = MessageProtocol.TCP)
        {
            if (mp.Equals(MessageProtocol.TCP))
            {
                for (int i = 1; i <= clients.Count; i++)
                {
                    clients[i].SendMessageTcp(message);
                }
            }
            else
            {
                for (int i = 1; i <= clients.Count; i++)
                {
                    UDP.SendMessageUdp(message, clients[i].udpEndPoint);
                }
            }
            
        }
        public void SendMessageToClient(string message, string ip)
        {
            ClientHandler clientHandler = TryToGetClientWithIp(ip);
            if (clientHandler != null) clientHandler.SendMessageTcp(message);
        }
        public void SendMessageToClient(string message, int id)
        {
            ClientHandler clientHandler = TryToGetClientWithId(id);
            if (clientHandler != null) clientHandler.SendMessageTcp(message);
        }
        public void SendMessageToClient(string message, ClientHandler client)
        {
            client.SendMessageTcp(message);
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
        public IPEndPoint udpEndPoint;

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

        // [LISTEN TO MESSAGES]
        void ListenToMessages()
        {
            int errorMessages = 0;
            byte[] bytes = new byte[1024];
            string str;

            while (handler.Connected)
            {
                try
                {
                    str = ReadLine2(handler, bytes);
                    if (!str.Equals(""))
                    {
                        server.OnMessageReceived(str, id, ip, Server.MessageProtocol.TCP);
                    }
                    else if(str.Equals(""))
                    {
                        errorMessages++;
                        if (errorMessages > 25)
                        {
                            ShutDownClient(1);
                            break;
                        }
                    }
                }
                catch (Exception e)
                {   
                    ShutDownClient(2);  // usually never called, but for safety
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
            while (reciever.Available > 0);
            
            return builder.ToString();
        }
        public void SendMessageTcp(string message)
        {
            byte[] dataToSend = Encoding.Unicode.GetBytes(message);
            handler.Send(dataToSend);
        }

        public void ShutDownClient(int error = 0, bool removeFromClientsList = true)
        {
            server.OnClientDisconnected(this, error.ToString());

            if(handler.Connected)
            {
                handler.Shutdown(SocketShutdown.Both);
                handler.Dispose();
            }

            if(removeFromClientsList) server.clients.Remove(this.id);
        }
    }

    public static class ConnectionExtensions
    {
        public static bool SocketSimpleConnected(this ClientHandler ch)
        {
            return !((ch.handler.Poll(1000, SelectMode.SelectRead) && (ch.handler.Available == 0)) || !ch.handler.Connected);
        }
        public static bool SocketSimpleConnected(Socket tcpHandler)
        {
            return !((tcpHandler.Poll(1000, SelectMode.SelectRead) && (tcpHandler.Available == 0)) || !tcpHandler.Connected);
        }

        public static string GetRemoteIp(this ClientHandler ch)
        {
            string rawRemoteIP = ch.handler.RemoteEndPoint.ToString();
            int dotsIndex = rawRemoteIP.LastIndexOf(":");
            string remoteIP = rawRemoteIP.Substring(0, dotsIndex);
            return remoteIP;
        }
        public static string GetRemoteIp(EndPoint ep)
        {
            string rawRemoteIP = ep.ToString();
            int dotsIndex = rawRemoteIP.LastIndexOf(":");
            string remoteIP = rawRemoteIP.Substring(0, dotsIndex);
            return remoteIP;
        }

        public static string GetRemoteIp(Socket tcpHandler)
        {
            string rawRemoteIP = tcpHandler.RemoteEndPoint.ToString();
            int dotsIndex = rawRemoteIP.LastIndexOf(":");
            string remoteIP = rawRemoteIP.Substring(0, dotsIndex);
            return remoteIP;
        }

        public static string GetRemoteIpAndPort(this ClientHandler ch)
        {
            return ch.handler.RemoteEndPoint.ToString();
        }

        public static string GetRemoteIpAndPort(Socket tcpHandler)
        {
            return tcpHandler.RemoteEndPoint.ToString();
        }

        public static bool AlreadyHasThisClient(this Server server, Socket socket)
        {
            if (server.TryToGetClientWithIp(GetRemoteIp(socket)) == null) return false;
            return true;
        }
    }

    //______________________________________________________________________________________________________

    public static class UDP
    {
        public static Server server;
        public static int portUdp;

        static IPEndPoint ipEndPointUdp;
        static Socket listenSocketUdp;

        public static bool listening;

        public static void StartUdpServer(int _port, Server _server)
        {
            portUdp = _port;
            server = _server;

            ipEndPointUdp = new IPEndPoint(IPAddress.Any, portUdp);
            listenSocketUdp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            listening = true;
            Task udpListenTask = new Task(ListenUDP);
            udpListenTask.Start();
        }

        static EndPoint remote;
        #region Listen UPD - doesn't require established connection
        private static void ListenUDP()
        {
            try
            {
                listenSocketUdp.Bind(ipEndPointUdp);
                byte[] data = new byte[1024];
                remote = new IPEndPoint(IPAddress.Any, portUdp);

                int bytes;
                while (listening)
                {
                    StringBuilder builder = new StringBuilder();
                    do
                    {
                        bytes = listenSocketUdp.ReceiveFrom(data, ref remote);
                        builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                    }
                    while (listenSocketUdp.Available > 0);

                    IPEndPoint remoteIp = remote as IPEndPoint;
                    string ip = ConnectionExtensions.GetRemoteIp(remoteIp);
                    ClientHandler clientToBind = server.TryToGetClientWithIp(ip);

                    if (clientToBind == null)
                    {
                        Console.WriteLine($"[SYSTEM_ERROR]: didn't find client in clients list with ip {ip}");
                        continue;
                    }

                    // on first UDP message bind IPEndPoint to selected ClientHandler
                    if (builder.ToString().StartsWith("init_udp"))
                    {
                        clientToBind.udpEndPoint = remoteIp;
                        Console.WriteLine($"[SYSTEM_MESSAGE]: initialized IPEndPoint for UDP messaging of client [{clientToBind.id}][{clientToBind.ip}]");
                    }
                    else
                    {
                        server.OnMessageReceived(builder.ToString(), clientToBind.id, clientToBind.ip, Server.MessageProtocol.UDP);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                CloseUdp();
            }
        }
        private static void CloseUdp()
        {
            listening = false;
            if (listenSocketUdp != null)
            {
                listenSocketUdp.Shutdown(SocketShutdown.Both);
                listenSocketUdp.Close();
                listenSocketUdp = null;
            }
        }
        #endregion

        public static void SendMessageUdp(string message, IPEndPoint remoteIp)
        {
            if (remoteIp != null)
            {
                byte[] data = Encoding.Unicode.GetBytes(message);
                listenSocketUdp.SendTo(data, remoteIp);
            }
            else
            {
                Console.WriteLine("Remote end point has not beed defined yet");
            }

        }

        static void WriteAddressToConsole()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[1];
            Console.WriteLine("Address = " + ipAddress);
            Console.WriteLine("_____________________\n");
        }
    }
}
