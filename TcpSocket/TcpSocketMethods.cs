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
        /// <param name="Buffer">The buffer.</param>
        /// <param name="CancellationToken">The cancellation token.</param>
        /// <returns>Whether the buffer was sent or not.</returns>
        public async ValueTask<bool> TrySendBufferAsync(byte[] Buffer, CancellationToken CancellationToken = default)
        {
            // 
            // Verify the passed parameter(s).
            // 

            if (Buffer == null ||
                Buffer.Length == 0)
            {
                this.Logger?.LogError($"The buffer is null or empty.");
                throw new ArgumentNullException(nameof(Buffer), "The buffer is null or empty.");
            }

            this.Logger?.LogTrace($"The TcpSocket::TrySendBufferAsync(...) function has been executed. [Buffer: {Buffer.Length}]");

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
                await NetworkStream.WriteAsync(Buffer, CancellationToken == default ? TimeoutSource?.Token ?? default : CancellationToken);
                WasBufferSent = true;
            }
            catch (Exception Exception)
            {
                Logger?.LogError(Exception, "Failed to send a buffer to the server.");
            }

            Stopwatch.Stop();

            // 
            // Return whether this buffer was sent or not.
            // 

            if (WasBufferSent)
                this.Logger?.LogDebug($"The buffer has been sent in {Stopwatch.Elapsed.TotalSeconds:N2} second(s).");
            else
                this.Logger?.LogError($"The buffer has not been sent and its completion has been aborted after {Stopwatch.Elapsed.TotalSeconds:N2} second(s).");

            return WasBufferSent;
        }
    }
}
