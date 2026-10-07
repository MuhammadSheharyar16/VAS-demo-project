# Gap register: AEGI VAS Architecture & Delivery Roadmap v1.0

The code follows the roadmap (7 October 2026). This page lists what the roadmap does not yet decide.
Each gap names where it lands in the code, so the team knows what to change once it is decided.

## A. Decisions needed before Fabric go-live (roadmap phases 1–2)

| # | Gap | Why it matters | What the build assumes today | Where to change it |
|---|-----|----------------|------------------------------|--------------------|
| A1 | **KPI formulas are not defined.** Examples: is approval rate approved ÷ all outcomes or approved ÷ decided? When does turnaround start and stop? What counts as "closed"? | Numbers will differ depending on the definition. The roadmap says React must not invent formulas, so the API must own them. | Approval = approved ÷ all outcomes. Closure = closed ÷ all applications. Turnaround = days from application to decision, weighted per application. Each is marked `ASSUMPTION` in code. | `Services/KpiService.cs`, `Reporting/Sql/*.sql` |
| A2 | **Fabric gold object names, columns and grain are unknown.** | The queries cannot run until they point at real views. | Placeholder views `gold.vw_vas_*`, with the expected columns documented. | `Reporting/Sql/*.sql` and its README |
| A3 | **Where KPI logic lives is not confirmed** (gold views or a semantic model). | The roadmap says to revisit the serving route if a semantic model owns the measures. | SQL analytics endpoint on a Lakehouse. | `Reporting/IReportingDataSource.cs` (swap in a new adapter) |
| A4 | **Revenue currency and units are not defined.** Is there a single currency or several? Is FX applied, and at which rate date? | Mixed currencies cannot be added together. | Single reporting currency, configurable (`Reporting:Currency`, default USD). | `appsettings.json`, `Revenue.sql` |
| A5 | **Time zone and date basis are not defined.** Application date or decision date? Local centre time or UTC? Calendar or fiscal year? | This changes which month a visa is counted in, and what the quarter presets mean. | Calendar months on `month_key`, UTC, calendar quarters. | `Reporting:TimeZone`, SQL `month_key`, `Header.jsx` presets |
| A6 | **Exact filter list is not fixed.** The roadmap says "reporting dates, country or centre where supported". Visa type and journey are not listed as filters. | The scope of the UI and API depends on it. | Period, country and centre as filters. Journey is a toggle on the turnaround chart only. | `ReportFilter.cs`, `Header.jsx` |
| A7 | **Data freshness source is not defined.** Which table records the last gold refresh, and after how many hours is data "stale"? | The roadmap requires freshness metadata in every response. | `gold.vw_vas_refresh_status.last_refreshed_utc`. Stale threshold unset, so data is never flagged stale. | `LastRefreshed.sql`, `Reporting:StaleAfterHours` |
| A8 | **Outcome categories are not listed.** The document says "approved, rejected, pending and other agreed outcomes". | Affects the donut chart and the approval rate. | Approved, Rejected, Pending, Withdrawn. | Data contract; UI colours in `Donut.jsx` |
| A9 | **Previous-period comparisons** (year-on-year, deltas) are not mentioned. | Executives usually expect them, but they are a formula decision. | Not shown. | New API fields if agreed |

## B. Identity and access (roadmap phases 2–3)

| # | Gap | What the build assumes |
|---|-----|------------------------|
| B1 | Name of the API permission (scope) and how executives are assigned (app role or security group) | Scope `Dashboard.Read`, app role `Dashboard.Viewer`. Both are required. Names are configurable. |
| B2 | Who owns admin consent and role assignment | Not decided. Needs a named owner. |
| B3 | Whether the Fabric workload identity is a dedicated registration or reuses the API identity | Dedicated registration (`Fabric:ClientId`). |
| B4 | Secret or certificate for the Fabric service principal, and the rotation owner and interval | Client secret held in Key Vault as `Fabric--ClientSecret`. No rotation schedule set. |
| B5 | Session behaviour: idle timeout, sign-out, Conditional Access, MFA | Tenant policies apply. Tokens are held in sessionStorage, so closing the tab signs the user out. |
| B6 | Who may see which country or centre | Common scope for all executives (the roadmap assumption). Filters only narrow results and never grant access. |

## C. Non-functional requirements (the roadmap marks these "open")

| # | Gap | What the build assumes |
|---|-----|------------------------|
| C1 | Response time, concurrency and data freshness targets | None invented. No caching beyond a 15-minute list of countries and centres. Measure in phase 4. |
| C2 | Access audit (who viewed what, retention) | Request telemetry goes to App Insights when configured. No separate audit trail, because the roadmap says this must be agreed separately. |
| C3 | Accessibility standard (for example WCAG 2.2 AA) | Keyboard tabs, ARIA labels, reduced-motion support and a semantic table. Not formally audited. |
| C4 | Supported browsers and devices (desktop only, or tablet and phone too) | Responsive down to 400 px. 3D needs WebGL. |
| C5 | Rate limits | 120 requests per user per minute (configurable). |
| C6 | Availability and support hours, RTO/RPO | Not defined. |

## D. Delivery and platform (roadmap phases 3 and 7–8)

| # | Gap | What the build assumes |
|---|-----|------------------------|
| D1 | Which CI/CD platform is the "existing source-control and CI/CD platform" | A GitHub Actions sample is included. Port it if AEGI uses Azure DevOps. |
| D2 | Azure region, subscription owner, App Service plan size | Not set. |
| D3 | Network policy (public or private ingress, Fabric egress) | Public HTTPS. The roadmap says not to infer private endpoints. |
| D4 | Environment names and URLs (Dev, UAT, Prod) and their redirect URIs | `appsettings.Development/UAT/Production.json` with `<placeholders>`. |
| D5 | Design sign-off: there are no approved Executive Summary or VAS designs yet | The current UI is a proposal for the design sign-off in phase 5. |
| D6 | Export, print or email of the dashboard | Not in scope and not mentioned. |
| D7 | Local development without Fabric access | Not in the roadmap. Added a Development-only sample data source and sign-in bypass. Both refuse to start in UAT or Production, and a test proves it. |

## Where the build intentionally differs from my earlier version

- Backend changed from Python/FastAPI to **ASP.NET Core** (roadmap "Backend framework"). The Python version is kept as `aegi-vas-dashboard-python-v1.zip` for reference.
- React and the API are now served from **one App Service origin**. There is no CORS and no separate frontend host.
- **The KPI formulas moved to the API.** React only formats what it receives. This includes the insights, the shares and the averages.
- **Result caching is removed** until performance has been measured (roadmap section 07).
- Every response carries **currency, freshness, applied filters and a correlation ID**. Errors are ProblemDetails.
