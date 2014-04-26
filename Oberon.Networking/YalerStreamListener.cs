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
// A YalerStreamListener takes a stream factory that supports
// TCP clients, and implements the Yaler reverse HTTP protocol
// on a TCP connection. As a result, this TCP connection can be
// used to serve *incoming* TCP connections, and thereby implement
// the listener functionality (TCP server).

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;

namespace Oberon.Networking.YalerStreams
{
    /// <summary>
    /// Object that emulates a listener socket (incoming) by opening an
    /// outgoing socket to a relay server. Once the initialization protocol
    /// has completed (upgrade from HTTP to PTTH protocol for reverse HTTP)
    /// then the socket is returned to the server application, which
    /// awaits incoming requests on this socket.
    /// </summary>
    public class YalerStreamListener : IStreamListener
    {
        public string LocalHostName { get; private set; }
        public int LocalPort { get; private set; }
        public string LocalUrl { get; private set; }
        public string Domain { get; private set; }
        string secretKey;
        public IStreamFactory relay;
        readonly object abortedLock = new object();
        bool aborted;

        /// <summary>
        /// Constructor for opening an emulated listener socket using
        /// the Yaler reverse HTTP protocol.
        /// Preconditions
        ///     host != null
        ///     host.Length > 0
        ///     port > 0
        ///     port leq 65536
        ///     relayDomain != null
        ///     relayDomain.Length >= 11
        /// </summary>
        /// <param name="host">Internet address or domain name of the
        /// relay server.</param>
        /// <param name="port">Port number of the relay service.</param>
        /// <param name="relayDomain">Yaler relay domain.</param>
        public YalerStreamListener(string relayHostName, int relayPort, IStreamFactory relay, string relayDomain, string relaySecretKey)
        {
            Contract.Requires(relayHostName != null);
            Contract.Requires(relayHostName.Length > 0);
            Contract.Requires(relayPort >= 0);
            Contract.Requires(relayPort <= 65535);
            Contract.Requires(relay != null);
            Contract.Requires(relayDomain != null);
            Contract.Requires(relayDomain.Length >= 11);
            Contract.Requires(relaySecretKey != null);
            Contract.Requires(relaySecretKey.Length > 0);
            LocalHostName = relayHostName;
            LocalPort = relayPort;
            this.relay = relay;
            Domain = relayDomain;
            this.secretKey = relaySecretKey;
            LocalUrl = "http://" + LocalHostName;
            if (LocalPort != 80)
            {
                LocalUrl = LocalUrl + ':' + LocalPort;
            }
            LocalUrl = LocalUrl + '/' + relayDomain;
        }

        /// <summary>
        /// Close the emulated listener.
        /// </summary>
        public void Dispose()
        {
            lock (abortedLock)
            {
                aborted = true;
            }
            LocalHostName = null;
            if (relay != null)
            {
                relay.Dispose();
                relay = null;
            }
            Domain = null;
            secretKey = null;
        }

        void Find(string pattern, Stream s, out bool found)
        {
            int[] x = new int[pattern.Length];
            byte[] b = new byte[1];
            int i = 0, j = 0, t = 0;
            do
            {
                found = true;
                for (int k = 0; (k != pattern.Length) && found; k++)
                {
                    if (i + k == j)
                    {
                        int n = s.Read(b, 0, 1);
                        x[j % x.Length] = n != 0 ? b[0] : -1;
                        j = j + 1;
                    }
                    t = x[(i + k) % x.Length];
                    found = pattern[k] == t;
                }
                i = i + 1;
            } while (!found && (t != -1));
        }

        void FindLocation(Stream s, out string host, out int port)
        {
            host = null;
            port = 80;
            bool found;
            Find("\r\nLocation: http://", s, out found);
            if (found)
            {
                char[] stringChars = new char[40];
                var stringIndex = 0;
                byte[] x = new byte[1];
                int n = s.Read(x, 0, 1);
                while ((n != 0) && (x[0] != ':') && (x[0] != '/'))
                {
                    stringChars[stringIndex] = (char)x[0];
                    stringIndex = stringIndex + 1;
                    n = s.Read(x, 0, 1);
                }
                if (x[0] == ':')
                {
                    port = 0;
                    n = s.Read(x, 0, 1);
                    while ((n != 0) && (x[0] != '/'))
                    {
                        port = 10 * port + x[0] - '0';
                        n = s.Read(x, 0, 1);
                    }
                }
                host = new string(stringChars, 0, stringIndex);
            }
        }

        /// <summary>
        /// Register this device at the relay service and return a socket
        /// for upcoming requests (and their responses).
        /// </summary>
        /// <returns>Socket on which a request can be awaited.</returns>
        public Stream Accept()
        {
            bool a;
            lock (abortedLock)
            {
                a = aborted;
            }
            if (a)
            {
                throw new InvalidOperationException();
            }
            else
            {
                string host = LocalHostName;
                int port = LocalPort;
                Stream s;
                bool acceptable;    // end of headers found
                int[] x = new int[3];
                byte[] b = new byte[1];

                do
                {
                    s = relay.Connect(LocalHostName, LocalPort);
                    do
                    {
                        byte[] buf = Encoding.UTF8.GetBytes(
                            "POST /" + Domain + " HTTP/1.1\r\n" +
                            "Upgrade: PTTH/1.0\r\n" +
                            "Connection: Upgrade\r\n" +
                            "Host: " + host + "\r\n\r\n");
                        s.Write(buf, 0, buf.Length);
                        for (int j = 0; j != 12; j = j + 1)
                        {
                            int n = s.Read(b, 0, 1);
                            x[j % 3] = n != 0 ? b[0] : -1;
                        }
                        if ((x[0] == '3') && (x[1] == '0') && (x[2] == '7'))
                        {
                            FindLocation(s, out host, out port);
                        }
                        Find("\r\n\r\n", s, out acceptable);
                        Contract.Assert(acceptable);  // test
                    } while (acceptable && ((x[0] == '2') && (x[1] == '0') && (x[2] == '4')));
                    if (!acceptable || (x[0] != '1') || (x[1] != '0') || (x[2] != '1'))
                    {
                        s.Close();
                        s = null;
                    }
                } while (acceptable && ((x[0] == '3') && (x[1] == '0') && (x[2] == '7')));
                return s;
            }
        }
    }
}
