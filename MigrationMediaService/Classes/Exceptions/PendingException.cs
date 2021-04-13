using System;
using System.Collections.Generic;
using System.Text;

namespace MigrationMediaService.Classes.Exceptions
{
    public class PendingException : Exception
    {
        public PendingException(string msg) : base(msg)
        { }
    }
}
