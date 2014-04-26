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
// Server-specific parts of Gsiot.Server namespace.
// See the book "Getting Started with the Internet of Things", Appendix C.
//
// This server implementation intentionally uses a single thread only, to
// make it simple, small, and with minimal memory overhead.
// If such a single-threaded server kept a connection to a client open,
// no requests from other clients could be handled. For this reason, the
// server explicitly closes the connection after handling a request, or
// after a timeout when no new data has arrived for a while.
// To explicitly close the connection after a request, the server sends
// the "Connection: close" header in its response.

using Oberon.Networking;
using Oberon.Networking.Netmf.SocketStreams;      // remove this dependency if you don't use sockets
using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
using System.Threading;

namespace Gsiot.Server
{
    /// <summary>
    /// An instance of class RequestHandlerContext provides information
    /// about the received HTTP request to a request handler. The request
    /// handler uses it to set up the HTTP response to this request, and
    /// if necessary, to construct URIs to the same service.
    /// </summary>
    public class RequestHandlerContext
    {
        // To keep this class simple, the following holds:
        //   - It only supports the most important HTTP headers.

        // server interface
        string serviceRoot;     // where ((serviceRoot != null) &&
        //                         ("serviceRoot is an absolute URI")
        string relayDomain;     // null iff no relay is used

        /// <summary>
        /// Constructor of RequestHandlerContext.
        /// </summary>
        /// <param name="serviceRoot">The URI relative to which the
        /// request URIs are processed, e.g., http://192.168.5.100:8080.
        /// </param>
        /// <param name="relayDomain">Indicates whether a relay is used;
        /// otherwise, it is null.</param>
        public RequestHandlerContext(string serviceRoot,
            string relayDomain)
        {
            Contract.Requires(serviceRoot != null);
            Contract.Requires(serviceRoot.Substring(0, 7) == "http://");
            Contract.Requires(serviceRoot[serviceRoot.Length - 1] != '/');
            this.serviceRoot = serviceRoot;
            this.relayDomain = relayDomain;
        }

        /// <summary>
        /// Before a request handler is called, this property is set to
        /// true if (and only if) the received request contained a
        /// Connection: close header. If the request handler wants to
        /// indicate that it wants to close the connection, it can set
        /// the property to true, which will add the Connection: close
        /// header to its response.
        /// </summary>
        public bool ConnectionClose { get; set; }

        // request interface

        string requestUri;

        /// <summary>
        /// This property tells you which kind of request has been
        /// received (an HTTP method such as GET or PUT). You only need
        /// to check this property if you want to support several HTTP
        /// methods in the same request handler, i.e., request patterns
        /// with a * wildcard at the beginning.
        /// </summary>
        public string RequestMethod { get; internal set; }

        /// <summary>
        /// This property contains the URI of the incoming request. You
        /// only need this property if you want to support several
        /// resources in the same request handler, i.e., request patterns
        /// with a * wildcard at the end.
        /// </summary>
        public string RequestUri
        {
            get { return requestUri; }

            internal set
            {
                Contract.Requires(value != null);
                Contract.Requires(value.Length > 0);
                Contract.Requires(value[0] == '/');
                if ((relayDomain != null) && (value == '/' + relayDomain))
                {   // After stripping away the relay prefix, this
                    // would be an illegal request URI (empty string).
                    value = value + '/';
                }
                requestUri = value;
            }
        }

        /// <summary>
        /// This property contains the content of the request’s
        /// Content-Length header if one was present; otherwise, it is
        /// null.
        /// </summary>
        public string RequestContentType { get; internal set; }

        /// <summary>
        /// This property contains the content of the request message body.
        /// </summary>
        public byte[] RequestContentBytes { get; internal set; }

        string requestContentString = null;

        /// <summary>
        /// This property contains the request message body converted into
        /// a string of text, with a UTF8 encoding. You only need this
        /// property for PUT and POST requests, since GET and DELETE have
        /// no message bodies.
        /// </summary>
        public string RequestContent
        {
            get
            {
                if (requestContentString == null)
                {
                    try
                    {
                        char[] chars = Encoding.UTF8.GetChars(RequestContentBytes);
                        requestContentString = new string(chars);
                    }
                    catch (Exception)
                    {
                        requestContentString = null;
                    }
                }
                return requestContentString;
            }
        }

