﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sample.Auditing.Data.Entities;
using Sample.Auditing.Data.Entities.Configurations;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sample.Auditing.Data
{
    public class CatalogDbContext : DbContext
    {
        private readonly ILoggerFactory loggerFactory;

        public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
         : base(options)
        {
        }

        public CatalogDbContext(DbContextOptions<CatalogDbContext> options, ILoggerFactory loggerFactory)
          : base(options)
        {
            this.loggerFactory = loggerFactory;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseLoggerFactory(loggerFactory);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfiguration(new ProductConfiguration());
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            var auditEntries = GetAudits();
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            await SaveAuditsAsync(auditEntries);
            return result;
        }


        IEnumerable<Audit> GetAudits()
        {
            throw new NotImplementedException();
        }

        async Task SaveAuditsAsync(IEnumerable<Audit> audits)
        {
            ChangeTracker.DetectChanges();
            var auditEntries = new List<Audit>();

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is Audit || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditEntry = new Audit()
                {
                    TableName = entry.Metadata.Relational().TableName,
                    Action = Enum.GetName(typeof(EntityState), entry.State)
                };
                auditEntries.Add(auditEntry);

                //foreach (var property in entry.Properties)
                //{
                //    if (property.IsTemporary)
                //    {
                //        // value will be generated by the database, get the value after saving
                //        auditEntry.TemporaryProperties.Add(property);
                //        continue;
                //    }

                //    string propertyName = property.Metadata.Name;
                //    if (property.Metadata.IsPrimaryKey())
                //    {
                //        auditEntry.KeyValues[propertyName] = property.CurrentValue;
                //        continue;
                //    }

                //    switch (entry.State)
                //    {
                //        case EntityState.Added:
                //            auditEntry.NewValues[propertyName] = property.CurrentValue;
                //            break;

                //        case EntityState.Deleted:
                //            auditEntry.OldValues[propertyName] = property.OriginalValue;
                //            break;

                //        case EntityState.Modified:
                //            if (property.IsModified)
                //            {
                //                auditEntry.OldValues[propertyName] = property.OriginalValue;
                //                auditEntry.NewValues[propertyName] = property.CurrentValue;
                //            }
                //            break;
                //    }
                //}
            }
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Audit> Audits { get; set; }
    }
}