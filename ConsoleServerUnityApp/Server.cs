using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using SendInfoOLD;

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
            Out.WriteLine($"| INFO |:\n Server created.");
        }

        public void Work()
        {
            Out.WriteLine($"| INFO |:\n Server - worked.");
            try
            {
                Listener.BeginAcceptTcpClient(AcceptCallback, Listener);
            }
            catch (Exception e)
            {
                Out.WriteLine($"| ERROR (WORK) |:\n {e.Message}");
            }
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            TcpListener _listener = (TcpListener)ar.AsyncState; //передается объект аргументом.
            try
            {
                
                ClientInfo _tcpClient = new ClientInfo();
                _tcpClient.Client = _listener.EndAcceptTcpClient(ar);

                newClients.Add(_tcpClient);

                Out.WriteLine($"| INFO |:\n Connected client - {((IPEndPoint)_tcpClient.Client.Client.RemoteEndPoint).Address}");

                NetworkStream networkStream = _tcpClient.Client.GetStream();
                _tcpClient.buffer = new byte[Global.LENGTHHEADER];
                networkStream.BeginRead(_tcpClient.buffer, 0, _tcpClient.buffer.Length, new AsyncCallback(ReadCallback), _tcpClient);

                _listener.BeginAcceptTcpClient(AcceptCallback, _listener);

                if (newClients.Count > 0)
                {
                    clients.AddRange(newClients);
                    newClients.Clear();
                }
            }
            catch (Exception e)
            {
                Out.WriteLine($"| ERROR (ACCEPT) |:\n {e.Message}");
            }
        }

        public void ReadCallback(IAsyncResult ar)
        {
            ClientInfo _nowTcpClient = (ClientInfo)ar.AsyncState;
            try
            {
                NetworkStream networkStream = _nowTcpClient.Client.GetStream();
                int read = networkStream.EndRead(ar);

                if (read > 0)
                {
                    string header = Encoding.Default.GetString(_nowTcpClient.buffer);
                    int lengthInfo = int.Parse(header);

                    MemoryStream memoryStream = new MemoryStream(lengthInfo);
                    byte[] temp = new byte[lengthInfo];
                    read = networkStream.Read(temp, 0, temp.Length);
                    memoryStream.Write(temp, 0, read);
                    BinaryFormatter binaryFormatter = new BinaryFormatter();
                    memoryStream.Position = 0;
                    Info sendInfo = (Info)binaryFormatter.Deserialize(memoryStream);
                    memoryStream.Close();

                    Out.WriteLine($"| MESSAGE |:\n {sendInfo.Message}");

                    if (sendInfo.FileSize > 0)
                    {
                        Out.WriteLine($"| FILE |:\n{sendInfo.FileName} - {sendInfo.FileSize}");
                        FileStream fileStream = new FileStream(sendInfo.FileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, sendInfo.FileSize);

                        do
                        {
                            temp = new byte[Global.MAXBUFFER];
                            read = networkStream.Read(temp, 0, temp.Length);

                            fileStream.Write(temp, 0, read);

                            if (fileStream.Length == sendInfo.FileSize)
                            {
                                fileStream.Close();
                                fileStream = null;
                                break;
                            }
                        } while (read > 0);

                        temp = null;
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }

                    SendData(sendInfo.Message, sendInfo.FileName, _nowTcpClient);

                    //TODO: Туть должно быть оповещение события Receive =)

                    _nowTcpClient.buffer = new byte[Global.LENGTHHEADER];
                    networkStream.BeginRead(_nowTcpClient.buffer, 0, _nowTcpClient.buffer.Length, new AsyncCallback(ReadCallback), _nowTcpClient);
                }
                else
                {
                    DeleteClient(_nowTcpClient);

                    //TODO: Туть должно быть оповещение события Disconnected =)
                }
            }
            catch (Exception e)
            {
                DeleteClient(_nowTcpClient);

                Out.WriteLine($"| ERROR (READ) |:\n {e.Message}");

                //TODO: Туть должно быть оповещение события Disconnected =)
            }
        }

        public void SendData(string _message, string _sendFileName, ClientInfo clientSendler)
        {
            Info info = new Info();
            info.Message = _message;

            if (String.IsNullOrEmpty(info.Message) == true && String.IsNullOrEmpty(_sendFileName) == true) return;

            if (_sendFileName != null)
            {
                FileInfo _fileInfo = new FileInfo(_sendFileName);
                if (_fileInfo.Exists)
                {
                    info.FileSize = (int)_fileInfo.Length;
                    info.FileName = _fileInfo.Name;
                }
                _fileInfo = null;
            }

            BinaryFormatter binaryFormatter = new BinaryFormatter();
            MemoryStream memoryStream = new MemoryStream();
            binaryFormatter.Serialize(memoryStream, info);
            memoryStream.Position = 0;
            byte[] infoBuffer = new byte[memoryStream.Length];
            int read = memoryStream.Read(infoBuffer, 0, infoBuffer.Length);
            memoryStream.Close();

            byte[] header = GetHeader(infoBuffer.Length);
            byte[] total = new byte[header.Length + infoBuffer.Length + info.FileSize];

            Buffer.BlockCopy(header, 0, total, 0, header.Length);
            Buffer.BlockCopy(infoBuffer, 0, total, header.Length, infoBuffer.Length);

            if (info.FileSize > 0)
            {
                FileStream fileStream = new FileStream(_sendFileName, FileMode.Open, FileAccess.Read);
                fileStream.Read(total, header.Length + infoBuffer.Length, info.FileSize);
                fileStream.Close();
                fileStream = null;
            }

            foreach (ClientInfo client in clients)
            {
                if (client.Client != clientSendler.Client)
                {
                    client.Client.GetStream().Write(total, 0, total.Length);
                }
            }

            header = null;
            infoBuffer = null;
            total = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Out.WriteLine($"| INFO |:\n RESEND MESSAGE - {((IPEndPoint)clientSendler.Client.Client.RemoteEndPoint).Address}");
        }

        private byte[] GetHeader(int length)
        {
            string header = length.ToString();
            if (header.Length < 9)
            {
                string zeros = null;
                for (int i = 0; i < (9 - header.Length); i++)
                {
                    zeros += "0";
                }
                header = zeros + header;
            }

            byte[] byteheader = Encoding.Default.GetBytes(header);


            return byteheader;
        }

        private void DeleteClient(ClientInfo nowTcpClient)
        {
            if (nowTcpClient != null && nowTcpClient.Client.Connected == true)
            {
                nowTcpClient.Client.GetStream().Close();
                nowTcpClient.Client.Close();
            }
        }

        /*public void WorkOld()
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
        }*/

        ~Server()
        {
            if (Listener != null)
            {
                Listener.Stop();
                Listener = null;
            }
            foreach (ClientInfo client in clients)
            {
                client.Client.Close();
            }
        }
    }

    public class ClientInfo
    {
        public TcpClient Client = new TcpClient();
        public byte[] buffer = null;
        public bool IsConnect { get; set; }

        public ClientInfo ()
        {
            IsConnect = true;
            Client.ReceiveBufferSize = Global.MAXBUFFER;
        }
    }
}
