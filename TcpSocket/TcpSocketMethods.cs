namespace TcpSocket
{
    using System;
    using System.Diagnostics;
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

            if (this.Connected == false)
            {
                Trace.WriteLine("Failed to send a message, the TCP socket was closed!", nameof(TrySendBufferAsync));
                return false;
            }

            // 
            // Can we still add items to the queue ?
            // 

            if (this.SendQueue.Completion.IsCompleted)
            {
                Trace.WriteLine("Failed to add a message to the queue, the queue was completed!", nameof(TrySendBufferAsync));
                return false;
            }

            // 
            // Setup a TcpMessage structure and an event for this message.
            // 

            var MessageToQueue = new TcpMessage(Buffer);

            // 
            // Try to add this message to the queue.
            // 

            var WasMessageAddedToQueue = await this.SendQueue.SendAsync(MessageToQueue);

            if (WasMessageAddedToQueue == false)
            {
                Trace.WriteLine("Failed to add a message to the queue!", nameof(TrySendBufferAsync));
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

                if (this.Connected == false)
                {
                    Trace.WriteLine("The TCP socket disconnected while we were waiting for completion!", nameof(TrySendBufferAsync));
                    break;
                }
            }

            // 
            // Return whether this buffer was sent or not.
            // 

            MessageToQueue.CompletionEvent.Dispose();
            return MessageToQueue.WasMessageSent;
        }
    }
}
