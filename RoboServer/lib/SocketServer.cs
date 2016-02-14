using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

using Newtonsoft.Json.Linq;
using System.Net.WebSockets;

namespace RoboServer.lib
{

    // ========================================================================================
    //  Socket server. Нужен для получения команд от браузера или мобильного приложения.
    //  Работа идет по протоколу WebSocket, с которым работают браузеры.
    // ========================================================================================

    class SocketServer
    {
        private TcpListener server;
        private int port;
        private MainForm form;
        private WebSockServer webSockServer;

        public class ConnectionInfo
        {
            public Socket clientSock;
            public string name;
            public int userID;
            public int selfID;
            public List<Device> deviceList;
            public string command;
            public byte[] buffer = new byte[1024];
        }

       // private Thread acceptThread;
        private List<ConnectionInfo> connections = new List<ConnectionInfo>();

        public SocketServer(string ip, int newPort, MainForm frm) 
        {
            form = frm;
            port = newPort;
            form.Invoke(new Action(() => frm.appendSockLogBox("\nSocket server created: ip = " + ip + " port = " + port + "\n")));
        }

        public void Start()
        {
            server = new TcpListener(IPAddress.Any, port);
            server.Start();

            AsyncCallback aCallback = new AsyncCallback(AcceptTcpClientCallback);
            server.Server.BeginAccept(aCallback, server.Server);
        }

        // настраиваем и запускаем сервер
        private void AcceptTcpClientCallback(IAsyncResult ar)
        {
            Socket client = (Socket)ar.AsyncState;
            Socket clientSocket = client.EndAccept(ar);
            AcceptConnection(clientSocket);
            AsyncCallback aCallback = new AsyncCallback(AcceptTcpClientCallback);
            server.Server.BeginAccept(aCallback, server.Server);
        }
        

        // ловим подключения
        private void AcceptConnection(Socket newClient)
        {
            ConnectionInfo connection = new ConnectionInfo();
            connection.clientSock = newClient;
            connection.name = "New user";

            form.Invoke(new Action(() => form.appendSockLogBox("\nNew connection!\n")));

            lock (connection) connections.Add(connection);
            connection.clientSock.BeginReceive(connection.buffer, 0, connection.buffer.Length, 0, new AsyncCallback(RecieveCallback), connection.clientSock);

        }

        private void RecieveCallback(IAsyncResult ar)
        {
            Socket handler = (Socket)ar.AsyncState;
            ConnectionInfo clientInfo = GetConnectionBySock(handler);
            if (clientInfo == null)
                throw new IndexOutOfRangeException("Dead man's letter");

            int bytesRead = handler.EndReceive(ar);

            if(bytesRead > 0)
            {
                clientInfo.command += Encoding.UTF8.GetString(clientInfo.buffer, 0, bytesRead);
                ProcessCommand(clientInfo);
                clientInfo.clientSock.BeginReceive(clientInfo.buffer, 0, clientInfo.buffer.Length, 0, new AsyncCallback(RecieveCallback), handler);
            }
        }

        private bool ProcessCommand(ConnectionInfo client)
        {
            int endPos = client.command.IndexOf(Environment.NewLine);
            if (endPos > -1)
            {
                string cmd = client.command.Substring(0, endPos);
                client.command.Remove(0, endPos);

                form.Invoke(new Action(() => form.appendSockLogBox("\nCommand : " + cmd)));

                //SendToAll(cmd + Environment.NewLine);

                return true;
            }
            return false;
        }

        //  Поиск соединения по сокету
        private ConnectionInfo GetConnectionBySock(Socket sock)
        {
            lock (connections) foreach (ConnectionInfo ci in connections)
                {
                    if (ci.clientSock == sock) return ci;
                }
            return null;
        }

        private ConnectionInfo getConnectionByID(int userID, int selfID)
        {
            lock (connections) foreach (ConnectionInfo ci in connections)
                {
                    if (ci.userID == userID && ci.selfID == selfID) return ci;
                }
            return null;
        }

        private List<ConnectionInfo> getConnectionsByUserID(int id)
        {
            List<ConnectionInfo> curUserConnections = new List<ConnectionInfo>();

            lock (connections) foreach (ConnectionInfo ci in connections)
                {
                    if (ci.userID == id) curUserConnections.Add(ci);
                }
            return curUserConnections;
        }

        public void SendToAll(string msg)
        {
            //  Строчку в байты

            byte[] byteData = Encoding.UTF8.GetBytes(msg);

            lock (connections) foreach (ConnectionInfo client in connections)
                    client.clientSock.Send(byteData, 0, byteData.Length, 0);
        }

        public SocketServer getServerHandle()
        {
            return this;
        }

        public void setWebSockServer(WebSockServer webSockServ)
        {
            webSockServer = webSockServ;
        }

        public JObject getClientsByUserID(int id)
        {
            List<ConnectionInfo> curUserConnections = getConnectionsByUserID(id);
            List<JObject> clients = new List<JObject>();

            //foreach (ConnectionInfo ci in curUserConnections)
            //{
            //    dynamic client = new JObject();
            //    client.id = ci.selfID;
            //    client.name = ci.name;
            //    client.deviceList = ci.deviceList;

            //    clients.Add(client);
            //}
            Device device1 = new Device();
            device1.id = 11;
            device1.name = "robot";

            Device device2 = new Device();
            device2.id = 22;
            device2.name = "lamp";

            List<Device> devList = new List<Device>();
            devList.Add(device1);
            devList.Add(device2);

            dynamic client1 = new JObject();
            client1.id = 1;
            client1.name = "Sergey";
            client1.deviceList = JToken.FromObject(devList);

            dynamic client2 = new JObject();
            client2.id = 2;
            client2.name = "Yura";
            client2.deviceList = JToken.FromObject(devList);

            clients.Add(client1);
            clients.Add(client2);

            dynamic list = new JObject();
            list.clients = JToken.FromObject(clients);

            return list;
        }

        public void sendToID(int userID, int remoteID, String message)
        {

            ConnectionInfo ci = getConnectionByID(userID, remoteID);

            byte[] byteData = Encoding.UTF8.GetBytes(message);

            ci.clientSock.Send(byteData, 0, byteData.Length, 0);
        }


    }
}
