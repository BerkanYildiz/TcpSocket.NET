namespace TcpSocket
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

    using Microsoft.Extensions.Logging;

    public partial class TcpSocket
    {
        /// <summary>
        /// Asynchronously try to send the given buffer to the server.
        /// </summary>
        /// <param name="Buffer">The buffer.</param>
        /// <returns>Whether the buffer was sent or not.</returns>
        public async Task<bool> TrySendBufferAsync(byte[] Buffer)
        {
            // 
            // Verify the passed parameter(s).
            // 

            if (Buffer == null ||
                Buffer.Length == 0)
            {
                this.Logger?.Log(LogLevel.Error, $"The buffer is null or empty.");
                throw new ArgumentNullException(nameof(Buffer), "The buffer is null or empty.");
            }

            this.Logger?.Log(LogLevel.Debug, $"The TcpSocket::TrySendBufferAsync(...) function has been executed. [Buffer: {Buffer.Length}]");

            // 
            // Are we still connected to the server ?
            // 

            if (this.IsConnected == false)
            {
                this.Logger?.Log(LogLevel.Error, $"Failed to send a message, the TCP socket was closed.");
                return false;
            }

            // 
            // Can we still add items to the queue ?
            // 

            if (this.SendQueue.Completion.IsCompleted)
            {
                this.Logger?.Log(LogLevel.Error, $"Failed to add a message to the queue, the queue was completed.");
                return false;
            }

            // 
            // Setup a TcpMessage structure and an event for this message.
            // 

            var MessageToQueue = new TcpMessage(Buffer);

            // 
            // Try to add this message to the queue.
            // 

            var Stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var WasMessageAddedToQueue = await this.SendQueue.SendAsync(MessageToQueue);

            if (WasMessageAddedToQueue == false)
            {
                this.Logger?.Log(LogLevel.Error, $"Failed to add a message to the queue. [MessageToQueue: {MessageToQueue}]");
                Stopwatch.Stop();
                MessageToQueue.CompletionEvent.Dispose();
                return false;
            }

            // 
            // Wait for its completion.
            // 

            while (!MessageToQueue.CompletionEvent.Wait(TimeSpan.FromMilliseconds(250)))
            {
                // 
                // Are we still connected to the server ?
                // 

                if (this.IsConnected == false)
                {
                    this.Logger?.Log(LogLevel.Warning, $"The TCP socket disconnected while we were waiting for completion. [MessageToQueue: {MessageToQueue}]");
                    break;
                }
            }

            Stopwatch.Stop();

            if (MessageToQueue.WasMessageSent)
                this.Logger?.Log(LogLevel.Information, $"The TCP message has been sent in {Stopwatch.Elapsed.TotalSeconds:N2} second(s). [MessageToQueue: {MessageToQueue}]");
            else
                this.Logger?.Log(LogLevel.Error, $"The TCP message has not been sent and its completion has been aborted after {Stopwatch.Elapsed.TotalSeconds:N2} second(s). [MessageToQueue: {MessageToQueue}]");

            // 
            // Return whether this buffer was sent or not.
            // 

            MessageToQueue.CompletionEvent.Dispose();
            return MessageToQueue.WasMessageSent;
        }
    }
}
