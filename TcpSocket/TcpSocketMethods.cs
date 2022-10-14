namespace TcpSocket
{
    using System;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    public partial class TcpSocket
    {
        /// <summary>
        /// Asynchronously try to send the given buffer to the server.
        /// </summary>
        /// <param name="InBuffer">The buffer.</param>
        /// <param name="InCancellationToken">The cancellation token.</param>
        /// <returns>Whether the buffer was sent or not.</returns>
        #if NET5_0 || NET6_0
        public async ValueTask<bool> TrySendBufferAsync(byte[] InBuffer, CancellationToken InCancellationToken = default)
        #else
        public async Task<bool> TrySendBufferAsync(byte[] InBuffer, CancellationToken InCancellationToken = default)
        #endif
        {
            // 
            // Verify the passed parameter(s).
            // 

            if (InBuffer == null ||
                InBuffer.Length == 0)
            {
                throw new ArgumentNullException(nameof(InBuffer), "The buffer is null or empty.");
            }

            // 
            // Are we still connected to the server ?
            // 

            if (this.IsConnected == false)
                return false;

            // 
            // Retrieve the network stream.
            // 
            
            var NetworkStream = (NetworkStream) null;
            try { NetworkStream = this.TcpClient.GetStream(); } catch (Exception) { return false; }

            // 
            // If we have a timeout setup for write operations.
            // 

            var TimeoutSource = this.TcpClient.SendTimeout != 0 ? new CancellationTokenSource(TimeSpan.FromMilliseconds(this.TcpClient.SendTimeout)) : null;

            // 
            // Send the buffer to the server.
            // 

            var WasBufferSent = false;

            try
            {
                await NetworkStream.WriteAsync(InBuffer, 0, InBuffer.Length, InCancellationToken == default ? TimeoutSource?.Token ?? default : InCancellationToken);
                WasBufferSent = true;
            }
            catch (Exception) { }

            // 
            // Return whether this buffer was sent or not.
            // 
            
            return WasBufferSent;
        }
    }
}
