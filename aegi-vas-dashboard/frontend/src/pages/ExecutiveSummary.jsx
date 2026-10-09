import { useEffect } from 'react'
import Panel from '../components/common/Panel'
import QueryState from '../components/common/QueryState'
import KpiCard from '../components/kpi/KpiCard'
import { display } from '../components/kpi/display'
import AreaChart from '../components/charts/AreaChart'
import Donut from '../components/charts/Donut'
import { useKpi } from '../hooks/useKpi'
import { periodLabel } from '../utils/format'

const SEVERITY = { trend: 'var(--ok)', attention: 'var(--warn)', info: 'var(--info)' }

// One request feeds the whole page: headline KPIs, their comparison, and the volume and outcome charts.
export default function ExecutiveSummary({ filters, onMeta }) {
  const summary = useKpi('summary', filters)

  useEffect(() => { if (summary.data) onMeta(summary.data.meta) }, [summary.data, onMeta])

  return (
    <>
      <QueryState query={summary} height={150}>
        {(s, meta) => {
          const sparks = {
            applications_received: s.volume.months.map((m) => m.total),
            closure_rate: s.closure.months.map((m) => m.closurePercent),
          }
          const comparedWith = s.comparedWith && periodLabel(s.comparedWith)
          return (
            <div className="kpis">
              {s.kpis.map((k) => (
                <KpiCard key={k.key} label={k.label} {...display(k.value, k.unit, meta.currency)} note={periodLabel(meta.filters)} spark={sparks[k.key]}
                  change={k.change} changeUnit={k.changeUnit} higherIsBetter={k.higherIsBetter} status={k.status} comparedWith={comparedWith} />
              ))}
            </div>
          )
        }}
      </QueryState>
      <div className="grid">
        <Panel span={8} title="Monthly application volumes" sub="Applications received per month, digital vs centre journey"
          actions={<div className="legend"><span><i style={{ background: 'var(--gold)' }} />Centre</span><span><i style={{ background: 'var(--info)' }} />Digital</span></div>}>
          <QueryState query={summary} height={240} isEmpty={(s) => s.volume.total === 0}>{(s) => <AreaChart months={s.volume.months} />}</QueryState>
        </Panel>
        <Panel span={4} title="Application outcomes" sub="Share of applications received in the period">
          <QueryState query={summary} height={240} isEmpty={(s) => s.outcomes.received === 0}>
            {(s) => <Donut outcomes={s.outcomes.outcomes} approvalPercent={s.outcomes.approvalPercent} />}
          </QueryState>
        </Panel>
        <Panel span={6} title="Key insights" sub="From this period's VAS data">
          <QueryState query={summary} height={180} isEmpty={(s) => s.highlights.length === 0}>
            {(s) => (
              <div className="insights">
                {s.highlights.map((h) => (
                  <div className="ins" key={h.title}><span className="sev" style={{ background: SEVERITY[h.kind] || 'var(--info)' }} /><div><b>{h.title}</b><span>{h.detail}</span></div></div>
                ))}
              </div>
            )}
          </QueryState>
        </Panel>
        <Panel span={6} title="Coming soon" sub="Reporting areas planned for later phases">
          <div className="insights">
            {(summary.data?.data.comingSoon || ['Operations', 'Finance', 'Alfit']).map((name) => (
              <div className="ins" key={name}><span className="sev" style={{ background: 'var(--line2)' }} />
                <div><b>{name}</b><span>This tab is reserved for a future phase.</span></div></div>
            ))}
          </div>
        </Panel>
      </div>
    </>
  )
}
