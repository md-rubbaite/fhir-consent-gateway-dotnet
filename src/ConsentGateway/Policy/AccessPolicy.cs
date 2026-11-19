using ConsentGateway.Domain;

namespace ConsentGateway.Policy;

[Flags]
public enum Obligation
{
    None = 0,

    /// <summary>Strip direct identifiers and generalise quasi-identifiers before returning data.</summary>
    DeIdentify = 1,

    /// <summary>Record as break-glass and notify the privacy officer for retrospective review.</summary>
    NotifyPrivacyOfficer = 2,
}

public sealed record Decision(bool Permit, string Reason, Obligation Obligations = Obligation.None)
{
    public static Decision Deny(string reason) => new(false, reason);
}

public interface IConsentStore
{
    IEnumerable<Consent> ForPatient(string patientId);
}

/// <summary>
/// Default-deny access policy for patient data. Each decision returns a reason and obligations,
/// so the gateway can enforce them and the audit trail can explain them.
/// </summary>
public sealed class AccessPolicy(IConsentStore consents, TimeProvider clock)
{
    public const int MinimumBreakGlassReasonLength = 15;

    public Decision Decide(Caller caller, string patientId, string? category)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (caller.Role == Role.Patient)
        {
            return caller.UserId == patientId
                ? new Decision(true, "Patient reading own record")
                : Decision.Deny("Patients may only read their own record");
        }

        return (caller.Role, caller.Purpose) switch
        {
            (Role.Practitioner, Purpose.Treatment) => TreatmentAccess(caller, patientId, category),
            (Role.Practitioner, Purpose.EmergencyTreatment) => BreakGlass(caller),
            (Role.Researcher, Purpose.Research) => ResearchAccess(patientId, category),
            _ => Decision.Deny($"Role {caller.Role} is not permitted for purpose {caller.Purpose}"),
        };
    }

    private Decision TreatmentAccess(Caller caller, string patientId, string? category)
    {
        var consent = Active(patientId, Purpose.Treatment)
            .FirstOrDefault(c => c.GranteeId == caller.UserId && Covers(c, category));
        return consent is null
            ? Decision.Deny(category is not null && Categories.Sensitive.Contains(category)
                ? $"No consent naming the sensitive category '{category}'"
                : "No active treatment consent for this practitioner")
            : new Decision(true, $"Treatment consent {consent.Id}");
    }

    private static Decision BreakGlass(Caller caller) =>
        caller.BreakGlassReason is { Length: >= MinimumBreakGlassReasonLength }
            ? new Decision(true, $"Break-glass: {caller.BreakGlassReason}", Obligation.NotifyPrivacyOfficer)
            : Decision.Deny($"Break-glass needs a documented reason of at least {MinimumBreakGlassReasonLength} characters");

    private Decision ResearchAccess(string patientId, string? category)
    {
        var consent = Active(patientId, Purpose.Research).FirstOrDefault(c => c.GranteeId == "*" && Covers(c, category));
        return consent is null
            ? Decision.Deny("Patient has not consented to research use")
            : new Decision(true, $"Research consent {consent.Id}", Obligation.DeIdentify);
    }

    private IEnumerable<Consent> Active(string patientId, Purpose purpose)
    {
        var now = clock.GetUtcNow();
        return consents.ForPatient(patientId).Where(c => !c.Revoked && c.Purpose == purpose && c.ValidUntil > now);
    }

    /// <summary>A null category means demographics only. "*" never covers sensitive categories.</summary>
    private static bool Covers(Consent consent, string? category) =>
        category is null
        || consent.Categories.Contains(category)
        || (consent.Categories.Contains("*") && !Categories.Sensitive.Contains(category));
}
