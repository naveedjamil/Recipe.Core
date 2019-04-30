using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Recipe.NetCore.Extensions
{
    public static class HttpRequestExtensions
    {
        /// <summary>
        /// Retrieve the raw body as a string from the Request.Body stream
        /// </summary>
        /// <param name="request">Request instance to apply to</param>
        /// <param name="encoding">Optional - Encoding, defaults to UTF8</param>
        /// <returns></returns>
        public static async Task<string> GetRawBodyStringAsync(this HttpRequest request, Encoding encoding = null)
        {
            var streamCopy = new MemoryStream();
            request.Body.CopyTo(streamCopy);
            streamCopy.Position = 0; // rewind

            var body = await new StreamReader(streamCopy).ReadToEndAsync();

            streamCopy.Position = 0; // rewind again
            request.Body = streamCopy; // put back in place for downstream handlers

            return body;
        }
    }
}
