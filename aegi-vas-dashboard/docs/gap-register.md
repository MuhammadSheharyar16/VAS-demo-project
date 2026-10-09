# Gap register: Phase 1 Proposition & Requirements v2 and UAT sizing baseline

The build follows the requirements document (v2) and the UAT sizing baseline slide. This page lists what those
two documents leave to be confirmed. Each gap names where it lands in the code, so the team knows what to change
once it is decided. Scope and design sign-off is due **13 October 2026**; UAT completion is targeted for **23 October 2026**.

## A. KPI definitions and data (requirements section 3.3, KPI Mapping document)

| # | Gap | What the build assumes today | Where to change it |
|---|-----|------------------------------|--------------------|
| A1 | **Received, processed and pending are not defined.** | Received = every application in the period. Pending = outcome "Pending". Processed = received minus pending. | `KpiService.OutcomesAsync` |
| A2 | **Approval and rejection rate denominators.** All applications, or decided applications only? | Share of all received applications. | `KpiService.OutcomesAsync` |
| A3 | **Closure and turnaround definitions.** What counts as closed; when turnaround starts and stops. | Closure = closed ÷ all applications. Turnaround = days from application to decision, averaged per application. | `KpiService`, `Closure.sql`, `Turnaround.sql` |
| A4 | **Processing stages are not listed.** | Stage names and order are read from the data (`stage`, `stage_order`). Sample data uses Submitted, In review, Decided, Closed. | `Stages.sql` |
| A5 | **Capacity measure for centres is not defined.** | Capacity used = applications handled in the centre ÷ centre capacity for the same months. Not shown when a visa type is selected, because capacity is not split by visa type. | `KpiService.CentresAsync`, `Centres.sql` |
| A6 | **Comparison period.** The document asks for period-on-period comparisons but not which period. | The previous period of the same length (Q3 is compared with Q2). No comparison is shown when that period has no data. | `ReportFilter.PreviousPeriod`, `KpiService.SummaryAsync` |
| A7 | **KPI thresholds are not agreed.** | None set in UAT or Production, so no status is shown. Development has illustrative values to show the design. | `Reporting:Thresholds` |
| A8 | **Fabric gold object names, columns and grain are unknown.** | Placeholder views `gold.vw_vas_*`, each at month × country × centre × visa type. | `Reporting/Sql/*.sql` and its README |
| A9 | **Where KPI logic lives** (gold views or a semantic model). | SQL analytics endpoint on a Lakehouse. | `IReportingDataSource` (swap in a new adapter) |
| A10 | **Revenue currency.** One currency or several; FX rate date. | Single reporting currency (`Reporting:Currency`, default USD). | `appsettings.json`, `Revenue.sql` |
| A11 | **Time zone and date basis.** Application date or decision date; calendar or fiscal year. | Calendar months on `month_key`, UTC, calendar quarters. Filtering is by month. | `Reporting:TimeZone`, SQL `month_key`, `Header.jsx` presets |
| A12 | **Outcome categories.** | Whatever the data returns. Approved, Rejected and Pending have fixed colours; others are grey. | `Donut.jsx` |

## B. Gold data refresh (sizing baseline: "TBC with Jaffery")

| # | Gap | What the build assumes |
|---|-----|------------------------|
| B1 | Refresh schedule, and after how many hours data counts as out of date | `Reporting:StaleAfterHours` is unset, so data is never flagged out of date. The last refresh time is always shown. |
| B2 | Which object records the last refresh | `gold.vw_vas_refresh_status.last_refreshed_utc` (`LastRefreshed.sql`). |
| B3 | SQL endpoint visibility: how soon refreshed gold tables appear in the SQL analytics endpoint | Not handled by the app. If the endpoint lags the Lakehouse, the refresh time shown must be the endpoint's, not the pipeline's. |

## C. Identity and access

| # | Gap | What the build assumes |
|---|-----|------------------------|
| C1 | Name of the API permission and how executives are assigned | Scope `Dashboard.Read`, app role `Dashboard.Viewer`. Both are required. Names are configurable. |
| C2 | Who owns admin consent and role assignment | Not decided. Needs a named owner. |
| C3 | Whether the Fabric tenant allows managed identities to read the SQL analytics endpoint | Yes: the Web App's system-assigned managed identity is added to the workspace. No client secret and no Key Vault (sizing baseline). **Not yet proven against a real endpoint.** |
| C4 | Session behaviour: idle timeout, sign-out, Conditional Access, MFA | Tenant policies apply. Tokens are held in sessionStorage, so closing the tab signs the user out. |
| C5 | Who may see which country or centre; PII and masking (requirements section 5.2) | Common scope for all executives. Filters only narrow results. The API returns aggregates only, never applicant-level rows. |

## D. Hosting and non-functional (sizing baseline rows marked proposed or pending)

| # | Gap | What the build assumes |
|---|-----|------------------------|
| D1 | App Service plan: B1 Linux, one instance, Always On | Captured in `infra/main.bicep`. Proposed, not approved. |
| D2 | Measured CPU and memory | **Pending.** `loadtest/load-test.mjs` is ready; it must run against the release container connected to UAT Fabric. |
| D3 | Container image size | 102 MB in a local build. Confirm from the first CI build. |
| D4 | Deployment slots | B1 has none. Move to Standard or Premium if UAT must test slot swaps. |
| D5 | Result caching | Off (`Reporting:CacheSeconds = 0`). Turn on only if the load test shows Fabric query time needs it. |
| D6 | CI/CD platform | GitHub Actions sample at the repository root. Port it if AEGI uses Azure DevOps. |
| D7 | Network policy (public or private ingress, Fabric egress), region, subscription owner | Public HTTPS. Not set. |
| D8 | Access audit, accessibility standard, supported browsers, availability targets | None invented. Keyboard tabs, ARIA labels, reduced-motion support, table alternatives for charts, and a non-WebGL fallback are built; not formally audited. |
| D9 | TLS mode to the Fabric SQL endpoint | `Mandatory`. Set `Fabric:Encrypt=Strict` only if the endpoint accepts TDS 8.0. |

## E. Scope notes

- **CSAT** is out of scope (requirements section 4) and is no longer shown as Coming Soon.
- **Dashboard customisation, ODIN and non-VAS data sources** are out of scope and not built.
- **Design sign-off**: the current UI is the proposal for the 13 October sign-off. The screenshots in `docs/screenshots/` predate the added views and need retaking.
- **Local development** uses a Development-only sample data source and sign-in bypass. Both refuse to start in UAT or Production, and a test proves it.
