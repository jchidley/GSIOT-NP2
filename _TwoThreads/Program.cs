using System.Threading;
using Microsoft.SPOT;

class TwoThreads
{
    static void Main()
    {
        var thread1 = new Thread(EvenActivity);
        var thread2 = new Thread(OddActivity);

        thread1.Start();
        thread2.Start();

        Thread.Sleep(Timeout.Infinite);
    }

    static void EvenActivity()
    {
        var x = 0;      // even number
        while (true)
        {
            Debug.Print(x.ToString());
            x = x + 2;
            Thread.Sleep(200);
        }
    }

    static void OddActivity()
    {
        var x = 1;      // odd number
        while (true)
        {
            Debug.Print("     " + x);
            x = x + 2;
            Thread.Sleep(300);
        }
    }
}

