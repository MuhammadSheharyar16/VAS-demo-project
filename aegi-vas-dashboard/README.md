# AEGI VAS Executive Dashboard

A React dashboard over the Microsoft Fabric gold layer, built to the
**AEGI VAS Architecture & End-to-End Delivery Roadmap v1.0**. A protected ASP.NET Core API checks that
the caller is an authorised executive, runs read-only KPI queries against Fabric and returns
dashboard-ready results. One Azure App Service hosts both the React assets and the API.

Phase 1: **Executive Summary** and **VAS** tabs are populated. Operations, Finance and Alfit are
placeholders, and CSAT is shown as Coming Soon.

```
Executive browser ──(1) Entra sign-in, MSAL + PKCE──▶ Microsoft Entra ID
        │
        └─(2) HTTPS + API access token──▶ Azure App Service: React assets + ASP.NET Core API
                                              │  validates token: signature, issuer, audience, expiry, scope, role
                                              │  validates filters
                                              ├─(3) managed identity ──▶ Azure Key Vault (Fabric credential)
                                              ├─(4)(5) service principal, read-only SQL ──▶ Fabric SQL analytics endpoint ▶ Lakehouse gold
                                              ├─ telemetry ──▶ Application Insights
                                              └─(6) JSON KPI results + currency + freshness ──▶ browser
```

See [`docs/architecture.html`](docs/architecture.html) for the full picture and
[`docs/gap-register.md`](docs/gap-register.md) for what the roadmap still has to decide.

## Project structure

```
aegi-vas-dashboard/
├── backend/
│   ├── Aegi.Vas.Dashboard.sln
│   ├── src/Aegi.Vas.Dashboard.Api/          ASP.NET Core 10 API, also serves the React build
│   │   ├── Program.cs                       pipeline: correlation ID, security headers, errors, auth, rate limit, SPA
│   │   ├── appsettings.json                 base settings
│   │   ├── appsettings.Development.json     sample data + local sign-in bypass (refused in other environments)
│   │   ├── appsettings.UAT.json             UAT placeholders
│   │   ├── appsettings.Production.json      Production placeholders
│   │   ├── Configuration/Options.cs         typed settings
│   │   ├── Security/AuthenticationSetup.cs  Entra JWT validation + DashboardAccess policy (scope + app role)
│   │   ├── Middleware/                      CorrelationIdMiddleware, SecurityHeadersMiddleware
│   │   ├── Endpoints/                       reporting endpoints, public client config
│   │   ├── Reporting/
│   │   │   ├── IReportingDataSource.cs      the data contract the API reads
│   │   │   ├── FabricSqlDataSource.cs       Microsoft.Data.SqlClient, service principal, pooled, read-only
│   │   │   ├── SampleDataSource.cs          Development-only sample rows
│   │   │   ├── ReportFilter.cs              filter validation
│   │   │   └── Sql/*.sql                    one parameterised query per KPI (embedded in the build)
│   │   ├── Services/KpiService.cs           every KPI formula lives here
│   │   ├── Health/                          readiness check against the reporting source
│   │   └── wwwroot/                         React build output (generated)
│   └── tests/Aegi.Vas.Dashboard.Api.Tests/  38 tests: data, filters, tokens, roles, outages, startup guards
├── frontend/                                React 19 + Vite
│   └── src/
│       ├── auth/                            MSAL React; settings loaded from /api/client-config
│       ├── api/                             fetch client, attaches the API token, surfaces correlation IDs
│       ├── hooks/useKpi.js                  React Query; changing a filter repeats the request
│       ├── pages/                           ExecutiveSummary, Vas, ComingSoon
│       ├── components/                      kpi/, charts/, three/ (3D revenue), layout/, common/
│       ├── theme/                           colours, motion
│       └── utils/format.js                  display formatting only (no KPI maths)
├── docs/
│   ├── architecture.html
│   ├── gap-register.md
│   └── screenshots/
└── .github/workflows/ci.yml                 sample build, test and publish pipeline
```

