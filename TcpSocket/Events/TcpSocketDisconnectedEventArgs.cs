namespace TcpSocket.Events
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    public class TcpSocketDisconnectedEventArgs : EventArgs
    {
        /// <summary>
        /// The socket that connected to the endpoint.
        /// </summary>
        public Socket Source;

        /// <summary>
        /// The endpoint the socket is supposedly connected to.
        /// </summary>
        public EndPoint EndPoint;

        /// <summary>
        /// The time at which the socket connected to the endpoint.
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpSocketDisconnectedEventArgs"/> class.
        /// </summary>
        /// <param name="Source">The source.</param>
        /// <param name="EndPoint">The endpoint.</param>
        /// <param name="Time">The time.</param>
        public TcpSocketDisconnectedEventArgs(Socket Source, EndPoint EndPoint, DateTime? Time = null)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source));
            }

            if (EndPoint == null)
            {
                throw new ArgumentNullException(nameof(EndPoint));
            }

            this.Source = Source;
            this.EndPoint = EndPoint;
            this.Time = Time.GetValueOrDefault(DateTime.UtcNow);
        }
    }
}