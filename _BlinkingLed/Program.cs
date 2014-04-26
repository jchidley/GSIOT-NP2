using Configuration;
using Microsoft.SPOT.Hardware;
using System.Threading;

class BlinkingLed
{
    static void Main()
    {
        var ledPort = new OutputPort(Parameters.LedPin, false);

        while (true)
        {
            ledPort.Write(true);    // turn on LED
            Thread.Sleep(500);      // wait 500 ms

            ledPort.Write(false);   // turn off LED
            Thread.Sleep(500);      // wait 500 ms
        }
    }
}