        internal bool RequestMatch(RoutingElement e)
        {
            Contract.Requires(e != null);
            Contract.Requires(e.Path != null);
            Contract.Requires(e.Path.Length > 0);
            Contract.Requires(e.Path[0] == '/');
            // Pattern = ( Method | '*') path [ '*' ]
            string uri = RequestUri;
            Contract.Requires(uri != null);
            int uriLength = uri.Length;
            Contract.Requires(uriLength >= 1);
            if (uri[0] != '/')      // some proxies return absolute URIs
            {
                return false;
            }
            string method = RequestMethod;
            Contract.Requires(method != null);
            int methodLength = method.Length;
            Contract.Requires(methodLength >= 3);
            if ((method != e.Method) && (e.Method != "*")) { return false; }

            var pos = 1;
            if (relayDomain != null)    // try to match relay domain
            {
                int relayPrefixLength = relayDomain.Length + 1;
                if (uriLength <= relayPrefixLength) { return false; }
                while (pos != relayPrefixLength)
                {
                    if (uri[pos] != relayDomain[pos - 1]) { return false; }
                    pos = pos + 1;
                }
                if (uri[pos] != '/') { return false; }
                pos = pos + 1;
            }
            // try to match request pattern
            int patternLength = e.Path.Length;
            if (uriLength < (pos - 1 + patternLength)) { return false; }
            var i = 1;
            while (i != patternLength)
            {
                if (uri[pos] != e.Path[i]) { return false; }
                pos = pos + 1;
                i = i + 1;
            }
            return ((pos == uriLength) || (e.Wildcard));
        }

        // server interface

        /// <summary>
        /// This method takes a path and constructs a relative URI out of
        /// it. If the request was relayed, this is taken into account.
        /// For example, BuildRequestUri("hello.html") may return
        /// /gsiot-FFMQ-TTD5/hello.html if the request pattern was
        /// "GET /hello*".
        /// You should use this method if your response contains relative
        /// hyperlinks to your server.
        /// 
        /// Preconditions
        ///     path != null
        ///     path.Length > 0
        ///     path[0] == '/'
        /// </summary>
        /// <param name="path">Relative path starting with a /.</param>
        /// <returns>The same string if no relay is used, or the
        /// same string prefixed with the relay domain otherwise.</returns>
        public string BuildRequestUri(string path)
        {
            Contract.Requires(path != null);
            Contract.Requires(path.Length > 0);
            Contract.Requires(path[0] == '/');
            return (relayDomain == null) ? path : "/" + relayDomain + path;
        }

        /// <summary>
        /// This method takes a path and constructs an absolute URI out of
        /// it. If the request was relayed, this is taken into account.
        /// For example, BuildAbsoluteRequestUri("hello.html") may return
        /// http://try.yaler.net/gsiot-FFMQ-TTD5/hello.html if the request
        /// pattern was "GET /hello*".
        /// You should use this method if your response contains absolute
        /// hyperlinks to your server.
        /// 
        /// Preconditions
        ///     path != null
        ///     path.Length > 0
        ///     path[0] == '/'
        /// </summary>
        /// <param name="path">Relative path starting with a /.</param>
        /// <returns>Absolute URI, containing relay domain if a relay
        /// is used.</returns>
        public string BuildAbsoluteRequestUri(string path)
        {
            Contract.Requires(path != null);
            Contract.Requires(path.Length > 0);
            Contract.Requires(path[0] == '/');
            return serviceRoot + BuildRequestUri(path);
        }

        // response interface

        int statusCode = 200;       // OK
        string responseContentType = "text/plain";
        int responseMaxAge = -1;    // no cache-control header

        /// <summary>
        /// This property can be set to indicate the status code of the
        /// response. The most important status codes for our purposes are:
        ///     200 (OK)
        ///     400 (Bad Request)
        ///     404 (Not Found)
        ///     405 (Method Not Allowed)
        /// </summary>
        public int ResponseStatusCode
        {
            get { return statusCode; }

            // -1 means "undefined value"
            set
            {
                Contract.Requires((value >= 100) || (value == -1));
                Contract.Requires(value < 600);
                statusCode = value;
            }
        }

        /// <summary>
        /// This property can be set to indicate the content type of the
        /// response. This so-called MIME type will become the value of
        /// the HTTP Content-Type header. The most important content
        /// types for our purposes are:
        /// • text/plain
        /// Used for a plain-text response such as a single numeric or
        /// text value.
        /// • text/csv
        /// Used to send a series of values.
        /// • text/html
        /// Used to send a response with formatted HTML.
        /// </summary>
        public string ResponseContentType
        {
            get { return responseContentType; }

            set
            {
                Contract.Requires(value != null);
                Contract.Requires(value.Length > 0);
                responseContentType = value;
            }
        }

        /// <summary>
        /// This property can be set to indicate the time that a
        /// resource remains valid, in seconds.
        /// </summary>
        public int ResponseMaxAge
        {
            get { return responseMaxAge; }

            set
            {
                Contract.Requires(value >= -1);
                responseMaxAge = value;
            }
        }

        /// <summary>
        /// This property can be set with the content of the response
        /// message (message body). It will be encoded in UTF8.
        /// </summary>
        public string ResponseContent { get; set; }

        /// <summary>
        /// This property can be set with the content of the response
        /// message (message body).
        /// </summary>
        public byte[] ResponseContentBytes { get; set; }

        internal int ResponseContentLength = -1;

