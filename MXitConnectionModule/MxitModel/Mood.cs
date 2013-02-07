using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MXitConnectionModule.MxitModel
{
    public static class Mood
    {
        internal const int None = 0;
        internal const int Angry = 1;
        internal const int Excited = 2;
        internal const int Grumpy = 3;
        internal const int Happy = 4;
        internal const int Inlove = 5;
        internal const int Invincible = 6;
        internal const int Sad = 7;
        internal const int Hot = 8;
        internal const int Sick = 9;
        internal const int Sleepy = 10;
        internal const int Bored = 11;
        internal const int Cold = 12;
        internal const int Confused = 13;
        internal const int Hungry = 14;
        internal const int Stressed = 15;

        public static String getMoodTextValue(int mood)
        {
            switch (mood)
            {
                case 0: return "None";
                case 1: return "Angry";
                default: return "None";
            }
        }

    }
}

