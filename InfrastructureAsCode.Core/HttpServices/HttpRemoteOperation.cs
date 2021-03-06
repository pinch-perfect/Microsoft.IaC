﻿using InfrastructureAsCode.Core.Enums;
using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace InfrastructureAsCode.Core.HttpServices
{
    public abstract class HttpRemoteOperation
    {
        #region CONSTRUCTOR

        public HttpRemoteOperation(string targetUrl, AuthenticationType authType, string user, string password, string domain = "")
        {
            this.TargetSiteUrl = targetUrl;
            this.AuthType = authType;
            this.User = user;
            this.Password = password;
            this.Domain = domain;

        }

        #endregion

        #region PROPERTIES

        public string TargetSiteUrl { get; set; }

        public AuthenticationType AuthType { get; set; }

        public string User { get; set; }

        public string Password { get; set; }

        public string Domain { get; set; }

        public abstract string OperationPageUrl { get; }

        public Dictionary<string, string> PostParameters = new Dictionary<string, string>();

        #endregion

        #region Methods


        public string ReadHiddenField(string pageHtml, string fieldName)
        {
            string result = "";
            string hiddenFieldFlag = string.Format("id=\"{0}\" value=\"", fieldName);
            int i = pageHtml.IndexOf(hiddenFieldFlag);
            if (i > -1)
            {
                i = i + hiddenFieldFlag.Length;
                int j = pageHtml.IndexOf("\"", i);
                result = HttpUtility.UrlEncode(pageHtml.Substring(i, j - i));
            }

            return result;
        }

        public string ReadInputFieldById(string pageHtml, string fieldName)
        {
            string result = "";
            string inputFieldFlag = string.Format("id=\"{0}\"", fieldName);
            int i = pageHtml.IndexOf(inputFieldFlag);
            if (i > -1)
            {
                i = i + inputFieldFlag.Length;
                int j = pageHtml.IndexOf("value=\"", i) + "value=\"".Length;
                int k = pageHtml.IndexOf("\"", j);
                result = HttpUtility.UrlEncode(pageHtml.Substring(j, k - j));
            }

            return result;
        }

        public string FormatOperationUrlString(string hostUrl, string operationPageUrl)
        {
            return hostUrl.TrimEnd(new char[] { '/' }) + operationPageUrl;
        }


        public void Execute()
        {
            try
            {
                string page = GetRequest();
                AnalyzeRequestResponse(page);
                //SetPostVariables();
                // MakePostRequest(page);
            }
            catch (Exception ex)
            {
                // TODO - Give better description for the exception
                Console.WriteLine(ex);
                throw new Exception("Execute failed.", ex);
            }
        }

        /// <summary>
        /// For optinal processing of the page in the inherited class
        /// </summary>
        /// <param name="page"></param>
        public abstract void AnalyzeRequestResponse(string page);


        /// <summary>
        /// To be implemented based on usage scenario
        /// </summary>
        public abstract void SetPostVariables();

        /// <summary>
        /// Handels the initial request of the page using given identity
        /// </summary>
        /// <returns></returns>
        protected string GetRequest()
        {
            string returnString = string.Empty;
            string url = FormatOperationUrlString(this.TargetSiteUrl, this.OperationPageUrl);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            //Set auth options based on auth model
            ModifyRequestBasedOnAuthPattern(request);

            // Set some reasonable limits on resources used by this request
            request.MaximumAutomaticRedirections = 6;
            request.MaximumResponseHeadersLength = 6;
            // Set user agent as valid text
            request.UserAgent = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)";

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                Encoding encode = System.Text.Encoding.GetEncoding("utf-8");

                Stream responseStream = response.GetResponseStream();
                if (response.ContentEncoding.ToLower().Contains("gzip"))
                {
                    responseStream = new GZipStream(responseStream, CompressionMode.Decompress);
                }
                else if (response.ContentEncoding.ToLower().Contains("deflate"))
                {
                    responseStream = new DeflateStream(responseStream, CompressionMode.Decompress);
                }

                // Get the response stream and store that as string
                using (StreamReader reader = new StreamReader(responseStream, encode))
                {
                    returnString = reader.ReadToEnd();
                    reader.Close();
                }
                response.Close();

                //Console.WriteLine(returnString);

                return returnString;
            }

        }

        /// <summary>
        /// Used to modify the HTTP request based on authentication type
        /// </summary>
        /// <param name="request"></param>
        private void ModifyRequestBasedOnAuthPattern(HttpWebRequest request)
        {

            // Change the model based on used auth type
            switch (this.AuthType)
            {
                case AuthenticationType.DefaultCredentials:
                    request.Credentials = CredentialCache.DefaultCredentials;
                    break;
                case AuthenticationType.NetworkCredentials:
                    NetworkCredential credential = new NetworkCredential(this.User, this.Password, this.Domain);
                    CredentialCache credentialCache = new CredentialCache();
                    credentialCache.Add(new Uri(this.TargetSiteUrl), "NTLM", credential);
                    request.Credentials = credentialCache;
                    break;
                case AuthenticationType.Office365:
                    // Convert password to secure string and create MSO Creds
                    var spoPassword = new SecureString();
                    foreach (char c in this.Password)
                    {
                        spoPassword.AppendChar(c);
                    }
                    SharePointOnlineCredentials Credentials = new SharePointOnlineCredentials(this.User, spoPassword);
                    Uri tenantUrlUri = new Uri(this.TargetSiteUrl);
                    string authCookieValue = Credentials.GetAuthenticationCookie(tenantUrlUri);
                    // Create fed auth Cookie and set that to http request properly to access Office365 site
                    Cookie fedAuth = new Cookie()
                    {
                        Name = "SPOIDCRL",
                        Value = authCookieValue.TrimStart("SPOIDCRL=".ToCharArray()),
                        Path = "/",
                        Secure = true,
                        HttpOnly = true,
                        Domain = new Uri(this.TargetSiteUrl).Host
                    };
                    // Hookup authentication cookie to request
                    request.CookieContainer = new CookieContainer();
                    request.CookieContainer.Add(fedAuth);
                    break;
                default:

                    break;
            }
        }

        /// <summary>
        /// Responsible of accessing the page and submitting the post. Generic handler for the post access
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        protected string MakePostRequest(string webPage)
        {
            // Add required stuff to validate the page request
            string postBody = "__REQUESTDIGEST=" + ReadHiddenField(webPage, "__REQUESTDIGEST") +
                                "&__EVENTVALIDATION=" + ReadHiddenField(webPage, "__EVENTVALIDATION") +
                                "&__VIEWSTATE=" + ReadHiddenField(webPage, "__VIEWSTATE");

            // Add operation specific parameters
            foreach (var item in this.PostParameters)
            {
                postBody = postBody + string.Format("&{0}={1}", item.Key, item.Value);
            }

            string results = string.Empty;

            try
            {
                string url = FormatOperationUrlString(this.TargetSiteUrl, this.OperationPageUrl);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                if (this.AuthType != AuthenticationType.Office365)
                {
                    // Get X-RequestDigest header for post. Required for most of the operations
                    request.Headers.Add("X-RequestDigest", GetUpdatedFormDigest(this.TargetSiteUrl));
                }

                // Note that this assumes that we use particular identity running the thread
                ModifyRequestBasedOnAuthPattern(request);

                // Set some reasonable limits on resources used by this request
                request.MaximumAutomaticRedirections = 6;
                request.MaximumResponseHeadersLength = 6;
                // Set credentials to use for this request.
                request.ContentType = "application/x-www-form-urlencoded";

                byte[] postByte = Encoding.UTF8.GetBytes(postBody);
                request.ContentLength = postByte.Length;
                Stream postStream = request.GetRequestStream();
                postStream.Write(postByte, 0, postByte.Length);
                postStream.Close();

                HttpWebResponse wResp = (HttpWebResponse)request.GetResponse();
                postStream = wResp.GetResponseStream();
                StreamReader postReader = new StreamReader(postStream);

                results = postReader.ReadToEnd();

                postReader.Close();
                postStream.Close();
            }
            catch (Exception ex)
            {
                // Give better description for the exception
                throw new Exception("MakePostRequest failed.", ex);
            }

            return results;
        }

        /// <summary>
        /// Used to get updated form digest for the operation. Required for most of the operations in on-prem or with Office365-D
        /// </summary>
        /// <param name="siteUrl">Url to access</param>
        /// <returns></returns>
        private string GetUpdatedFormDigest(string siteUrl)
        {
            try
            {
                string url = siteUrl.TrimEnd(new char[] { '/' }) + "/_vti_bin/sites.asmx";
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";

                // Note that this assumes that we use particular identity running the thread
                request.Credentials = CredentialCache.DefaultCredentials;

                // Set some reasonable limits on resources used by this request
                request.MaximumAutomaticRedirections = 6;
                request.MaximumResponseHeadersLength = 6;
                // Set credentials to use for this request.
                request.ContentType = "text/xml";

                var payload =
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                    "  <soap:Body>" +
                    "    <GetUpdatedFormDigest xmlns=\"http://schemas.microsoft.com/sharepoint/soap/\" />" +
                    "  </soap:Body>" +
                    "</soap:Envelope>";

                byte[] postByte = Encoding.UTF8.GetBytes(payload);
                request.ContentLength = postByte.Length;
                Stream postStream = request.GetRequestStream();
                postStream.Write(postByte, 0, postByte.Length);
                postStream.Close();

                HttpWebResponse wResp = (HttpWebResponse)request.GetResponse();
                postStream = wResp.GetResponseStream();
                StreamReader postReader = new StreamReader(postStream);

                string results = postReader.ReadToEnd();

                postReader.Close();
                postStream.Close();

                var startTag = "<GetUpdatedFormDigestResult>";
                var endTag = "</GetUpdatedFormDigestResult>";
                var startTagIndex = results.IndexOf(startTag);
                var endTagIndex = results.IndexOf(endTag, startTagIndex + startTag.Length);
                string newFormDigest = null;
                if ((startTagIndex >= 0) && (endTagIndex > startTagIndex))
                {
                    newFormDigest = results.Substring(startTagIndex + startTag.Length, endTagIndex - startTagIndex - startTag.Length);
                }
                return newFormDigest;

            }
            catch (Exception ex)
            {
                // TODO - Give better description for the exception
                throw new Exception("GetUpdatedFormDigest failed.", ex);
            }
        }
        #endregion
    }
}