        /// <summary>
        /// This method takes a string and sets up the response message
        /// body accordingly. Parameter textType indicates the content
        /// type, e.g., text/plain, text/html, etc. This method sets the
        /// response status code to 200 (OK).
        /// This method is provided for convenience so that status code,
        /// content, and content type need not be set separately.
        /// 
        /// Preconditions
        ///     content != null
        ///     textType != null
        ///     textType.Length > 0
        /// </summary>
        /// <param name="content">HTTP message body as a string, which
        /// will be encoded as UTF-8.</param>
        /// <param name="textType">A MIME type that consists of text.
        /// </param>
        public void SetResponse(string content, string textType)
        {
            Contract.Requires(content != null);
            Contract.Requires(textType != null);
            Contract.Requires(textType.Length > 0);
            ResponseStatusCode = 200;    // OK
            ResponseContentType = textType;
            ResponseContent = content;
        }

        /// <summary>
        /// This method takes a byte array and sets up the response message
        /// body accordingly. Parameter contentType indicates the content
        /// type, e.g., application/octet-stream. This method sets the
        /// response status code to 200 (OK).
        /// This method is provided for convenience so that status code,
        /// content, and content type need not be set separately.
        /// 
        /// Preconditions
        ///     content != null
        ///     length >= 0
        ///     length lessOrEqual content.Length
        ///     contentType != null
        ///     contentType.Length > 0
        /// </summary>
        /// <param name="content"></param>
        /// <param name="length"></param>
        /// <param name="contentType"></param>
        public void SetResponse(byte[] content, int length, string contentType)
        {
            Contract.Requires(content != null);
            Contract.Requires(length >= 0);
            Contract.Requires(length <= content.Length);
            Contract.Requires(contentType != null);
            Contract.Requires(contentType.Length > 0);
            ResponseStatusCode = 200;    // OK
            ResponseContentType = contentType;
            ResponseContentBytes = content;
            ResponseContentLength = length;
        }
    }


    /// <summary>
    /// The delegate type RequestHandler determines the parameter
    /// (context) and result (void) that a method must have so that it
    /// can be added to a request routing collection.
    /// 
    /// Preconditions
    ///     context != null
    /// </summary>
    /// <param name="context">Context with its request and server state
    /// set up, but without response state yet.</param>
    public delegate void RequestHandler(RequestHandlerContext context);


    // RequestRouting

    /// <summary>
    /// One element of a request routing specification.
    /// </summary>
    public sealed class RoutingElement
    {
        internal RoutingElement next;
        internal string Method;
        internal string Path;
        internal bool Wildcard;
        internal RequestHandler Handler;

        internal RoutingElement(string method, string path, bool wildcard,
            RequestHandler handler)
        {
            Contract.Requires(method != null);
            Contract.Requires(method.Length >= 3);
            Contract.Requires(path != null);
            Contract.Requires(path.Length > 0);
            Contract.Requires(path[0] == '/');
            Contract.Requires(handler != null);
            Method = method;
            Path = path;
            Wildcard = wildcard;
            Handler = handler;
        }
    }

    /// <summary>
    /// An instance of class RequestRouting is automatically created as a
    /// property when a new HttpServer object is created. Because it
    /// implements the IEnumerable interface and provides an Add method,
    /// it supports C# collection initializers. This means that instead
    /// of explicitly calling the Add method with the parameters pattern
    /// and handler, an initializer with pattern and handler as elements
    /// can be used.
    /// </summary>
    public class RequestRouting : IEnumerable
    {
        RoutingElement first;

        class Enumerator : IEnumerator
        {
            RoutingElement first;
            RoutingElement current;

            internal Enumerator(RoutingElement first)
            {
                this.first = first;
            }

            object IEnumerator.Current
            {
                get
                {
                    return current;
                }
            }

            bool IEnumerator.MoveNext()
            {
                if (current == null)
                {
                    current = first;
                }
                else
                {
                    current = current.next;
                }
                return (current != null);
            }

            void IEnumerator.Reset()
            {
                current = null;
            }
        }

        public IEnumerator GetEnumerator()
        {
            return new Enumerator(first);
        }

        /// <summary>
        /// This method adds a new request routing element to the
        /// collection, consisting of a request pattern and a request
        /// handler.
        /// 
        /// Preconditions
        ///     pattern != null
        ///     pattern.Length >= 3
        ///     handler != null
        /// </summary>
        /// <param name="pattern">Request pattern (method, path).</param>
        /// <param name="handler">Request handler.</param>
        public void Add(string pattern, RequestHandler handler)
        {
            Contract.Requires(pattern != null);
            Contract.Requires(pattern.Length >= 3);
            Contract.Requires(handler != null);
            var e = Parse(pattern, handler);
            Contract.Requires(e != null);
            if (first == null)
            {
                first = e;
            }
            else
            {
                RoutingElement h = first;
                while (h.next != null) { h = h.next; }
                h.next = e;
            }
        }

