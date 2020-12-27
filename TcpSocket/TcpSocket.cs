namespace TcpSocket
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

    using global::TcpSocket.Events;

    public partial class TcpSocket : IDisposable
    {
        /// <summary>
        /// Gets the TCP client.
        /// </summary>
        private TcpClient TcpClient
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="TcpSocket"/>
        /// was connected to the server during the last operation.
        /// </summary>
        public bool Connected
        {
            get
            {
                if (this.IsDisconnecting)
                {
                    return false;
                }

                return this.TcpClient?.Connected ?? false;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is currently disconnecting.
        /// </summary>
        public bool IsDisconnecting
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a value indicating whether this instance has been disposed.
        /// </summary>
        public bool IsDisposed
        {
            get;
            private set;
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
            this.TcpClient.NoDelay = false;
            this.TcpClient.SendBufferSize = 4096;
            this.TcpClient.ReceiveBufferSize = 8192;
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

                // 
                // We've connected to the server, invoke the handlers
                // subscribed to the socket connected event.
                // 

                if (this.OnSocketConnected != null)
                {
                    try
                    {
                        this.OnSocketConnected.Invoke(this, new TcpSocketConnectedEventArgs(this.TcpClient.Client, this.TcpClient.Client.RemoteEndPoint));
                    }
                    catch (Exception)
                    {
                        // ...
                    }
                }
            }

            // 
            // Return whether we are connected to the server.
            // 

            return this.TcpClient.Connected;
        }

        /// <summary>
        /// Performs application-defined tasks associated with
        /// freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // 
            // Was this instance already disposed ?
            // 

            if (this.IsDisposed)
            {
                return;
            }

            this.IsDisposed = true;

            // 
            // Mark this TcpSocket as terminating.
            // 

            this.IsDisconnecting = true;

            // 
            // Disconnect the TcpClient.
            // 

            if (this.Connected || this.TcpClient?.Client?.RemoteEndPoint != null)
            {
                try
                {
                    this.TcpClient?.Client?.Disconnect(false);
                }
                catch (Exception)
                {
                    // 
                    // An internal error occurred.
                    // The developer most likely never initiated a connection in the first place.
                    // 
                }
            }

            // 
            // Mark the queue as completed.
            // 

            this.SendQueue?.Complete();

            // 
            // If one or both threads are running...
            // 

            if ((this.ReceiveThread?.IsAlive ?? false) || (this.SendThread?.IsAlive ?? false))
            {
                // 
                // Asynchronously terminate both receive/send threads.
                // 

                Task.Run(async () =>
                {
                    // 
                    // Wait for 2 second(s) in case the threads terminate themselves.
                    // 

                    var WaitDuration = TimeSpan.FromSeconds(2);
                    var WaitEndTime = DateTime.UtcNow.Add(WaitDuration);

                    while (DateTime.UtcNow < WaitEndTime)
                    {
                        // 
                        // Are both threads stopped yet ?
                        // 

                        if ((this.ReceiveThread?.IsAlive ?? true) || (this.SendThread?.IsAlive ?? true))
                        {
                            return;
                        }

                        // 
                        // Well, we waiting then..
                        // 

                        await Task.Delay(50);
                    }

                    // 
                    // Terminate the receive thread.
                    // 

                    if (this.ReceiveThread?.IsAlive ?? false)
                    {
                        try
                        {
                            this.ReceiveThread.Abort();
                        }
                        catch (ThreadStateException)
                        {
                            // 
                            // The thread was already starting to die.
                            // 
                        }
                    }

                    // 
                    // Terminate the send thread.
                    // 

                    if (this.SendThread?.IsAlive ?? false)
                    {
                        try
                        {
                            this.SendThread.Abort();
                        }
                        catch (ThreadStateException)
                        {
                            // 
                            // The thread was already starting to die.
                            // 
                        }
                    }

                }).Wait();
            }

            // 
            // Dispose the TcpClient.
            // 

            this.TcpClient?.Dispose();
        }
    }
}
