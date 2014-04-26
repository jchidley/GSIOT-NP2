using Configuration;
using Gsiot.XivelyClient;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System;
using System.Threading;

class HelloXively
{
    static void Main()
    {
        const int samplingPeriod = 6000;   // 6 seconds

        var voltagePort = new AnalogInput(Parameters.AnalogPin);
        var lowPort = new OutputPort(Parameters.LowPin, false);
        var highPort = new OutputPort(Parameters.HighPin, true);

        voltagePort.Scale = 3.3;                    // convert to Volt

        while (true)
        {
            WaitUntilNextPeriod(samplingPeriod);
            double value = voltagePort.Read();
            string sample = "voltage," + value.ToString("f");
            Debug.Print("new message: " + sample);
            XivelyClient.Send(Parameters.ApiKey, Parameters.FeedId, sample);
        }
    }

    static void WaitUntilNextPeriod(int period)
    {
        long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        var offset = (int)(now % period);
        int delay = period - offset;
        Debug.Print("sleep for " + delay + " ms\r\n");
        Thread.Sleep(delay);
    }
}
