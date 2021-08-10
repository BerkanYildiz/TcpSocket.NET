namespace TcpSocket
{
    using System;
    using System.IO;
    using System.Net.Sockets;
    using System.Threading;

    using Microsoft.Extensions.Logging;

    using global::TcpSocket.Events;

    public partial class TcpSocket
    {
        /// <summary>
        /// Gets or sets the thread that receives data from the server.
        /// </summary>
        private Thread ReceiveThread
        {
            get;
            set;
        }

        /// <summary>
        /// The entry point of the thread that receives data from the server.
        /// </summary>
        private void ReceiveThreadRoutine()
        {
            var ThisThreadName = Thread.CurrentThread.Name;
            this.Logger?.LogTrace($"The start routine of the '{ThisThreadName}' thread is executing.");

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

                var NetworkStream = (NetworkStream) null;

                try
                {
                    NetworkStream = this.TcpClient.GetStream();
                }
                catch (InvalidOperationException Exception)
                {
                    // 
                    // We got disconnected during a previous operation.
                    // 

                    this.Logger?.LogError(Exception, "Failed to retrieve the network stream.");
                    break;
                }

                // 
                // Wait for data to read.
                // 

                #if SYNCHRONOUS_WAIT_FOR_DATA
                while (NetworkStream.DataAvailable == false && this.IsConnected)
                {
                    Debug.WriteLine($"[*] DataAvailable: {NetworkStream.DataAvailable} / Connected: {this.IsConnected}");
                    Thread.Sleep(1);
                }

                // 
                // Are we still connected to the server after waiting for data ?
                // 

                if (this.IsConnected == false)
                {
                    break;
                }
                #endif

                // 
                // Receive data from the server.
                // 

                var NumberOfBytesRead = 0;

                try
                {
                    NumberOfBytesRead = NetworkStream.Read(ReadBuffer);
                }
                catch (IOException Exception)
                {
                    // 
                    // The connection was forcefully closed while we were waiting for data,
                    // or the socket was configured to throw an exception after a certain period of time (timeout).
                    // 

                    this.Logger?.LogError(Exception, "Failed to read data from the network stream.");
                    break;
                }

                // 
                // If there is nothing to read, the server disconnected us or we timed out.
                // 

                if (NumberOfBytesRead == 0)
                {
                    this.Logger?.LogDebug($"TcpSocket->NumberOfBytesRead: {NumberOfBytesRead}.");
                    break;
                }

                // 
                // We've got data from the server, invoke the handlers
                // subscribed to the socket received data event.
                // 

                if (this.OnBufferReceived != null)
                {
                    try
                    {
                        this.OnBufferReceived.Invoke(this, new TcpSocketBufferReceivedEventArgs(this.TcpClient.Client, ReadBuffer, NumberOfBytesRead));
                    }
                    catch (Exception)
                    {
                        // ...
                    }
                }
            }

            // 
            // Debug.
            // 

            this.IsDisconnecting = true;
            this.Logger?.LogTrace($"The '{ThisThreadName}' thread is terminating.");
        }
    }
}
