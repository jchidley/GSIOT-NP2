using Configuration;
using Microsoft.SPOT.Hardware;
using System.Threading;

class LightSwitch
{
    static void Main()
    {
        var switchPort = new InputPort(Parameters.ButtonPin, false,
            Port.ResistorMode.Disabled);
        var ledPort = new OutputPort(Parameters.LedPin, false);

        while (true)
        {
            bool isClosed = switchPort.Read();
            if (isClosed)
            {
                ledPort.Write(true);
            }
            else
            {
                ledPort.Write(false);
            }
            Thread.Sleep(100);              // 100 milliseconds
        }
    }
}
