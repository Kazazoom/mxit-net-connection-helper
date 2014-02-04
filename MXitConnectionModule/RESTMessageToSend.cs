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
using System.Text.RegularExpressions;
//using RestSharp.Authenticators;
using MySql.Data.MySqlClient;


namespace MXitConnectionModule
{
    [Serializable]
    public class RESTMessageToSend
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(RESTConnectionHelper));

        public String Body;
        public bool ContainsMarkup = true;
        public String From = "";
        public List<RESTMessageLink> Links = new List<RESTMessageLink>();
        public String To = "";
        public bool Spool = true;
        public int SpoolTimeOut = 604800; //604800 = 7 days in seconds
        private int _ToCount = 0;
        public int InternalReference; //Optional, used for tracking internal broadcast messages back to original message

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
            Body = ProcessAppLinks(messageToSend.Body, messageToSend.From);            
        }

        public RESTMessageToSend(String rFrom, String rBody)
        {
            From = rFrom;
            Body = ProcessAppLinks(rBody, rFrom);
        }

        public RESTMessageToSend(String rFrom, String rTo, String rBody)
        {
            From = rFrom;
            this.addMxitUserIDTo(rTo);
            Body = ProcessAppLinks(rBody, rFrom);
        }

        /// <summary>
        /// Process any App links that may be in the message Body text
        /// </summary>        
        /// 
        public string ProcessAppLinks(String msgBody, String msgFrom = "") {            
            string regexLink = @"\[.*?\|.*?\]"; //Links should follow the format: [Try this App|appid]

            MatchCollection matches = Regex.Matches(msgBody, regexLink);
            int counter = 0;
            if (matches.Count > 0)
            {
                try
                {
                    foreach (Match m in matches)
                    {
                        //break up the link [text|applink] into it's parts
                        string matchText = m.ToString();
                        matchText = matchText.Replace("[", "");
                        matchText = matchText.Replace("]", "");

                        String[] linkParts = matchText.Split(new char[] { '|' }, 3);

                        string linkText = linkParts[0];
                        string linkTarget = linkParts[1];

                        Boolean isShowInTempWindow = true;
                        if (linkParts.Length > 2)
                            isShowInTempWindow = (linkParts[2].ToLower() == "true");
                        
                        //check that the links have all the necessary parts
                        if ((linkText.Length > 0) && (linkTarget.Length > 0))
                        {
                            //if the sender of the message matches the target link, don't open link as a new contact
                            if (msgFrom == linkTarget)
                            {
                                isShowInTempWindow = false;
                            }

                            RESTMessageLink appLink = new RESTMessageLink(linkText, linkTarget, isShowInTempWindow);
                            Links.Add(appLink);

                            //replace the link with the placeholder text: {n}
                            msgBody = msgBody.Replace(m.ToString(), "{" + counter.ToString() + "}");

                            counter++;
                        }
                    }

                }
                catch (Exception e)
                {
                    logger.Error("Error parsing message body: " + msgBody, e);
                }
            }

            return msgBody;
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

        /* This method requires that you setup a sent table:
         * 
CREATE TABLE `tableprefix_rest_message_sent` (
  `RestMessageSentOID_rms` bigint(20) NOT NULL AUTO_INCREMENT,
  `InternalReference_rms` int(9) DEFAULT NULL,
  `FromContactName_rms` varchar(255) DEFAULT NULL,
  `Body_rms` varchar(1000) DEFAULT NULL,
  `DateTimeSent_rms` datetime NOT NULL,
  `To_rms` text CHARACTER SET latin1,
  `ErrorResponse_rms` text CHARACTER SET latin1,
  `ErrorResponseCode_rms` varchar(20) DEFAULT NULL,
  PRIMARY KEY (`RestMessageSentOID_rms`),
  KEY `INDEX_DATE` (`DateTimeSent_rms`)
) ENGINE=InnoDB AUTO_INCREMENT=0 DEFAULT CHARSET=utf8;
         */
        public bool persistRESTMessageSent_toDB(String RestResponseContent, String RestStatusCode)
        {
            bool success = false;

            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - START");

            using (MySqlConnection conn = new MySqlConnection(ConnectionConfig.connectionString_msgDB))
            {
                using (MySqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.CommandText = @"
                                INSERT INTO " + ConnectionConfig.DBPrefix_msgDB + @"rest_message_sent
                                (InternalReference_rms, FromContactName_rms, Body_rms, DateTimeSent_rms, To_rms, ErrorResponse_rms, ErrorResponseCode_rms) 
                                VALUES (@InternalReference, @FromContactName, @Body, now(), @To, @ErrorResponse, @ErrorResponseCode)";

                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("@FromContactName", this.From);
                        command.Parameters.AddWithValue("@Body", this.Body);
                        command.Parameters.AddWithValue("@To", this.To);
                        command.Parameters.AddWithValue("@ErrorResponse", RestResponseContent);
                        command.Parameters.AddWithValue("@ErrorResponseCode", RestStatusCode);

                        //If we have set a internalReference then store it:
                        if (this.InternalReference != null)
                        {
                            command.Parameters.AddWithValue("@InternalReference", this.InternalReference);
                        } else {
                            command.Parameters.AddWithValue("@InternalReference", null);
                        }


                        conn.Open();
                        command.ExecuteNonQuery();

                        success = true;
                    }
                    catch (Exception ex)
                    {
                        logger.Error("[" + MethodBase.GetCurrentMethod().Name + " - Problem persisting rest message sent: " + ex.ToString());
                        success = false;
                        return success;
                    } //catch
                }//using - command
            }//using - connection

            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - END");

            return success;
        }
    }
}
