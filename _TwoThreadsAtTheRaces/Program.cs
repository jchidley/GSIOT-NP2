using System;
using System.Threading;

class TwoThreadsAtTheRaces
{
    static int x;

    static void Main()
    {
        var thread1 = new Thread(Activity1);
        var thread2 = new Thread(Activity2);

        thread1.Start();
        thread2.Start();

        Thread.Sleep(Timeout.Infinite);
        // sooner or later, this program will throw an
        // exception due to a race condition
    }

    static void Activity1()
    {
        while (true)
        {
            x = 0;
            if (x != 0) { throw new Exception(); }
        }
    }

    static void Activity2()
    {
        while (true)
        {
            x = 1;
            if (x != 1) { throw new Exception(); }
        }
    }
}

