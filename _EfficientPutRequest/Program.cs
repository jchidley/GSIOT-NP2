using Configuration;
using Microsoft.SPOT;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace GsiotNP2
{
    public class EfficientPutRequest
    {
        public static void Main()
        {
            // this is the "sample" we want to send to Xively
            var sample = "EfficientPutRequest," + Debug.GC(true);
            // var sample = "EfficientPutRequest,43";
            // convert sample to byte array
            byte[] contentBuffer = Encoding.UTF8.GetBytes(sample);
            const int timeout = 5000;       // 5 seconds

            // produce request
            using (Socket connection = Connect("api.xively.com", timeout))
            {
                SendRequest(connection, Parameters.ApiKey, Parameters.FeedId,
                            sample);
                Thread.Sleep(timeout + 1000); // wait at least until timeout 
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
    }

}