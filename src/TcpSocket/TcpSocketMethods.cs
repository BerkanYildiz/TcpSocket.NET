namespace TcpSocket
{
    using System;
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

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
            // Are we still connected to the server ?
            // 

            if (this.TcpClient.Connected == false)
            {
                return false;
            }

            // 
            // Can we still add items to the queue ?
            // 

            if (this.SendQueue.Completion.IsCompleted)
            {
                return false;
            }

            // 
            // Setup a TcpMessage structure and an event for this message.
            // 

            var MessageToQueue = new TcpMessage(Buffer);
            MessageToQueue.CompletionEvent = new ManualResetEventSlim(false);

            // 
            // Try to add this message to the queue.
            // 

            var WasMessageAddedToQueue = await this.SendQueue.SendAsync(MessageToQueue);

            if (WasMessageAddedToQueue == false)
            {
                MessageToQueue.CompletionEvent.Dispose();
                return false;
            }

            // 
            // Wait for its completion.
            // 

            while (!MessageToQueue.CompletionEvent.Wait(TimeSpan.FromMilliseconds(500)))
            {
                // 
                // Are we still connected to the server ?
                // 

                if (this.TcpClient.Connected == false)
                {
                    break;
                }
            }

            // 
            // Return whether this buffer was sent or not.
            // 

            var IsCompleted = MessageToQueue.CompletionEvent.IsSet;
            MessageToQueue.CompletionEvent.Dispose();
            return IsCompleted;
        }
    }
}
