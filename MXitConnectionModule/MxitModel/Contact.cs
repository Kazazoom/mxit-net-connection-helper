using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MXitConnectionModule.MxitModel
{
    public class Contact
    {
        public String DisplayName;
        public String AvatarId;
        public MxitModel.State State;
        public String UserId;
        public bool Blocked;
        public String Group;
        public Int16 ContactType;
        public Int16 SubscriptionType;

        //Custom parameters:
        public bool isSelected = false;
    }
}
