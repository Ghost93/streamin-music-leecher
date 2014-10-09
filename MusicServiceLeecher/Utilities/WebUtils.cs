using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace MusicServiceLeecher.Utilities
{
    public static class WebUtils
    {
        public static HttpWebResponse GetResponse(WebRequest request)
        {
            WebResponse response = request.GetResponse();
            HttpWebResponse httpResponse = response as HttpWebResponse;

            if (httpResponse == null)
            {
                throw new WebException("Failed casting response to http response", null, WebExceptionStatus.UnknownError, response);
            }

            if (httpResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new WebException("Something went wrong", null, WebExceptionStatus.ConnectFailure, response);
            }

            return httpResponse;
        }

        public static WebRequest CreateRequest(Uri uri,NameValueCollection headers=null)
        {
            WebRequest request = WebRequest.CreateHttp(uri);//uri.ToString().Replace("https","http"));
            if (headers != null)
            {
                request.Headers.Add(headers);
            }
            request.Method = "GET";
            return request;
        }

        public static HtmlDocument GetResponseHtml(HttpWebResponse httpResponse)
        {
            return GetResponseHtml(GetResponseText(httpResponse, Encoding.UTF8));
        }

        public static HtmlDocument GetResponseHtml(string responseText)
        {
            HtmlDocument responseHtml = new HtmlDocument();
            responseHtml.Load(new StringReader(responseText));
            return responseHtml;
        }

        public static string GetResponseText(HttpWebResponse httpResponse,Encoding encoding)
        {
            string res;
            using (StreamReader sr = new StreamReader(httpResponse.GetResponseStream(), encoding))
            {
                res = sr.ReadToEnd();
            }
            return res;
        }
    }
}
