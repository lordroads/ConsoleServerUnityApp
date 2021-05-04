using System;

namespace ConsoleServerUnityApp
{
    class Program
    {
        [Obsolete]
        static void Main(string[] args)
        {
                string host = System.Net.Dns.GetHostName();
                System.Net.IPAddress[] ips = System.Net.Dns.GetHostByName(host).AddressList;
                foreach (var item in ips)
                {
                    Console.WriteLine($"IP adress {item}");
                }
                

                Server server = new Server(Port: 90, Console.Out);
                server.Work();

            Console.ReadLine();
        }
    }
}
