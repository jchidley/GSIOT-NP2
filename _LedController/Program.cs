using Configuration;
using Gsiot.Server;

class LedController
{
    static void Main()
    {
        var ledActuator = new DigitalActuator
        {
            OutputPin = Parameters.LedPin
        };

        var webServer = new HttpServer
        {
            // RelayDomain = Parameters.RelayDomain,
            // RelaySecretKey = Parameters.RelaySecretKey,
            RequestRouting =
            {
                {
                    "PUT /led/target",
                    new ManipulatedVariable
                    {
                        FromHttpRequest =
                            CSharpRepresentation.TryDeserializeBool,
                        ToActuator = ledActuator.HandlePut
                    }.HandleRequest
                }
            }
        };

        webServer.Run();
    }
}