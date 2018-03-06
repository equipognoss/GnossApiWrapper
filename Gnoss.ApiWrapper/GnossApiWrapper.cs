﻿using Es.Riam.Util;
using Gnoss.ApiWrapper.Exceptions;
using Gnoss.ApiWrapper.Helpers;
using Gnoss.ApiWrapper.OAuth;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using System.Xml;

namespace Gnoss.ApiWrapper
{

    /// <summary>
    /// Basic wrapper of GNOSS Api
    /// </summary>
    public abstract class GnossApiWrapper
    {
        #region Configuración Variables entorno
        
        /*
        Variables de entorno a definir (¡¡IMPORTANTE!! respetar mayúsuclas y minúsculas) si no se van a usar los archivos de configuración. Las variables definidas en los archivos Oauth.config y log4csharp.config son:

         - Variable para usar variables de entorno
	        useEnvironmentVariables = true/false
 
         - Variables del OAuth
	        consumerKey
	        consumerSecret
	        apiEndpointV2
	        apiEndpointV3
	        tokenKey
	        tokenSecret
	        communityShortName
	        ontologyName
	        communityId = (VALOR OPCIONAL, si no se le va a dar valor puede no declararse)
	        localImagesRoute = (VALOR OPCIONAL, si no se le va a dar valor puede no declararse)
	        loadIdentifier = (VALOR OPCIONAL, si no se le va a dar valor puede no declararse)
	        developerEmail

         - Variables del OAuth Segundo (sólo API V2)
	        Las mismas que las anteriores pero añadiéndole al final "Second"
	        consumerKeySecond
	        consumerSecretSecond
	        ...

         - Variables del Log
	        logLevel
	        fileRoute (Ésta es la que usa el API V2)
	        logPath = (Ésta la usa el API V3)
	        logFileName = (Ésta la usa el API V3)
	
         - Variables de ApplicationInsights
	        useApplicationInsights = true/false
	        implementationKey
	        logLocation
        */
        
        #endregion

        #region Members

        private const string _affinityTokenKey = "AffinityTokenGnossAPI";

        private static ConcurrentDictionary<int, string> _affinityTokenPerProcess = new ConcurrentDictionary<int, string>();

        /// <summary>
        /// The executable location
        /// </summary>
        protected string executableLocation = System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase;

        private OAuthInfo _oauth = null;
        private ILogHelper mLog;

