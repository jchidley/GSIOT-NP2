/* Copyright (c) 2013 Oberon microsystems, Inc. (Switzerland)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License. */

// Originally developed for the book
//   "Getting Started with the Internet of Things", by Cuno Pfister.
//   Copyright 2011 Cuno Pfister, Inc., 978-1-4493-9357-1.
//
// Version 4.3, for the .NET Micro Framework release 4.3.
//
// Internal abstractions, not documented.

using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Oberon.Networking.Netmf.SocketStreams
{
    internal class SocketStreamListener : IStreamListener
    {
        public bool IsOpen { get; private set; }
        public string LocalHostName { get; private set; }
        public int LocalPort { get; set; }
        public string LocalUrl { get; private set; }

        // configuration properties specific to sockets (usually Ethernet sockets)
        public int Backlog { get; set; }

        Socket listener;

        public SocketStreamListener(int localPort)
        {
            Contract.Requires(localPort >= 0);
            Contract.Requires(localPort <= 65535);
            IsOpen = false;
            LocalHostName = null;
            LocalPort = localPort;
            Backlog = 0;
            if (!IsOpen) { Open(); }  // added jcc
        }

        public void Open()
        {
            Contract.Requires(!IsOpen);
            Contract.Requires(LocalPort >= 0);
            Contract.Requires(LocalPort <= 65535);
            if (Backlog <= 0) { Backlog = 4; }

            Contract.Assert(listener == null);
            try
            {
                IPAddress myAdr = IPAddress.GetDefaultLocalAddress();
                while (myAdr == IPAddress.Any)      // only necessary for lwIP stack?
                {
                    Thread.Sleep(1000);             // wait until local address is set
                    myAdr = IPAddress.GetDefaultLocalAddress();
                }
                LocalHostName = myAdr.ToString();
                LocalUrl = "http://" + LocalHostName;
                if (LocalPort != 80)
                {
                    LocalUrl = LocalUrl + ':' + LocalPort;
                }
                listener = new Socket(AddressFamily.InterNetwork,
                                        SocketType.Stream, ProtocolType.Tcp);
                listener.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
                listener.Bind(new IPEndPoint(IPAddress.Any, LocalPort));
                listener.Listen(Backlog);
                IsOpen = true;
            }
            catch (SocketException e)
            {
                if (listener != null) { Dispose(); }
                listener = null;
                throw new IOException("socket error " + e.ErrorCode, e);
            }
        }

        public void Dispose()
        {
            if (IsOpen)
            {
                IsOpen = false;
                Contract.Assert(listener != null);
                listener.Close();
                listener = null;
                LocalHostName = null;
            }
        }

        public Stream Accept()
        {
            if (!IsOpen) { Open(); }  // added jcc
            try
            {
                Socket s = listener.Accept();
                // Socket.Accept may throw an exception: SocketException,
                // ObjectDisposedException, or InvalidOperationException
                return new SocketStream(s);
            }
            catch (SocketException e)     // this may happen, treat as expected error
            {
                throw new IOException("socket error " + e.ErrorCode, e);
            }
        }
    }
}
