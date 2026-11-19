using System.Security.Cryptography;
using System.Text;
using ConsentGateway.Domain;

namespace ConsentGateway.Policy;

/// <summary>
/// Removes direct identifiers and generalises quasi-identifiers, in the spirit of the HIPAA Safe Harbor method.
/// Patient ids become keyed pseudonyms, so a researcher can link one patient's records without learning who they are.
/// </summary>
public sealed class DeIdentifier(byte[] pseudonymKey)
{
    public string Pseudonym(string patientId) =>
        "anon-" + Convert.ToHexString(HMACSHA256.HashData(pseudonymKey, Encoding.UTF8.GetBytes(patientId)))[..12].ToLowerInvariant();

    public object Patient(Patient p, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(p);
        var age = today.Year - p.BirthDate.Year - (today < p.BirthDate.AddYears(today.Year - p.BirthDate.Year) ? 1 : 0);
        return new
        {
            resourceType = "Patient",
            id = Pseudonym(p.Id),
            gender = p.Gender,
            // Safe Harbor: ages over 89 are aggregated.
            ageBand = age >= 90 ? "90+" : $"{age / 10 * 10}-{(age / 10 * 10) + 9}",
        };
    }

    public object Observation(Observation o)
    {
        ArgumentNullException.ThrowIfNull(o);
        return new
        {
            resourceType = "Observation",
            id = Pseudonym(o.Id),
            subject = Pseudonym(o.PatientId),
            category = o.Category,
            code = o.Code,
            display = o.Display,
            value = o.Value,
            unit = o.Unit,
            effectiveYear = o.Effective.Year,
        };
    }
}