        public ILogHelper Log
        {
            get { return mLog; }
            set { mLog = value; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Consturtor of <see cref="GnossApiWrapper"/>
        /// </summary>
        /// <param name="communityShortName">Community short name which you want to use the API</param>
        /// <param name="oauth">OAuth information to sign the Api requests</param>
        public GnossApiWrapper(OAuthInfo oauth, string communityShortName = null)
        {
            _oauth = oauth;
            CommunityShortName = communityShortName;
            this.mLog = LogHelper.Instance;
        }

        /// <summary>
        /// Consturtor of <see cref="GnossApiWrapper"/>
        /// </summary>
        /// <param name="configFilePath">Configuration file path, with a structure like http://api.gnoss.com/v3/exampleConfig.txt </param>
        public GnossApiWrapper(string configFilePath)
        {
            LoadConfigFile(configFilePath);
            this.mLog = LogHelper.Instance;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Load the basic parameters for the API
        /// </summary>
        protected virtual void LoadApi() { }

        private static string GenerateAffinityToken()
        {
            string tokenAfinidad = Guid.NewGuid().ToString();

            if (HttpContext.Current != null && HttpContext.Current.Request != null && !string.IsNullOrEmpty(HttpContext.Current.Request.Headers["X-Request-ID"]))
            {
                tokenAfinidad = HttpContext.Current.Request.Headers["X-Request-ID"];
            }

            try
            {
                if (HttpContext.Current != null && HttpContext.Current.Items != null)
                {

                    HttpContext.Current.Items.Add(_affinityTokenKey, tokenAfinidad);
                }
                else
                {
                    _affinityTokenPerProcess.TryAdd(Thread.CurrentThread.ManagedThreadId, tokenAfinidad);
                }
            }
            catch { }

            return tokenAfinidad;
        }

        private static string GetAffinityToken()
        {
            string resultado = null;

            try
            {
                if (HttpContext.Current != null && HttpContext.Current.Items != null)
                {
                    if (HttpContext.Current.Items.Contains(_affinityTokenKey))
                    {
                        resultado = HttpContext.Current.Items[_affinityTokenKey] as string;
                    }
                }
                else
                {
                    if (_affinityTokenPerProcess.ContainsKey(Thread.CurrentThread.ManagedThreadId))
                    {
                        resultado = _affinityTokenPerProcess[Thread.CurrentThread.ManagedThreadId];
                    }
                }
            }
            catch { }

            return resultado;
        }

        #region WebRequest

        /// <summary>
        /// Make a POST request to an url with an oauth sign and an object in the body of the request as json
        /// </summary>
        /// <param name="url">Url to make the request</param>
        /// <param name="model">Object to send in the body request as json</param>
        /// <param name="acceptHeader">(Optional) Accept header</param>
        /// <returns>Response of the server</returns>
        protected string WebRequestPostWithJsonObject(string url, object model, string acceptHeader = "")
        {
            string json = JsonConvert.SerializeObject(model);
            return WebRequest("POST", url, json, "application/json", acceptHeader);
        }

        /// <summary>
        /// Request an url with an oauth sign
        /// </summary>
        /// <param name="httpMethod">Http method (GET, POST, PUT...)</param>
        /// <param name="url">Url to make the request</param>
        /// <param name="postData">(Optional) Post data to send in the body request</param>
        /// <param name="contentType">(Optional) Content type of the postData</param>
        /// <param name="acceptHeader">(Optional) Accept header</param>
        /// <returns>Response of the server</returns>
        protected string WebRequest(string httpMethod, string url, string postData = "", string contentType = "", string acceptHeader = "")
        {
            HttpWebRequest webRequest = null;
            StreamWriter requestWriter = null;
            string responseData = "";

            webRequest = System.Net.WebRequest.Create(url) as HttpWebRequest;
            webRequest.Method = httpMethod;
            webRequest.ServicePoint.Expect100Continue = false;
            webRequest.Timeout = 200000;
            
            string signUrl = webRequest.RequestUri.ToString();

            if (!string.IsNullOrEmpty(webRequest.RequestUri.Query))
            {
                signUrl = webRequest.RequestUri.ToString().Replace(webRequest.RequestUri.Query, "");
            }

            OAuthInfo OAuth2 = new OAuthInfo(signUrl, OAuthInstance.Token, OAuthInstance.TokenSecret, OAuthInstance.ConsumerKey, OAuthInstance.ConsumerSecret, OAuthInstance.DeveloperEmail);
            string[] partesUrlOAuth = OAuth2.OAuthSignedUrl.Split('?');

            partesUrlOAuth = partesUrlOAuth[1].Split('&');
            string consumer_key = partesUrlOAuth[0].Split('=')[1];
            string token = partesUrlOAuth[1].Split('=')[1];
            string method = partesUrlOAuth[2].Split('=')[1];
            string timestamp = partesUrlOAuth[3].Split('=')[1];
            string nonce = partesUrlOAuth[4].Split('=')[1];
            string version = partesUrlOAuth[5].Split('=')[1];
            string signature = partesUrlOAuth[6].Split('=')[1];
            string oauth = string.Format("OAuth realm=\"Example\", oauth_consumer_key=\"{0}\", oauth_token=\"{1}\", oauth_signature_method=\"{2}\", oauth_signature=\"{3}\", oauth_timestamp=\"{4}\", oauth_nonce=\"{5}\", oauth_version=\"{6}\"", consumer_key, token, method, signature, timestamp, nonce, version);

            webRequest.Headers.Add("Authorization", oauth);
            webRequest.Headers.Add("X-Request-ID", AffinityToken);

            if (!string.IsNullOrEmpty(contentType))
            {
                webRequest.ContentType = contentType;
            }
            if (!string.IsNullOrEmpty(acceptHeader))
            {
                webRequest.Accept = acceptHeader;
            }

            if (httpMethod == "POST" || httpMethod == "PUT" || httpMethod == "DELETE")
            {
                requestWriter = new StreamWriter(webRequest.GetRequestStream());
                try
                {
                    requestWriter.Write(postData);
                }
                finally
                {
                    requestWriter.Close();
                    requestWriter = null;
                }
            }
            try
            {
                responseData = WebResponseGet(webRequest);
            }
            catch (WebException ex)
            {
                string message = null;
                try
                {
                    StreamReader sr = new StreamReader(ex.Response.GetResponseStream());
                    message = sr.ReadToEnd();
                    LogHelper.Instance.Error($"{ex.Message}. \r\nResponse: {message}");
                }
                catch { }

                if (!string.IsNullOrEmpty(message))
                {
                    throw new GnossAPIException(message);
                }

                // Error reading the error response, throw the original exception
                throw;
            }

            webRequest = null;

            return responseData;
        }

        /// <summary>
        /// Make a http get request
        /// </summary>
        /// <param name="pWebRequest">HttpWebRequest object</param>
        /// <returns>Server response</returns>
        protected string WebResponseGet(HttpWebRequest pWebRequest)
        {
            StreamReader responseReader = null;
            string responseData = "";

            try
            {
                responseReader = new StreamReader(pWebRequest.GetResponse().GetResponseStream(), Encoding.UTF8);
                responseData = responseReader.ReadToEnd();
            }
            finally
            {
                if (responseReader != null)
                {
                    responseReader.Close();
                    responseReader = null;
                }
            }
            return responseData;
        }

        private void LoadConfigFile(string filePath)
        {
            if (UsarVariablesEntorno)
            {
                LoadEnvironmentVariables();
            }
            else if (File.Exists(filePath))
            {
                XmlDocument docXml = new XmlDocument();

                docXml.Load(filePath);
                ReadConfigFile(docXml);
            }
        }

        protected virtual void LoadEnvironmentVariables()
        {
            string apiUrl, consumerKey, consumerSecret, tokenKey, tokenSecret;
            string developerEmail = string.Empty;
            IDictionary variablesEntorno = Environment.GetEnvironmentVariables();
            
            if (variablesEntorno.Contains("consumerKey"))
            {
                consumerKey = Environment.GetEnvironmentVariable("consumerKey");
            }
            else
            {
                throw new GnossAPIException("The environment variables doesn't contains consumerKey");
            }
            
            if (variablesEntorno.Contains("consumerSecret"))
            {
                consumerSecret = Environment.GetEnvironmentVariable("consumerSecret");
            }
            else
            {
                throw new GnossAPIException("The environment variables doesn't contains consumerSecret");
            }
            
            if (variablesEntorno.Contains("tokenKey"))
            {
                tokenKey = Environment.GetEnvironmentVariable("tokenKey");
            }
            else
            {
                throw new GnossAPIException("The environment variables doesn't contains tokenKey");
            }

            if (variablesEntorno.Contains("tokenSecret"))
            {
                tokenSecret = Environment.GetEnvironmentVariable("tokenSecret");
            }
            else
            {
                throw new GnossAPIException("The environment variables doesn't contains tokenSecret");
            }
            
            if (variablesEntorno.Contains("apiEndpointV3"))
            {
                apiUrl = Environment.GetEnvironmentVariable("apiEndpointV3");
            }
            else
            {
                throw new GnossAPIException("The environment variables doesn't contains apiEndpointV3");
            }
            
            if (variablesEntorno.Contains("communityShortName"))
            {
                CommunityShortName = Environment.GetEnvironmentVariable("communityShortName");
            }
            else
            {
                throw new GnossAPIException("The environment variables doesn't contains communityShortName");
            }
            
            if (variablesEntorno.Contains("developerEmail"))
            {
                developerEmail = Environment.GetEnvironmentVariable("developerEmail");
            }

            ConfigureLogFromConfigFile(null);

            OAuthInstance = new OAuthInfo(apiUrl, tokenKey, tokenSecret, consumerKey, consumerSecret, developerEmail);
        }

        /// <summary>
        /// Read the configuration from a configuration file
        /// </summary>
        /// <param name="docXml">XmlDocument with the configuration</param>
        protected virtual void ReadConfigFile(XmlDocument docXml)
        {
            string apiUrl, consumerKey, consumerSecret, tokenKey, tokenSecret;
            string developerEmail = string.Empty;


            XmlNode consumerNode = docXml.SelectSingleNode("config/consumer/consumerKey");
            if (consumerNode != null)
            {
                consumerKey = consumerNode.InnerText;
            }
            else
            {
                throw new GnossAPIException("The config file doesn't contains config/consumer/consumerKey");
            }

            XmlNode consumerSecretNode = docXml.SelectSingleNode("config/consumer/consumerSecret");
            if (consumerSecretNode != null)
            {
                consumerSecret = consumerSecretNode.InnerText;
            }
            else
            {
                throw new GnossAPIException("The config file doesn't contains config/consumer/consumerSecret");
            }

            XmlNode tokenNode = docXml.SelectSingleNode("config/token/tokenKey");
            if (tokenNode != null)
            {
                tokenKey = tokenNode.InnerText;
            }
            else
            {
                throw new GnossAPIException("The config file doesn't contains config/token/tokenKey");
            }

            XmlNode tokenSecretNode = docXml.SelectSingleNode("config/token/tokenSecret");
            if (tokenSecretNode != null)
            {
                tokenSecret = tokenSecretNode.InnerText;
            }
            else
            {
                throw new GnossAPIException("The config file doesn't contains config/token/tokenSecret");
            }

            XmlNode endpoindNode = docXml.SelectSingleNode("config/apiEndpoint");
            if (endpoindNode != null)
            {
                apiUrl = endpoindNode.InnerText;
            }
            else
            {
                throw new GnossAPIException("The config file doesn't contains config/apiEndpoint");
            }

            XmlNode communityShorNameNode = docXml.SelectSingleNode("config/communityShortName");
            if (communityShorNameNode != null)
            {
                CommunityShortName = communityShorNameNode.InnerText;
            }
            else
            {
                throw new GnossAPIException("The config file doesn't contains config/communityShortName");
            }

            XmlNode developerEmailNode = docXml.SelectSingleNode("config/developerEmail");
            if (developerEmailNode != null)
            {
                developerEmail = developerEmailNode.InnerText;
            }

            ConfigureLogFromConfigFile(docXml);

            OAuthInstance = new OAuthInfo(apiUrl, tokenKey, tokenSecret, consumerKey, consumerSecret, developerEmail);
        }

        private void ConfigureLogFromConfigFile(XmlDocument docXml)
        {
            string logPath = string.Empty;
            string logFileName = string.Empty;
            string logLevel = string.Empty;

            if (UsarVariablesEntorno)
            {
                logPath = Environment.GetEnvironmentVariable("logPath");
                logFileName = Environment.GetEnvironmentVariable("logFileName");
                logLevel = Environment.GetEnvironmentVariable("logLevel");
            }
            else
            {
                XmlNode logPathNode = docXml.SelectSingleNode("config/log/logPath");
                if (logPathNode != null)
                {
                    logPath = logPathNode.InnerText;
                }

                XmlNode logFileNameNode = docXml.SelectSingleNode("config/log/logFileName");
                if (logFileNameNode != null)
                {
                    logFileName = logFileNameNode.InnerText;
                }

                XmlNode logLevelNode = docXml.SelectSingleNode("config/log/logLevel");
                if (logLevelNode != null)
                {
                    logLevel = logLevelNode.InnerText;
                }
            }

            if (!string.IsNullOrEmpty(logPath) && !string.IsNullOrEmpty(logFileName) && !string.IsNullOrEmpty(logLevel))
            {
                if (logPath.StartsWith("\\"))
                {
                    logPath = HttpContext.Current.Server.MapPath(logPath);
                }

                LogHelper.LogDirectory = logPath;
                LogHelper.LogFileName = logFileName;
                switch (logLevel)
                {
                    case "TRACE":
                        LogHelper.LogLevel = LogLevels.TRACE;
                        break;
                    case "DEBUG":
                        LogHelper.LogLevel = LogLevels.DEBUG;
                        break;
                    case "INFO":
                        LogHelper.LogLevel = LogLevels.INFO;
                        break;
                    case "WARN":
                        LogHelper.LogLevel = LogLevels.WARN;
                        break;
                    case "ERROR":
                        LogHelper.LogLevel = LogLevels.ERROR;
                        break;
                    case "FATAL":
                        LogHelper.LogLevel = LogLevels.FATAL;
                        break;
                    case "OFF":
                        LogHelper.LogLevel = LogLevels.OFF;
                        break;
                }
            }
            //else
            //{
            //    Log4csharpException logEx = new Log4csharpException("Las variables de entorno 'logPath' ó 'logFileName' ó 'logLevel' están mal configuradas.");
            //    throw logEx;
            //}
        }

        /// <summary>
        /// Changes the current community short name by the indicated short name.
        /// </summary>
        /// <param name="newCommunityShortName">New community short name</param>
        protected void ChangeChargeCommunityShortName(string newCommunityShortName)
        {
            CommunityShortName = newCommunityShortName;
        }

        #endregion

        #endregion

        #region Properties

        /// <summary>
        /// Community short name
        /// </summary>
        public string CommunityShortName
        {
            get; set;
        }

        /// <summary>
        /// Gets the API url
        /// </summary>
        public string ApiUrl
        {
            get
            {
                return OAuthInstance.ApiUrl;
            }
        }

        /// <summary>
        /// Gets or sets the oauth info
        /// </summary>
        public OAuthInfo OAuthInstance
        {
            get
            {
                return _oauth;
            }
            set
            {
                _oauth = value;
                LoadApi();
            }
        }

        public string AffinityToken
        {
            get
            {
                string affinityToken = GetAffinityToken();

                if (string.IsNullOrEmpty(affinityToken))
                {
                    affinityToken = GenerateAffinityToken();
                }

                return affinityToken;
            }
        }

        public bool UsarVariablesEntorno
        {
            get
            {
                return (Environment.GetEnvironmentVariable("useEnvironmentVariables") != null && Environment.GetEnvironmentVariable("useEnvironmentVariables").ToLower().Equals("true"));
            }
        }

        #endregion
    }
}