using System;
using System.Threading;

class TwoThreadsInTheLocks
{
    static int x = 0;

    static object monitor = new object();

    static void Main()
    {
        var thread1 = new Thread(Activity1);
        var thread2 = new Thread(Activity2);

        thread1.Start();
        thread2.Start();

        Thread.Sleep(Timeout.Infinite);
    }

    static void Activity1()
    {
        while (true)
        {
            lock (monitor)
            {
                x = 0;
                if (x != 0) { throw new Exception(); }
            }
        }
    }

    static void Activity2()
    {
        while (true)
        {
            lock (monitor)
            {
                x = 1;
                if (x != 1) { throw new Exception(); }
            }
        }
    }
}
