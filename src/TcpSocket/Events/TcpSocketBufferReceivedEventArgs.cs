namespace TcpSocket.Events
{
    using System;
    using System.Net.Sockets;

    public class TcpSocketBufferReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// The socket that received this buffer.
        /// </summary>
        public Socket Source;

        /// <summary>
        /// The received buffer.
        /// </summary>
        public byte[] Buffer;

        /// <summary>
        /// The number of bytes read.
        /// </summary>
        public int NumberOfBytesRead;

        /// <summary>
        /// The time at which the socket received this buffer.
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpSocketBufferReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="Source">The source.</param>
        /// <param name="Buffer">The buffer.</param>
        /// <param name="NumberOfBytesRead">The number of bytes read.</param>
        /// <param name="Time">The time.</param>
        public TcpSocketBufferReceivedEventArgs(Socket Source, byte[] Buffer, int NumberOfBytesRead, DateTime? Time = null)
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
            this.NumberOfBytesRead = NumberOfBytesRead;
            this.Time = Time.GetValueOrDefault(DateTime.UtcNow);
        }
    }
}