﻿using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sample.Auditing.Data.Entities;
using Sample.Auditing.Data.Entities.Configurations;
using Sample.Auditing.Data.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Sample.Auditing.Data
{
    public class CatalogDbContext : DbContext
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IHttpContextAccessor httpContextAccessor;


        public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
         : base(options)
        {
        }

        public CatalogDbContext(DbContextOptions<CatalogDbContext> options, ILoggerFactory loggerFactory, IHttpContextAccessor httpContextAccessor)
          : base(options)
        {
            this.loggerFactory = loggerFactory;
            this.httpContextAccessor = httpContextAccessor;
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
            var temoraryAuditModels = AuditNonTemporaryProperties();
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            await AuditTemporaryProperties(temoraryAuditModels);

            return result;
        }


        IEnumerable<AuditModel> AuditNonTemporaryProperties()
        {
            ChangeTracker.DetectChanges();
            var auditModels = new List<AuditModel>();

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is Audit || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditEntry = new AuditModel()
                {
                    TableName = entry.Metadata.Relational().TableName,
                    Action = entry.State,
                    Username = this.httpContextAccessor.HttpContext.User?.Identity?.Name
                };

                auditModels.Add(auditEntry);

                foreach (var property in entry.Properties)
                {
                    if (property.IsTemporary)
                    {
                        // value will be generated by the database, get the value after saving
                        auditEntry.TemporaryProperties.Add(property);
                        continue;
                    }

                    string propertyName = property.Metadata.Name;
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue;
                        continue;
                    }

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                            break;

                        case EntityState.Deleted:
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            break;

                        case EntityState.Modified:
                            if (property.IsModified)
                            {
                                auditEntry.OldValues[propertyName] = property.OriginalValue;
                                auditEntry.NewValues[propertyName] = property.CurrentValue;
                            }
                            break;
                    }
                }
            }

            return auditModels;

        }

        async Task AuditTemporaryProperties(IEnumerable<AuditModel> temoraryAuditModels)
        {
            if (temoraryAuditModels != null && temoraryAuditModels.Any())
            {
                foreach (var auditEntry in temoraryAuditModels)
                {
                    // Get the final value of the temporary properties
                    foreach (var prop in auditEntry.TemporaryProperties)
                    {
                        if (prop.Metadata.IsPrimaryKey())
                        {
                            auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                        }
                        else
                        {
                            auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                        }
                    }
                    // Save the Audit entry
                    Audits.Add(auditEntry.ToAudit());
                }

                await SaveChangesAsync();
            }
            await Task.CompletedTask;
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Audit> Audits { get; set; }
    }
}
