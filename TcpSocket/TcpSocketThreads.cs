namespace TcpSocket
{
    using System;
    using System.IO;
    using System.Net.Sockets;
    using System.Threading;

    using global::TcpSocket.Events;

    public partial class TcpSocket
    {
        /// <summary>
        /// Gets or sets the thread that receives data from the server.
        /// </summary>
        private Thread ReceiveThread { get; set; }

        /// <summary>
        /// The entry point of the thread that receives data from the server.
        /// </summary>
        private void ReceiveThreadRoutine()
        {
            var ThisThreadName = Thread.CurrentThread.Name;

            // 
            // Initialize our read-buffer.
            // 

            var ReadBuffer = new byte[this.TcpClient.ReceiveBufferSize];

            // 
            // While we are connected to the server...
            // 

            while (this.IsConnected)
            {
                // 
                // Retrieve the network stream.
                // 

                var NetworkStream = (Stream) null;

                try
                {
                    NetworkStream = this.SslNetworkStream != null ? (Stream) this.SslNetworkStream : (Stream) this.TcpClient.GetStream();
                }
                catch (InvalidOperationException)
                {
                    // 
                    // We got disconnected during a previous operation.
                    // 
                    
                    break;
                }

                // 
                // Receive data from the server.
                // 

                int NumberOfBytesRead;

                try
                {
                    NumberOfBytesRead = NetworkStream.Read(ReadBuffer, 0, ReadBuffer.Length);
                }
                catch (IOException)
                {
                    // 
                    // The connection was forcefully closed while we were waiting for data,
                    // or the socket was configured to throw an exception after a certain period of time (timeout).
                    // 
                    
                    break;
                }

                // 
                // If there is nothing to read, the server disconnected us or we timed out.
                // 

                if (NumberOfBytesRead == 0) { break; }

                // 
                // We've got data from the server, invoke the handlers
                // subscribed to the socket received data event.
                // 

                if (this.OnBufferReceived != null)
                {
                    try
                    {
                        this.OnBufferReceived.Invoke(this, new TcpSocketBufferReceivedEventArgs(this.TcpClient.Client, ReadBuffer, NumberOfBytesRead));
                    } catch (Exception) { }
                }
            }

            // 
            // Debug.
            // 

            this.IsDisconnecting = true;
        }
    }
}
