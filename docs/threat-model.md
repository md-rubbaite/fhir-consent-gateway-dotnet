# Threat model (STRIDE summary)

| Threat | Example | Control in this design | Production addition |
|---|---|---|---|
| Spoofing | Caller claims to be a practitioner | Identity comes from the caller context | OIDC / SMART on FHIR JWT validation, mTLS between services |
| Tampering | Consent edited to widen access | Only the patient can grant or revoke | Signed consent records, immutable history |
| Repudiation | "I never opened that record" | Every decision is audited, including denials | Ship the audit to WORM storage and a SIEM; retain for 6+ years |
| Information disclosure | Research export leaks identity | `DeIdentify` obligation, keyed pseudonyms, age bands | Expert-determination review, k-anonymity checks for small cohorts |
| Information disclosure | Over-broad consent exposes mental-health data | `*` never covers sensitive categories | Configurable sensitive categories (HIV, substance use, genetics) |
| Denial of service | Bulk scraping of patients | — | Rate limits per client, anomaly alerts on access volume |
| Elevation of privilege | Break-glass misuse | Mandatory reason, flagged audit, privacy-officer notification | Time-boxed break-glass sessions, mandatory retrospective review |

Regulatory mapping to check per deployment: HIPAA Privacy and Security Rules (US), GDPR Art. 9 (EU),
and the Bangladesh Digital Security / Data Protection rules where applicable.
