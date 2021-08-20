namespace TcpSocket
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using global::TcpSocket.Events;

    public partial class TcpSocket : IDisposable
    {
        /// <summary>
        /// Gets the TCP client.
        /// </summary>
        public TcpClient TcpClient
        {
            get;
        }

        /// <summary>
        /// Gets the logger.
        /// </summary>
        private ILogger Logger
        {
            get;
            set;
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
                    return false;

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
        /// <param name="ReceiveBufferSize">The size of the buffer used to receive data</param>
        /// <param name="SendBufferSize">The size of the buffer used to send data.</param>
        /// <param name="ReceiveTimeout">The time limit in milliseconds to receive a message before aborting.</param>
        /// <param name="SendTimeout">The time limit in milliseconds to send a message before aborting.</param>
        /// <param name="NoDelay">Whether to immediately send the network data or wait for the buffer to fill a bit.</param>
        /// <param name="Logger">The logging handler instance used to print debug messages and traces.</param>
        public TcpSocket(int ReceiveBufferSize = 8192, int SendBufferSize = 8192, int ReceiveTimeout = 0, int SendTimeout = 0, bool NoDelay = false)
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
        }

        /// <summary>
        /// Sets the logger.
        /// </summary>
        /// <param name="InLogger">The logger.</param>
        public void SetLogger(ILogger InLogger)
        {
            // 
            // Initialize the logging handler.
            // 

            this.Logger = InLogger;
            this.Logger?.Log(LogLevel.Trace, "The TcpSocket::SetLogger(...) function has been executed.");
        }

        /// <summary>
        /// Asynchronously try to connect to the given remote endpoint.
        /// </summary>
        /// <param name="InEndPoint">The remote endpoint.</param>
        /// <returns>whether we successfully connected or not.</returns>
        #if NET5_0
        public async ValueTask<bool> TryConnectAsync(EndPoint InEndPoint)
        #else
        public async Task<bool> TryConnectAsync(EndPoint InEndPoint)
        #endif
        {
            this.Logger?.Log(LogLevel.Trace, "The TcpSocket::TryConnectAsync(...) function has been executed.");

            // 
            // Verify the passed parameters.
            // 

            if (InEndPoint == null)
                throw new ArgumentNullException(nameof(InEndPoint));

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

            this.Logger?.Log(LogLevel.Information, $"Attempting to connect to {InEndPoint}.");
            var Stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                using (var CancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    switch (InEndPoint)
                    {
                        case IPEndPoint IpEndPoint:
                            await Task.Run(() => this.TcpClient.ConnectAsync(IpEndPoint.Address, IpEndPoint.Port), CancellationTokenSource.Token);
                            break;

                        case DnsEndPoint DnsEndPoint:
                            await Task.Run(() => this.TcpClient.ConnectAsync(DnsEndPoint.Host, DnsEndPoint.Port), CancellationTokenSource.Token);
                            break;

                        default:
                            throw new ArgumentException("The remote endpoint is not a DnsEndPoint nor a IPEndPoint", nameof(InEndPoint));
                    }
                }
                    
            }
            catch (SocketException)
            {
                // 
                // The client has failed to connect to the server.
                // 
            }
            catch (TaskCanceledException)
            {
                // 
                // The client has failed to connect in the timespan in has been given.
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
                // Start the thread.
                // 

                this.Logger?.Log(LogLevel.Debug, $"Trying to start a new thread named '{this.ReceiveThread.Name}'...");
                this.ReceiveThread.Start();
                this.Logger?.Log(LogLevel.Debug, $"The thread named '{this.ReceiveThread.Name}' has started.");

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
            this.Logger?.LogTrace($"Disposing the TcpClient and disconnecting from the server.");

            // 
            // Was this instance already disposed ?
            // 

            if (this.IsDisposed)
                return;

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
                    this.TcpClient?.Close();
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
            // If the thread is still running...
            // 

            if (this.ReceiveThread?.IsAlive ?? false)
            {
                // 
                // Wait for it to terminate.
                // 

                try
                {
                    this.ReceiveThread.Join();
                }
                catch (ThreadStateException)
                {
                    // 
                    // The thread was already terminating.
                    // 
                }
            }

            // 
            // Dispose the TcpClient.
            // 

            this.TcpClient?.Dispose();
        }
    }
}
