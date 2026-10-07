# KPI queries

One parameterised, read-only query per KPI. View and column names are **placeholders** until
discovery confirms the real Fabric gold structures (roadmap work package "Fabric and reporting").

Every query receives the same parameters:

| Parameter | Type | Meaning |
|---|---|---|
| `@From`, `@To` | `char(7)` `yyyy-MM` | Inclusive reporting months |
| `@Country` | `nvarchar(100)` or NULL | Optional country filter |
| `@Centre` | `nvarchar(100)` or NULL | Optional centre filter |
| `@Journey` | `nvarchar(20)` | Only in `Turnaround.sql`: `digital` or `centre` |

Keep the output column names the same when you swap in real views; the C# code reads them by name.
If KPI formulas turn out to live in a semantic model, do not recreate them here (see roadmap section 08).
