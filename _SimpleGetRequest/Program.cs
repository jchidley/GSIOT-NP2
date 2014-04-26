using Configuration;
using Microsoft.SPOT;
using System.IO;
using System.Net;
using System.Text;

class SimpleGetRequest
{
    static void Main()
    {
        // produce request
        var requestUri =
            "http://api.xively.com/v2/feeds/" + Parameters.FeedId + ".csv";
        using (var request = (HttpWebRequest)WebRequest.Create(requestUri))
        {
            request.Method = "GET";

            // headers
            request.Headers.Add("X-ApiKey", Parameters.ApiKey);

            // send request and receive response
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                // consume response
                HandleResponse(response);
            }
        }
    }

    public static void HandleResponse(HttpWebResponse response)
    {
        // response status line
        Debug.Print("HTTP/" + response.ProtocolVersion + " " +
                    response.StatusCode + " " +
                    response.StatusDescription);

        // response headers
        string[] headers = response.Headers.AllKeys;
        foreach (string name in headers)
        {
            Debug.Print(name + ": " + response.Headers[name]);
        }

        // response body
        var buffer = new byte[(int)response.ContentLength];
        Stream stream = response.GetResponseStream();
        int toRead = buffer.Length;
        while (toRead > 0)
        {
            // already read: buffer.Length - toRead
            int read = stream.Read(buffer, buffer.Length - toRead, toRead);
            toRead = toRead - read;
        }
        char[] chars = Encoding.UTF8.GetChars(buffer);
        Debug.Print(new string(chars));
    }
}
