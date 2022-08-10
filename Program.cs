using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;

namespace TrafficForwarder
{
    class Program
    {
        class Data
        {
            public StateObject<Data> RemoteState;
        }

        static SocketServer<Data> srv = new SocketServer<Data>();
        static SocketClient<Data> cli = new SocketClient<Data>();


        static IPAddress RemoteIp = IPAddress.Parse("52.128.23.153");
        static int RemotePort = 80;
        static int LocalPort = 8080;

		//Test

        /// <summary>
        /// Listen connection on port 8080 and forward them to 52.128.23.153:80. Just a demo program :-)
        /// </summary>
        static void Main(string[] args)
        {
            // Client connected, open connection to remote
            srv.ClientConnectedEvent += (sender, o) => 
            {
                Console.WriteLine("srv.ClientConnectedEvent:" + o.workSocket.RemoteEndPoint.ToString());
                var clientState= cli.Connect(RemoteIp, RemotePort);
                clientState.UserData.RemoteState = o;
                o.UserData.RemoteState = clientState;
            };

            // Data received from client, forward to remote.
            srv.DataReceivedEvent += (sender, o, data) =>
            {
                Console.WriteLine("srv.DataReceivedEvent:" + o.workSocket.RemoteEndPoint.ToString() + " " + data.Length + " bytes");

                Stopwatch sw = new Stopwatch();
                sw.Start();

                while (o.UserData.RemoteState == null || o.UserData.RemoteState.workSocket.Connected == false)
                {
                    System.Threading.Thread.Sleep(5);
                    if (sw.ElapsedMilliseconds > 10000)
                    {
                        srv.Close(o);
                    }
                }
                cli.Send(o.UserData.RemoteState, data);
            };

            // Client disconnected. Disconnect connection to remote
            srv.DisconnectedEvent += (sender, o) =>
            {
                Console.WriteLine("srv.DisconnectedEvent");
                cli.Close(o.UserData.RemoteState);
            };

            // Data received from remote forward to our client
            cli.DataReceivedEvent += (sender, o, data) =>
            {
                Console.WriteLine("cli.DataReceivedEvent:" + o.workSocket.RemoteEndPoint.ToString() + " " + data.Length + " bytes");
                srv.Send(o.UserData.RemoteState, data);
            };

            // Remote disconnected, disconnect out client.
            cli.DisconnectedEvent += (sender, o) =>
            {
                Console.WriteLine("cli.DisconnectedEvent");
                srv.Close(o.UserData.RemoteState);
            };

            // Start listening. Blocking call.
            srv.StartListening(IPAddress.Parse("127.0.0.1"), LocalPort);
        }

    }
}
