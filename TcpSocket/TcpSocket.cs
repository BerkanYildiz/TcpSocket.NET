namespace TcpSocket
{
    using System;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;

    using global::TcpSocket.Events;

    public partial class TcpSocket : IDisposable
    {
        /// <summary>
        /// Gets the TCP client.
        /// </summary>
        public TcpClient TcpClient { get; }

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
        public bool IsDisconnecting { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance has been disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpSocket"/> class.
        /// </summary>
        /// <param name="InReceiveBufferSize">The size of the buffer used to receive data</param>
        /// <param name="InSendBufferSize">The size of the buffer used to send data.</param>
        /// <param name="InReceiveTimeout">The time limit in milliseconds to receive a message before aborting.</param>
        /// <param name="InSendTimeout">The time limit in milliseconds to send a message before aborting.</param>
        /// <param name="InNoDelay">Whether to immediately send the network data or wait for the buffer to fill a bit.</param>
        /// <param name="InSslConfig">The Ssl/Tls configuration for the network stream.</param>
        public TcpSocket(int InReceiveBufferSize = 8192, int InSendBufferSize = 8192, int InReceiveTimeout = 0, int InSendTimeout = 0, bool InNoDelay = false, TcpSocketSslConfig InSslConfig = null)
        {
            // 
            // Initialize the TCP client.
            // 
            
            this.TcpClient = new TcpClient(AddressFamily.InterNetwork)
            {
                ReceiveBufferSize = InReceiveBufferSize,
                SendBufferSize = InSendBufferSize,
                ReceiveTimeout = InReceiveTimeout,
                SendTimeout = InSendTimeout,
                NoDelay = InNoDelay,
            };

            // 
            // Setup the Ssl/Tls configuration.
            // 

            if (InSslConfig != null)
            {
                if (string.IsNullOrEmpty(InSslConfig.RemoteServerName))
                    throw new ArgumentException("The RemoteServerName cannot be null or empty");

                if (InSslConfig.RemoteCertificateValidationCallback == null)
                    throw new ArgumentException("The RemoteCertificateValidationCallback cannot be null");

                this.SslConfig = InSslConfig;
            }
        }

        /// <summary>
        /// Asynchronously try to connect to the given remote endpoint.
        /// </summary>
        /// <param name="InEndPoint">The remote endpoint.</param>
        /// <returns>whether we successfully connected or not.</returns>
        #if NET5_0 || NET6_0
        public async ValueTask<bool> TryConnectAsync(EndPoint InEndPoint)
        #else
        public async Task<bool> TryConnectAsync(EndPoint InEndPoint)
        #endif
        {
            // 
            // Verify the passed parameters.
            // 

            if (InEndPoint == null)
                throw new ArgumentNullException(nameof(InEndPoint));

            if (InEndPoint.GetType() != typeof(DnsEndPoint) && InEndPoint.GetType() != typeof(IPEndPoint))
                throw new ArgumentException("The remote endpoint is not a DnsEndPoint nor a IPEndPoint", nameof(InEndPoint));

            // 
            // Are we already connected to a remote endpoint ?
            // 

            if (this.TcpClient.Connected)
                throw new InvalidOperationException("The TcpSocket is already connected to a remote endpoint.");

            // 
            // Asynchronously attempt to connect to the server.
            // 

            try
            {
                using (var CancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    switch (InEndPoint)
                    {
                        case IPEndPoint IpEndPoint:
                            await Task.Run(() => this.TcpClient.ConnectAsync(IpEndPoint.Address, IpEndPoint.Port), CancellationTokenSource.Token);
                            break;

                        case DnsEndPoint DnsEndPoint:
                            await Task.Run(() => this.TcpClient.ConnectAsync(DnsEndPoint.Host, DnsEndPoint.Port), CancellationTokenSource.Token);
                            break;
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

            // 
            // If we are connected to the server, start the receive/send threads.
            // 

            if (this.TcpClient.Connected)
            {
                // 
                // Ensure the stream is secure.
                // 

                if (this.SslConfig != null)
                {
                    this.SslNetworkStream = new SslStream(this.TcpClient.GetStream(), true, this.SslConfig.RemoteCertificateValidationCallback, this.SslConfig.LocalCertificateSelectionCallback, this.SslConfig.EncryptionPolicy);
                    try { await this.SslNetworkStream.AuthenticateAsClientAsync(this.SslConfig.RemoteServerName, null, this.SslConfig.EnabledSslProtocols, false); } catch (Exception) { }

                    if (!this.SslNetworkStream.IsAuthenticated || !this.SslNetworkStream.IsEncrypted)
                    {
                        this.SslNetworkStream.Dispose();
                        this.SslNetworkStream = null;
                        this.TcpClient.Dispose();
                        throw new SecurityException("The secure network stream couldn't be authenticated or setup");
                    }
                }

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
                
                this.ReceiveThread.Start();

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
                    catch (Exception) { }
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

            // 
            // Dispose the SslStream.
            // 
            
            this.SslNetworkStream?.Dispose();
        }
    }
}
