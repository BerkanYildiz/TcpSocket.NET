namespace TcpSocket
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

    using Microsoft.Extensions.Logging;

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
        /// Gets the logger.
        /// </summary>
        private ILogger Logger
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="TcpSocket"/>
        /// was connected to the server during the last operation.
        /// </summary>
        public bool IsConnected
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
        /// Gets a value indicating whether this instance is currently disconnecting.
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
        /// <param name="SendBufferSize">The size of the buffer used to send data.</param>
        /// <param name="ReceiveBufferSize">The size of the buffer used to receive data</param>
        /// <param name="ReceiveTimeout">The time limit in milliseconds to receive a message before aborting.</param>
        /// <param name="SendTimeout">The time limit in milliseconds to send a message before aborting.</param>
        /// <param name="NoDelay">Whether to immediately send the network data or wait for the buffer to fill a bit.</param>
        /// <param name="Logger">The logging handler instance used to print debug messages and traces.</param>
        public TcpSocket(int SendBufferSize = 4096, int ReceiveBufferSize = 8192, int ReceiveTimeout = 0, int SendTimeout = 0, bool NoDelay = false, ILogger Logger = null)
        {
            // 
            // Initialize the TCP client.
            // 

            this.TcpClient = new TcpClient(AddressFamily.InterNetwork)
            {
                ReceiveBufferSize = ReceiveBufferSize,
                SendBufferSize = SendBufferSize,
                ReceiveTimeout = ReceiveTimeout,
                SendTimeout = SendTimeout,
                NoDelay = NoDelay,
            };

            // 
            // Initialize the sent messages queue.
            // 

            this.SendQueue = new BufferBlock<TcpMessage>(new DataflowBlockOptions
            {
                EnsureOrdered = true,
            });

            // 
            // Initialize the logging handler.
            // 

            this.Logger = Logger;
            this.Logger?.Log(LogLevel.Debug, "The TcpSocket::TcpSocket(...) function has been executed.");
        }

        /// <summary>
        /// Asynchronously try to connect to the given remote endpoint.
        /// </summary>
        /// <param name="Hostname">The hostname.</param>
        /// <param name="Port">The port.</param>
        /// <returns>whether we successfully connected or not.</returns>
        public async Task<bool> TryConnectAsync(string Hostname, int Port)
        {
            this.Logger?.Log(LogLevel.Debug, "The TcpSocket::TryConnectAsync(...) function has been executed.");

            // 
            // Verify the passed parameters.
            // 

            if (string.IsNullOrEmpty(Hostname))
            {
                this.Logger?.Log(LogLevel.Error, "The hostname is null or empty.");
                throw new ArgumentNullException(nameof(Hostname), "The hostname is null or empty.");
            }

            if (Port <= IPEndPoint.MinPort || Port > IPEndPoint.MaxPort)
            {
                this.Logger?.Log(LogLevel.Error, $"The port number is out of range. [Port: {Port}]");
                throw new ArgumentException("The port number must be between 0 and 65535.", nameof(Port));
            }

            // 
            // Are we already connected to a remote endpoint ?
            // 

            if (this.TcpClient.Connected)
            {
                this.Logger?.Log(LogLevel.Warning, $"The TcpSocket is already connected to a remote endpoint.");
                throw new InvalidOperationException("The TcpSocket is already connected to a remote endpoint.");
            }

            // 
            // Asynchronously attempt to connect to the server.
            // 

            this.Logger?.Log(LogLevel.Information, $"Attempting to connect to {Hostname}:{Port}.");
            var Stopwatch = System.Diagnostics.Stopwatch.StartNew();

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

            Stopwatch.Stop();

            if (this.IsConnected)
                this.Logger?.Log(LogLevel.Information, $"The connection attempt took {Stopwatch.Elapsed.TotalSeconds:N2} second(s) to complete.");
            else
                this.Logger?.Log(LogLevel.Warning, $"The connection attempt took {Stopwatch.Elapsed.TotalSeconds:N2} second(s) to complete but failed to succeed.");

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

                this.Logger?.Log(LogLevel.Debug, $"Trying to start a new thread named '{this.ReceiveThread.Name}'...");
                this.ReceiveThread.Start();
                this.Logger?.Log(LogLevel.Debug, $"The thread named '{this.ReceiveThread.Name}' has started.");

                this.Logger?.Log(LogLevel.Debug, $"Trying to start a new thread named '{this.SendThread.Name}'...");
                this.SendThread.Start();
                this.Logger?.Log(LogLevel.Debug, $"The thread named '{this.SendThread.Name}' has started.");

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
            this.Logger?.Log(LogLevel.Information, $"Disposing the TcpClient and disconnecting from the server.");

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

            if (this.IsConnected || this.TcpClient?.Client?.RemoteEndPoint != null)
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
