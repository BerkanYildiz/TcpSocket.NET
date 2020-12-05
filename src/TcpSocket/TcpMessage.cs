namespace TcpSocket
{
    using System;
    using System.Threading;

    public class TcpMessage
    {
        /// <summary>
        /// Gets the data to send to the server.
        /// </summary>
        public byte[] Buffer
        {
            get;
        }

        /// <summary>
        /// Gets the time at which this operation has been requested.
        /// </summary>
        public DateTime Time
        {
            get;
        }

        /// <summary>
        /// Gets or sets the event raised when this message will be completely sent to the server.
        /// </summary>
        public ManualResetEventSlim CompletionEvent
        {
            get;
            set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpMessage"/> class.
        /// </summary>
        /// <param name="Buffer">The buffer.</param>
        public TcpMessage(byte[] Buffer)
        {
            this.Buffer = Buffer;
            this.Time = DateTime.UtcNow;
        }
    }
}
