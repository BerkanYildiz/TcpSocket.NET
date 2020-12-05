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
        /// Gets the TCP client.
        /// </summary>
        private TcpClient TcpClient
        {
            get;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpSocket"/> class.
        /// </summary>
        public TcpSocket()
        {
            // 
            // Initialize the TCP client.
            // 

            this.TcpClient = new TcpClient(AddressFamily.InterNetwork);
            this.TcpClient.NoDelay = true;
            this.TcpClient.SendBufferSize = 2048;
            this.TcpClient.ReceiveBufferSize = 2048;
            this.TcpClient.ReceiveTimeout = 0;
            this.TcpClient.SendTimeout = 0;
            this.TcpClient.ExclusiveAddressUse = false;

            // 
            // Initialize the sent messages queue.
            // 

            this.SendQueue = new BufferBlock<TcpMessage>(new DataflowBlockOptions
            {
                BoundedCapacity = 100,
                EnsureOrdered = true
            });
        }

        /// <summary>
        /// Asynchronously try to connect to the given remote endpoint.
        /// </summary>
        /// <param name="Hostname">The hostname.</param>
        /// <param name="Port">The port.</param>
        /// <returns>whether we successfully connected or not.</returns>
        public async Task<bool> TryConnectAsync(string Hostname, int Port)
        {
            // 
            // Verify the passed parameters.
            // 
            
            if (string.IsNullOrEmpty(Hostname))
            {
                throw new ArgumentNullException(nameof(Hostname), "The hostname must not be null or empty.");
            }

            if (Port <= IPEndPoint.MinPort || Port > IPEndPoint.MaxPort)
            {
                throw new ArgumentException("The port number must be between 0 and 65535.", nameof(Port));
            }

            // 
            // Are we already connected to a remote endpoint ?
            // 

            if (this.TcpClient.Connected)
            {
                throw new InvalidOperationException("The TcpSocket is already connected to a remote endpoint.");
            }

            // 
            // Asynchronously attempt to connect to the server.
            // 

            try
            {
                await this.TcpClient.ConnectAsync(Hostname, Port);
            }
            catch (SocketException)
            {
                // 
                // The client has failed to connect to the server.
                // 
            }

            // 
            // If we are connected to the server, start the receive/send threads.
            // 

            if (this.TcpClient.Connected)
            {
                // 
                // Setup the thread that receives data from the server.
                // 

                this.ReceiveThread = new Thread(this.ReceiveThreadRoutine);
                this.ReceiveThread.Name = "NetworkThread-Receive";
                this.ReceiveThread.Priority = ThreadPriority.AboveNormal;
                this.ReceiveThread.IsBackground = true;

                // 
                // Setup the thread that sends data to the server.
                // 

                this.SendThread = new Thread(this.SendThreadRoutine);
                this.SendThread.Name = "NetworkThread-Send";
                this.SendThread.Priority = ThreadPriority.AboveNormal;
                this.SendThread.IsBackground = true;

                // 
                // Start both threads.
                // 

                this.ReceiveThread.Start();
                this.SendThread.Start();
            }

            // 
            // Return whether we are connected to the server.
            // 

            return this.TcpClient.Connected;
        }
    }
}