        /// <summary>
        /// This method adds a new request routing element to the
        /// collection, consisting of a request pattern and a request
        /// handler.
        /// 
        /// Preconditions
        ///     method != null
        ///     method.Length >= 3
        ///     path != null
        ///     path.Length > 0
        ///     path[0] == '/'
        ///     handler != null
        /// </summary>
        /// <param name="method">Method of the request pattern.</param>
        /// <param name="path">Path of the request pattern.</param>
        /// <param name="wildcard">If true, the pattern accepts all URIs starting with the given pattern.</param>
        /// <param name="handler">Request handler.</param>
        /// <returns></returns>
        public RoutingElement Add(string method, string path, bool wildcard, RequestHandler handler)
        {
            Contract.Requires(method != null);
            Contract.Requires(method.Length >= 3);
            Contract.Requires(path != null);
            Contract.Requires(path.Length > 0);
            Contract.Requires(path[0] == '/');
            Contract.Requires(handler != null);
            var e = new RoutingElement(method, path, wildcard, handler);
            if (first == null)
            {
                first = e;
            }
            else
            {
                RoutingElement h = first;
                while (h.next != null) { h = h.next; }
                h.next = e;
            }
            return e;
        }

        /// <summary>
        /// Removes a routing element.
        /// 
        /// Preconditions
        ///   e != null
        ///   "e is in list of routing elements"
        /// </summary>
        /// <param name="e">Routing element to be removed.</param>
        public void Remove(RoutingElement e)
        {
            Contract.Requires(e != null);
            Contract.Requires(first != null);
            if (e == first)
            {
                first = e.next;
            }
            else
            {
                RoutingElement h = first;
                while (h.next != e) { h = h.next; Contract.Requires(h != null); }
                h.next = e.next;
            }
        }

        RoutingElement Parse(string p, RequestHandler handler)
        {
            string method;
            string path;
            bool wildcard;
            string[] s = p.Split(' ');
            if ((s.Length != 2) ||
                (s[0].Length <= 0) ||
                (s[1].Length <= 0))
            {
                return null;
            }
            method = s[0];
            int index = s[1].IndexOf('*');
            if (index == -1)
            {
                wildcard = false;
                path = s[1];
            }
            else if (index != s[1].Length - 1)
            {
                return null;
            }
            else
            {
                wildcard = true;
                path = s[1].Substring(0, index);
            }
            return new RoutingElement(method, path, wildcard, handler);
        }
    }


    // HTTP server

    /// <summary>
    /// An instance of class HttpServer represents a web service that
    /// handles HTTP requests at a particular port, or uses a relay server
    /// to make it accessible even without a public Internet address.
    /// </summary>
    public class HttpServer
    {
        /// <summary>
        /// Indicates whether server is already initialized.
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// Factory for producing streams to the remote host.
        /// </summary>
        public IStreamFactory StreamFactory { get; set; }

        /// <summary>
        /// Optional property, which determines whether a relay is used,
        /// and if one is used, what domain name is registered at the
        /// relay. By default, it is null.
        /// </summary>
        public string RelayDomain { get; set; }

        /// <summary>
        /// Mandatory property if a relay is used. The key is used for
        /// authenticating the device at the relay. The secret key is
        /// never sent over the network.
        /// If the server does not use a relay (see RelayDomain above),
        /// this property is ignored.
        /// </summary>
        public string RelaySecretKey { get; set; }

        /// <summary>
        /// Mandatory property. At least one request routing element
        /// should be added to this property to support at least one
        /// request URI.
        /// NEW
        /// When the server instance is created, an empty RequestRouting
        /// instance is created, so this property usually need not be
        /// set by the client.
        /// </summary>
        public RequestRouting RequestRouting { get; set; }

        /// <summary>
        /// Diagnostic information.
        /// </summary>
        public ServerDiagnostics Diagnostics { get; private set; }
        /// <summary>
        /// Determines whether exceptions in a request handler are
        /// caught or not. During debugging, you usually don't want
        /// exceptions to be caught. At run-time, you may want a
        /// different behavior.
        /// </summary>
        public bool CatchRequestFailures { get; set; }

        IStreamListener streamListener;
        string serviceRootPath;

        static void DebugPrint(string s)
        {
            // Microsoft.SPOT.Debug.Print(s); 
        }

        /// <summary>
        /// A server that receives and interprets HTTP requests, calls
        /// your request handlers, and returns HTTP responses.
        /// Unless its RelayDomain and RelaySecretKey are set up before
        /// opening it, it does not use a relay service. This means it
        /// only works on your local network, or you need to use port
        /// forwarding or a similar mechanism to make your device visible
        /// on the Internet at large.
        /// </summary>
        public HttpServer()
        {
            IsOpen = false;
            StreamFactory = null;
            RelayDomain = null;
            RelaySecretKey = null;
            RequestRouting = new RequestRouting();
            Diagnostics = null;
            serviceRootPath = null;
            CatchRequestFailures = false;
            DebugPrint("HttpServer: server created");
            DebugPrint("HttpServer()");  // added jcc
        }

