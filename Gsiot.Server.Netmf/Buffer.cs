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
// Provides a mechanism for concurrent actors to exchange data in
// a thread-safe way.

using System.Threading;

namespace Gsiot.Server
{
    internal static class BigLock
    {
        internal readonly static object Lock = new object();
    }

    /// <summary>
    /// The Buffer class provides a means for parallel actors to exchange
    /// data in a thread-safe way. One actor puts objects into a buffer
    /// instance, the other fetches them from the buffer.
    /// This buffer class is able to buffer at most one object at a time:
    /// if the producer puts a new object into the buffer, its old contents
    /// is replaced by the new one. When the consumer gets the current
    /// buffer contents, the buffer is not modified in any way.
    /// This buffer class is intended for actors that are synchronized to
    /// real-time ("time-triggered"); no mechanism for direct
    /// synchronization between the actors is provided.
    /// </summary>
    public sealed class Buffer
    {
        object buffer = null;

        /// <summary>
        /// Puts object o, which may be null, into the buffer.
        /// The current buffer contents is overwritten with the new one.
        /// It is safe to call this method from different threads.
        /// </summary>
        /// <param name="o">New value to be buffered.</param>
        public void HandlePut(object o)
        {
            lock (BigLock.Lock)
            {
                buffer = o;
            }
        }

        /// <summary>
        /// Returns the object that is currently contained in the buffer,
        /// or null.
        /// It is safe to call this method from different threads.
        /// </summary>
        /// <returns>Currently buffered value. May be null.</returns>
        public object HandleGet()
        {
            object o;
            lock (BigLock.Lock)
            {
                o = buffer;
            }
            return o;
        }
    }
}
