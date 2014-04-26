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

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace System.Threading
{
    public delegate void WaitCallback(object state);

    internal class WorkItem
    {
        internal readonly WaitCallback callback;
        internal readonly object state;

        internal WorkItem(WaitCallback callback, object state)
        {
            Contract.Requires(callback != null);
            this.callback = callback;
            this.state = state;
        }
    }

    public static class ThreadPool
    {
        static Thread[] threads;
        static BoundedQueue workItemQueue;

        public static void Open(int nofThreads)
        {
            Contract.Requires(nofThreads > 0);
            threads = new Thread[nofThreads];
            var i = 0;
            while (i != nofThreads)
            {
                threads[i] = new Thread(Run);
                threads[i].Priority = ThreadPriority.Lowest;
                i = i + 1;
            }
            workItemQueue = new BoundedQueue { Capacity = nofThreads };
        }

        public static void Start()
        {
            var i = 0;
            while (i != threads.Length)
            {
                threads[i].Start();
                i = i + 1;
            }
        }

        public static bool QueueUserWorkItem(WaitCallback callback,
                                                object state)
        {
            Contract.Requires(callback != null);
            Contract.Requires(threads != null);
            var item = new WorkItem(callback, state);
            workItemQueue.Enqueue(item);
            return true;
        }

        static void Run()
        {
            while (true)
            {
                var obj = workItemQueue.Dequeue();      // may block!
                var item = (WorkItem)obj;
                try
                {
                    item.callback(item.state);
                }
                catch (Exception e)
                {
                    Trace.Fail("ThreadPool.Run exception in callback:\r\n" + e);
                }
            }
        }
    }
    static class BigLock
    {
        internal readonly static object Lock = new object();
    }

    internal sealed class BoundedQueue
    {
        AutoResetEvent nonEmptySignal = new AutoResetEvent(false);
        AutoResetEvent nonFullSignal = new AutoResetEvent(false);

        int capacity = 0;           // where (capacity >= 0)
        Queue queue = new Queue();  // where (queue != null)

        internal int Capacity         // once set, remains immutable
        {
            get { return capacity; }

            set
            {
                Contract.Requires(value > 0);
                capacity = value;
            }
        }

        internal void Enqueue(object o)
        {
            Contract.Requires(capacity > 0);
            // this method may block
            int count;
            lock (BigLock.Lock)
            {
                count = queue.Count;
                while (count == capacity)
                {
                    Monitor.Exit(BigLock.Lock);
                    nonFullSignal.WaitOne();
                    Monitor.Enter(BigLock.Lock);
                    count = queue.Count;
                }
                queue.Enqueue(o);
            }
            if (count == 0)
            {
                nonEmptySignal.Set();
            }
        }

        internal object Dequeue()
        {
            Contract.Requires(capacity > 0);
            // this method may block
            object o = null;
            int count;
            lock (BigLock.Lock)
            {
                count = queue.Count;
                while (count == 0)
                {
                    Monitor.Exit(BigLock.Lock);
                    nonEmptySignal.WaitOne();
                    Monitor.Enter(BigLock.Lock);
                    count = queue.Count;
                }
                o = queue.Dequeue();
            }
            if (count == capacity)
            {
                nonFullSignal.Set();
            }
            return o;
        }
    }
}
