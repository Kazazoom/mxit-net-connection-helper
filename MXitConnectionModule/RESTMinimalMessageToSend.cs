using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MXit;
using MXit.Messaging;
using log4net;

namespace MXitConnectionModule
{
    [Serializable]
    class RESTMinimalMessageToSend
    {
        public String Body;
        public bool ContainsMarkup = true;
        public String From = "";
        public List<RESTMessageLink> Links = new List<RESTMessageLink>();
        public String To = "";
        public bool Spool = true;
        public int SpoolTimeOut = 604800; //604800 = 7 days in seconds

        public RESTMinimalMessageToSend()
        {

        }

        
        public RESTMinimalMessageToSend(RESTMessageToSend messageToSend)
        {
            Body = messageToSend.Body;
            ContainsMarkup = messageToSend.ContainsMarkup;
            From = messageToSend.From;
            Links = messageToSend.Links;
            To = messageToSend.To;
            Spool = messageToSend.Spool;
            SpoolTimeOut = messageToSend.SpoolTimeOut;
        }
    }    
}
