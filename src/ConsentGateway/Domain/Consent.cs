namespace ConsentGateway.Domain;

public enum Purpose
{
    /// <summary>HL7 v3 TREAT: treatment.</summary>
    Treatment,

    /// <summary>HL7 v3 ETREAT: emergency treatment ("break-glass").</summary>
    EmergencyTreatment,

    /// <summary>HL7 v3 HRESCH: healthcare research.</summary>
    Research,
}

public enum Role
{
    Patient,
    Practitioner,
    Researcher,
    PrivacyOfficer,
}

/// <param name="GranteeId">Practitioner id, or "*" for any approved researcher when Purpose is Research.</param>
/// <param name="Categories">Observation categories covered. "*" covers every non-sensitive category.</param>
public sealed record Consent(
    string Id,
    string PatientId,
    string GranteeId,
    Purpose Purpose,
    IReadOnlySet<string> Categories,
    DateTimeOffset ValidUntil,
    bool Revoked = false);

/// <summary>The authenticated caller. In production this comes from an OIDC / SMART on FHIR access token.</summary>
public sealed record Caller(string UserId, Role Role, Purpose Purpose, string? BreakGlassReason = null);