        /// <summary>
        /// A server that receives and interprets HTTP requests, calls
        /// your request handlers, and returns HTTP responses.
        /// A relay service will be used to make the device accessible
        /// to the Internet at large, even through firewalls and NATs.
        /// 
        /// Preconditions
        ///   relayDomain != null
        ///   relayDomain.Length > 0
        ///   relaySecretKey != null
        ///   relaySecretKey.Length > 0
        /// </summary>
        /// <param name="relayDomain"></param>
        /// <param name="relaySecretKey"></param>
        public HttpServer(string relayDomain, string relaySecretKey)
        {
            Contract.Requires(relayDomain != null);
            Contract.Requires(relayDomain.Length > 0);
            Contract.Requires(relaySecretKey != null);
            Contract.Requires(relaySecretKey.Length > 0);
            IsOpen = false;
            StreamFactory = null;
            RelayDomain = relayDomain;
            RelaySecretKey = relaySecretKey;
            RequestRouting = new RequestRouting();
            Diagnostics = null;
            serviceRootPath = null;
            DebugPrint("HttpServer: server created");
            DebugPrint("HttpServer(string relayDomain, string relaySecretKey)");  // added jcc
            Open();
        }

        /// <summary>
        /// This method completes the initialization of the server. If a
        /// relay is used, it performs the first registration of the
        /// device at the relay. Before it is called, the server
        /// properties must have been set up. Normally, you don’t need to
        /// call this method, since it is called by Run if necessary.
        /// </summary>
        public void Open()
        {
            Contract.Requires(!IsOpen);
            const int relayPort = 80;
            const int localPort = 80;

            // default configuration for the book - must be removed for use on devices without Ethernet:
            StreamFactory = new SocketStreamFactory();

            Contract.Requires(StreamFactory != null);
            // Difference to GSIoT book: a stream factory MUST be
            // plugged in as part of the server's configuration!

            if (RelayDomain != null)
            {
                Contract.Requires(StreamFactory != null);
                Contract.Requires(RelayDomain.Length > 0);
                Contract.Requires(RelaySecretKey != null);
                Contract.Requires(RelaySecretKey.Length > 0);
                if ((RelayDomain == "gsioT-FFMQ-TTD5") ||
                    (RelayDomain == "<insert your relay domain here>"))
                {
                    throw new Exception(
                        "Please use your own relay domain!\r\n" +
                        "See http://www.gsiot.info/yaler/ for more information on how to\r\n" +
                        "get your own relay domain and secret relay key.");
                }
                DebugPrint("StreamFactory.Listen(\"try.yaler.net\", relayPort, RelayDomain, RelaySecretKey)");  // added jcc
                streamListener = StreamFactory.Listen("try.yaler.net", relayPort, RelayDomain, RelaySecretKey);
            }
            else
            {
                DebugPrint("StreamFactory.Listen(localPort)");  // added jcc
                streamListener = StreamFactory.Listen(localPort);
            }
            Contract.Ensures(streamListener != null);
            Contract.Requires(RequestRouting != null);

            serviceRootPath = streamListener.LocalUrl;
            Trace.TraceInformation("Base Uri: " + serviceRootPath + "/");

            Diagnostics = new ServerDiagnostics();
            Diagnostics.StartTime = DateTime.Now;

            IsOpen = true;
            DebugPrint("HttpServer: server opened");
        }

        public void Check()
        {
            Open();
        }

        public void Close()
        {
            IsOpen = false;
            if (streamListener != null)
            {
                streamListener.Dispose();
            }
            streamListener = null;
            StreamFactory = null;
            DebugPrint("HttpServer: server closed");
        }

        /// <summary>
        /// Add a new request routing element while server is already running.
        /// 
        /// Precondition
        ///   pattern != null
        ///   pattern.Length >= 3
        ///   handler != null
        /// </summary>
        /// <param name="pattern">Request pattern.</param>
        /// <param name="handler">Request handler.</param>
        public void Add(string pattern, RequestHandler handler)
        {
            Contract.Requires(pattern != null);
            Contract.Requires(pattern.Length >= 3);
            Contract.Requires(handler != null);
            RequestRouting.Add(pattern, handler);
        }

