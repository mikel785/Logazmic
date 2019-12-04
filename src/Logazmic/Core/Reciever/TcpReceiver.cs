﻿using Logazmic.Core.Readers;

namespace Logazmic.Core.Reciever
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    public class TcpReceiver : ReceiverBase
    {
        private Socket server;

        public int Port { get; set; }
        public bool IpV6 { get; set; }
        public int BufferSize { get; set; }

        public TcpReceiver()
        {
            Port = 4505;
            BufferSize = 128 * 1024; // 128Kb
        }

        protected override void DoInitialize()
        {
            if (server != null) return;

            server = new Socket(IpV6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork, SocketType.Stream,
                ProtocolType.Tcp);

            // allow other apps listen the same port
            server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);
            server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            var endpoint = new IPEndPoint(IpV6 ? IPAddress.IPv6Any : IPAddress.Any, Port);
            server.Bind(endpoint);
            server.Listen(100);
            server.ReceiveBufferSize = BufferSize;
            
            var args = new SocketAsyncEventArgs();
            args.Completed += AcceptAsyncCompleted;

            server.AcceptAsync(args);
        }

        void AcceptAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (server == null || e.SocketError != SocketError.Success) return;

            //This is original from Log2Console source
            new Thread(Start) { IsBackground = true }.Start(e.AcceptSocket);

            e.AcceptSocket = null;
            server.AcceptAsync(e);
        }

        void Start(object newSocket)
        {
            try
            {
                var logStreamReader = LogReaderFactory.LogStreamReader(LogFormat);
                logStreamReader.DefaultLogger = "TcpLogger";

                using (var socket = (Socket) newSocket)
                {
                    using (var ns = new NetworkStream(socket, FileAccess.Read, false))
                    {
                        int bytesRead;
                        do
                        {
                            foreach (var logMessage in logStreamReader.NextLogEvents(ns, out bytesRead))
                            {
                                logMessage.LoggerName = string.Format(":{1}.{0}", logMessage.LoggerName, Port);
                                OnNewMessage(logMessage);
                            }
                        } while (server != null && bytesRead > 0);
                    }
                }
            }
            catch (IOException e)
            {
                Console.WriteLine(e);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public override void Terminate()
        {
            if (server == null) return;

            server.Close();
            server = null;
        }
    }
}
