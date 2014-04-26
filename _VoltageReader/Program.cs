using Configuration;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System.Threading;

class VoltageReader
{
    static void Main()
    {
        var voltagePort = new AnalogInput(Parameters.AnalogPin);
        var lowPort = new OutputPort(Parameters.LowPin, false);
        var highPort = new OutputPort(Parameters.HighPin, true);

        voltagePort.Scale = 3.3;                    // convert to Volt

        while (true)
        {
            double value = voltagePort.Read();
            Debug.Print(value.ToString("f"));       // fixed-point format
            Thread.Sleep(3000);                     // 3 seconds
        }
    }
}
