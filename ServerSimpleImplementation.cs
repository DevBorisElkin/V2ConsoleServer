using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace V2ConsoleServer
{
    class ServerSimpleImplementation
    {
        int port = 8384;
        Server server;
        public ServerSimpleImplementation()
        {
            server = new Server();

            server.OnServerStartedEvent += ServerStarted;
            server.OnServerShutDownEvent += ServerShutDown;
            server.OnClientConnectedEvent += ClientConnected;
            server.OnClientDisconnectedEvent += ClientDisconnected;
            server.OnMessageReceivedEvent += MessageReceived;

            server.StartServer(port);

            while (true)
            {
                string message = Console.ReadLine();
                server.SendMessageToAllClients(message);
            }
        }

        void ServerStarted() { Console.WriteLine($"[SERVER_LAUNCHED][{server.ip}]"); }
        void ServerShutDown() { Console.WriteLine($"[SERVER_SHUTDOWN][{server.ip}]"); }
        void ClientConnected(ClientHandler clientHandler) { Console.WriteLine($"[CLIENT_CONNECTED][{clientHandler.id}][{clientHandler.ip}]"); }
        void ClientDisconnected(ClientHandler clientHandler, string error) { Console.WriteLine($"[CLIENT_DISCONNECTED][{clientHandler.id}][{clientHandler.ip}]: {error}"); }
        void MessageReceived(string message, ClientHandler clientHandler) {   Console.WriteLine($"[CLIENT_MESSAGE][{clientHandler.id}][{clientHandler.ip}]: {message}");   }
    }
}