## API

All reporting endpoints need an Entra access token with scope `Dashboard.Read` and app role `Dashboard.Viewer`.
Query parameters: `from`, `to` (`yyyy-MM`, inclusive, 36 months at most), and optional `country` and `centre`.

| Endpoint | Returns |
|---|---|
| `GET /api/v1/executive-summary` | Six headline KPIs, highlights, Coming Soon list |
| `GET /api/v1/vas/volume` | Visas per month, centre vs digital |
| `GET /api/v1/vas/outcomes` | Approved, rejected, pending and withdrawn shares |
| `GET /api/v1/vas/closure` | Closure % overall and by month |
| `GET /api/v1/vas/turnaround?journey=digital\|centre` | Average days by visa type |
| `GET /api/v1/vas/revenue` | Revenue by country and centre, with shares |
| `GET /api/v1/vas/visa-types` | Volume and outcome rates by visa type |
| `GET /api/v1/filters` | Countries, centres and month range in the data |
| `GET /api/client-config` | Public sign-in settings for the SPA (anonymous) |
| `GET /health/live`, `/health/ready` | Liveness; readiness including Fabric connectivity (anonymous) |

Every reporting response is `{ data, meta }`. `meta` contains the applied filters, `currency`, `timeZone`,
`freshness { lastRefreshedUtc, isStale, source }`, `generatedUtc` and `correlationId`.
Errors are RFC 7807 ProblemDetails with a `correlationId`: 400 for invalid filters, 401 and 403 for access, 503 when Fabric is down.

## Run locally (sample data, no Azure needed)

Needs the .NET 10 SDK and Node 20 or later.

```bash
# 1. Build the React app into the API's wwwroot
cd frontend
npm install
npm run build

# 2. Run the API, which also serves the site
cd ../backend/src/Aegi.Vas.Dashboard.Api
dotnet run            # http://localhost:5080  (Development: sample data, sign-in bypass)
```

For live React editing, keep the API running and also run `npm run dev` in `frontend/`
(http://localhost:5173 proxies `/api` to the API).

Run the tests with `cd backend && dotnet test`.

## Connect to Fabric and Entra (UAT / Production)

Fill in `appsettings.UAT.json` or `appsettings.Production.json`, or use App Service application settings, which take precedence.

1. **Entra app registrations**
   - API: expose the scope `Dashboard.Read` and create the app role `Dashboard.Viewer`. Assign the role to executives (Enterprise applications → Users and groups). Set `Authentication:TenantId` and `Authentication:ApiAudience` (`api://<api-client-id>`).
   - SPA: single-page application platform, with a redirect URI equal to the site URL. Add the delegated permission `Dashboard.Read` and grant admin consent. Set `Authentication:SpaClientId`.
   - Fabric workload: a separate service principal with a secret or certificate. Set `Fabric:ClientId`.
2. **Fabric access.** A tenant admin enables *Service principals can use Fabric APIs*. Add the workload principal to the workspace with the lowest role that can read the gold views, and grant `SELECT` only on the reporting views.
3. **Key Vault.** Store the secret as `Fabric--ClientSecret`. Turn on the App Service system-assigned managed identity and give it *Key Vault Secrets User* on that vault only. Set `KeyVault:Uri`.
4. **Fabric endpoint.** Set `Fabric:SqlEndpoint` (Lakehouse → SQL analytics endpoint → connection string) and `Fabric:Database`.
5. **Queries.** Point `Reporting/Sql/*.sql` at the confirmed gold views (see gap register items A1–A3).
6. **Telemetry.** Set `ApplicationInsights:ConnectionString`.
7. **Deploy.** Run `npm run build`, then `dotnet publish`, then zip-deploy to App Service with `ASPNETCORE_ENVIRONMENT=UAT` or `Production`.

The app refuses to start in UAT or Production if sample data or the sign-in bypass is configured.
