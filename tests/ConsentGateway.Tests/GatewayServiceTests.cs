using System.Text.Json;
using ConsentGateway.Audit;
using ConsentGateway.Domain;
using ConsentGateway.Policy;

namespace ConsentGateway.Tests;

public sealed class GatewayServiceTests
{
    private readonly InMemoryConsentStore _consents = new();
    private readonly AuditLog _audit = new();
    private readonly GatewayService _gateway;

    public GatewayServiceTests() =>
        _gateway = new GatewayService(new ClinicalStore(), new AccessPolicy(_consents, TimeProvider.System),
            new DeIdentifier(new byte[32]), _audit, TimeProvider.System);

    [Fact]
    public void Denials_are_audited_too()
    {
        var r = _gateway.SearchObservations(new Caller("dr-1", Role.Practitioner, Purpose.Treatment), "p-1001", Categories.MentalHealth);

        Assert.False(r.Permitted);
        var e = Assert.Single(_audit.All());
        Assert.False(e.Permitted);
        Assert.Equal("dr-1", e.UserId);
    }

    [Fact]
    public void Break_glass_access_is_flagged_in_the_audit_trail()
    {
        var r = _gateway.ReadPatient(new Caller("dr-9", Role.Practitioner, Purpose.EmergencyTreatment, "Unconscious patient in ED, no consent available"), "p-1001");

        Assert.True(r.Permitted);
        Assert.True(Assert.Single(_audit.All()).BreakGlass);
    }

    [Fact]
    public void Research_output_contains_no_direct_identifiers()
    {
        _consents.Add(new Consent("c-r", "p-1002", "*", Purpose.Research, new HashSet<string> { "*" }, DateTimeOffset.UtcNow.AddDays(1)));
        var researcher = new Caller("res-1", Role.Researcher, Purpose.Research);

        var patientJson = JsonSerializer.Serialize(_gateway.ReadPatient(researcher, "p-1002").Body);
        var obsJson = JsonSerializer.Serialize(_gateway.SearchObservations(researcher, "p-1002", Categories.VitalSigns).Body);

        foreach (var identifier in new[] { "Karim", "Tanvir", "1934", "Chattogram", "p-1002" })
        {
            Assert.DoesNotContain(identifier, patientJson);
            Assert.DoesNotContain(identifier, obsJson);
        }

        using var doc = JsonDocument.Parse(patientJson);
        Assert.Equal("90+", doc.RootElement.GetProperty("ageBand").GetString());
        Assert.Contains("anon-", obsJson);
    }

    [Fact]
    public void Pseudonyms_are_stable_so_records_can_be_linked()
    {
        var d = new DeIdentifier(new byte[32]);

        Assert.Equal(d.Pseudonym("p-1"), d.Pseudonym("p-1"));
        Assert.NotEqual(d.Pseudonym("p-1"), d.Pseudonym("p-2"));
    }
}
