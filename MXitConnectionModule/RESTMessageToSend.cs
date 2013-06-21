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

                        String[] linkParts = matchText.Split(new char[] { '|' }, 2);

                        string linkText = linkParts[0];
                        string linkTarget = linkParts[1];
                        
                        //check that the links have all the necessary parts
                        if ((linkText.Length > 0) && (linkTarget.Length > 0))
                        {
                            bool linkTempContact = true;
                            //if the sender of the message matches the target link, don't open link as a new contact
                            if (msgFrom == linkTarget)
                            {
                                linkTempContact = false;
                            }

                            RESTMessageLink appLink = new RESTMessageLink(linkText, linkTarget, linkTempContact);
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

    }
}
