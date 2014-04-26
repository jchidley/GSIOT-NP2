using Configuration;
using Microsoft.SPOT;
using System.IO;
using System.Net;
using System.Text;

namespace GsiotNP2
{
    public class SimplePutRequest
    {
        public static void Main()
        {
            // this is the "sample" we want to send to Xively
            var sample = "SimplePutRequest," + Debug.GC(true);
            // var sample = "SimplePutRequest,42";
            // convert sample to byte array
            byte[] buffer = Encoding.UTF8.GetBytes(sample);
            
            // produce request
            var requestUri =
                "http://api.xively.com/v2/feeds/" + Parameters.FeedId + ".csv";
            using (var request = (HttpWebRequest)WebRequest.Create(requestUri))
            {
                request.Method = "PUT";

                // headers
                request.ContentType = "text/csv";
                request.ContentLength = buffer.Length;
                request.Headers.Add("X-ApiKey", Parameters.ApiKey);

                // content
                Stream s = request.GetRequestStream();
                s.Write(buffer, 0, buffer.Length);

                // send request and receive response
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    // consume response
                    Debug.Print("Status code: " + response.StatusCode);
                }
            }
        }

    }
}
