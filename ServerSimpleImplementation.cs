using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using static V2ConsoleServer.Server;

namespace V2ConsoleServer
{
    // SIMPLE AND QUICK IMPLEMENTATION
    class ServerSimpleImplementation
    {
        int portTcp = 8384;
        int portUdp = 8385;
        Server server;
        public ServerSimpleImplementation()
        {
            Console.Title = "Simple Console Server";
            server = new Server();
            UDP.StartUdpServer(portUdp, server);

            server.OnServerStartedEvent += ServerStarted;
            server.OnServerShutDownEvent += ServerShutDown;
            server.OnClientConnectedEvent += ClientConnected;
            server.OnClientDisconnectedEvent += ClientDisconnected;
            server.OnMessageReceivedEvent += MessageReceived;

            server.StartServer(portTcp);

            while (true)
                ReadConsole();
        }

        void ReadConsole()
        {
            string consoleString = Console.ReadLine();

            if (consoleString != "")
            {
                if (consoleString.StartsWith("tcp "))
                {
                    consoleString = consoleString.Replace("tcp ", "");
                    server.SendMessageToAllClients(consoleString);
                }
                else if (consoleString.StartsWith("udp "))
                {
                    consoleString = consoleString.Replace("udp ", "");
                    server.SendMessageToAllClients(consoleString, MessageProtocol.UDP);
                }
                else
                {
                    server.SendMessageToAllClients(consoleString);
                }
            }
        }

        void ServerStarted() { Console.WriteLine($"[SERVER_LAUNCHED][{server.ip}]"); }
        void ServerShutDown() { Console.WriteLine($"[SERVER_SHUTDOWN][{server.ip}]"); }
        void ClientConnected(ClientHandler clientHandler) { Console.WriteLine($"[CLIENT_CONNECTED][{clientHandler.id}][{clientHandler.ip}]"); }
        void ClientDisconnected(ClientHandler clientHandler, string error) { Console.WriteLine($"[CLIENT_DISCONNECTED][{clientHandler.id}][{clientHandler.ip}]: {error}"); }
        void MessageReceived(string message, int id, string ip, MessageProtocol mp) {   Console.WriteLine($"[CLIENT_MESSAGE][{mp}][{id}][{ip}]: {message}");   }
    }
}
