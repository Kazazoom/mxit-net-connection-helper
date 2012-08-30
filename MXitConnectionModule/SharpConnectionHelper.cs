/// Mxit-Net-Connection-Helper
/// Author: Eric Clements (Kazazoom) - eric@kazazoom.com
/// License: BSD-3 (https://github.com/Kazazoom/mxit-net-connection-helper/blob/master/license.txt)

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


namespace MXitConnectionModule
{
    /// <summary>
    /// This singleton will manage C# API connections for you. It will do re-connection and retry logic transparently. 
    /// Call it from your Controller Class Start method with:
    ///     MXitConnectionModule.ExternalAppAPI.CommsCallback callback = new Callback();
    ///     MXitConnectionModule.ConnectionManager.Instance.InitializeConnection(externalAppNameTemp, externalAppPasswordTemp, callback);
    /// 
    /// And where you want to send a message, simply call:
    ///     MXitConnectionModule.ConnectionManager.Instance.SendMessage((MessageToSend)message);
    /// </summary>
    class SharpConnectionHelperSingleton
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(SharpConnectionHelperSingleton));
        bool isTestMode = false;

        private ExternalAppAPI.CommsClient client = null;
        String MyAppName;
        String MyAppPassword;
        private int keepAlivePeriod = 3 * 30 * 1000;/// Keep alive period (in ms) and timer
        private Timer keepAliveTimer = null;
        private static ExternalAppAPI.CommsCallback theCallback;// = new Callback();
        private bool isReconnecting;
        private Boolean isKeepAliveRunning;
        private static volatile SharpConnectionHelperSingleton instance;
        private static object syncRoot = new Object();

        private SharpConnectionHelperSingleton()
        {
        }

        /// <summary>
        /// The instance through which the Singleton is accessed
        /// </summary>
        public static SharpConnectionHelperSingleton Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                            instance = new SharpConnectionHelperSingleton();
                    }
                }

                return instance;
            }
        }


        /// <summary>
        /// Gets a value indicating whether this controller was started and is currently running.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (this)
                {
                    bool isClientActive = this.client != null;
                    bool isClientGood = (!this.isReconnecting);
                    bool isClientRunning = (isClientActive && isClientGood);
                    return isClientRunning;
                }
            }
        }

        /// <summary>
        /// Sometimes we need to check if we are already reconnecting so that we dont start reconnecting again
        /// </summary>
        public bool IsReconnecting
        {
            get
            {
                return isReconnecting;
            }
        }

        /// <summary>
        /// The sendMessage method, wrapped in logic that will reconnect if anything goes wrong/throws a comms exception
        /// </summary>
        public bool SendMessage(IMessageToSend message)
        {
            logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] - START");
            bool success = false;

            //logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Message sent to: " + message.To + "\n" + message.Body);

            //Console.WriteLine(DateTime.Now.ToString() + " MESSAGE: " + message.ToString());

            if (!isTestMode)
            {
                try
                {
                    SharpConnectionHelperSingleton.Instance.client.SendMessage((MessageToSend)message);

                    success = true;
                }
                catch (System.TimeoutException te)
                {
                    Console.WriteLine(DateTime.Now.ToString() + " Timeout Exception. Going to try to reconnect...");
                    logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] - Timeout Exception. Going to try to reconnect");
                    this.ReConnectFull();
                    //consider resending message here.
                    success = false;
                }
                catch (System.ServiceModel.CommunicationObjectAbortedException coae)
                {
                    Console.WriteLine(DateTime.Now.ToString() + " Communication Object Aborted Exception. Going to try to reconnect...");
                    logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] - Communication Object Aborted Exception. Going to try to reconnect");
                    this.ReConnectFull();

                    success = false;
                }
                catch (System.ServiceModel.CommunicationObjectFaultedException cofe)
                {
                    Console.WriteLine(DateTime.Now.ToString() + " Communication Object Faulted Exception. Going to try to reconnect...");
                    logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] - Communication Object Faulted Exception. Going to try to reconnect");
                    this.ReConnectFull();

                    success = false;
                }
                catch (System.ServiceModel.CommunicationException ce)
                {
                    Console.WriteLine(DateTime.Now.ToString() + " Communication Exception. ");
                    logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] - Communication Exception: " + ce.ToString());

                    success = false;
                }
                catch (Exception e)
                {
                    Console.WriteLine(DateTime.Now.ToString() + " System Exception. Going to try to reconnect...");
                    logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] - System Exception: " + e.ToString());

                    success = false;
                }
            }
            else
                //Test mode:
            {
                //Pretend to do something for the duration of time that a sendmessage would run
                Thread.Sleep(150);
            }

            //************************ handle communication exception here?

            logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] - END");

            return success;
        }

        /// <summary>
        /// The requestPayment method, wrapped in logic that will reconnect if anything goes wrong/throws a comms exception
        /// </summary>
        public void RequestPayment(PaymentRequest paymentRequest)
        {
            try
            {
                this.client.RequestPayment(paymentRequest);
            }
            catch (System.TimeoutException te)
            {
                Console.WriteLine(DateTime.Now.ToString() + " Timeout Exception. Going to try to reconnect...");
                logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] - Timeout Exception. Going to try to reconnect");
                this.ReConnectFull();
            }
            catch (System.ServiceModel.CommunicationObjectAbortedException coae)
            {
                Console.WriteLine(DateTime.Now.ToString() + " Communication Object Aborted Exception. Going to try to reconnect...");
                logger.Error("[" + MethodBase.GetCurrentMethod().Name + "()] - Communication Object Aborted Exception. Going to try to reconnect: " + coae.ToString());
                this.ReConnectFull();
            }
            catch (System.ServiceModel.CommunicationObjectFaultedException cofe)
            {
                Console.WriteLine(DateTime.Now.ToString() + " Communication Object Faulted Exception. Going to try to reconnect...");
                logger.Error("[" + MethodBase.GetCurrentMethod().Name + "()] - Communication Object Faulted Exception. Going to try to reconnect: " + cofe.ToString());
                this.ReConnectFull();
            }
            catch (System.ServiceModel.CommunicationException ce)
            {
                Console.WriteLine(DateTime.Now.ToString() + " Communication Exception. ");
                logger.Error("[" + MethodBase.GetCurrentMethod().Name + "()] - Communication Exception: " + ce.ToString());

            }
        }

        /// <summary>
        /// The GetUser method, wrapped in logic that will reconnect if anything goes wrong/throws a comms exception
        /// </summary>
        public UserInfo GetUser(String userID)
        {
            //SHOULD PROBABLY PUT A TRY HERE
            if (!isTestMode)
            {
                try
                {
                    return this.client.GetUser(userID);
                }
                catch (System.TimeoutException te)
                {
                    Console.WriteLine(DateTime.Now.ToString() + " Timeout Exception. Going to try to reconnect...");
                    logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] - Timeout Exception. Going to try to reconnect");
                    return null;
                }
                catch (System.ServiceModel.CommunicationObjectAbortedException coae)
                {
                    Console.WriteLine(DateTime.Now.ToString() + " Communication Object Aborted Exception. Going to try to reconnect...");
                    logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] - Communication Object Aborted Exception. Going to try to reconnect");
                    return null;
                }
                catch (System.ServiceModel.CommunicationObjectFaultedException cofe)
                {
                    Console.WriteLine(DateTime.Now.ToString() + " Communication Object Faulted Exception. Going to try to reconnect...");
                    logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] - Communication Object Faulted Exception. Going to try to reconnect");
                    return null;
                }
                catch (System.ServiceModel.CommunicationException ce)
                {
                    Console.WriteLine(DateTime.Now.ToString() + " Communication Exception. ");
                    logger.Error("[" + MethodBase.GetCurrentMethod().Name + "()] - Communication Exception: " + ce.ToString());
                    return null;
                }
            }
            else
            {
                //isTestMode:
                return (new UserInfo("yourmxitid", "yourname", DateTime.Now, "ZA", "ZA", "Johannesburg", DeviceInfo.DefaultDevice, GenderType.Male, "ZA"));
            }
        }

        /// <summary>
        /// The ConfirmPayment method, wrapped in logic that will reconnect if anything goes wrong/throws a comms exception
        /// </summary>
        public long? ConfirmPayment(int vendorId, String txRef)
        {
            try
            {
                return this.client.ConfirmPayment(vendorId, txRef);
            }
            catch (System.TimeoutException te)
            {
                Console.WriteLine(DateTime.Now.ToString() + " Timeout Exception. Going to try to reconnect...");
                logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] - Timeout Exception. Going to try to reconnect");
                this.ReConnectFull();
                return 0;
            }
            catch (System.ServiceModel.CommunicationObjectAbortedException coae)
            {
                Console.WriteLine(DateTime.Now.ToString() + " Communication Object Aborted Exception. Going to try to reconnect...");
                logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] - Communication Object Aborted Exception. Going to try to reconnect");
                this.ReConnectFull();
                return 0;
            }
            catch (System.ServiceModel.CommunicationObjectFaultedException cofe)
            {
                Console.WriteLine(DateTime.Now.ToString() + " Communication Object Faulted Exception. Going to try to reconnect...");
                logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] - Communication Object Faulted Exception. Going to try to reconnect");
                this.ReConnectFull();
                return 0;
            }
            catch (System.ServiceModel.CommunicationException ce)
            {
                Console.WriteLine(DateTime.Now.ToString() + " Communication Exception. ");
                logger.Error("[" + MethodBase.GetCurrentMethod().Name + "()] - Communication Exception: " + ce.ToString());
                return 0;
            }
        }

        /// <summary>
        /// Just a wrapper method that checks whether to connect to Prod (MXit Server) or Test service (our own little test stub)
        /// </summary>
        public void InitializeConnection(string appName, string appPassword, ExternalAppAPI.CommsCallback callback)
        {
            if (!isTestMode)
                InitializeConnectionProd(appName, appPassword, callback);
            else
                InitializeConnectionTest(appName, appPassword, callback);
        }

        /// <summary>
        /// Connect to the MXit API Server with suppplied API key and password
        /// </summary>
        private void InitializeConnectionProd(string appName, string appPassword, ExternalAppAPI.CommsCallback callback)
        {
            logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] - START");

            MyAppName = appName;
            MyAppPassword = appPassword;

            this.Connect(callback);

            if (this.IsRunning)
            {
                // Create a new keep-alive timer
                logger.Debug("[" + MethodBase.GetCurrentMethod().Name + "()] Setting up keep alive timer for period: " + this.keepAlivePeriod);
                Console.WriteLine(DateTime.Now.ToString() + " Starting keepAliveTimer...");
                this.keepAliveTimer = new Timer(new TimerCallback(this.KeepAlive), null, this.keepAlivePeriod, this.keepAlivePeriod);
            }
            logger.Debug("[" + MethodBase.GetCurrentMethod().Name + "()] - END");
        }

        /// <summary>
        /// This is where you can connect to your own test stub
        /// </summary>
        private void InitializeConnectionTest(string appName, string appPassword, ExternalAppAPI.CommsCallback callback)
        {
            //IMXitLoadTestCallback theCallback = new ServerStubCallbackClient();

            //MXitLoadTestClient serviceClient = new MxitLoadTestClient(new InstanceContext(theCallback));

            //String result = serviceClient.GetMessage("message from client");

            //Console.WriteLine("getMessage: " + result);
        }

        /// <summary>
        /// Connection workflow logic with backoff time of 60s
        /// </summary>
        public void Connect(ExternalAppAPI.CommsCallback callback)
        {
            logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] - START");

            theCallback = callback;

            lock (this)
            {
                // Already connected
                if (this.client != null)
                {
                    throw new InvalidOperationException("Already connected.");
                }

                bool isConnected = false;
                bool isFirstTime = true;
                while (!isConnected)
                {
                    try
                    {
                        if (!isFirstTime)
                        {
                            logger.Debug("" + MethodBase.GetCurrentMethod().Name + "()] Waiting for 60s before trying to reconnect.");
                            Console.WriteLine(DateTime.Now.ToString() + " Waiting for 60s before trying to reconnect");
                            isFirstTime = false;
                            Thread.Sleep(60 * 1000);
                        }

                        logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Creating commsClient...");
                        Console.WriteLine(DateTime.Now.ToString() + " Creating commsClient...");
                        // Assign the context to the client

                        this.client = new ExternalAppAPI.CommsClient(new InstanceContext(theCallback));
                    }
                    catch (Exception ex)
                    {
                        logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Problem creating commsClient:" + ex.ToString());
                        Console.WriteLine(DateTime.Now.ToString() + " Problem creating commsClient, going to keep on trying");
                        isFirstTime = false;
                    }
                    
                    try
                    {
                        // We're now ready to connect
                        logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Connecting to MXit API as "+ MyAppName);

                        Console.WriteLine(DateTime.Now.ToString() + " Connecting to MXit Server... ("+MyAppName+")");
                        this.client.Connect(MyAppName, MyAppPassword, SDK.Instance);
                        
                        Console.WriteLine(DateTime.Now.ToString() + " CONNECTED!");
                        isConnected = true;
                        logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Connected.");
                    }
                    catch (Exception ex)
                    {
                        logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Problem connecting to MXit Server:" + ex.ToString());
                        Console.WriteLine(DateTime.Now.ToString() + " Problem connecting to MXit Server, going to keep on trying");
                        isFirstTime = false;
                    }
                }

                logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] - END");
            }
        }

        /// <summary>
        /// This wraps the connect method with re-try logic and backoff times
        /// </summary>
      public void ReConnectFull()
        {
            logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] - START");

            logger.Info("[" + MethodBase.GetCurrentMethod().Name + "()] - Reconnecting.");

            if (!isReconnecting)
            {
                lock (this)
                {
                    if (!isReconnecting)
                    {
                        isReconnecting = true;

                        try
                        {
                            // Disconnect and close the connection to the ExternalAppAPI
                            logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Disconnecting from MXit");
                            Console.WriteLine(DateTime.Now.ToString() + " Trying to disconnect from MXit...");
                            this.client.Disconnect();
                            Console.WriteLine(DateTime.Now.ToString() + " Managed to disconnect.");
                            Console.WriteLine(DateTime.Now.ToString() + " Trying to close commsClient...");
                            this.client.Close();
                            Console.WriteLine(DateTime.Now.ToString() + " Managed to close commsClient.");
                            this.client = null;

                            logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Finished trying to close commsClient.");

                        }
                        catch (Exception ex)
                        {
                            logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "Problem disconnecting while reconnecting, this is normal:" + ex.ToString());
                        }

                        bool isConnected = false;
                        bool isFirstTime = true;
                        while (!isConnected)
                        {
                            if (!isFirstTime)
                            {
                                logger.Error("" + MethodBase.GetCurrentMethod().Name + "()] Waiting for 60s before trying to reconnect.");
                                Console.WriteLine(DateTime.Now.ToString() + " Waiting for 60s before trying to reconnect");
                                isFirstTime = false;
                                Thread.Sleep(60 * 1000);
                            }

                            try
                            {
                                Console.WriteLine(DateTime.Now.ToString() + " Trying to create new commsClient from ExternalAppAPI...");
                                // Assign the context to the client

                                this.client = new ExternalAppAPI.CommsClient(new InstanceContext(theCallback));
                            }
                            catch (System.ServiceModel.CommunicationObjectAbortedException caex)
                            {
                                logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] CommunicationObjectAbortedException: " + caex.ToString());
                                Console.WriteLine(DateTime.Now.ToString() + " Problem creating commsClient: CommunicationObjectAbortedException");
                            }
                            catch (Exception ex)
                            {
                                logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] Problem creating commsClient:" + ex.ToString());
                                Console.WriteLine(DateTime.Now.ToString() + " Problem creating commsClient.");
                            }

                            bool connectedToServer = false;
                            try
                            {

                                // We're now ready to connect
                                logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()]" + string.Format("Connecting to MXit API version {0} as {1}", this.client.Version(), MyAppName));

                                Console.WriteLine(DateTime.Now.ToString() + " Connecting to MXit Server...");
                                this.client.Connect(MyAppName, MyAppPassword, SDK.Instance);

                                connectedToServer = true;
                                Console.WriteLine(DateTime.Now.ToString() + " Connected to MXit Server, but need to start KeepAlive.");

                                logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] Connected.");

                            }
                            catch (Exception ex)
                            {
                                logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Problem connecting to MXit Server:" + ex.ToString());
                                Console.WriteLine(DateTime.Now.ToString() + " Problem connecting to MXit Server, going to keep on trying");
                                isFirstTime = false;
                            }

                            //Start KEEPALIVE TIMER
                            try
                            {
                                if (connectedToServer && (this.client != null))
                                {
                                    // Create a new keep-alive timer
                                    logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Setting up keep alive timer for period: " + this.keepAlivePeriod);
                                    Console.WriteLine(DateTime.Now.ToString() + " Starting keepAliveTimer...");
                                    this.keepAliveTimer = new Timer(new TimerCallback(this.KeepAlive), null, this.keepAlivePeriod, this.keepAlivePeriod);

                                    Console.WriteLine(DateTime.Now.ToString() + " CONNECTED. KeepAlive started!");
                                    isConnected = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] Problem connecting to MXit Server:" + ex.ToString());
                                Console.WriteLine(DateTime.Now.ToString() + " Problem connecting to MXit Server, going to keep on trying");
                                isFirstTime = false;
                                isConnected = false;
                            }

                        }
                        isReconnecting = false;
                        //this.sendAdminMessage("Reconnected to MXit Server at " + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString());
                    }
                }
                logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] - END");
            }
            else
            {
                Console.WriteLine(DateTime.Now.ToString() + " Not reconnecting because we are already trying to reconnect in another thread...");
                logger.Error("[" +MethodBase.GetCurrentMethod().Name + "()] Not reconnecting because we are already trying to reconnect in another thread");
            }
        }

        /// <summary>
        /// Disconnect from the MXit External App API
        /// </summary>
        public void Disconnect()
        {
            logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] - START");
            lock (this)
            {
                // Server not connected
                if (this.client == null)
                {
                    logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Not Connected");
                    throw new InvalidOperationException("Not connected.");
                }

                try
                {
                    // Dispose our keep-alive timer
                    logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Killing the keep-alive timer");
                    Console.WriteLine(DateTime.Now.ToString() + " Stopping keepAliveTimer...");
                    this.keepAliveTimer.Dispose();
                }
                catch (Exception ex)
                {
                    logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Problem stopping keepAlive:" + ex.ToString());
                    Console.WriteLine(DateTime.Now.ToString() + " Problem stopping keepAlive!");
                }
                this.keepAliveTimer = null;

                try
                {
                    // Disconnect and close the connection to the ExternalAppAPI
                    logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Disconnecting from MXit");
                    Console.WriteLine(DateTime.Now.ToString()+ " Disconnecting commsClient...");
                    this.client.Disconnect();
                }
                catch (Exception ex)
                {
                    logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Problem disconnecting:" + ex.ToString());
                    Console.WriteLine(DateTime.Now.ToString() + " Problem disconnecting commsClient!");
                }

                try
                {
                    this.client.Close();
                }
                catch (Exception ex)
                {
                    logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Problem disconnecting:" + ex.ToString());
                    Console.WriteLine(DateTime.Now.ToString() + " Problem closing commsClient!");
                }
                this.client = null;

                // Disconnected!
                logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] Disconnected.");
                Console.WriteLine(DateTime.Now.ToString() + " DISCONNECTED.");

            }
        }

        /// <summary>
        /// Keep-alive method to maintain the connection to MXit's ExternalAppAPI.<br />
        /// <br />
        /// This method should be called periodically to prevent an active WCF connection from
        /// being closed due to a timeout, or establish a new connection in-case of a communication
        /// failure.
        /// </summary>
        /// <param name="stateInfo">Timer state info (ignored).</param>
        private void KeepAlive(object stateInfo)
        {
            if (!isKeepAliveRunning)
            {
                isKeepAliveRunning = true;
                // Log a message
                logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] - START");

                try
                {
                    // Fire off a keep-alive to prevent the connection from being closed due to a timeout

                    if (!this.isReconnecting)
                    {
                        //Console.WriteLine("Sending keepAlive to MXit Server...");
                        this.client.KeepAlive();
                        //Console.WriteLine("Sent keepAlive to MXit Server.");
                    }
                    else
                    {
                        Console.WriteLine("Not running keepAlive because ConnectionHelper is trying to reconnect!");
                    }
                }
                catch (CommunicationObjectFaultedException exKeepAliveComms)
                {
                    // Something went wrong with our comms, probably a network connection failure
                    logger.Error("[" +MethodBase.GetCurrentMethod().Name + "Network problem sending keepalive:" + exKeepAliveComms.ToString());
                    Console.WriteLine("Network problem sending keepAlive, going to try to reconnect!");
                    this.ReConnectFull();

                }
                catch (Exception exKeepAliveOther)
                {
                    // Something else went wrong while trying to call KeepAlive
                    logger.Error("[" +MethodBase.GetCurrentMethod().Name + "System problem sending keepalive:" + exKeepAliveOther.ToString());
                    Console.WriteLine("System problem sending keepAlive, going to try to reconnect!");
                    this.ReConnectFull();
                }
                isKeepAliveRunning = false;
            }
            logger.Debug("[" +MethodBase.GetCurrentMethod().Name + "()] - END");
        }


        public void RedirectRequest(MXit.Navigation.RedirectRequest redirectRequest)
        {
            try
            {
                Instance.client.RedirectUser(redirectRequest);
            }
            catch (Exception e)
            {
                Console.WriteLine(DateTime.Now.ToString() + " System Exception:" + e.ToString());
                logger.Error("[" + MethodBase.GetCurrentMethod().Name + " - - System Exception: " + e.ToString());
            }
        }

        /// <summary>
        /// The requestPayment method, wrapped in logic that will reconnect if anything goes wrong/throws a comms exception
        /// </summary>
        public ImageStripReference RegisterImageStrip(String name, Bitmap image, int frameWidth, int frameHeight, int layer)
        {
            try
            {
                return this.client.RegisterImageStrip(name, image, frameWidth, frameHeight, layer);
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now.ToString() + " Exception registering new image.");
                logger.Error("[" + MethodBase.GetCurrentMethod().Name + "()] - Exception registering new image: " + ex.ToString());
                return null;
            }
        }
    }
}