        /// <summary>
        /// This method calls Open if it was not called already by the
        /// application, and then enters an endless loop where it
        /// repeatedly waits for incoming requests, accepts them, and
        /// performs the necessary processing for handling the request.
        /// </summary>
        public void Run()
        {
            if (!IsOpen) { Open(); }
            DebugPrint("HttpServer: running");
            while (IsOpen)      // IsOpen is never set to false, except in Close
            {
                Stream connection = null;
                do      // open connection, then handle one or more requests, until connection is closed by client or by request handler
                {
                    // wait for next request until this succeeds
                    while (connection == null)
                    {
                        try
                        {
                            connection = streamListener.Accept();
                            DebugPrint("connection opened");
                        }
                        catch (IOException e)
                        {
                            // e.g. DNS lookup or connect to relay server may fail sporadically
                            Trace.TraceError("HttpServer: stream error in Gsiot.Server.HttpServer.Run.Accept:\r\n" + e.Message);
                            Contract.Assert(connection == null);
                            Diagnostics.AcceptErrors = Diagnostics.AcceptErrors + 1;
                            Trace.TraceInformation("HttpServer: recovering from DNS lookup failure...");
                            Thread.Sleep(1000);
                        }
                        catch (Exception e)
                        {
                            Trace.TraceError("HttpServer: exception in Run.Accept (possibly Yaler refresh):\r\n" + e);
                            Diagnostics.AcceptFailures = Diagnostics.AcceptFailures + 1;
                        }
                    }
                    // connection != null

                    // handle request
                    var connectionClose = false;
                    try
                    {
                        Diagnostics.RequestsTotal = Diagnostics.RequestsTotal + 1;
                        ConsumeRequest(connection, serviceRootPath,
                            RelayDomain, RequestRouting,
                            CatchRequestFailures,
                            ref connectionClose);
                    }
                    catch (IOException e)
                    {
                        // possibly device was disconnected, or host has sent no data (read timeout)
                        DebugPrint("HttpServer: stream error in Run.ConsumeRequest:\r\n" + e.Message);
                        Diagnostics.RequestHandlerErrors = Diagnostics.RequestHandlerErrors + 1;
                    }
                    if (connectionClose)
                    {
                        connection.Close();
                        connection = null;
                        DebugPrint("HttpServer: connection closed");
                    }
                } while (connection != null);
            }
            DebugPrint("HttpServer: server connection has been closed");
        }

