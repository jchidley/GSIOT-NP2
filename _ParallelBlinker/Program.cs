using Configuration;
using Gsiot.Server;
using Microsoft.SPOT.Hardware;
using System.Threading;

class ParallelBlinker
{
    static void Main()
    {
        var buffer = new Buffer { };

        var blinker = new Blinker { SourceBuffer = buffer };

        // PowerShell PS> Invoke-WebRequest -Uri "http://try.yaler.net/gsiot-8a3m-5w8t/blinkingPeriod/target" -Method Put -Body 100 -TimeoutSec 2
        var webServer = new HttpServer
        {
            RelayDomain = Parameters.RelayDomain,
            RelaySecretKey = Parameters.RelaySecretKey,
            RequestRouting =
            {
                {
                    "PUT /blinkingPeriod/target",
                    new ManipulatedVariable
                    {
                        FromHttpRequest =
                            CSharpRepresentation.TryDeserializeInt,
                        ToActuator = buffer.HandlePut
                    }.HandleRequest
                },
                {
                    "GET /blinkingPeriod/target.html",
                    HandleBlinkTargetHtml
                }
            }
        };

        var blinkerThread = new Thread(blinker.Run);
        blinkerThread.Start();
        webServer.Run();
    }

    static void HandleBlinkTargetHtml(RequestHandlerContext context)
    {
        string requestUri =
            context.BuildRequestUri("/blinkingPeriod/target");
        var script =
            @"<html>
                <head>
                  <script type=""text/javascript"">
                    var r;
                    try {
                      r = new XMLHttpRequest();
                    } catch (e) {
                      r = new ActiveXObject('Microsoft.XMLHTTP');
                    }
                    function put (content) {
                      r.open('PUT', '" + requestUri + @"');
                      r.setRequestHeader(""Content-Type"", ""text/plain"");
                      r.send(document.getElementById(""period"").value);
                    }
                  </script>
                </head>
                <body>
                  <p>
                    <input type=""text"" value=""500"" id=""period"">
                    <input
                      type=""button"" value=""Set"" onclick=""put()""/>
                  </p>
                </body>
              </html>";
        context.SetResponse(script, "text/html");
    }

}

public class Blinker
{
    public Buffer SourceBuffer { get; set; }

    public void Run()
    {
        var ledPort = new OutputPort(Parameters.LedPin, false);
        var period = 500;
        var on = true;
        while (true)
        {
            Thread.Sleep(period / 2);

            object setpoint = SourceBuffer.HandleGet();
            if (setpoint != null)
            {
                period = (int)setpoint;
                period = period > 10000 ? 10000 : period;
                period = period < 20 ? 20 : period;
            }

            on = !on;
            ledPort.Write(on);
        }
    }
}

