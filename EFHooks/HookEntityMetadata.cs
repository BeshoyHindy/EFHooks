using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;

namespace EFHooks
{
	/// <summary>
	/// Contains entity state, and an indication wether is has been changed.
	/// </summary>
	public class HookEntityMetadata
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="HookEntityMetadata" /> class.
        /// </summary>
        /// <param name="state">The presave state.</param>
		/// <param name="entry">The db entry.</param>
        /// <param name="context">The existing context.</param>
		public HookEntityMetadata( EntityState state, HookedDbContext context, DbEntityEntry entry = null)
		{
            PreSaveState = state;
            CurrentEntry = entry;
            CurrentContext = context;
		}

        /// <summary>
        /// The presave state
        /// </summary>
        public EntityState PreSaveState { get; private set; }
        /// <summary>
        /// The current db entry
        /// </summary>
        public DbEntityEntry CurrentEntry { get; private set; }
		/// <summary>
        /// The current context.
		/// </summary>
		public HookedDbContext CurrentContext { get; private set; }

	}
}