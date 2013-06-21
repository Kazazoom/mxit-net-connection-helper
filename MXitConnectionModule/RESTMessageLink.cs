using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MXitConnectionModule
{
    [Serializable]
    public struct RESTMessageLink
    {
        public bool CreateTemporaryContact;
        public string TargetService;
        public string Text;

        public RESTMessageLink(string linkText, string linkTarget, bool linkTempContact = true) {
            Text = linkText;
            TargetService = linkTarget;
            CreateTemporaryContact = linkTempContact;
        }
    }
}
