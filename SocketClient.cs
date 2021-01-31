using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TrafficForwarder
{
    class SocketClient<T> where T : new()
    {
        public delegate void ConnectedHandler(object sender, StateObject<T> o);
        public event ConnectedHandler ConnectedEvent;

        public delegate void DisconnectedHandler(object sender, StateObject<T> o);
        public event DisconnectedHandler DisconnectedEvent;

        public delegate void DataReceivedHandler(object sender, StateObject<T> o, byte[] data);
        public event DataReceivedHandler DataReceivedEvent;

        public delegate void SendCompleteHandler(object sender, StateObject<T> o, int bytesSent);
        public event SendCompleteHandler SendCompleteEvent;

        public bool UseReceiveFifo = false;

        public StateObject<T> Connect(IPAddress ip, int port)
        {
            IPEndPoint remoteEP = new IPEndPoint(ip, port);

            Socket client = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            StateObject<T> state = new StateObject<T>();
            state.UserData = new T();
            state.workSocket = client;
            if (UseReceiveFifo)
                state.ReceiveFifo = new FastFifo();

            client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), state);

            return state;
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                StateObject<T> state = (StateObject<T>)ar.AsyncState;

                Socket client = state.workSocket;

                client.EndConnect(ar);

                if (ConnectedEvent != null)
                    ConnectedEvent(this, state);

                state.workSocket.BeginReceive(state.buffer, 0, StateObject<T>.BufferSize, 0, new AsyncCallback(ReadCallback), state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }


        public void ReadCallback(IAsyncResult ar)
        {
            string content = string.Empty;

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
