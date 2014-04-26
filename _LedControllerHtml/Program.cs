using Configuration;
using Gsiot.Server;

class LedControllerHtml
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
                },
                {
                    "GET /led/target.html",
                    HandleLedTargetHtml
                }
            }
        };

        webServer.Run();
    }

    static void HandleLedTargetHtml(RequestHandlerContext context)
    {
        string requestUri = context.BuildRequestUri("/led/target");
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
                      r.setRequestHeader('Content-Type', 'text/plain');
                      r.send(content);
                    }
                  </script>
                </head>
                <body>
                  <p>
                    <input type=""button"" value=""Switch LED on""  
                      onclick=""put('true')""/>
                    <input type=""button"" value=""Switch LED off"" 
                      onclick=""put('false')""/>
                    <input type=""button"" value=""Bah"" 
                      onclick=""put('bah')""/>
                  </p>
                </body>
             </html>";
        context.SetResponse(script, "text/html");
    }
}
