using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Common;
using System.Linq;
using System;

namespace EFHooks
{
	/// <summary>
	/// An Entity Framework DbContext that can be hooked into by registering EFHooks.IHook objects.
	/// </summary>
	public abstract class HookedDbContext : DbContext
    {
        /// <summary>
        /// Hook decorator to support ordering
        /// </summary>
        protected class HookDecorator
        {
            /// <summary>
            /// The hook.
            /// </summary>
            public IHook Hook { get; set; }
            /// <summary>
            /// The order.
            /// </summary>
            public Int32? Order { get; set; }
        }

		/// <summary>
		/// The hooks.
		/// </summary>
        protected IList<HookDecorator> _hooks;
		/// <summary>
		/// The exception hooks.
		/// </summary>
        protected IList<IExceptionHook> _exceptionHooks;

		/// <summary>
		/// Initializes a new instance of the <see cref="HookedDbContext" /> class, initializing empty lists of hooks.
		/// </summary>
		public HookedDbContext()
		{
            _hooks = new List<HookDecorator>();
		}

		/// <summary>
        /// Initializes a new instance of the <see cref="HookedDbContext" /> class, filling <see cref="_hooks"/> and <see cref="_hooks"/>.
		/// </summary>
		/// <param name="hooks">The hooks.</param>
		public HookedDbContext(IHook[] hooks)
		{
            _hooks = hooks.Select(h => new HookDecorator { Hook = h }).ToList();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="HookedDbContext" /> class, using the specified <paramref name="nameOrConnectionString"/>, initializing empty lists of hooks.
		/// </summary>
		/// <param name="nameOrConnectionString">The name or connection string.</param>
		public HookedDbContext(string nameOrConnectionString)
			: base(nameOrConnectionString)
        {
            _hooks = new List<HookDecorator>();
		}

		/// <summary>
        /// Initializes a new instance of the <see cref="HookedDbContext" /> class, using the specified <paramref name="nameOrConnectionString"/>, , filling <see cref="_hooks"/> and <see cref="_hooks"/>.
		/// </summary>
		/// <param name="hooks">The hooks.</param>
		/// <param name="nameOrConnectionString">The name or connection string.</param>
		public HookedDbContext(IHook[] hooks, string nameOrConnectionString)
			: base(nameOrConnectionString)
        {
            _hooks = hooks.Select(h => new HookDecorator { Hook = h }).ToList();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="HookedDbContext" /> class using the an existing connection to connect 
		/// to a database. The connection will not be disposed when the context is disposed. (see <see cref="DbContext"/> overloaded constructor)
		/// </summary>
		/// <param name="existingConnection">An existing connection to use for the new context.</param>
		/// <param name="contextOwnsConnection">If set to true the connection is disposed when the context is disposed, otherwise the caller must dispose the connection.</param>
		/// <remarks>Main reason for allowing this, is to enable reusing another database connection. (For instance one that is profiled by Miniprofiler (http://miniprofiler.com/)).</remarks>
		public HookedDbContext(DbConnection existingConnection, bool contextOwnsConnection)
			: base(existingConnection, contextOwnsConnection)
        {
            _hooks = new List<HookDecorator>();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="HookedDbContext" /> class using the an existing connection to connect 
		/// to a database. The connection will not be disposed when the context is disposed. (see <see cref="DbContext"/> overloaded constructor)
		/// </summary>
		/// <param name="hooks">The hooks.</param>
		/// <param name="existingConnection">An existing connection to use for the new context.</param>
		/// <param name="contextOwnsConnection">If set to true the connection is disposed when the context is disposed, otherwise the caller must dispose the connection.</param>
		/// <remarks>Main reason for allowing this, is to enable reusing another database connection. (For instance one that is profiled by Miniprofiler (http://miniprofiler.com/)).</remarks>
		public HookedDbContext(IHook[] hooks, DbConnection existingConnection, bool contextOwnsConnection)
			: base(existingConnection, contextOwnsConnection)
        {
            _hooks = hooks.Select(h => new HookDecorator { Hook = h }).ToList();
		}
		/// <summary>
		/// Registers a hook
		/// </summary>
        /// <param name="hook">The hook to register.</param>
        /// <param name="order">Optional ordering to run the hook in</param>
		public void RegisterHook(IHook hook, Int32? order = null)
		{
            _hooks.Add(new HookDecorator { Hook = hook, Order = order });
		}

		/// <summary>
		/// Saves all changes made in this context to the underlying database.
		/// </summary>
		/// <returns>
		/// The number of objects written to the underlying database.
		/// </returns>
		public override int SaveChanges()
		{
            var modifiedEntries = this.ChangeTracker.Entries()
                                            .Where(x => x.State != EntityState.Unchanged && x.State != EntityState.Detached)
                                            .Select(x => new HookedEntityEntry { Entry = x, PreSaveState = x.State })
                                            .ToList();

			ExecutePreActionHooks(modifiedEntries, false);//Regardless of validation (executing the hook possibly fixes validation errors)

			var hasValidationErrors = this.Configuration.ValidateOnSaveEnabled && this.ChangeTracker.Entries().Any(x => x.State != EntityState.Unchanged && !x.GetValidationResult().IsValid);

			if (!hasValidationErrors)
			{
				ExecutePreActionHooks(modifiedEntries, true);
			}

            var changes = 0;
            try
            {
                changes = base.SaveChanges();
            }
            catch (Exception e)
            {
                // Run all 'Exception' hooks
                ExecuteExceptionHooks(e);
                throw;
            }

            ExecutePostActionHooks(modifiedEntries);

			return changes;
		}
		/// <summary>
		/// Executes the pre action hooks, filtered by <paramref name="requiresValidation"/>.
		/// </summary>
		/// <param name="modifiedEntries">The modified entries to execute hooks for.</param>
		/// <param name="requiresValidation">if set to <c>true</c> executes hooks that require validation, otherwise executes hooks that do NOT require validation.</param>
		private void ExecutePreActionHooks(IEnumerable<HookedEntityEntry> modifiedEntries, bool requiresValidation)
		{
            if (!_hooks.Any(h => h is IPreActionHook)) return;

			foreach (var entry in modifiedEntries)
			{
                // For each preaction hook ordered by Order where null values are last (since null means it doesn't matter what order they have)
				foreach (
					var hook in _hooks.Where( h => h.Hook is IPreActionHook )
                        .Where(x => (x.Hook.HookStates & entry.PreSaveState) == entry.PreSaveState && (x.Hook as IPreActionHook).RequiresValidation == requiresValidation)
                        .OrderByDescending( x => x.Order.HasValue ).ThenBy( x => x.Order )
                        )
				{
                    var metadata = new HookEntityMetadata(entry.PreSaveState, this, entry.Entry);
                    hook.Hook.HookObject(entry.Entry.Entity, metadata);
				}
			}
		}

        private void ExecutePostActionHooks(IEnumerable<HookedEntityEntry> modifiedEntries)
        {
            if (!_hooks.Any(h => h is IPostActionHook)) return;

            foreach (var entityEntry in modifiedEntries)
            {
                var entry = entityEntry;

                //Obtains hooks that 'listen' to one or more Entity States
                foreach (var hook in _hooks.Where(h => h is IPostActionHook)
                    .Where(x => (x.Hook.HookStates & entry.PreSaveState) == entry.PreSaveState)
                    .OrderByDescending(x => x.Order.HasValue).ThenBy(x => x.Order)
                    )
                {
                    var metadata = new HookEntityMetadata(entry.PreSaveState, this, entry.Entry);
                    hook.Hook.HookObject(entry.Entry.Entity, metadata);
                }
            }

        }

        private void ExecuteExceptionHooks(Exception e)
        {
            if (_exceptionHooks.Count == 0) return;

            foreach( var hook in _exceptionHooks )
            {
                hook.Exception(e, this);
            }
        }
	}
}