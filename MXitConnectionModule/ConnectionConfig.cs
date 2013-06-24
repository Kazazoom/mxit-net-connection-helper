using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using log4net;


namespace MXitConnectionModule
{
    using System.Configuration;

    public static class ConnectionConfig
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(ConnectionConfig));

        #region Constructors

        /// <summary>
        /// Initializes static members of the <see cref="Config"/> class.
        /// </summary>
        static ConnectionConfig()
        {
            LoadConfig();
        }

        #endregion Constructors


        #region Public Properties

        public static int QueueHelperMaxThreads_IncomingMessage { get; set; }
        public static int QueueHelperMaxThreads_OutgoingMessage { get; set; }
        public static int QueueHelperMaxThreads_OutgoingRESTMessage { get; set; }
        //For persisting messages sent - Start:
        internal static string DBPrefix_msgDB { get; set; }
        internal static String connectionString_msgDB;
        private static string tempDBName_msgDB = "non assigned variable";
        private static string tempDBPass_msgDB = "non assigned variable";
        private static string tempDBUser_msgDB = "non assigned variable";
        private static string tempDBServer_msgDB = "non assigned variable";
        internal static Int16 tempDBMaxPoolSize_msgDB;
        //For persisting messages sent - End:

        #endregion Public Properties

        public static void ReloadConfig()
        {
            ConfigurationManager.RefreshSection("appSettings");
            LoadConfig();
        }

        #region Private Methods

        /// <summary>
        /// Loads the ConnectionConfig.
        /// </summary>
        private static void LoadConfig()
        {
            String temp;

            temp = ConfigurationManager.AppSettings["QueueHelperMaxThreads_IncomingMessage"];
            if (!string.IsNullOrEmpty(temp))
            {
                int tempInt = Convert.ToInt32(temp);
                QueueHelperMaxThreads_IncomingMessage = tempInt;
            }
            else
            {
                QueueHelperMaxThreads_IncomingMessage = 20;
                Console.WriteLine(DateTime.Now.ToString() + " " + "WARNING: Defaulting config value: QueueHelperMaxThreads_IncomingMessage = 20");
            }

            temp = ConfigurationManager.AppSettings["QueueHelperMaxThreads_OutgoingMessage"];
            if (!string.IsNullOrEmpty(temp))
            {
                int tempInt = Convert.ToInt32(temp);
                QueueHelperMaxThreads_OutgoingMessage = tempInt;
            }
            else
            {
                QueueHelperMaxThreads_OutgoingMessage = 20;
                Console.WriteLine(DateTime.Now.ToString() + " " + "WARNING: Defaulting config value: QueueHelperMaxThreads_OutgoingMessage = 20");
            }

            temp = ConfigurationManager.AppSettings["QueueHelperMaxThreads_OutgoingRESTMessage"];
            if (!string.IsNullOrEmpty(temp))
            {
                int tempInt = Convert.ToInt32(temp);
                QueueHelperMaxThreads_OutgoingRESTMessage = tempInt;
            }
            else
            {
                QueueHelperMaxThreads_OutgoingRESTMessage = 20;
                Console.WriteLine(DateTime.Now.ToString() + " " + "WARNING: Defaulting config value: QueueHelperMaxThreads_OutgoingRESTMessage = 20");
            }

            // **************************** UserDB *************************************** - Start

            temp = ConfigurationManager.AppSettings["DBPrefix_msgDB"];
            if (!string.IsNullOrEmpty(temp))
            {
                DBPrefix_msgDB = temp;
            }

            tempDBName_msgDB = ConfigurationManager.AppSettings["DatabaseName_msgDB"];
            if (!string.IsNullOrEmpty(tempDBName_msgDB))
            {

                tempDBName_msgDB = tempDBName_msgDB.ToLowerInvariant();
                logger.Debug("[Controller:Controller()] DatabaseName (msgDB):" + tempDBName_msgDB);
            }

            tempDBUser_msgDB = ConfigurationManager.AppSettings["DatabaseUser_msgDB"];
            if (!string.IsNullOrEmpty(tempDBUser_msgDB))
            {
                tempDBUser_msgDB = tempDBUser_msgDB.ToLowerInvariant();
                logger.Debug("[Controller:Controller()] DatabaseUser (msgDB):" + tempDBUser_msgDB);
            }

            tempDBPass_msgDB = ConfigurationManager.AppSettings["DatabasePass_msgDB"];
            if (!string.IsNullOrEmpty(tempDBPass_msgDB))
            {
                tempDBPass_msgDB = tempDBPass_msgDB.ToLowerInvariant();
                logger.Debug("[Controller:Controller()] DatabasePassword (msgDB):" + tempDBPass_msgDB);
            }

            tempDBServer_msgDB = ConfigurationManager.AppSettings["DatabaseServer_msgDB"];
            if (!string.IsNullOrEmpty(tempDBServer_msgDB))
            {
                tempDBServer_msgDB = tempDBServer_msgDB.ToLowerInvariant();
                logger.Debug("[Controller:Controller()] DatabaseServer (msgDB):" + tempDBServer_msgDB);
            }

            temp = ConfigurationManager.AppSettings["DBMaxPoolSize_msgDB"];
            if (!string.IsNullOrEmpty(temp))
            {
                Int16 tempInt = Convert.ToInt16(temp);
                tempDBMaxPoolSize_msgDB = tempInt;
                logger.Debug("[Controller:Controller()] DBMaxPoolSize:" + tempDBMaxPoolSize_msgDB);
            }
            else
            {
                tempDBMaxPoolSize_msgDB = 5;
            }

            connectionString_msgDB = "SERVER=" + tempDBServer_msgDB + ";"
                + "DATABASE=" + tempDBName_msgDB + ";"
                + "UID=" + tempDBUser_msgDB + ";"
                + "PASSWORD=" + tempDBPass_msgDB + ";"
                + "Pooling=true;"
                + "Min Pool Size=2;"
                + "Max Pool Size=" + tempDBMaxPoolSize_msgDB + ";";

            Console.WriteLine(DateTime.Now.ToString() + " Connecting to msgDB database: " + tempDBServer_msgDB);

            // **************************** UserDB *************************************** - End

        }

        #endregion Private Methods
    }
}
