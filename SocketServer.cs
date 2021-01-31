using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TrafficForwarder
{
    class SocketServer<T> where T : new()
    {

        public ManualResetEvent allDone = new ManualResetEvent(false);

        public delegate void ClientConnectedHandler(object sender, StateObject<T> o);
        public event ClientConnectedHandler ClientConnectedEvent;

        public delegate void DisconnectedHandler(object sender, StateObject<T> o);
        public event DisconnectedHandler DisconnectedEvent;

        public delegate void DataReceivedHandler(object sender, StateObject<T> o, byte[] data);
        public event DataReceivedHandler DataReceivedEvent;

        public delegate void SendCompleteHandler(object sender, StateObject<T> o, int bytesSent);
        public event SendCompleteHandler SendCompleteEvent;

        public bool UseReceiveFifo = false;

        public SocketServer()
        {
        }

        public void StartListening(IPAddress ipAddress, int port)
        {
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

            Socket listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (true)
                {
                    allDone.Reset();
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            allDone.Set();

            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            StateObject<T> state = new StateObject<T>();
            state.UserData = new T();
            state.workSocket = handler;
            if (UseReceiveFifo)
                state.ReceiveFifo = new FastFifo();

            if (ClientConnectedEvent != null)
                ClientConnectedEvent(this, state);

            handler.BeginReceive(state.buffer, 0, StateObject<T>.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        public void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            StateObject<T> state = (StateObject<T>)ar.AsyncState;
            Socket handler = state.workSocket;

            int bytesRead = 0;

            try
            {
                bytesRead = handler.EndReceive(ar);
            }
            catch (Exception)
            {
                if (DisconnectedEvent != null)
                    DisconnectedEvent(this, state);

                return;
            }

            if (bytesRead > 0)
            {
                var bytes = new byte[bytesRead];
                Array.Copy(state.buffer, bytes, bytesRead);

                if (UseReceiveFifo)
                    state.ReceiveFifo.Push(bytes);

                if (DataReceivedEvent != null)
                    DataReceivedEvent(this, state, bytes);
            }
            else
            {
                Close(state);
                return;
            }
            handler.BeginReceive(state.buffer, 0, StateObject<T>.BufferSize, 0,
            new AsyncCallback(ReadCallback), state);
        }

        public void Send(StateObject<T> state, byte[] data)
        {
            if (!state.workSocket.Connected)
                return;

            state.workSocket.BeginSend(data, 0, data.Length, 0,
                new AsyncCallback(SendCallback), state);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                StateObject<T> state = (StateObject<T>)ar.AsyncState;
                int bytesSent = state.workSocket.EndSend(ar);
                if (SendCompleteEvent != null)
                    SendCompleteEvent(this, state, bytesSent);

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void Close(StateObject<T> state)
        {
            if (!state.workSocket.Connected)
                return;

            state.workSocket.Shutdown(SocketShutdown.Both);
            state.workSocket.Close();

            if (DisconnectedEvent != null)
                DisconnectedEvent(this, state);
        }
    }
}
