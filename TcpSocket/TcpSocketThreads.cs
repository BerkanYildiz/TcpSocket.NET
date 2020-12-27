namespace TcpSocket
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Sockets;
    using System.Threading;
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
            var ThisThreadName = Thread.CurrentThread.Name;
            Trace.WriteLine($"The {ThisThreadName} has started.", ThisThreadName);

            // 
            // Initialize our read-buffer.
            // 

            var ReadBuffer = new byte[this.TcpClient.ReceiveBufferSize];

            // 
            // While we are connected to the server...
            // 

            while (this.Connected)
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
                // Wait for data to read.
                // 

                #if SYNCHRONOUS_WAIT_FOR_DATA
                while (NetworkStream.DataAvailable == false && this.Connected)
                {
                    Debug.WriteLine($"[*] DataAvailable: {NetworkStream.DataAvailable} / Connected: {this.Connected}");
                    Thread.Sleep(1);
                }
                #endif

                // 
                // Are we still connected to the server after waiting for data ?
                // 

                if (this.Connected == false)
                {
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
                catch (ThreadAbortException)
                {
                    // 
                    // The TcpSocket wants to stop receiving data and attempted to terminate this thread. 
                    // Gracefully break the loop and return.
                    // 

                    return;
                }
                catch (IOException)
                {
                    // 
                    // The connection was forcefully closed while we were waiting for data,
                    // or the socket was configured to throw an exception after a certain period of time (timeout).
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
                        // ...
                    }
                }
            }

            // 
            // Debug.
            // 

            this.IsDisconnecting = true;
            Trace.WriteLine($"The {ThisThreadName} is terminating.", ThisThreadName);
        }

        /// <summary>
        /// The entry point of the thread that sends data to the server.
        /// </summary>
        private void SendThreadRoutine()
        {
            var ThisThreadName = Thread.CurrentThread.Name;
            Trace.WriteLine($"The {ThisThreadName} has started.", ThisThreadName);

            // 
            // Initialize our write-buffer.
            // 

            var WriteBuffer = new byte[this.TcpClient.SendBufferSize];

            // 
            // While we are connected to the server...
            // 

            while (this.Connected)
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

                try
                {
                    MessageToSend = this.SendQueue.Receive();
                }
                catch (InvalidOperationException)
                {
                    // 
                    // The queue was completed while we were waiting for a message.
                    // We have nothing to send anymore, break the loop.
                    // 

                    break;
                }

                // 
                // Are we still connected to the server ?
                // 

                if (this.Connected == false)
                {
                    MessageToSend.WasMessageSent = false;
                    MessageToSend.CompletionEvent.Set();
                    break;
                }

                // 
                // Send data to the server.
                // 

                var NumberOfBytesWritten = 0;

                try
                {
                    NetworkStream.Write(MessageToSend.Buffer, 0, MessageToSend.Buffer.Length);
                    NumberOfBytesWritten = MessageToSend.Buffer.Length;
                }
                catch (ThreadAbortException)
                {
                    // 
                    // The TcpSocket wants to stop sending data and attempted to terminate this thread. 
                    // Gracefully break the loop and return.
                    // 

                    return;
                }
                catch (IOException)
                {
                    // 
                    // The connection was forcefully closed while we were sending data,
                    // or the socket was configured to throw an exception after a certain period of time (timeout).
                    // 

                    MessageToSend.WasMessageSent = false;
                    MessageToSend.CompletionEvent.Set();
                    break;
                }

                // 
                // If we failed to write, an unexpected error occurred.
                // 

                if (NumberOfBytesWritten == 0)
                {
                    MessageToSend.WasMessageSent = false;
                    MessageToSend.CompletionEvent.Set();
                    break;
                }

                // 
                // Signal that the queued message was processed.
                // 

                MessageToSend.WasMessageSent = true;
                MessageToSend.CompletionEvent.Set();

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
                        // ...
                    }
                }
            }

            // 
            // Debug.
            // 

            this.IsDisconnecting = true;
            Trace.WriteLine($"The {ThisThreadName} is terminating.", ThisThreadName);

            // 
            // We stopped processing the send queue, block any attempts to add more messages to the queue.
            // 

            if (!this.SendQueue.Completion.IsCompleted)
            {
                this.SendQueue.Complete();
            }

            // 
            // For the messages that are still in the queue,
            // complete them.
            // 

            if (this.SendQueue.TryReceiveAll(out var Queue))
            {
                Trace.WriteLine($"Finalizing {Queue.Count} message(s) that were left in the queue after its completion.");

                // 
                // For each messages in the queue...
                // 

                foreach (var Entry in Queue)
                {
                    // 
                    // Signal that the queued message has been processed.
                    // 

                    Entry.WasMessageSent = false;
                    Entry.CompletionEvent.Set();
                }
            }
        }
    }
}
