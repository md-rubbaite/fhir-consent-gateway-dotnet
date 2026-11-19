namespace ConsentGateway.Domain;

/// <summary>Simplified FHIR R4 Patient. Only the fields needed to show minimisation rules.</summary>
public sealed record Patient(
    string Id,
    string Family,
    string Given,
    DateOnly BirthDate,
    string Gender,
    string? Phone,
    string? City)
{
    public string ResourceType => "Patient";
}

/// <summary>Simplified FHIR R4 Observation.</summary>
public sealed record Observation(
    string Id,
    string PatientId,
    string Category,
    string Code,
    string Display,
    decimal Value,
    string Unit,
    DateTimeOffset Effective)
{
    public string ResourceType => "Observation";
}

/// <summary>Observation categories (HL7 observation-category plus a sensitive local category).</summary>
public static class Categories
{
    public const string VitalSigns = "vital-signs";
    public const string Laboratory = "laboratory";

    /// <summary>Sensitive: needs explicit consent naming this category. Never covered by "all".</summary>
    public const string MentalHealth = "mental-health";

    public static readonly IReadOnlySet<string> Sensitive = new HashSet<string>(StringComparer.Ordinal) { MentalHealth };
}
