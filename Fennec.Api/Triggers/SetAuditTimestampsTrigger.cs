using EntityFrameworkCore.Triggered;
using Fennec.Api.Models;
using Fennec.Api.Services;
using JetBrains.Annotations;

namespace Fennec.Api.Triggers;

[UsedImplicitly]
public class SetAuditTimestampsTrigger(
    IClockService clockService    
) : IBeforeSaveTrigger<EntityBase>
{
    public Task BeforeSave(ITriggerContext<EntityBase> context, CancellationToken cancellationToken)
    {
        switch (context.ChangeType)
        {
            case ChangeType.Added:
                context.Entity.CreatedAt = clockService.GetCurrentInstant();
                context.Entity.UpdatedAt = context.Entity.CreatedAt;
                break;
            
            case ChangeType.Modified:
                context.Entity.UpdatedAt = clockService.GetCurrentInstant();
                break;
        }
        
        return Task.CompletedTask;
    }
}