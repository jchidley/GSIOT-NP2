using Configuration;
using Gsiot.Server;

class HelloWeb
{
    static void Main()
    {
        var webServer = new HttpServer
        {
            RelayDomain = Parameters.RelayDomain,
            RelaySecretKey = Parameters.RelaySecretKey,

            RequestRouting =
            {
                { "GET /hello", context =>
                      { context.SetResponse("Hello Web", "text/plain"); }
                },
                
                { "GET /about", context =>
                      { context.SetResponse("Netduino Plus 2 running .net micro framework", "text/plain"); }
                }
 
            }
        };
        webServer.Run();
    }
}