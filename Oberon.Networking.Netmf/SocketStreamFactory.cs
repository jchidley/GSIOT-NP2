﻿/* Copyright (c) 2013 Oberon microsystems, Inc. (Switzerland)
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

using Oberon.Networking.YalerStreams;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Oberon.Networking.Netmf.SocketStreams
{
    public sealed class SocketStreamFactory : IStreamFactory
    {
        public void Dispose() { }

        // client aspect

        public Stream Connect(string hostName, int port)
        {
            Contract.Requires(hostName != null);
            Contract.Requires(hostName.Length > 0);
            Contract.Requires(port >= 0);
            Contract.Requires(port <= 65535);

            // look up relay host's domain name,
            // to find IP address(es)
            IPHostEntry hostEntry = Dns.GetHostEntry(hostName);
            // SocketException in GetHostEntry would
            // indicate that host was not found
            //Debug.Print(host + " is resolved");
            // extract a returned address
            try
            {
                IPAddress hostAddress = hostEntry.AddressList[0];
                IPEndPoint remoteEndPoint = new IPEndPoint(hostAddress, port);

                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
                socket.Connect(remoteEndPoint);
                return new SocketStream(socket);
            }
            catch (SocketException e)
            {
                Dispose();
                throw new IOException("socket error " + e.ErrorCode, e);
            }
        }


        // server aspect

        public IStreamListener Listen(int localPort)
        {
            Contract.Requires(localPort >= 0);
            Contract.Requires(localPort <= 65535);
            return new SocketStreamListener(localPort);
        }

        public IStreamListener Listen(string relayHostName, int relayPort, string relayDomain, string relaySecretKey)
        {
            Contract.Requires(relayHostName != null);
            Contract.Requires(relayHostName.Length > 0);
            Contract.Requires(relayPort >= 0);
            Contract.Requires(relayPort <= 65535);
            Contract.Requires(relayDomain != null);
            Contract.Requires(relayDomain.Length >= 11);
            Contract.Requires(relaySecretKey != null);
            Contract.Requires(relaySecretKey.Length > 0);
            return new YalerStreamListener(relayHostName, relayPort, this, relayDomain, relaySecretKey);
        }
    }
}
