namespace TcpSocket
{
    using System;
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

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
        /// Gets or sets the thread that sends data to the server.
        /// </summary>
        private Thread SendThread
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the queue of messages to send to the server.
        /// </summary>
        private BufferBlock<TcpMessage> SendQueue
        {
            get;
        }

        /// <summary>
        /// The entry point of the thread that receives data from the server.
        /// </summary>
        private void ReceiveThreadRoutine()
        {
            // 
            // Initialize our read-buffer.
            // 

            var ReadBuffer = new byte[this.TcpClient.ReceiveBufferSize];

            // 
            // While we are connected to the server...
            // 

            while (this.TcpClient.Connected)
            {
                // 
                // Retrieve the network stream.
                // 

                var NetworkStream = (NetworkStream) null;

                try
                {
                    NetworkStream = this.TcpClient.GetStream();
                }
                catch (InvalidOperationException)
                {
                    // 
                    // We got disconnected during a previous operation.
                    // 

                    break;
                }

                // 
                // Receive data from the server.
                // 

                var NumberOfBytesRead = 0;

                try
                {
                    NumberOfBytesRead = NetworkStream.Read(ReadBuffer, 0, ReadBuffer.Length);
                }
                catch (IOException)
                {
                    // 
                    // The connection was forcefully closed while we were waiting for data.
                    // 

                    break;
                }

                // 
                // If there is nothing to read, the server disconnected us.
                // 

                if (NumberOfBytesRead == 0)
                {
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
                        // ..
                    }
                }
            }
        }

        /// <summary>
        /// The entry point of the thread that sends data to the server.
        /// </summary>
        private void SendThreadRoutine()
        {
            // 
            // Initialize our write-buffer.
            // 

            var WriteBuffer = new byte[this.TcpClient.SendBufferSize];

            // 
            // While we are connected to the server...
            // 

            while (this.TcpClient.Connected)
            {
                // 
                // Retrieve the network stream.
                // 

                var NetworkStream = (NetworkStream) null;

                try
                {
                    NetworkStream = this.TcpClient.GetStream();
                }
                catch (InvalidOperationException)
                {
                    // 
                    // We got disconnected during a previous operation.
                    // 

                    break;
                }

                // 
                // Wait for a message to send.
                // 

                var MessageToSend = (TcpMessage) null;

                while (MessageToSend == null)
                {
                    MessageToSend = this.SendQueue.Receive();

                    // 
                    // Did we get a message yet ?
                    // 

                    if (MessageToSend == null)
                    {
                        // 
                        // Are we still connected to the server ?
                        // 

                        if (this.TcpClient.Connected == false)
                        {
                            break;
                        }
                    }
                }

                // 
                // Are we still connected to the server ?
                // 

                if (this.TcpClient.Connected == false)
                {
                    break;
                }

                Trace.Assert(MessageToSend != null, "MessageToSend was null after the BufferBlock wait loop");

                // 
                // Send data to the server.
                // 

                var NumberOfBytesWritten = 0;

                try
                {
                    NetworkStream.Write(MessageToSend.Buffer, 0, MessageToSend.Buffer.Length);
                    NumberOfBytesWritten = MessageToSend.Buffer.Length;
                }
                catch (IOException)
                {
                    // 
                    // The connection was forcefully closed while we were sending data.
                    // 

                    break;
                }

                // 
                // If we failed to write, an unexpected error occurred.
                // 

                if (NumberOfBytesWritten == 0)
                {
                    break;
                }

                // 
                // If the queued message had a completion event, signal it.
                // 

                Trace.Assert(MessageToSend.CompletionEvent != null, "The completion event of the queued message was null");

                if (MessageToSend.CompletionEvent != null)
                {
                    MessageToSend.CompletionEvent.Set();
                }

                // 
                // We've sent data to the server, invoke the handlers
                // subscribed to the socket sent data event.
                // 

                if (this.OnBufferSent != null)
                {
                    try
                    {
                        this.OnBufferSent.Invoke(this, new TcpSocketBufferSentEventArgs(this.TcpClient.Client, WriteBuffer, NumberOfBytesWritten));
                    }
                    catch (Exception)
                    {
                        // ..
                    }
                }
            }

            // 
            // We stopped processing the send queue, block any attempts to add more messages to the queue.
            // 

            this.SendQueue.Complete();
        }
    }
}
