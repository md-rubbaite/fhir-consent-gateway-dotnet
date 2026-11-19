using System.Collections.Concurrent;
using ConsentGateway.Policy;

namespace ConsentGateway.Domain;

/// <summary>Synthetic demo data. No real patient information.</summary>
public sealed class ClinicalStore
{
    private readonly Dictionary<string, Patient> _patients = new(StringComparer.Ordinal)
    {
        ["p-1001"] = new("p-1001", "Rahman", "Ayesha", new DateOnly(1988, 4, 12), "female", "+880-1700-000001", "Dhaka"),
        ["p-1002"] = new("p-1002", "Karim", "Tanvir", new DateOnly(1934, 9, 2), "male", null, "Chattogram"),
    };

    private readonly List<Observation> _observations =
    [
        new("o-1", "p-1001", Categories.VitalSigns, "8867-4", "Heart rate", 72, "/min", new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero)),
        new("o-2", "p-1001", Categories.Laboratory, "2339-0", "Glucose", 5.4m, "mmol/L", new DateTimeOffset(2026, 10, 1, 9, 5, 0, TimeSpan.Zero)),
        new("o-3", "p-1001", Categories.MentalHealth, "44249-1", "PHQ-9 total score", 11, "{score}", new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.Zero)),
        new("o-4", "p-1002", Categories.VitalSigns, "8480-6", "Systolic blood pressure", 138, "mm[Hg]", new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero)),
    ];

    public Patient? FindPatient(string id) => _patients.GetValueOrDefault(id);

    public IEnumerable<Observation> Observations(string patientId, string category) =>
        _observations.Where(o => o.PatientId == patientId && o.Category == category);
}

public sealed class InMemoryConsentStore : IConsentStore
{
    private readonly ConcurrentDictionary<string, Consent> _consents = new(StringComparer.Ordinal);

    public IEnumerable<Consent> ForPatient(string patientId) => _consents.Values.Where(c => c.PatientId == patientId);

    public void Add(Consent consent)
    {
        ArgumentNullException.ThrowIfNull(consent);
        _consents[consent.Id] = consent;
    }

    public Consent? Find(string id) => _consents.GetValueOrDefault(id);

    public void Revoke(string id)
    {
        if (_consents.TryGetValue(id, out var c))
        {
            _consents[id] = c with { Revoked = true };
        }
    }
}
