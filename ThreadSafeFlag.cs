using System;

namespace WalkieTalkieApp
{
    public class ThreadSafeFlag
    {
        private volatile bool flag; // Renombrado para evitar ambigüedad

        public bool Value
        {
            get { return flag; }
            set { flag = value; }
        }
    }
}