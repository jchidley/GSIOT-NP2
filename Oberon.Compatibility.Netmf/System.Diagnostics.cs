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
// See Microsoft's API documentation for details.

using Microsoft.SPOT;

namespace System.Diagnostics
{
    public sealed class Trace
    {
        public static void TraceInformation(string message)
        {
            Debug.Print(DateTime.Now.ToString("u") + ", INFO,  " + message);
        }

        public static void TraceWarning(string message)
        {
            Debug.Print(DateTime.Now.ToString("u") + ", WARN,  " + message);
        }

        public static void TraceError(string message)
        {
            Debug.Print(DateTime.Now.ToString("u") + ", ERROR, " + message);
        }

        public static void Fail(string message)
        {
            Debug.Print(DateTime.Now.ToString("u") + ", FAIL,  " + message);
        }
    }
}