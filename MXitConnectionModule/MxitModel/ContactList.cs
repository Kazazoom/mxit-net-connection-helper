using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MXitConnectionModule.MxitModel
{
    public class ContactList
    {
        public Contact[] Contacts;

        public void clearSelected()
        {
            foreach (Contact friend in Contacts)
            {
                friend.isSelected = false;
            }
        }

        public void toggleSelected(String friendMxitUserID)
        {
            Contact friend = Array.Find(Contacts,
	                            element => element.UserId == friendMxitUserID);

            if (friend != null)
            {
                friend.isSelected = !friend.isSelected;
            }
        }
    }

    
}
