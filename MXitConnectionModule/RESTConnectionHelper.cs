/// Mxit-Net-Connection-Helper
/// Author: Eric Clements (Kazazoom) - eric@kazazoom.com
/// License: BSD-3 (https://github.com/Kazazoom/mxit-net-connection-helper/blob/master/license.txt)
/// Credits: Based on RestSharp and Newtonsoft approach first drafted by George Ziady (Springfisher) - george@springfisher.com

using System;
using System.Configuration;
using System.Reflection;
using System.Threading;
using System.ServiceModel;
using System.Drawing;
using System.Collections.Generic;
using log4net;
using MXit;
using MXit.Messaging;
using MXit.Messaging.MessageElements;
using MXit.Billing;
using MXit.User;
using RestSharp;
using Newtonsoft;
using Newtonsoft.Json;
using MXit.OAuth2;
using MXit.Async;
using System.Linq;

namespace MXitConnectionModule
{
    /// <summary>
    /// This singleton will manage REST API connections for you. It will do authentication transparently. 
    /// 
    /// Please add the following line to your Controller and add this Project as a reference to your main project.
    ///     using MXitConnectionModule;    
    /// 
    /// Call the following method your Controller Class Start method with:
    ///     MXitConnectionModule.RESTConnectionHelper.Instance.InitializeAuthorizationValuesAndAuthenticate(clientID, clientSecret);
    /// 
    /// Where you want to send a message via the REST API, simply call:
    ///     RESTMessageToSend rMessageToSend = new RESTMessageToSend("yourmobiappname", "Hello World"); //(From,Body);
    ///     rMessageToSend.addMxitUserIDTo(toMxitUserID); //Add users using this method as it allows you to specify more than one user
    ///     bool result = RESTConnectionHelper.Instance.SendMessage(rMessageToSend);
    ///     
    /// You can also use the Mxit ExternalAppAPI SDK messageToSend class, for example:
    ///     MessageToSend messageToSend = messageReceived.CreateReplyMessage();
    ///     messageToSend.Append("Hello World", Color.Red, TextMarkup.Bold);
    ///     RESTConnectionHelper.Instance.SendMessage(messageToSend);
    /// 
    /// </summary>
    public class RESTConnectionHelper
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(RESTConnectionHelper));

        private String REST_ClientId;
        private String REST_ClientSecret;

        private String REST_grant_type = "client_credentials";
        private String REST_scope = "message/send profile/private profile/public graph/read";
        private String REST_AccessToken;

        private String REST_Auth_BaseUrl = "https://auth.mxit.com";
        private String REST_API_BaseUrl = "http://api.mxit.com";

        private bool isReAuthenticating; //Needed when we want to check that we aren't already busy re-authenticating in another thread
        private int retryAuthCount = 3; //How many times to retry authentication
        private int retryAuthTimeMs = 5 * 1000; //How long to wait between retries. 

        private static volatile RESTConnectionHelper instance;
        private static object syncRoot = new Object();

        private RESTConnectionHelper()
        {
        }

        /// <summary>
        /// The instance through which the Singleton is accessed
        /// </summary>
        public static RESTConnectionHelper Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                            instance = new RESTConnectionHelper();
                    }
                }

                return instance;
            }
        }


        /// <summary>
        /// Gets a value indicating whether an accessToken has already been requested.
        /// </summary>
        public bool IsAccessTokenAvailable
        {
            get
            {
                lock (this)
                {
                    bool isAccessTokenAvailable = (!String.IsNullOrEmpty(this.REST_AccessToken));
                    return isAccessTokenAvailable;
                }
            }
        }

        /// <summary>
        /// Sometimes we need to check if we are already reconnecting so that we dont start reconnecting again
        /// </summary>
        public bool IsReAuthenticating
        {
            get
            {
                return isReAuthenticating;
            }
        }

        public void EnqueueMessage(RESTMessageToSend rMessageToSend)
        {
            ThreadPool_SendRESTMessage.Instance.EnqueueItem(rMessageToSend);
        }

        public bool SendMessage(MessageToSend messageToSend)
        {
            MXitConnectionModule.RESTMessageToSend RESTMessageToSend = new RESTMessageToSend(messageToSend);
            return this.SendMessage(RESTMessageToSend);
        }

        public bool SendMessageFromUserAToUserB(String token, MessageToSend messageToSend)
        {
            MXitConnectionModule.RESTMessageToSend RESTMessageToSend = new RESTMessageToSend(messageToSend);
            return this.SendMessageFromUserAToUserB(token, RESTMessageToSend);
        }

        public bool SendMessageFromUserAToUserB(String token, RESTMessageToSend restMessageToSend)
        {
            System.Net.HttpStatusCode responseCode;
            bool sentMessageOK = false;

            sentMessageOK = SendMessageFromUserAToUserB(token, restMessageToSend, out responseCode);

            return sentMessageOK;
        }

        public bool SendMessage(RESTMessageToSend restMessageToSend)
        {
            bool success = false;

            System.Net.HttpStatusCode responseCode;
            bool sentMessageOK = false;

            sentMessageOK = SendMessage(restMessageToSend, out responseCode);

            if (sentMessageOK) // If all went well sending the message
            {
                success = true;
            }
            else // If something went wrong sending the message (sentMessagOK == false)
            {
                bool isRetryableError = (responseCode != System.Net.HttpStatusCode.BadRequest); //Only retry if it wasn't our error
                bool isAuthenticationExpired = (responseCode == System.Net.HttpStatusCode.Unauthorized);

                if (isRetryableError)
                {
                    logger.Debug(MethodBase.GetCurrentMethod().Name + "() - SendMessage Failed, retry sending...");
                    if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " SendMessage Failed, retry sending...");

                    bool reauthenticationSuccess = false;
                    if (isAuthenticationExpired)
                    {
                        // was an expired error, so lets re-authenticate:
                        reauthenticationSuccess = this.doRESTAuthentication();

                        if (reauthenticationSuccess)
                        {
                            if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Redoing authentication...");
                            sentMessageOK = SendMessage(restMessageToSend, out responseCode);
                        }
                    }
                    else // Was an internal error, so lets just retry sending the message:
                    {
                        sentMessageOK = SendMessage(restMessageToSend, out responseCode);
                    }


                    if (sentMessageOK)
                    {
                        if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Managed to send message after retry.");

                        success = true;
                    }
                    else
                    {
                        logger.Error(MethodBase.GetCurrentMethod().Name + "() - Could not send message even after retry.");
                        if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Could not send message even after retry.");
                    }
                }
                else //Not a retryable error
                {
                    logger.Error(MethodBase.GetCurrentMethod().Name + "() - Could not send message due to non retryable error: " + responseCode);
                    if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Could not send message due to non retryable error: " + responseCode);
                }
            }

            return success;
        }


        /// <summary>
        /// The sendMessage method
        /// </summary>
        public bool SendMessage(RESTMessageToSend rMessageToSend, out System.Net.HttpStatusCode responseCode)
        {
            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - START");
            bool success = false;
            responseCode = System.Net.HttpStatusCode.Unauthorized;//Need to improve this

            try
            {
                logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Creating RestClient...");
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Creating RestClient...");

                var client = new RestClient();

                client.BaseUrl = "http://api.mxit.com";
                client.Authenticator = new RESTMxitOAuth2Authenticator(this.REST_AccessToken);

                logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Creating RestRequest...");
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Creating RestRequest...");

                var REST_SendMessageRequest = new RestRequest();
                REST_SendMessageRequest.Method = Method.POST;
                REST_SendMessageRequest.RequestFormat = DataFormat.Json;
                REST_SendMessageRequest.AddHeader("Content-Type", "application/json");
                REST_SendMessageRequest.AddHeader("Accept", "application/json");
                REST_SendMessageRequest.Resource = "/message/send/"; //Resource points to the method of the API we want to access

                REST_SendMessageRequest.AddBody(rMessageToSend);

                //Start - Temporary Code
                //TODO: REMOVE ONCE MXIT MESSAGE PARSING IS FIXED
                //The below is a Hack to allow the Links Array to be appended if it contains 1 or more values, or remove it otherwise
                //(if an empty Links array is passed, the message won't send correctly)
                Parameter messageBody = REST_SendMessageRequest.Parameters.Find(
                                        delegate(Parameter p)
                                        {
                                            return p.Name == "application/json";
                                        });
                if (messageBody != null) {
                    //If an empty Link Array exists, remove it from the JSON message
                    messageBody.Value = messageBody.Value.ToString().Replace("\"Links\":[],", "");
                    REST_SendMessageRequest.Parameters.Find(
                                        delegate(Parameter p)
                                        {
                                            return p.Name == "application/json";
                                        }).Value = messageBody.Value;
                }
                //End - Temporary Code

                logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Executing RESTRequest (SendMessage)");
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Executing RESTRequest (SendMessage)");

                RestResponse RESTResponse = (RestResponse)client.Execute(REST_SendMessageRequest);

                //Set the out parameter, so that the calling method can redo auth if needed and retry:
                System.Net.HttpStatusCode RESTResponseHTTPStatusCode = RESTResponse.StatusCode;
                bool sentMessageOK = (RESTResponseHTTPStatusCode == System.Net.HttpStatusCode.OK);

                //Persist the message sent to DB:
                rMessageToSend.persistRESTMessageSent_toDB(RESTResponse.Content, RESTResponse.StatusCode.ToString());

                if (sentMessageOK)
                {
                    logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Sent message OK.");
                    if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Sent message to OK.");

                    success = true;
                }
                else // Something went wrong, we'll handle the error code in the calling wrapper method
                {
                    logger.Error(MethodBase.GetCurrentMethod().Name + "() - RestSendMessage Failed: (user:" + rMessageToSend.To + ") (responseCode: " + (Int16)RESTResponseHTTPStatusCode + ")");
                    if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " RestSendMessage FAILED. (responseCode: " + (Int16)RESTResponseHTTPStatusCode + ")");

                    responseCode = RESTResponse.StatusCode;

                    success = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now.ToString() + " Exception sending REST message:" + ex.ToString());
                logger.Error(MethodBase.GetCurrentMethod().Name + "() - Exception sending REST message: " + ex.GetType() + " " + ex.ToString());
                success = false;
            }

            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - END");
            return success;
        }

        /// <summary>
        /// The sendMessage method
        /// </summary>
        //public bool SendMessageFromFriendToFriend2(RESTMessageToSend rMessageToSend, out System.Net.HttpStatusCode responseCode)
        //public bool SendMessageFromUserAToUserB(String RequestToken, String MxitUserID_From, String MxitUserID_To, String messageBody, out System.Net.HttpStatusCode responseCode)
        public bool SendMessageFromUserAToUserB(String RequestToken, RESTMessageToSend rMessageToSend, out System.Net.HttpStatusCode responseCode)
        {
            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - START");
            bool success = false;
            responseCode = System.Net.HttpStatusCode.Unauthorized;//Need to improve this

            try
            {
                logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Creating RestClient...");
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Creating RestClient...");

                var client = new RestClient();

                client.BaseUrl = "http://api.mxit.com";
                client.Authenticator = new RESTMxitOAuth2Authenticator(RequestToken);

                logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Creating RestRequest...");
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Creating RestRequest...");

                var RESTRequest = new RestRequest();
                RESTRequest.Method = Method.POST;
                RESTRequest.RequestFormat = DataFormat.Json;
                RESTRequest.AddHeader("Content-Type", "application/json");
                RESTRequest.AddHeader("Accept", "application/json");
                RESTRequest.Resource = "/message/send/"; //Resource points to the method of the API we want to access

                RESTRequest.AddBody(rMessageToSend);

                logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Executing RESTRequest (SendMessage)");
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Executing RESTRequest (SendMessage)");

                RestResponse RESTResponse = (RestResponse)client.Execute(RESTRequest);

                //Set the out parameter, so that the calling method can redo auth if needed and retry:
                System.Net.HttpStatusCode RESTResponseHTTPStatusCode = RESTResponse.StatusCode;
                bool sentMessageOK = (RESTResponseHTTPStatusCode == System.Net.HttpStatusCode.OK);

                if (sentMessageOK)
                {
                    logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Sent message OK.");
                    if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Sent message to OK.");

                    success = true;
                }
                else // Something went wrong, we'll handle the error code in the calling wrapper method
                {
                    logger.Error(MethodBase.GetCurrentMethod().Name + "() - RestSendMessage Failed: (user:" + rMessageToSend.To + ") (responseCode: " + (Int16)RESTResponseHTTPStatusCode + ")");
                    if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " RestSendMessage FAILED. (responseCode: " + (Int16)RESTResponseHTTPStatusCode + ") Detail:" + RESTResponse.Content);

                    responseCode = RESTResponse.StatusCode;

                    success = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now.ToString() + " Exception sending REST message:" + ex.ToString());
                logger.Error(MethodBase.GetCurrentMethod().Name + "() - Exception sending REST message: " + ex.GetType() + " " + ex.ToString());
                success = false;
            }

            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - END");
            return success;
        }

        /// <summary>
        /// The GetProfile method
        /// </summary>
        public bool GetUserMxitProfile(String OAuth2Token, String MxitUserID, out MxitModel.FullProfile userProfile)
        {
            userProfile = new MxitModel.FullProfile();

            System.Net.HttpStatusCode responseCode;
            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - START");
            bool success = false;
            responseCode = System.Net.HttpStatusCode.Unauthorized;//Need to improve this

            try
            {
                logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Creating RestClient...");
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Creating RestClient...");

                var client = new RestClient();

                client.BaseUrl = "http://api.mxit.com";
                client.Authenticator = new RESTMxitOAuth2Authenticator(OAuth2Token);

                logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Creating RestRequest...");
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Creating RestRequest...");

                var RESTRequest = new RestRequest();
                RESTRequest.Method = Method.GET;
                RESTRequest.RequestFormat = DataFormat.Json;
                RESTRequest.AddHeader("Content-Type", "application/json");
                RESTRequest.AddHeader("Accept", "application/json");
                RESTRequest.Resource = "/user/profile"; //Resource points to the method of the API we want to access

                logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Executing RESTRequest (SendMessage)");
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Executing RESTRequest (SendMessage)");

                RestResponse RESTResponse = (RestResponse)client.Execute(RESTRequest);

                //Set the out parameter, so that the calling method can redo auth if needed and retry:
                System.Net.HttpStatusCode RESTResponseHTTPStatusCode = RESTResponse.StatusCode;
                bool responseOK = (RESTResponseHTTPStatusCode == System.Net.HttpStatusCode.OK);

                if (responseOK)
                {
                    logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Get Profile OK.");
                    //convert the rest response into a profile
                    userProfile = JsonConvert.DeserializeObject<MxitModel.FullProfile>(RESTResponse.Content);
                    if (logger.IsDebugEnabled) Console.WriteLine(userProfile.ToString());
                    success = true;
                }
                else // Something went wrong, we'll handle the error code in the calling wrapper method
                {
                    logger.Error(MethodBase.GetCurrentMethod().Name + "() - RestGetProfile Failed: (responseCode: " + (Int16)RESTResponseHTTPStatusCode + ")");
                    if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " RestSendMessage FAILED. (responseCode: " + (Int16)RESTResponseHTTPStatusCode + ")");

                    responseCode = RESTResponse.StatusCode;

                    success = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now.ToString() + " Exception sending REST message:" + ex.ToString());
                logger.Error(MethodBase.GetCurrentMethod().Name + "() - Exception sending REST message: " + ex.GetType() + " " + ex.ToString());
                success = false;
            }

            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - END");
            return success;
        }

        /// <summary>
        /// The GetProfile method
        /// </summary>
        public bool GetContactList(String OAuth2Token, String MxitUserID, out MxitModel.ContactList userContactList)
        {
            userContactList = new MxitModel.ContactList();
            MxitModel.ContactList partialContactList = new MxitModel.ContactList();

            System.Net.HttpStatusCode responseCode;
            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - START");
            bool success = false;
            responseCode = System.Net.HttpStatusCode.Unauthorized;//Need to improve this

            try
            {
                logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Creating RestClient...");
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Creating RestClient...");

                var client = new RestClient();

                client.BaseUrl = "http://api.mxit.com";
                client.Authenticator = new RESTMxitOAuth2Authenticator(OAuth2Token);

                logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Creating RestRequest...");
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Creating RestRequest...");

                int count = 26; //how many contacts to retrieve at a time, max is 26
                int skip = 0;
                const int MAX_ITERATIONS = 30; //MAX_ITERATIONS * count will give the max number of contacts that can be retrieved

                var RESTRequest = new RestRequest();
                RESTRequest.Method = Method.GET;
                RESTRequest.RequestFormat = DataFormat.Json;
                RESTRequest.AddHeader("Content-Type", "application/json");
                RESTRequest.AddHeader("Accept", "application/json");

                do
                {                    
                    RESTRequest.Resource = "/user/socialgraph/contactlist?filter=@Friends&skip=" + (count*skip) + "&count=" + count; //Resource points to the method of the API we want to access
                    partialContactList = new MxitModel.ContactList();                    
                logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Executing RESTRequest (ContactList)");
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Executing RESTRequest (ContactList)");

                RestResponse RESTResponse = (RestResponse)client.Execute(RESTRequest);

                //Set the out parameter, so that the calling method can redo auth if needed and retry:
                System.Net.HttpStatusCode RESTResponseHTTPStatusCode = RESTResponse.StatusCode;
                bool responseOK = (RESTResponseHTTPStatusCode == System.Net.HttpStatusCode.OK);

                if (responseOK)
                {
                    logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Get ContactList OK.");
                        //convert the rest response into a list of contacts
                        partialContactList = JsonConvert.DeserializeObject<MxitModel.ContactList>(RESTResponse.Content);
                    if (logger.IsDebugEnabled) Console.WriteLine(userContactList.ToString());
                    success = true;
                }
                else // Something went wrong, we'll handle the error code in the calling wrapper method
                {
                    logger.Error(MethodBase.GetCurrentMethod().Name + "() - GetContactList Failed: (responseCode: " + (Int16)RESTResponseHTTPStatusCode + ") Reason: " + RESTResponse.Content);
                    if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " GetContactList FAILED. (responseCode: " + (Int16)RESTResponseHTTPStatusCode + ") Reason: " + RESTResponse.Content);

                    responseCode = RESTResponse.StatusCode;

                    success = false;
                }

                    //merge the partial array with the full one
                    userContactList.Contacts = userContactList.Contacts.Concat(partialContactList.Contacts).ToArray();                    

                    skip++;
                //continue while there are still results, no errors occur, or we have looped less times than the limit specified
                } while ((partialContactList.getTotalFriendCount() > 0) && (skip <= MAX_ITERATIONS) && (success == true));
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now.ToString() + " Exception GetContactList:" + ex.ToString());
                logger.Error(MethodBase.GetCurrentMethod().Name + "() - Exception GetContactList: " + ex.GetType() + " " + ex.ToString());
                success = false;
            }

            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - END");
            return success;
        }


        /// <summary>
        /// The GetUserAvatar method
        /// </summary>
        public bool GetUserAvatar(String OAuth2Token, out byte[] byteArray, out string mimeType)
        {

            mimeType = "";
            byteArray = new byte[0];

            System.Net.HttpStatusCode responseCode;
            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - START");
            bool success = false;
            responseCode = System.Net.HttpStatusCode.Unauthorized; //Need to improve this

            try
            {
                logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Creating RestClient...");
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Creating RestClient...");
                var client = new RestClient();

                client.BaseUrl = "http://api.mxit.com";
                client.Authenticator = new RESTMxitOAuth2Authenticator(OAuth2Token);

                logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Creating RestRequest...");
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Creating RestRequest...");
                var REST_SendMessageRequest = new RestRequest();
                REST_SendMessageRequest.Method = Method.GET;
                REST_SendMessageRequest.RequestFormat = DataFormat.Json;
                REST_SendMessageRequest.AddHeader("Content-Type", "application/json");
                REST_SendMessageRequest.AddHeader("Accept", "application/json");
                REST_SendMessageRequest.Resource = "/user/avatar"; //Resource points to the method of the API we want to access

                logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Executing RESTRequest (GetUserAvatar)");
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Executing RESTRequest (GetUserAvatar)");

                RestResponse RESTResponse = (RestResponse)client.Execute(REST_SendMessageRequest);

                //Set the out parameter, so that the calling method can redo auth if needed and retry:
                System.Net.HttpStatusCode RESTResponseHTTPStatusCode = RESTResponse.StatusCode;
                bool responseOK = (RESTResponseHTTPStatusCode == System.Net.HttpStatusCode.OK);

                if (responseOK)
                {
                    logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Get User Avatar OK.");

                    mimeType = RESTResponse.ContentType;
                    byteArray = RESTResponse.RawBytes;

                    success = true;
                }
                else // Something went wrong, we'll handle the error code in the calling wrapper method
                {
                    logger.Error(MethodBase.GetCurrentMethod().Name + "() - RestGetUserAvatar Failed: (responseCode: " + (Int16)RESTResponseHTTPStatusCode + ")");
                    if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " RestGetUserAvatar FAILED. (responseCode: " + (Int16)RESTResponseHTTPStatusCode + ")");

                    responseCode = RESTResponse.StatusCode;

                    success = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now.ToString() + " Exception getting User Avatar:" + ex.ToString());
                logger.Error(MethodBase.GetCurrentMethod().Name + "() - Exception getting User Avatar: " + ex.GetType() + " " + ex.ToString());
                success = false;
            }

            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - END");
            return success;
        }


        /// <summary>
        /// Connect to the MXit API Server with suppplied clientId and clientSecret
        /// </summary>
        public bool InitializeAuthorizationValuesAndAuthenticate(String aClientID, String aClientSecret)
        {
            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - START");

            bool success = false;
            this.REST_AccessToken = "";

            logger.Debug(MethodBase.GetCurrentMethod().Name + "() Setting up REST authentication values (ClientID=" + aClientID + "), (ClientSecret=" + aClientSecret + ").");
            Console.WriteLine(DateTime.Now.ToString() + " Setting up REST authentication values (ClientID=" + aClientID + "), (ClientSecret=" + aClientSecret + ").");

            this.REST_ClientId = aClientID;
            this.REST_ClientSecret = aClientSecret;

            this.doRESTAuthentication();

            if (this.IsAccessTokenAvailable)
            {
                logger.Debug(MethodBase.GetCurrentMethod().Name + "() Received authentication token: " + this.REST_AccessToken);
                Console.WriteLine(DateTime.Now.ToString() + " Received authentication token: " + this.REST_AccessToken);

                success = true;
            }
            else
            {
                success = false;
            }

            logger.Debug("[" + MethodBase.GetCurrentMethod().Name + "()] - END");

            return success;
        }

        /// <summary>
        /// Connection workflow logic with backoff time of 60s
        /// </summary>
        private bool doRESTAuthentication()
        {
            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - START");

            bool success = false;
            this.REST_AccessToken = "";

            lock (this)
            {
                isReAuthenticating = true;
                // Already connected
                //if (this.IsAccessTokenAvailable)
                //{
                //throw new InvalidOperationException("Already connected.");
                //}

                bool isAuthenticated = false;
                bool isFirstTime = true;
                int tryCounter = 0;

                while (!isAuthenticated && (tryCounter < this.retryAuthCount))
                {
                    try
                    {
                        if (!isFirstTime)
                        {
                            logger.Debug("" + MethodBase.GetCurrentMethod().Name + "()] Waiting for " + this.retryAuthTimeMs + "ms before retrying REST Auth.");
                            Console.WriteLine(DateTime.Now.ToString() + " Waiting for " + this.retryAuthTimeMs + "ms before retrying REST Auth.");
                            Thread.Sleep(this.retryAuthTimeMs);
                        }

                        logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Creating RestClient...");
                        Console.WriteLine(DateTime.Now.ToString() + " Creating RestClient...");

                        /* See http://restsharp.org/ for more info about the following authentication flow logic:
                        */

                        var client = new RestClient();
                        client.BaseUrl = this.REST_Auth_BaseUrl;

                        logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Creating HttpBasicAuthenticator...");
                        Console.WriteLine(DateTime.Now.ToString() + " Creating HttpBasicAuthenticator...");

                        //connect using the Client ID and Client Secret provided on the code.mxit.com Dashboard
                        client.Authenticator = new HttpBasicAuthenticator(this.REST_ClientId, this.REST_ClientSecret);

                        logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Creating RestRequest..");
                        Console.WriteLine(DateTime.Now.ToString() + " Creating RestRequest...");

                        var RESTRequest = new RestRequest();
                        RESTRequest.Resource = "/token";
                        RESTRequest.Method = Method.POST;

                        //Always "client_credentials" (http://dev.mxit.com/docs/authentication)
                        Parameter parameter1 = new Parameter();
                        parameter1.Name = "grant_type";
                        parameter1.Value = this.REST_grant_type;
                        parameter1.Type = ParameterType.GetOrPost;
                        RESTRequest.Parameters.Add(parameter1);

                        //The list of scopes which you are requesting access to. (http://dev.mxit.com/docs/authentication)
                        Parameter parameter2 = new Parameter();
                        parameter2.Name = "scope";
                        parameter2.Value = this.REST_scope;
                        parameter2.Type = ParameterType.GetOrPost;
                        RESTRequest.Parameters.Add(parameter2);

                        logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Executing RestRequest...");
                        Console.WriteLine(DateTime.Now.ToString() + " Executing RestRequest...");

                        RestResponse RESTResponse = (RestResponse)client.Execute(RESTRequest);

                        //Deserialize the response string from the REST request, and convert to a Dictionary collection:
                        Dictionary<string, string> RESTResponseDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(RESTResponse.Content);

                        //Get the accessToken from the Dictionary:
                        String receivedAccessToken = RESTResponseDict["access_token"];

                        bool gotValidAccessToken = (!String.IsNullOrEmpty(receivedAccessToken));

                        if (gotValidAccessToken)
                        {
                            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - AccessToken RECEIVED: " + receivedAccessToken);
                            Console.WriteLine(DateTime.Now.ToString() + " AccessToken RECEIVED: " + receivedAccessToken);

                            this.REST_AccessToken = receivedAccessToken;
                            isAuthenticated = true;
                            success = true;
                        }
                        else
                        {
                            logger.Error(MethodBase.GetCurrentMethod().Name + "() - AccessToken Empty!");
                            Console.WriteLine(DateTime.Now.ToString() + " AccessToken Empty!");
                            isAuthenticated = false;
                            success = false;
                        }

                    }
                    catch (Exception ex)
                    {
                        logger.Debug(MethodBase.GetCurrentMethod().Name + "() - Problem authenticating against Mxit REST Service: " + ex.ToString());
                        Console.WriteLine(DateTime.Now.ToString() + " Problem authenticating against Mxit REST Service, going to keep on trying");
                        isFirstTime = false;
                    }

                    //Increment the try counter within the loop:
                    tryCounter += 1;

                    isFirstTime = false;

                } //loop

                logger.Debug("[" + MethodBase.GetCurrentMethod().Name + "()] - END");

                isReAuthenticating = false;

            }//lock

            return success;
        }


        public void RedirectRequest(MXit.Navigation.RedirectRequest redirectRequest)
        {
            try
            {
                //Todo
            }
            catch (Exception e)
            {
                Console.WriteLine(DateTime.Now.ToString() + " System Exception:" + e.ToString());
                logger.Error(MethodBase.GetCurrentMethod().Name + " - System Exception: " + e.ToString());
            }
        }


        public void ProcessOAuth2Token(MXit.OAuth2.TokenResponse tokenResponseReceived)
        {
            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - START");

            bool isTokenResultSuccess = (tokenResponseReceived.Result == AsyncOperationResult.Success);

            if (isTokenResultSuccess) //We got a succesfull result:
            {
                bool isUserAllowed = (tokenResponseReceived.AuthorizationResult == AuthorizationResult.Allow);
                bool isUserAlwaysAllowed = (tokenResponseReceived.AuthorizationResult == AuthorizationResult.AlwaysAllow);

                if (isUserAllowed || isUserAlwaysAllowed) //We got permission:
                {
                    if ((String)tokenResponseReceived.Context == ".p")
                    {
                        MxitModel.FullProfile userProfile;

                        //Do something with the tokenReceived.AccessToken
                        this.GetUserMxitProfile(tokenResponseReceived.AccessToken, tokenResponseReceived.UserId, out userProfile);
                    }
                    else if ((String)tokenResponseReceived.Context == ".sf")
                    {
                        MxitModel.ContactList userContactList;

                        //Do something with the tokenReceived.AccessToken
                        this.GetContactList(tokenResponseReceived.AccessToken, tokenResponseReceived.UserId, out userContactList);

                        for (int i=0; i < userContactList.Contacts.Length; i++)
                        {
                            Console.WriteLine(userContactList.Contacts[i].DisplayName);
                        }
                    }
                    else if ((String)tokenResponseReceived.Context == ".f")
                    {
                        System.Net.HttpStatusCode responseCode;
                        //MXitConnectionModule.RESTConnectionHelper.Instance.SendMessageFromFriendToFriend(tokenResponseReceived.AccessToken, tokenResponseReceived.UserId, "m42992584002", "Merry Christmas! It's morning, and we've got nowhere to go. So wake me up in about an hour or so. It's Christmas day, and since we've got nowhere to be, stoke that braai and throw on another boerewors for me. It's Christmas night, and there's nothing I'd rather do than chill by the light of the Christmas tree with you.", out responseCode);

                        MessageToSend messageToSendToFriend = new MessageToSend(tokenResponseReceived.UserId, "m42992584002", DeviceInfo.DefaultDevice);
                        messageToSendToFriend.AppendLine("Light text", System.Drawing.Color.Black);
                        messageToSendToFriend.AppendLine("Bold text", System.Drawing.Color.Purple, new TextMarkup[] { TextMarkup.Bold });
                        messageToSendToFriend.AppendLine(MessageBuilder.Elements.CreateLink("Tip Link", ".tip"));

                        MXitConnectionModule.RESTConnectionHelper.Instance.SendMessageFromUserAToUserB(tokenResponseReceived.AccessToken, messageToSendToFriend);
                    }
                }
                else
                {
                    //Call a hook that will display a message to the user asking him to allow access to proceed.
                }
            }
            else
            {
                //Some error occured.
                if (logger.IsDebugEnabled) Console.WriteLine(DateTime.Now.ToString() + " Token request was not succesfull: " + tokenResponseReceived.Result);
                logger.Error(MethodBase.GetCurrentMethod().Name + " Token request was not succesfull: " + tokenResponseReceived.Result);
            }

            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - END");
        }

    }
}
