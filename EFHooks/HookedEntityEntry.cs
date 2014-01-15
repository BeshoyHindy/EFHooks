using System.Data.Entity;
using System.Data.Entity.Infrastructure;

namespace EFHooks
{
    internal class HookedEntityEntry
    {
        public DbEntityEntry Entry { get; set; }
		/// <summary>
		/// Gets or sets the state of the entity before saving.
		/// </summary>
		/// <value>
		/// The state of the entity before saving.
		/// </value>
        public EntityState PreSaveState { get; set; }
    }
}