using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MXitConnectionModule.MxitModel
{
    public class ContactList
    {
        public Contact[] Contacts;
        public int selectedFriendCount { get; private set; }

        public ContactList()
        {
            selectedFriendCount = 0;
        }

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
                if (friend.isSelected)
                {
                    friend.isSelected = false;
                    lock (this) selectedFriendCount--;
                }
                else
                {
                    friend.isSelected = true;
                    lock (this) selectedFriendCount++;
                }
                
            }
        }

        public void selectAll()
        {
            foreach (Contact friend in Contacts)
            {
                friend.isSelected = true;
            }

            selectedFriendCount = Contacts.Length;
        }

        public void unSelectAll()
        {
            foreach (Contact friend in Contacts)
            {
                friend.isSelected = false;
            }

            selectedFriendCount = 0;
        }


    }

    
}
