using System;
using Nancy;

namespace NancyKayakDemo
{
    public static class ResponseExtensions
    {

        /// <summary>
        /// Since Responce.Headers is a dictionary we can only set a single cookie value.
        /// To accommodate mutiple cookies we simply aggregate them in a newline delimited string.
        /// Kayak will split each header value on \r\n and render a new header line with the approprate key for each value
        /// </summary>
        /// <param name="response"></param>
        public static String GetAllCookies(this Response response)
        {
            var allCookies = new System.Text.StringBuilder();
            for (int i = 0; i < response.Cookies.Count; i++)
            {
                allCookies.Append(response.Cookies[i].ToString());
                if (i + 1 < response.Cookies.Count)
                {
                    //HTTP Spec says RN is to be used as in header 
                    //so we'll be sure we get that.
                    allCookies.Append("\r\n");
                }
            }
            return allCookies.ToString();
        }
    }
}
