using Configuration;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class HelloXivelySockets
{
    static void Main()
    {
        const int samplingPeriod = 6000;   // 6 seconds

        var voltagePort = new AnalogInput(Parameters.AnalogPin);
        var lowPort = new OutputPort(Parameters.LowPin, false);
        var highPort = new OutputPort(Parameters.HighPin, true);

        voltagePort.Scale = 3.3;                    // convert to Volt

        Socket connection = null;

        while (true)   // main loop
        {
            WaitUntilNextPeriod(samplingPeriod);

            if (connection == null)   // create connection
            {
                try
                {
                    connection = Connect("api.xively.com",
                        samplingPeriod / 2);
                }
                catch
                {
                    Debug.Print("connection error");
                }
            }

            if (connection != null)
            {
                try
                {
                    double value = voltagePort.Read();
                    string sample = "HelloXivelySockets," + Debug.GC(true);
                    // string sample = "voltage," + value.ToString("f");
                    SendRequest(connection, Parameters.ApiKey,
                                Parameters.FeedId, sample);
                }
                catch (SocketException)
                {
                    connection.Close();
                    connection = null;
                }
            }
        }
    }

    static Socket Connect(string host, int timeout)
    {
        // look up host's domain name, to find IP address(es)
        IPHostEntry hostEntry = Dns.GetHostEntry(host);
        // extract a returned address
        IPAddress hostAddress = hostEntry.AddressList[0];
        IPEndPoint remoteEndPoint = new IPEndPoint(hostAddress, 80);

        // connect!
        Debug.Print("connect...");
        var connection = new Socket(AddressFamily.InterNetwork,
            SocketType.Stream, ProtocolType.Tcp);
        connection.Connect(remoteEndPoint);
        connection.SetSocketOption(SocketOptionLevel.Tcp,
            SocketOptionName.NoDelay, true);
        connection.SendTimeout = timeout;
        return connection;
    }

    static void SendRequest(Socket s, string apiKey, string feedId,
        string content)
    {
        byte[] contentBuffer = Encoding.UTF8.GetBytes(content);
        const string CRLF = "\r\n";
        var requestLine =
            "PUT /v2/feeds/" + feedId + ".csv HTTP/1.1" + CRLF;
        byte[] requestLineBuffer = Encoding.UTF8.GetBytes(requestLine);
        var headers =
            "Host: api.xively.com" + CRLF +
            "X-ApiKey: " + apiKey + CRLF +
            "Content-Type: text/csv" + CRLF +
            "Content-Length: " + contentBuffer.Length + CRLF +
            CRLF;
        byte[] headersBuffer = Encoding.UTF8.GetBytes(headers);
        s.Send(requestLineBuffer);
        s.Send(headersBuffer);
        s.Send(contentBuffer);
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
