namespace TcpSocket
{
    using System.Net.Security;
    using System.Security.Authentication;

    /// <remarks>
    /// Should we use <see cref="System.Net.Security.SslClientAuthenticationOptions"/> instead?
    /// </remarks>>
    public class TcpSocketSslConfig
    {
        public string RemoteServerName { get; set; }
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback { get; set; }
        public LocalCertificateSelectionCallback LocalCertificateSelectionCallback { get; set; } = null;
        public EncryptionPolicy EncryptionPolicy { get; set; } = EncryptionPolicy.RequireEncryption;

        #if NET5_0 || NET6_0
        public SslProtocols EnabledSslProtocols { get; set; } = SslProtocols.Tls12 | SslProtocols.Tls13;
        #else
        public SslProtocols EnabledSslProtocols { get; set; } = SslProtocols.Tls12;
        #endif
    }
}
