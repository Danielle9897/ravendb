//using System;
//using System.IO;
//using System.Net;
//using System.Net.Http;
//using System.Net.Http.Headers;
//using System.Threading.Tasks;
//using Raven.Abstractions.Connection;
//using Raven.Abstractions.Extensions;

//namespace Raven.Abstractions.OAuth
//{
//    public class BasicAuthenticator : AbstractAuthenticator
//    {
//        private readonly bool enableBasicAuthenticationOverUnsecuredHttp;

//        public BasicAuthenticator(bool enableBasicAuthenticationOverUnsecuredHttp)
//        {
//            this.enableBasicAuthenticationOverUnsecuredHttp = enableBasicAuthenticationOverUnsecuredHttp;
//        }

//        public async Task<Action<HttpClient>> HandleOAuthResponseAsync(string oauthSource, string apiKey)
//        {
//            using (var httpClient = new HttpClient(new HttpClientHandler()))
//            {
//                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("grant_type", "client_credentials");
//                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json") { CharSet = "UTF-8" });

//                httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
//                httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

//                if (string.IsNullOrEmpty(apiKey) == false)
//                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Api-Key", apiKey);

//                if (oauthSource.StartsWith("https", StringComparison.OrdinalIgnoreCase) == false && enableBasicAuthenticationOverUnsecuredHttp == false)
//                    throw new InvalidOperationException(BasicOAuthOverHttpError);

//                var requestUri = oauthSource;
//                var response = await httpClient.GetAsync(requestUri)
//                                               .ConvertSecurityExceptionToServerNotFound()
//                                               .AddUrlIfFaulting(new Uri(requestUri)).ConfigureAwait(false);

//                var stream = await response.GetResponseStreamWithHttpDecompression().ConfigureAwait(false);
//                using (var reader = new StreamReader(stream))
//                {
//                    var currentOauthToken = reader.ReadToEnd();
//                    CurrentToken = currentOauthToken;
//                    CurrentTokenWithBearer = "Bearer " + currentOauthToken;
//                    return (Action<HttpClient>)(SetAuthorization);
//                }
//            }
//        }

//        private const string BasicOAuthOverHttpError = @"Attempting to authenticate using basic security over HTTP would expose user credentials (including the password) in clear text to anyone sniffing the network.
//Your OAuth endpoint should be using HTTPS, not HTTP, as the transport mechanism.
//You can setup the OAuth endpoint in the RavenDB server settings ('Raven/OAuthTokenServer' configuration value), or setup your own behavior by providing a value for:
//    documentStore.Conventions.HandleUnauthorizedResponse
//If you are on an internal network or requires this for testing, you can disable this warning by calling:
//    documentStore.JsonRequestFactory.EnableBasicAuthenticationOverUnsecuredHttpEvenThoughPasswordsWouldBeSentOverTheWireInClearTextToBeStolenByHackers = true;
//";
//    }
//}
