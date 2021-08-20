namespace TcpSocket
{
    using System;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    public partial class TcpSocket
    {
        /// <summary>
        /// Asynchronously try to send the given buffer to the server.
        /// </summary>
        /// <param name="InBuffer">The buffer.</param>
        /// <param name="InCancellationToken">The cancellation token.</param>
        /// <returns>Whether the buffer was sent or not.</returns>
        #if NET5_0
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
                this.Logger?.LogError($"The buffer is null or empty.");
                throw new ArgumentNullException(nameof(InBuffer), "The buffer is null or empty.");
            }

            this.Logger?.LogTrace($"The TcpSocket::TrySendBufferAsync(...) function has been executed. [Buffer: {InBuffer.Length}]");

            // 
            // Are we still connected to the server ?
            // 

            if (this.IsConnected == false)
            {
                this.Logger?.LogError($"Failed to send a message, the TCP socket was closed.");
                return false;
            }

            // 
            // Retrieve the network stream.
            // 
            
            var NetworkStream = (NetworkStream) null;

            try
            {
                NetworkStream = this.TcpClient.GetStream();
            }
            catch (Exception Exception)
            {
                this.Logger?.LogError(Exception, $"Failed to retrieve the network stream.");
                return false;
            }

            // 
            // If we have a timeout setup for write operations.
            // 

            var TimeoutSource = this.TcpClient.SendTimeout != 0 ? new CancellationTokenSource(TimeSpan.FromMilliseconds(this.TcpClient.SendTimeout)) : null;
            var Stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 
            // Send the buffer to the server.
            // 

            var WasBufferSent = false;

            try
            {
                await NetworkStream.WriteAsync(InBuffer, 0, InBuffer.Length, InCancellationToken == default ? TimeoutSource?.Token ?? default : InCancellationToken);
                WasBufferSent = true;
            }
            catch (Exception Exception)
            {
                this.Logger?.LogError(Exception, $"Failed to send a buffer ({InBuffer.Length} bytes) to the server.");
            }

            Stopwatch.Stop();

            // 
            // Return whether this buffer was sent or not.
            // 

            if (WasBufferSent)
                this.Logger?.LogDebug($"The buffer ({InBuffer.Length} bytes) has been sent in {Stopwatch.Elapsed.TotalSeconds:N2} second(s).");
            else
                this.Logger?.LogError($"The buffer ({InBuffer.Length} bytes) has not been sent and its completion has been aborted after {Stopwatch.Elapsed.TotalSeconds:N2} second(s).");

            return WasBufferSent;
        }
    }
}
