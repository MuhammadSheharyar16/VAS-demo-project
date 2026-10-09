# AEGI VAS Executive Dashboard

A React dashboard over the Microsoft Fabric gold layer, built to the
**AEGI VAS Executive Dashboard – Phase 1 Proposition & Requirements v2** and the **UAT sizing baseline**.
A protected ASP.NET Core API checks that the caller is an authorised executive, runs read-only KPI queries
against Fabric and returns dashboard-ready results. One custom container on Azure App Service hosts both the
React assets and the API.

Phase 1: **Executive Summary** and **VAS** are active. **Operations, Finance and Alfit** are Coming Soon
placeholders. CSAT is out of scope and is not shown.

```
Executive browser ──(1) Entra sign-in, MSAL + PKCE──▶ Microsoft Entra ID
        │
        └─(2) HTTPS + API access token──▶ Azure App Service (custom container): React assets + ASP.NET Core API
                                              │  validates token: signature, issuer, audience, expiry, scope, role
                                              │  validates filters
                                              ├─(3) managed identity token ──▶ Microsoft Entra ID   (no secret, no Key Vault)
                                              ├─(4) read-only SQL as the managed identity ──▶ Fabric SQL analytics endpoint ▶ Lakehouse gold
                                              ├─ telemetry ──▶ Application Insights
                                              └─(5) JSON KPI results + currency + freshness ──▶ browser
```

See [`docs/architecture.html`](docs/architecture.html) for the full picture and
[`docs/gap-register.md`](docs/gap-register.md) for what is still to be confirmed with AEGI.

## What the dashboard shows

| Requirement area (section 3) | Where |
|---|---|
| Visa applications and volumes | Received, processed and pending KPIs; monthly volumes by journey |
| Visa outcomes | Approval and rejection rate KPIs; outcome shares |
| Visa processing and journey | Processing stages by journey; turnaround by journey and visa type; closure by month |
| VAS revenue | Revenue KPI; revenue by country, centre and visa type |
| Visa types | Volume and outcome rates by visa type |
| Centre performance | Applications, journey mix, processed, approval, turnaround, revenue and capacity used per centre |

Filters: period (presets or any month range), country, centre and visa type. Every headline KPI is compared
with the previous period of the same length, and shows a status when a threshold is configured for it.

## Project structure

```
(repository root)
├── .github/workflows/ci.yml                 build, test, container build and smoke test, optional registry push
└── aegi-vas-dashboard/
    ├── Dockerfile                           one image: React build + API
    ├── backend/
    │   ├── Aegi.Vas.Dashboard.sln
    │   ├── src/Aegi.Vas.Dashboard.Api/      ASP.NET Core 10 API, also serves the React build
    │   │   ├── Program.cs                   pipeline: correlation ID, security headers, errors, auth, rate limit, SPA
    │   │   ├── appsettings*.json            base, Development (sample data), UAT and Production placeholders
    │   │   ├── Configuration/Options.cs     typed settings
    │   │   ├── Security/AuthenticationSetup.cs  Entra JWT validation + DashboardAccess policy (scope + app role)
    │   │   ├── Middleware/                  CorrelationIdMiddleware, SecurityHeadersMiddleware
    │   │   ├── Endpoints/                   reporting endpoints, public client config
    │   │   ├── Reporting/
    │   │   │   ├── IReportingDataSource.cs  the data contract the API reads
    │   │   │   ├── FabricSqlDataSource.cs   Microsoft.Data.SqlClient, managed identity, pooled, read-only
    │   │   │   ├── CachedReportingDataSource.cs  optional short result cache (off by default)
    │   │   │   ├── SampleDataSource.cs      Development-only sample rows
    │   │   │   ├── ReportFilter.cs          filter validation, comparison period
    │   │   │   └── Sql/*.sql                one parameterised query per KPI (embedded in the build)
    │   │   ├── Services/KpiService.cs       every KPI formula, comparison and status lives here
    │   │   ├── Health/                      readiness check against the reporting source
    │   │   └── wwwroot/                     React build output (generated)
    │   └── tests/Aegi.Vas.Dashboard.Api.Tests/  50 tests: data, filters, comparisons, tokens, roles, outages, startup guards
    ├── frontend/                            React 19 + Vite
    │   └── src/
    │       ├── auth/                        MSAL React; settings loaded from /api/client-config
    │       ├── api/                         fetch client, attaches the API token, surfaces correlation IDs
    │       ├── hooks/useKpi.js              React Query; changing a filter repeats the request
    │       ├── pages/                       ExecutiveSummary, Vas, ComingSoon
    │       ├── components/                  kpi/, charts/, three/ (3D revenue), layout/, common/
    │       ├── theme/                       colours, motion
    │       └── utils/format.js              display formatting only (no KPI maths)
    ├── infra/                               Bicep for the App Service baseline (B1 Linux, container, managed identity)
    ├── loadtest/load-test.mjs               10-user load test for the sizing baseline
    └── docs/                                architecture, gap register, screenshots
```

## API

All reporting endpoints need an Entra access token with scope `Dashboard.Read` and app role `Dashboard.Viewer`.
Query parameters: `from`, `to` (`yyyy-MM`, inclusive, 36 months at most; default is year to date up to the latest
month in the data), and optional `country`, `centre` and `visaType`.

