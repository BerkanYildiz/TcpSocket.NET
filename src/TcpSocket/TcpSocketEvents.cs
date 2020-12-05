namespace TcpSocket
{
    using System;
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using global::TcpSocket.Events;

    public partial class TcpSocket
    {
        /// <summary>
        /// Gets or sets the event handler invoked when the socket has connected to its endpoint.
        /// </summary>
        public EventHandler<TcpSocketConnectedEventArgs> OnSocketConnected
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the event handler invoked when the socket has disconnected from its endpoint.
        /// </summary>
        public EventHandler<TcpSocketDisconnectedEventArgs> OnSocketDisconnected
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the event handler invoked when the socket has received data.
        /// </summary>
        public EventHandler<TcpSocketBufferReceivedEventArgs> OnBufferReceived
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the event handler invoked when the socket has sent data.
        /// </summary>
        public EventHandler<TcpSocketBufferSentEventArgs> OnBufferSent
        {
            get;
            set;
        }
    }
}
