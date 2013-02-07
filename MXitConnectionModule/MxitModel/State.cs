using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MXitConnectionModule.MxitModel
{
    public class State
    {
        public Availability Availability;
        public bool IsOnline;
        public DateTime LastModified;
        public DateTime LastOnline;
        public int Mood;
        public String StatusMessage;
    }
}
