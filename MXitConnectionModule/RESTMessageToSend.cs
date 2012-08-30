/// Mxit-Net-Connection-Helper
/// Author: Eric Clements (Kazazoom) - eric@kazazoom.com
/// License: BSD-3 (https://github.com/Kazazoom/mxit-net-connection-helper/blob/master/license.txt)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using log4net;
using MXit;
using MXit.Messaging;

//using RestSharp;
using Newtonsoft;
using Newtonsoft.Json;
//using RestSharp.Authenticators;


namespace MXitConnectionModule
{
    [Serializable]
    public class RESTMessageToSend
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(RESTConnectionHelper));

        public String Body;
        public bool ContainsMarkup = true;
        public String From = ""; 
        public String To = "";
        private int _ToCount = 0;

        public RESTMessageToSend()
        {

        }

        public RESTMessageToSend(MessageReceived messageReceived)
        {
            From = messageReceived.To;
            To = messageReceived.From;
        }

        public RESTMessageToSend(MessageToSend messageToSend)
        {
            From = messageToSend.From;
            this.addMxitUserIDTo(messageToSend.To);
            Body = messageToSend.Body;
        }

        public RESTMessageToSend(String rFrom, String rBody)
        {
            From = rFrom;
            Body = rBody;
        }

        public RESTMessageToSend(String rFrom, String rTo, String rBody)
        {
            From = rFrom;
            this.addMxitUserIDTo(rTo);
            Body = rBody;
        }

        /// <summary>
        /// Add additional MxitUserID's to the 'To' field.
        /// MxitUserIDsString needs to be command delimited string of MxitUserIDs
        /// </summary>
        public bool addMxitUserIDTo(String MxitUserIDsString)
        {
            bool success = false;

            try
            {
                if (!String.IsNullOrEmpty(To))
                {
                    To = To + "," + MxitUserIDsString;
                }
                else
                {
                    To = MxitUserIDsString;
                }
                
                int newUserIDCount = MxitUserIDsString.Split(',').Length;
                _ToCount += newUserIDCount;

                success = true;
            }
            catch (Exception ex)
            {
                logger.Error(MethodBase.GetCurrentMethod().Name + "() - Exception: " + ex.ToString());
                return false;
            }

            return success;
        }

        public int getToUserCount()
        {
            return _ToCount;
        }

    }
}
