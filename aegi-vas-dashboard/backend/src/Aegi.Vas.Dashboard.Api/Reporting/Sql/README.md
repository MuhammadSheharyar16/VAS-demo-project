# KPI queries

One parameterised, read-only query per KPI. View and column names are **placeholders** until
discovery confirms the real Fabric gold structures (KPI Mapping document, requirements section 3.3).

Every query receives the same parameters:

| Parameter | Type | Meaning |
|---|---|---|
| `@From`, `@To` | `char(7)` `yyyy-MM` | Inclusive reporting months |
| `@Country` | `nvarchar(100)` or NULL | Optional country filter |
| `@Centre` | `nvarchar(100)` or NULL | Optional centre filter |
| `@VisaType` | `nvarchar(100)` or NULL | Optional visa type filter |
| `@Journey` | `nvarchar(20)` | Only in `Turnaround.sql`: `digital` or `centre` |

Views the queries expect, each at the grain month x country x centre x visa type (plus journey where shown):

| Query | View | Extra columns |
|---|---|---|
| `Volume.sql`, `FilterOptions.sql` | `gold.vw_vas_visa_volume` | `journey`, `visa_count` |
| `Outcomes.sql` | `gold.vw_vas_visa_outcomes` | `outcome`, `visa_count` |
| `Closure.sql` | `gold.vw_vas_visa_closure` | `closed_count`, `total_count` |
| `Stages.sql` | `gold.vw_vas_application_stages` | `journey`, `stage`, `stage_order`, `application_count` |
| `Turnaround.sql` | `gold.vw_vas_turnaround` | `journey`, `total_turnaround_days`, `application_count` |
| `Revenue.sql` | `gold.vw_vas_revenue` | `revenue_amount` |
| `VisaTypes.sql` | `gold.vw_vas_visa_type_performance` | `visa_count`, `approved_count`, `rejected_count`, `pending_count`, `closed_count` |
| `Centres.sql` | `gold.vw_vas_centre_performance` | `application_count`, `centre_journey_count`, `processed_count`, `approved_count`, `total_turnaround_days`, `revenue_amount` |
| `Centres.sql` | `gold.vw_vas_centre_capacity` (month x country x centre) | `capacity_count` |
| `LastRefreshed.sql` | `gold.vw_vas_refresh_status` | `last_refreshed_utc` |

Keep the output column names the same when you swap in real views; the C# code reads them by name.
If KPI formulas turn out to live in a semantic model, do not recreate them here.
