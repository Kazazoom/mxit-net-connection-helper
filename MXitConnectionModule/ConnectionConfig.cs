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

        }

        #endregion Private Methods
    }
}
