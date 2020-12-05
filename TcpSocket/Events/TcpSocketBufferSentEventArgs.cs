namespace TcpSocket.Events
{
    using System;
    using System.Net.Sockets;

    public class TcpSocketBufferSentEventArgs : EventArgs
    {
        /// <summary>
        /// The socket that sent this buffer.
        /// </summary>
        public Socket Source;

        /// <summary>
        /// The sent buffer.
        /// </summary>
        public byte[] Buffer;

        /// <summary>
        /// The number of bytes sent.
        /// </summary>
        public int NumberOfBytesWritten;

        /// <summary>
        /// The time at which the socket sent this buffer.
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpSocketBufferSentEventArgs"/> class.
        /// </summary>
        /// <param name="Source">The source.</param>
        /// <param name="Buffer">The buffer.</param>
        /// <param name="NumberOfBytesWritten">The number of bytes written.</param>
        /// <param name="Time">The time.</param>
        public TcpSocketBufferSentEventArgs(Socket Source, byte[] Buffer, int NumberOfBytesWritten, DateTime? Time = null)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source));
            }

            if (Buffer == null)
            {
                throw new ArgumentNullException(nameof(Buffer));
            }

            this.Source = Source;
            this.Buffer = Buffer;
            this.NumberOfBytesWritten = NumberOfBytesWritten;
            this.Time = Time.GetValueOrDefault(DateTime.UtcNow);
        }
    }
}