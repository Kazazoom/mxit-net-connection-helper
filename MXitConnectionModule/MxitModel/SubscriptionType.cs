using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MXitConnectionModule.MxitModel
{
    public static class SubscriptionType
    {
        internal const byte Unknown = (byte)'N';
        internal const byte Friends = (byte)'B';
        internal const byte InviteSent = (byte)'P';
        internal const byte InviteReceived = (byte)'A';
        internal const byte Deleted = (byte)'D';
        internal const byte Rejected = (byte)'R';
    }
}
