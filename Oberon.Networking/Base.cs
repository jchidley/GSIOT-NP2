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

using System;
using System.IO;

namespace Oberon.Networking
{
    public interface IStreamListener : IDisposable
    {
        string LocalHostName { get; }
        int LocalPort { get; }
        string LocalUrl { get; }
        Stream Accept();
    }

    public interface IStreamFactory : IDisposable
    {
        // client aspect
        Stream Connect(string remoteHostName, int remotePort);
        // server aspect
        IStreamListener Listen(int localPort);
        IStreamListener Listen(string relayHostName, int relayPort, string relayDomain, string relaySecretKey);
    }
}