        /// <summary>
        /// Private method that handles an incoming request.
        /// It sets up a RequestHandlerContext instance with the data from
        /// the incoming HTTP request, finds a suitable request handler to
        /// produce the response, and then sends the response as an HTTP
        /// response back to the client.
        /// Preconditions
        ///     "connection is open"
        ///     serviceRoot != null
        ///     serviceRoot.Length > 8
        ///     "serviceRoot starts with 'http://' and ends with '/'"
        ///     requestRouting != null
        /// </summary>
        /// <param name="connection">Open TCP/IP connection</param>
        /// <param name="serviceRoot">The absolute URI that is a prefix of
        /// all request URIs that this web service supports. It must start
        /// with "http://" and must end with "/".</param>
        /// <param name="relayDomain">Host name or Internet address of the
        /// relay to be used, or null if no relay is used</param>
        /// <param name="requestRouting">Collection of
        ///   { request pattern, request handler}
        /// pairs</param>
        /// <param name="connectionClose">Return parameter that indicates
        /// that the connection should be closed after this call. This may
        /// be because the incoming request has a "Connection: close"
        /// header, because the request handler has set the
        /// ConnectionClose property, or because some error occurred.
        /// </param>
        internal static void ConsumeRequest(Stream connection,
            string serviceRoot, string relayDomain,
            RequestRouting requestRouting,
            bool catchRequestFailures,
            ref bool connectionClose)
        {
            Contract.Requires(connection != null);
            Contract.Requires(serviceRoot != null);
            Contract.Requires(serviceRoot.Length > 8);
            Contract.Requires(serviceRoot.Substring(0, 7) == "http://");
            Contract.Requires(serviceRoot[serviceRoot.Length - 1] != '/');
            Contract.Requires(requestRouting != null);

            // initialization --------------------------------------------
            DebugPrint("HttpServer: ConsumeRequest - initialize");
            HttpReader reader = new HttpReader();
            HttpWriter writer = new HttpWriter();
            var context = new RequestHandlerContext(serviceRoot,
                relayDomain);

            // receive request -------------------------------------------
            reader.Attach(connection);

            // read request line
            DebugPrint("HttpServer: ConsumeRequest - read request line");
            string httpMethod;
            string requestUri;
            string httpVersion;

            reader.ReadStringToBlank(out httpMethod);
            reader.ReadStringToBlank(out requestUri);
            if (reader.Status == HttpStatus.RequestUriTooLong)
            {
                context.ResponseStatusCode = 414;    // Request-URI Too Long
                context.ResponseContentType = "text/plain";
                context.ResponseContent = "request URI too long";
                reader.Detach();
                connectionClose = true;
                DebugPrint("HttpServer: ConsumeRequest - request URI too long!");
                return;
            }
            reader.ReadFieldValue(out httpVersion);
            if (reader.Status != HttpStatus.BeforeContent)  // error
            {
                reader.Detach();
                connectionClose = true;
                DebugPrint("HttpServer: ConsumeRequest - could not read HTTP version!");
                return;
            }

            context.RequestMethod = httpMethod;
            context.RequestUri = requestUri;
            // ignore version

            // headers
            DebugPrint("HttpServer: ConsumeRequest - read headers");
            string fieldName;
            string fieldValue;
            int requestContentLength = -1;
            reader.ReadFieldName(out fieldName);
            while (reader.Status == HttpStatus.BeforeContent)
            {
                reader.ReadFieldValue(out fieldValue);
                if (fieldValue != null)
                {
                    Contract.Assert(reader.Status ==
                                    HttpStatus.BeforeContent);
                    if (fieldName == "Connection")
                    {
                        connectionClose =
                            (connectionClose ||
                            (fieldValue == "close") ||
                             (fieldValue == "Close"));
                    }
                    else if (fieldName == "Content-Type")
                    {
                        context.RequestContentType = fieldValue;
                        DebugPrint("HttpServer: ConsumeRequest - request Content-Type: " + fieldValue);
                    }
                    else if (fieldName == "Content-Length")
                    {
                        if (Utilities.TryParseUInt32(fieldValue,
                            out requestContentLength))
                        {
                            // content length is now known
                            DebugPrint("HttpServer: ConsumeRequest - request Content-Length: " + requestContentLength);
                        }
                        else
                        {
                            DebugPrint("HttpServer: ConsumeRequest - request syntax error");
                            //reader.Status = HttpStatus.SyntaxError;
                            reader.Detach();
                            connectionClose = true;
                            return;
                        }
                    }
                }
                else
                {
                    // it's ok to skip header whose value is too long
                }
                Contract.Assert(reader.Status == HttpStatus.BeforeContent);
                reader.ReadFieldName(out fieldName);
            }
            if (reader.Status != HttpStatus.InContent)
            {
                reader.Detach();
                connectionClose = true;
                return;
            }

            // content
            DebugPrint("HttpServer: ConsumeRequest - read content");
            context.RequestContentBytes = null;
            if (requestContentLength > 0)
            {
                // receive content
                context.RequestContentBytes = new byte[requestContentLength];
                int toRead = requestContentLength;
                var read = 0;
                while ((toRead > 0) && (read >= 0))
                {
                    // already read: requestContentLength - toRead
                    read = reader.ReadContent(context.RequestContentBytes,
                        requestContentLength - toRead, toRead);
                    if (read < 0) { break; }    // timeout or shutdown
                    toRead = toRead - read;
                }
            }

            reader.Detach();
            if (reader.Status != HttpStatus.InContent)
            {
                connectionClose = true;
                return;
            }

            // delegate request processing to a request handler ----------
            DebugPrint("HttpServer: ConsumeRequest - call request handler");
            var match = false;
            foreach (RoutingElement e in requestRouting)
            {
                if (context.RequestMatch(e))
                {
                    context.ConnectionClose = false;
                    context.ResponseStatusCode = -1;                                    // undefined
                    Contract.Requires(context.ResponseContentType != null);
                    Contract.Requires(context.ResponseContentType == "text/plain");     // default

                    e.Handler(context);
                    if (catchRequestFailures)
                    {
                        try
                        {
                            e.Handler(context);
                        }
                        catch (Exception h)
                        {
                            DebugPrint("HttpServer: exception in request handler: " + h);
                        }
                    }
                    else
                    {
                        e.Handler(context);
                    }

                    Contract.Ensures(context.ResponseStatusCode >= 100);
                    Contract.Ensures(context.ResponseStatusCode < 600);
                    Contract.Ensures(context.ResponseContentType != null);
                    Contract.Ensures(context.ResponseContentType.Length > 0);
                    connectionClose = connectionClose || context.ConnectionClose;
                    match = true;
                    break;
                }
            }
            if (!match)
            {
                context.ResponseStatusCode = 404;    // Not Found
                context.ResponseContentType = "text/plain";
                context.ResponseContent = "404 error - resource not found";
                DebugPrint("HttpServer: no matching request handler found");
            }
            Contract.Assert(context.ResponseContentType != null);

            // send response ---------------------------------------------
            DebugPrint("HttpServer: ConsumeRequest - send response");
            writer.Attach(connection);

            // status line
            DebugPrint("HttpServer: ConsumeRequest - send status line");
            writer.WriteString("HTTP/1.1 ");
            writer.WriteString(context.ResponseStatusCode.ToString());
            writer.WriteLine(" ");  // omit optional reason phrase

            // headers
            DebugPrint("HttpServer: ConsumeRequest - send headers");
            if (connectionClose)
            {
                writer.WriteLine("Connection: close");
            }
            if (context.ResponseMaxAge > 0)
            {
                writer.WriteLine("Cache-Control: max-age=" + context.ResponseMaxAge);
            }
            else if (context.ResponseMaxAge == 0)
            {
                writer.WriteLine("Cache-Control: no-cache");
            }
            writer.WriteString("Content-Type: ");
            writer.WriteLine(context.ResponseContentType);
            DebugPrint("HttpServer: ConsumeRequest - response Content-Type: " + context.ResponseContentType);

            if (context.ResponseContent != null)
            {
                DebugPrint("HttpServer: ConsumeRequest - response content is text");
                context.ResponseContentBytes =
                    Encoding.UTF8.GetBytes(context.ResponseContent);
                Contract.Assert(context.ResponseContentBytes != null);
            }
            if (context.ResponseContentBytes != null)
            {
                DebugPrint("HttpServer: ConsumeRequest - content available as binary");
                if (context.ResponseContentLength == -1)
                {
                    context.ResponseContentLength = context.ResponseContentBytes.Length;    // default is to take the entire buffer
                }
                writer.WriteString("Content-Length: ");
                writer.WriteLine(context.ResponseContentLength.ToString());
                DebugPrint("HttpServer: ConsumeRequest - response Content-Length: " + context.ResponseContentLength);
            }
            else
            {
                writer.WriteString("Content-Length: 0");
                DebugPrint("HttpServer: ConsumeRequest - response Content-Length: 0");
            }

            // content
            DebugPrint("HttpServer: ConsumeRequest - send content");
            writer.WriteBeginOfContent();
            if (context.ResponseContentBytes != null)    // send content
            {
                writer.WriteContent(context.ResponseContentBytes, 0, context.ResponseContentLength);
            }
            DebugPrint("HttpServer: response sent");

            writer.Detach();

            uint availableMemory = Microsoft.SPOT.Debug.GC(true);   // TODO define portable abstraction for free memory
            Trace.TraceInformation(context.RequestMethod + " " +
                                   context.RequestUri + " -> " +
                                   context.ResponseStatusCode + " [" + availableMemory + "]");
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////

        void ConsumeRequestItem(object state)
        {
            Contract.Requires(state != null);
            Contract.Requires(state is Stream);
            var connection = (Stream)state;

            var i = 0;
            while (connection != null)
            {
                i = i + 1;

                // handle request
                try
                {
                    Diagnostics.RequestsTotal = Diagnostics.RequestsTotal + 1;
                    var connectionClose = false;
                    // ignore connectionClose, i.e., always
                    // close connection after a request
                    ConsumeRequest(connection, serviceRootPath,
                        RelayDomain, RequestRouting,
                        CatchRequestFailures,
                        ref connectionClose);
                    if (connectionClose)
                    {
                        connection.Close();
                        connection = null;
                    }
                }
                catch (IOException e)
                {
                    // possibly device was disconnected, or host has sent no data (read timeout)
                    Trace.TraceError("HttpServer: stream error in ConsumeRequestItem.ConsumeRequest:\r\n" + e.Message);
                    Diagnostics.RequestHandlerErrors = Diagnostics.RequestHandlerErrors + 1;
                    connection.Close();
                    connection = null;
                }
            }
            DebugPrint("HttpServer: exit ConsumeRequestItem");
        }

        void ProduceRequestItems(object state)
        {
            if (!IsOpen) { Open(); }
            while (IsOpen)
            {
                // wait for next request
                Stream connection = null;
                try
                {
                    connection = streamListener.Accept();
                    Contract.Ensures(connection != null);
                }
                catch (IOException e)
                {
                    Contract.Assert(connection == null);
                    // possibly device was disconnected
                    Trace.Fail("stream error in ProduceRequestItems.Accept:\r\n" + e.Message);
                    Diagnostics.AcceptErrors = Diagnostics.AcceptErrors + 1;
                    Thread.Sleep(500);
                }
                catch (Exception e)
                {
                    Contract.Assert(connection == null);
                    Trace.Fail("exception in ProduceRequestItems.Accept:\r\n" + e);
                    Diagnostics.AcceptFailures = Diagnostics.AcceptFailures + 1;
                    Thread.Sleep(500);
                }
                if (connection != null)
                {
                    ThreadPool.QueueUserWorkItem(ConsumeRequestItem, connection);
                }
            }
            DebugPrint("ProduceRequestItems: closed");
        }

        public void Start(int nofThreads)
        {
            Trace.TraceInformation("HttpServer: start with " + nofThreads + " thread(s)");
            Contract.Requires(nofThreads > 0);
            ThreadPool.Open(nofThreads + 1);    // add one thread for the server itself
            ThreadPool.Start();
            ThreadPool.QueueUserWorkItem(ProduceRequestItems, null);
        }

        public void Start()
        {
            Start(1);
        }


        //Thread oneThread;
        //
        //public void StartOne()
        //{
        //    oneThread = new Thread(Run);
        //    oneThread.Start();
        //}
    }


    class ServerUtilities
    {
        internal static bool TryParseUInt32(string s, out int result)
        {
            Contract.Requires(s != null);
            result = 0;
            if (s.Length > 0)
            {
                var r = 0;
                foreach (char c in s)
                {
                    if ((c < '0') || (c > '9')) { return false; }
                    var n = (int)(c - '0');
                    r = (r * 10) + n;
                }
                result = r;
                return true;
            }
            return false;
        }
    }
}
