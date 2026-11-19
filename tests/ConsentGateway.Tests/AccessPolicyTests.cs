using ConsentGateway.Domain;
using ConsentGateway.Policy;

namespace ConsentGateway.Tests;

public sealed class AccessPolicyTests
{
    private readonly InMemoryConsentStore _consents = new();
    private readonly AccessPolicy _policy;

    public AccessPolicyTests() => _policy = new AccessPolicy(_consents, TimeProvider.System);

    private static Caller Doctor(string id = "dr-1") => new(id, Role.Practitioner, Purpose.Treatment);

    private void Grant(string grantee, Purpose purpose, params string[] categories) =>
        _consents.Add(new Consent($"c-{Guid.NewGuid():N}", "p-1001", grantee, purpose,
            new HashSet<string>(categories, StringComparer.Ordinal), DateTimeOffset.UtcNow.AddDays(30)));

    [Fact]
    public void Default_is_deny()
    {
        var d = _policy.Decide(Doctor(), "p-1001", Categories.VitalSigns);

        Assert.False(d.Permit);
    }

    [Fact]
    public void Patient_reads_own_record_but_not_others()
    {
        Assert.True(_policy.Decide(new Caller("p-1001", Role.Patient, Purpose.Treatment), "p-1001", Categories.MentalHealth).Permit);
        Assert.False(_policy.Decide(new Caller("p-1001", Role.Patient, Purpose.Treatment), "p-1002", null).Permit);
    }

    [Fact]
    public void Consent_for_all_categories_does_not_unlock_sensitive_data()
    {
        Grant("dr-1", Purpose.Treatment, "*");

        Assert.True(_policy.Decide(Doctor(), "p-1001", Categories.Laboratory).Permit);
        var mh = _policy.Decide(Doctor(), "p-1001", Categories.MentalHealth);
        Assert.False(mh.Permit);
        Assert.Contains("sensitive", mh.Reason);
    }

    [Fact]
    public void Explicit_sensitive_consent_permits_that_category()
    {
        Grant("dr-1", Purpose.Treatment, Categories.MentalHealth);

        Assert.True(_policy.Decide(Doctor(), "p-1001", Categories.MentalHealth).Permit);
    }

    [Fact]
    public void Consent_is_per_practitioner()
    {
        Grant("dr-1", Purpose.Treatment, "*");

        Assert.False(_policy.Decide(Doctor("dr-2"), "p-1001", Categories.VitalSigns).Permit);
    }

    [Fact]
    public void Revoked_and_expired_consents_are_ignored()
    {
        _consents.Add(new Consent("c-old", "p-1001", "dr-1", Purpose.Treatment, new HashSet<string> { "*" }, DateTimeOffset.UtcNow.AddMinutes(-1)));
        _consents.Add(new Consent("c-rev", "p-1001", "dr-1", Purpose.Treatment, new HashSet<string> { "*" }, DateTimeOffset.UtcNow.AddDays(1), Revoked: true));

        Assert.False(_policy.Decide(Doctor(), "p-1001", Categories.VitalSigns).Permit);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("urgent", false)]
    [InlineData("Unconscious patient in ED, no consent available", true)]
    public void Break_glass_requires_a_documented_reason(string? reason, bool expected)
    {
        var d = _policy.Decide(new Caller("dr-9", Role.Practitioner, Purpose.EmergencyTreatment, reason), "p-1001", Categories.MentalHealth);

        Assert.Equal(expected, d.Permit);
        if (expected)
        {
            Assert.True(d.Obligations.HasFlag(Obligation.NotifyPrivacyOfficer));
        }
    }

    [Fact]
    public void Research_needs_research_consent_and_always_de_identifies()
    {
        var researcher = new Caller("res-1", Role.Researcher, Purpose.Research);
        Assert.False(_policy.Decide(researcher, "p-1001", Categories.VitalSigns).Permit);

        Grant("*", Purpose.Research, Categories.VitalSigns);
        var d = _policy.Decide(researcher, "p-1001", Categories.VitalSigns);

        Assert.True(d.Permit);
        Assert.Equal(Obligation.DeIdentify, d.Obligations);
    }

    [Fact]
    public void Researcher_cannot_claim_treatment_purpose()
    {
        Grant("res-1", Purpose.Treatment, "*");

        Assert.False(_policy.Decide(new Caller("res-1", Role.Researcher, Purpose.Treatment), "p-1001", Categories.VitalSigns).Permit);
    }
}
