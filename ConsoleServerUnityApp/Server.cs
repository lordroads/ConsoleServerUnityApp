using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System;

namespace ConsoleServerUnityApp
{
    public class Server
    {
        public TcpListener Listener;
        public List<ClientInfo> clients = new List<ClientInfo>();
        public List<ClientInfo> newClients = new List<ClientInfo>();
        public static Server server;
        static System.IO.TextWriter Out;

        public Server (int Port, System.IO.TextWriter _Out)
        {
            Out = _Out;
            Server.server = this;

            Listener = new TcpListener(IPAddress.Any, Port);
            Listener.Start();
        }

        public void Work()
        {
            try
            {
                Listener.BeginAcceptTcpClient(AcceptCallback, Listener);
            }
            catch (Exception e)
            {
                Out.WriteLine($"| ERROR |:\n {e.Message}");
            }
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            TcpListener _listener = (TcpListener)ar.AsyncState; //передается объект аргументом.
            try
            {
                
            }
            catch (Exception e)
            {
                Out.WriteLine($"| ERROR |:\n {e.Message}");
            }
        }

        public void WorkOld()
        {
            Thread clientListener = new Thread(ListenerClients);
            clientListener.Start();

            while (true)    
            {
                foreach (ClientInfo client in clients)
                {
                    if (client.IsConnect)
                    {
                        NetworkStream stream = client.Client.GetStream();
                        while (stream.DataAvailable)
                        {
                            int ReadByte = stream.ReadByte();
                            if (ReadByte != -1)
                            {
                                client.buffer.Add((byte)ReadByte);
                            }
                        }
                        if (client.buffer.Count > 0)
                        {
                            Out.WriteLine("Resend");
                            foreach (ClientInfo otherClient in clients)
                            {
                                byte[] msg = client.buffer.ToArray();
                                client.buffer.Clear();
                                foreach (ClientInfo _otherClient in clients)
                                {
                                    if (_otherClient != client)
                                    {
                                        try
                                        {
                                            _otherClient.Client.GetStream().Write(msg, 0, msg.Length);
                                        }
                                        catch
                                        {
                                            _otherClient.IsConnect = false;
                                            _otherClient.Client.Close();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                clients.RemoveAll(delegate (ClientInfo CI)
                {
                    if (!CI.IsConnect)
                    {
                        Server.Out.WriteLine("Client DISCONECT");
                        return true;
                    }
                    return false;
                });

                if (newClients.Count > 0)
                {
                    clients.AddRange(newClients);
                    newClients.Clear();
                }
            }
        }

        ~Server()
        {
            if (Listener != null)
            {
                Listener.Stop();
            }
            foreach (ClientInfo client in clients)
            {
                client.Client.Close();
            }
        }
        //Ожидание новых клиентов и добавление их в массив
        private void ListenerClients()
        {
            while (true)
            {
                server.newClients.Add(new ClientInfo(server.Listener.AcceptTcpClient()));
                Out.WriteLine("New Client");
            }
        }
    }

    public class ClientInfo
    {
        public TcpClient Client { get; set; }
        public byte[] buffer = null;
        //public List<byte> buffer = new List<byte>();
        public bool IsConnect { get; set; }

        public ClientInfo (TcpClient tcpClient)
        {
            Client = tcpClient;
            IsConnect = true;
            Client.ReceiveBufferSize = Global.MAXBUFFER;
        }
    }
}
