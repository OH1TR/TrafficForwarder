using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace TrafficForwarder
{
    public class StateObject<T> where T : new()
    {
        public const int BufferSize = 8192;

        public byte[] buffer = new byte[BufferSize];

        public StringBuilder sb = new StringBuilder();

        public Socket workSocket = null;

        public FastFifo ReceiveFifo = null;

        public T UserData = default(T);
    }
}
