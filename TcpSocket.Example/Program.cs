namespace TcpSocket.Example
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
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
            // Increase the amount of threads dedicated to tasks.
            // 

            ThreadPool.GetMinThreads(out var MinWorkerThreads, out var MinCompletionThreads);
            ThreadPool.SetMinThreads(MinWorkerThreads * 3, MinCompletionThreads);
            ThreadPool.GetMinThreads(out MinWorkerThreads, out MinCompletionThreads);

            Console.WriteLine($"[*] MinThreads: {MinWorkerThreads} / {MinCompletionThreads}");

            // 
            // Show the number of messages sent in the console title.
            // 

            Task.Run(async () =>
            {
                while (true)
                {
                    Console.Title = $".NET - TcpSocket - [NumberOfMessagesSent: {Interlocked.Read(ref NumberOfMessagesSent)}]";
                    await Task.Delay(100);
                }
            });

            // 
            // Initialize a new TCP socket.
            // 

            var TcpSocket = new TcpSocket();
            TcpSocket.OnSocketConnected += OnSocketConnected;
            TcpSocket.OnSocketDisconnected += OnSocketDisconnected;
            TcpSocket.OnBufferReceived += OnBufferReceived;
            TcpSocket.OnBufferSent += OnBufferSent;

            using (TcpSocket)
            {
                // 
                // Connect to the server.
                // 

                await TcpSocket.TryConnectAsync("localhost", 6969);

                // 
                // Asynchronously spam the server.
                // 

                var SpamTasks = new Task[20];
                var ShouldStopTasks = false;
                var BufferToSend = new byte[32];

                for (var I = 0; I < SpamTasks.Length; I++)
                {
                    SpamTasks[I] = Task.Run(async () =>
                    {
                        while (TcpSocket.Connected && !ShouldStopTasks)
                        {
                            var HasSentMessage = await TcpSocket.TrySendBufferAsync(BufferToSend);

                            if (HasSentMessage == false)
                            {
                                Console.WriteLine($"[*] Failed to send a message during the while loop.");
                            }
                        }
                    });
                }

                // 
                // Wait.
                // 

                Console.ReadKey();

                // 
                // Wait for every tasks to finish.
                // 

                ShouldStopTasks = true;
                Task.WaitAll(SpamTasks);
                Console.WriteLine("[*] Every tasks were terminated, disposing the TCP socket...");
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[*] Quitting...");
            Console.ResetColor();

            await Task.Delay(1500);
        }

        /// <summary>
        /// Called when the TCP socket has connected to the server.
        /// </summary>
        /// <param name="Sender">The sender.</param>
        /// <param name="EventArgs">The <see cref="TcpSocketConnectedEventArgs"/> instance containing the event data.</param>
        private static void OnSocketConnected(object Sender, TcpSocketConnectedEventArgs EventArgs)
        {
            var EndPoint = EventArgs.EndPoint is IPEndPoint Ip ? Ip.ToString() :
                                EventArgs.EndPoint is DnsEndPoint Dns ? string.Join(".", Dns.Host, Dns.Port) :
                                "(NULL)";

            Trace.WriteLine($"[*] We've connected to {EndPoint}!");
        }

        /// <summary>
        /// Called when the TCP socket has disconnected from the server.
        /// </summary>
        /// <param name="Sender">The sender.</param>
        /// <param name="EventArgs">The <see cref="TcpSocketDisconnectedEventArgs"/> instance containing the event data.</param>
        private static void OnSocketDisconnected(object Sender, TcpSocketDisconnectedEventArgs EventArgs)
        {
            var EndPoint = EventArgs.EndPoint is IPEndPoint Ip ? Ip.ToString() :
                EventArgs.EndPoint is DnsEndPoint Dns ? Dns.ToString() :
                "(NULL)";

            Trace.WriteLine($"[*] We've disconnected {EndPoint} from the server!");
        }

        /// <summary>
        /// Called when the TCP socket has received data from the server.
        /// </summary>
        /// <param name="Sender">The sender.</param>
        /// <param name="EventArgs">The <see cref="TcpSocketBufferReceivedEventArgs"/> instance containing the event data.</param>
        private static void OnBufferReceived(object Sender, TcpSocketBufferReceivedEventArgs EventArgs)
        {
            // Trace.WriteLine($"[*] Received {EventArgs.NumberOfBytesRead} bytes from the server!");
        }

        private static long NumberOfMessagesSent = 0;

        /// <summary>
        /// Called when the TCP socket has sent data to the server.
        /// </summary>
        /// <param name="Sender">The sender.</param>
        /// <param name="EventArgs">The <see cref="TcpSocketBufferSentEventArgs"/> instance containing the event data.</param>
        private static void OnBufferSent(object Sender, TcpSocketBufferSentEventArgs EventArgs)
        {
            Trace.WriteLine($"[*] Sent {EventArgs.NumberOfBytesWritten} bytes to the server!");
            Interlocked.Increment(ref NumberOfMessagesSent);
        }
    }
}
