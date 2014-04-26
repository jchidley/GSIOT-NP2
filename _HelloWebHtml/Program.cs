using Configuration;
using Gsiot.Server;
using System;

class HelloWebHtml
{
    static void Main()
    {
        // PowerShell PS> Invoke-WebRequest -Uri "http://try.yaler.net/gsiot-8a3m-5w8t/hello.html" -Method Get
        // or 
        var webServer = new HttpServer
        {
            // RelayDomain = Parameters.RelayDomain,
            // RelaySecretKey = Parameters.RelaySecretKey,
            RequestRouting =
            {
                { "GET /hello.html", HandleGetHelloHtml },
                { "GET /about.html", HandleGetAboutHtml }
            }
        };
        webServer.Run();
    }

    static void HandleGetHelloHtml(RequestHandlerContext context)
    {
        string s =
            "<html>\r\n" +
            "\t<body>\r\n" +
            "\t\tHello <strong>Web</strong> at " +
                DateTime.Now + "\r\n" +
            "\t</body>\r\n" +
            "</html>";
        context.SetResponse(s, "text/html");
    }

    static void HandleGetAboutHtml(RequestHandlerContext context)
    {
        string s =
            "<html>\r\n" +
            "\t<body>\r\n" +
            "\t\t<h1>About</h1>\r\n" +
            "\t\t<p>This is a Netduino Plus 2 running .net Micro Framework</p>\r\n" +
            "\t</body>\r\n" +
            "</html>";
        context.SetResponse(s, "text/html");
    }
}