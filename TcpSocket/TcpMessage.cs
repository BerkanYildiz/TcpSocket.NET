namespace TcpSocket
{
    using System;

    public class TcpMessage
    {
        /// <summary>
        /// Gets the data to send to the server.
        /// </summary>
        public byte[] Buffer { get; }

        /// <summary>
        /// Gets the time at which this operation has been requested.
        /// </summary>
        public DateTime Time { get; }

        /// <summary>
        /// Gets a value indicating whether this message was sent to the server.
        /// </summary>
        public bool WasMessageSent { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpMessage"/> class.
        /// </summary>
        /// <param name="InBuffer">The buffer.</param>
        public TcpMessage(byte[] InBuffer)
        {
            this.Buffer = InBuffer;
            this.Time = DateTime.UtcNow;
            this.WasMessageSent = false;
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"[Buffer: {Buffer?.Length ?? 0}, Time: {Time:G}, WasMessageSent: {WasMessageSent}]";
        }
    }
}
