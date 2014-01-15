using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EFHooks
{
    /// <summary>
    /// A hook for when an exception happens while saving changes
    /// </summary>
    public interface IExceptionHook
    {
        /// <summary>
        /// The method called when there is an exception
        /// </summary>
        void Exception(Exception e, HookedDbContext context);
    }
}
