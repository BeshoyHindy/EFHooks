Fork of EFHooks

Added support for Ordered hooks and exception hooking

Ordered hooking allows users to set an order to triggers

Example


            this.RegisterHook(new IdPreInsertHook(), 0);
            this.RegisterHook(new DeletablePreDeleteHook(), 1);
            this.RegisterHook(new VersionHook(), 2);
            this.RegisterHook(new StampHook(), 3);
            this.RegisterHook(new FingerprintHook(), 4);
            
Exception hooking is mainly used for Audit trails - if you hook events to create an audit log automatically in your app, you need to be able to back out the audits if a commit should throw an exception.

Example Audit hook:

    private class AuditHook : PreActionHook<IAudit>, IExceptionHook
    {
        public AuditHook()
        {
            _audits = new List<AuditLog>();
        }

        // Keep a list around so we can back these records out if the commit fails
        private IList<AuditLog> _audits;

        public override void Hook(IAudit entity, HookEntityMetadata metadata)
        {
            // Note that if we were not using a client calculated Guid Id we would have to audit the Added records after a commit because EF won't generate an Id for us

            Guid? entryId = null;
            if (entity is IId)
                entryId = (entity as IId).Id;

            var entityType = entity.GetType();
            entityType = ObjectContext.GetObjectType(entityType);

            AuditLog audit = null;

            if (metadata.PreSaveState == EntityState.Deleted)
                audit = new AuditLog { Created = DateTime.UtcNow, Entity = entityType.Name, EntityId = entryId, Operation = AuditOperation.DELETE };

            if (metadata.PreSaveState == EntityState.Modified)
                audit = new AuditLog { Created = DateTime.UtcNow, Entity = entityType.Name, EntityId = entryId, Operation = AuditOperation.UPDATE };

            if (metadata.PreSaveState == EntityState.Added)
                audit = new AuditLog { Created = DateTime.UtcNow, Entity = entityType.Name, EntityId = entryId, Operation = AuditOperation.CREATE };

            if( audit == null ) return;

            _audits.Add(audit);
            metadata.CurrentContext.Set<AuditLog>().Add(audit);
        }

        public void Exception(Exception e, HookedDbContext context)
        {
            // Back out the audit changes since the save failed
            context.Set<AuditLog>().RemoveRange(_audits);
        }

        public override EntityState HookStates
        {
            get { return EntityState.Added | EntityState.Deleted | EntityState.Modified; }
        }
    }
            
