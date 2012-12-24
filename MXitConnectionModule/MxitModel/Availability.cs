using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MXitConnectionModule.MxitModel
{
    public enum Availability: int
    {
        Connected = 0,
        ReadyToChat = 1,
        Busy = 2,
        Reachable = 3,
        Disconnected = 4
    }
}
