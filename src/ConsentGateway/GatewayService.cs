using ConsentGateway.Audit;
using ConsentGateway.Domain;
using ConsentGateway.Policy;

namespace ConsentGateway;

public sealed record GatewayResult(bool Permitted, string Reason, object? Body);

/// <summary>Policy enforcement point: decide, audit (including denials), fetch, then apply obligations.</summary>
public sealed class GatewayService(ClinicalStore data, AccessPolicy policy, DeIdentifier deIdentifier, AuditLog audit, TimeProvider clock)
{
    public GatewayResult ReadPatient(Caller caller, string patientId)
    {
        var decision = Authorize(caller, "read Patient", patientId, null);
        if (!decision.Permit)
        {
            return new(false, decision.Reason, null);
        }

        var patient = data.FindPatient(patientId);
        if (patient is null)
        {
            return new(true, "Not found", null);
        }

        object body = decision.Obligations.HasFlag(Obligation.DeIdentify)
            ? deIdentifier.Patient(patient, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
            : patient;
        return new(true, decision.Reason, body);
    }

    public GatewayResult SearchObservations(Caller caller, string patientId, string category)
    {
        var decision = Authorize(caller, "search Observation", patientId, category);
        if (!decision.Permit)
        {
            return new(false, decision.Reason, null);
        }

        var observations = data.Observations(patientId, category).ToList();
        var entries = decision.Obligations.HasFlag(Obligation.DeIdentify)
            ? observations.Select(o => deIdentifier.Observation(o)).ToList()
            : observations.Cast<object>().ToList();

        return new(true, decision.Reason, new { resourceType = "Bundle", type = "searchset", total = entries.Count, entry = entries });
    }

    private Decision Authorize(Caller caller, string action, string patientId, string? category)
    {
        var decision = policy.Decide(caller, patientId, category);
        audit.Record(new AuditEvent(
            clock.GetUtcNow(),
            caller.UserId,
            caller.Role,
            caller.Purpose,
            action,
            patientId,
            category,
            decision.Permit,
            decision.Reason,
            decision.Obligations.HasFlag(Obligation.NotifyPrivacyOfficer)));
        return decision;
    }
}
