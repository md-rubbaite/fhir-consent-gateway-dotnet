using System.Security.Cryptography;
using ConsentGateway;
using ConsentGateway.Audit;
using ConsentGateway.Domain;
using ConsentGateway.Policy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ClinicalStore>();
builder.Services.AddSingleton<InMemoryConsentStore>();
builder.Services.AddSingleton<IConsentStore>(sp => sp.GetRequiredService<InMemoryConsentStore>());
builder.Services.AddSingleton<AccessPolicy>();
builder.Services.AddSingleton<AuditLog>();
// Production: load the pseudonymisation key from Azure Key Vault / AWS KMS and rotate per study.
builder.Services.AddSingleton(new DeIdentifier(RandomNumberGenerator.GetBytes(32)));
builder.Services.AddSingleton<GatewayService>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();

// DEMO ONLY: identity from headers. Production: validate an OIDC / SMART on FHIR JWT and map its claims.
static Caller? CallerFrom(HttpRequest r) =>
    Enum.TryParse<Role>(r.Headers["X-Role"], true, out var role) && Enum.TryParse<Purpose>(r.Headers["X-Purpose"], true, out var purpose)
        && !string.IsNullOrWhiteSpace(r.Headers["X-User"])
        ? new Caller(r.Headers["X-User"]!, role, purpose, r.Headers["X-Break-Glass-Reason"])
        : null;

static IResult Denied(string reason) => Results.Json(
    new { resourceType = "OperationOutcome", issue = new[] { new { severity = "error", code = "forbidden", diagnostics = reason } } },
    statusCode: StatusCodes.Status403Forbidden);

app.MapGet("/fhir/Patient/{id}", (string id, HttpRequest req, GatewayService gw) =>
{
    if (CallerFrom(req) is not { } caller)
    {
        return Results.Unauthorized();
    }

    var r = gw.ReadPatient(caller, id);
    return !r.Permitted ? Denied(r.Reason) : r.Body is null ? Results.NotFound() : Results.Ok(r.Body);
});

app.MapGet("/fhir/Observation", (string patient, string category, HttpRequest req, GatewayService gw) =>
{
    if (CallerFrom(req) is not { } caller)
    {
        return Results.Unauthorized();
    }

    var r = gw.SearchObservations(caller, patient, category);
    return r.Permitted ? Results.Ok(r.Body) : Denied(r.Reason);
});

// Patients manage their own consents.
app.MapPost("/consents", (ConsentRequest body, HttpRequest req, InMemoryConsentStore store, TimeProvider clock) =>
{
    if (CallerFrom(req) is not { Role: Role.Patient } caller)
    {
        return Denied("Only the patient can grant consent");
    }

    var consent = new Consent($"c-{Guid.NewGuid():N}"[..14], caller.UserId, body.GranteeId, body.Purpose,
        new HashSet<string>(body.Categories, StringComparer.Ordinal), clock.GetUtcNow().AddDays(Math.Clamp(body.Days, 1, 365)));
    store.Add(consent);
    return Results.Created($"/consents/{consent.Id}", consent);
});

app.MapDelete("/consents/{id}", (string id, HttpRequest req, InMemoryConsentStore store) =>
{
    var caller = CallerFrom(req);
    var consent = store.Find(id);
    if (consent is null || caller is not { Role: Role.Patient } || caller.UserId != consent.PatientId)
    {
        return Denied("Only the patient who granted this consent can revoke it");
    }

    store.Revoke(id);
    return Results.NoContent();
});

app.MapGet("/audit", (HttpRequest req, AuditLog audit) =>
    CallerFrom(req) is { Role: Role.PrivacyOfficer } ? Results.Ok(audit.All()) : Denied("Privacy officer only"));

app.Run();

internal sealed record ConsentRequest(string GranteeId, Purpose Purpose, string[] Categories, int Days);

public partial class Program;
