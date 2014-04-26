using Configuration;
using Gsiot.Server;
using Microsoft.SPOT.Hardware;

class VoltageMonitor
{
    static void Main()
    {
        var lowPort = new OutputPort(Parameters.LowPin, false);
        var highPort = new OutputPort(Parameters.HighPin, true);

        var voltageSensor = new AnalogSensor
        {
            InputPin = Parameters.AnalogPin,
            MinValue = 0.0,
            MaxValue = 3.3
        };

        var webServer = new HttpServer
        {
            RelayDomain = Parameters.RelayDomain,
            RelaySecretKey = Parameters.RelaySecretKey,
            RequestRouting =
            {
                {
                    "GET /voltage/actual",
                    new MeasuredVariable
                    {
                        FromSensor = voltageSensor.HandleGet
                    }.HandleRequest
                }
            }
        };

        webServer.Run();
    }
}