| Endpoint | Returns |
|---|---|
| `GET /api/v1/executive-summary` | Nine headline KPIs with previous-period comparison and status, monthly volumes, outcomes, closure, highlights, Coming Soon list |
| `GET /api/v1/vas/volume` | Applications per month, centre vs digital |
| `GET /api/v1/vas/outcomes` | Received, processed, pending, approval and rejection rate, outcome shares |
| `GET /api/v1/vas/closure` | Closure % overall and by month |
| `GET /api/v1/vas/stages` | Applications that reached each processing stage, by journey |
| `GET /api/v1/vas/turnaround?journey=digital\|centre` | Average days by visa type |
| `GET /api/v1/vas/revenue` | Revenue by country and centre, and by visa type, with shares |
| `GET /api/v1/vas/visa-types` | Volume and outcome rates by visa type |
| `GET /api/v1/vas/centres` | Centre performance, including capacity used |
| `GET /api/v1/filters` | Countries, centres, visa types and month range in the data |
| `GET /api/client-config` | Public sign-in settings for the SPA (anonymous) |
| `GET /health/live` | Liveness; never touches Fabric (anonymous). **Use this as the App Service health check path.** |
| `GET /health/ready` | Readiness including Fabric connectivity, result held for 30 seconds (anonymous) |

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

## Build and run the container

```bash
docker build -t aegi-vas-dashboard .
docker run --rm -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Development aegi-vas-dashboard   # sample data on http://localhost:8080
```

The image listens on port 8080 and runs as a non-root user. It is based on the default (Ubuntu) ASP.NET image
because Microsoft.Data.SqlClient needs the ICU libraries; do not switch to Alpine or chiseled images without adding them.

## Deploy to UAT / Production

Hosting baseline (proposed): App Service **B1 Linux, one instance, Always On**, custom container, system-assigned
managed identity, **no Key Vault**. `infra/main.bicep` captures it; fill in `infra/uat.bicepparam`.
B1 has no deployment slots, so use Standard or Premium if UAT must test slot swaps.

1. **Entra app registrations**
   - API: expose the scope `Dashboard.Read` and create the app role `Dashboard.Viewer`. Assign the role to executives
     (Enterprise applications → Users and groups). Set `Authentication:TenantId`, `Authentication:ApiAudience`
     (the Application ID URI) and `Authentication:ApiClientId` (the client ID). The API accepts both v1 and v2 access tokens.
   - SPA: single-page application platform, with a redirect URI equal to the site URL. Add the delegated permission
     `Dashboard.Read` and grant admin consent. Set `Authentication:SpaClientId`.
2. **Push the image** to Azure Container Registry (the CI workflow does this once its repository variables are set).
3. **Create the Web App**: `az deployment group create -g <rg> -f infra/main.bicep -p infra/uat.bicepparam`.
   The deployment output includes the managed identity's principal ID.
4. **Fabric access.** A tenant admin enables *Service principals can use Fabric APIs*. Add the Web App's managed identity
   to the workspace with the lowest role that can read the gold views, and grant `SELECT` only on the reporting views.
   Nothing else is needed: there is no client secret and no Key Vault.
5. **Queries.** Point `Reporting/Sql/*.sql` at the confirmed gold views (see its README and the gap register), then run the
   connection check. It signs in, runs every KPI query once and prints the row count or the Fabric error per query,
   which names any missing view or column:
   ```bash
   # from a workstation, signed in with `az login` as someone who can read the gold views
   cd backend/src/Aegi.Vas.Dashboard.Api
   Fabric__SqlEndpoint=<endpoint> Fabric__Database=<lakehouse> Fabric__Credential=Default dotnet run -- --check-fabric
   # in the deployed container (uses the managed identity): dotnet Aegi.Vas.Dashboard.Api.dll --check-fabric
   ```
6. **Telemetry.** Set `ApplicationInsights:ConnectionString`.

Settings can also be supplied as App Service application settings (`Section__Key`), which take precedence over the files.
The app refuses to start in UAT or Production if sample data or the sign-in bypass is configured.

| Setting | Purpose |
|---|---|
| `Fabric:Credential` | `ManagedIdentity` (hosted) or `Default` (developer sign-in via `az login`, for running against Fabric locally) |
| `Fabric:ManagedIdentityClientId` | Only for a user-assigned identity |
| `Fabric:Encrypt` | `Mandatory` (default) or `Strict` |
| `Reporting:StaleAfterHours` | Flag data as out of date this long after the last gold refresh. Unset until the refresh schedule is agreed |
| `Reporting:Thresholds:<kpi_key>` | `Watch` and `Poor` values for a KPI status. Unset in UAT and Production until AEGI agrees them |
| `Reporting:CacheSeconds` | Reuse query results for identical filters. `0` (off) until the load test shows it is needed |
| `RateLimiting:PermitsPerMinute` | Requests per signed-in user per minute (default 120) |

## Load test (sizing baseline)

```bash
node loadtest/load-test.mjs --url https://<uat-app> --users 10 --duration 300 --token <access-token>
```

Ten virtual executives open the Executive Summary and VAS tabs and change filters. The script reports response
times per endpoint; read CPU and memory for the same window from App Service metrics. One token shares one
rate-limit allowance, so raise `RateLimiting__PermitsPerMinute` for the test window.

A local run on **sample data** (container limited to 1 vCPU and 1.75 GB, 10 users, 60 seconds) gave a 102 MB image,
about 55 MB of memory, under 10% CPU and p95 under 40 ms. That proves the container and the script; it says nothing
about Fabric query time. The real measurement needs the release container connected to the UAT gold views.
