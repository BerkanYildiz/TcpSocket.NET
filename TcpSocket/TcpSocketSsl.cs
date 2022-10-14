namespace TcpSocket
{
    using System.Net.Security;

    public partial class TcpSocket
    {
        /// <summary>
        /// Gets the Ssl/Tls configuration for the network stream.
        /// </summary>
        private TcpSocketSslConfig SslConfig { get; }

        /// <summary>
        /// Gets or sets the Ssl/Tls stream for the secured network communication channel.
        /// </summary>
        private SslStream SslNetworkStream { get; set; }
    }
}
