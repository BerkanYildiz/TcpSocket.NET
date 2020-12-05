namespace TcpSocket.Example
{
    using System;
    using System.Threading.Tasks;

    using global::TcpSocket;
    using global::TcpSocket.Events;

    internal static class Program
    {
        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <param name="Args">The arguments.</param>
        private static async Task Main(string[] Args)
        {
            // 
            // Initialize a new TCP socket.
            // 

            var TcpSocket = new TcpSocket();
            TcpSocket.OnBufferReceived += OnBufferReceived;
            TcpSocket.OnBufferSent += OnBufferSent;

            // 
            // Connect to the server.
            // 

            var WasSocketConnected = await TcpSocket.TryConnectAsync("srv1.hwidspoofer.com", 6969);
            Console.WriteLine($"[*] WasSocketConnected: {WasSocketConnected}");

            // 
            // Send a message to the server.
            // 

            for (int I = 0; I < 50; I++)
            {
                await TcpSocket.TrySendBufferAsync(new byte[512]);
            }

            // 
            // Wait.
            // 

            Console.ReadKey();
        }

        /// <summary>
        /// Called when the TCP socket has received data from the server.
        /// </summary>
        /// <param name="Sender">The sender.</param>
        /// <param name="EventArgs">The <see cref="TcpSocketBufferReceivedEventArgs"/> instance containing the event data.</param>
        private static void OnBufferReceived(object Sender, TcpSocketBufferReceivedEventArgs EventArgs)
        {
            Console.WriteLine($"[*] Received {EventArgs.NumberOfBytesRead} bytes from the server!");
        }

        /// <summary>
        /// Called when the TCP socket has sent data to the server.
        /// </summary>
        /// <param name="Sender">The sender.</param>
        /// <param name="EventArgs">The <see cref="TcpSocketBufferSentEventArgs"/> instance containing the event data.</param>
        private static void OnBufferSent(object Sender, TcpSocketBufferSentEventArgs EventArgs)
        {
            Console.WriteLine($"[*] Sent {EventArgs.NumberOfBytesWritten} bytes to the server!");
        }
    }
}
