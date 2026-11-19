using System.Collections.Concurrent;
using ConsentGateway.Domain;

namespace ConsentGateway.Audit;

/// <summary>Shaped after FHIR AuditEvent: who, what, why, and the outcome.</summary>
public sealed record AuditEvent(
    DateTimeOffset Recorded,
    string UserId,
    Role Role,
    Purpose Purpose,
    string Action,
    string PatientId,
    string? Category,
    bool Permitted,
    string Reason,
    bool BreakGlass);

/// <summary>Append-only audit trail. Production: ship to immutable storage and a SIEM, keep for 6+ years.</summary>
public sealed class AuditLog
{
    private readonly ConcurrentQueue<AuditEvent> _events = new();

    public void Record(AuditEvent auditEvent) => _events.Enqueue(auditEvent);

    public IReadOnlyList<AuditEvent> All() => _events.ToList();
}
