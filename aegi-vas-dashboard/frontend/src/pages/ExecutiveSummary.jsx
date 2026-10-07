import { useEffect } from 'react'
import Panel from '../components/common/Panel'
import QueryState from '../components/common/QueryState'
import KpiCard from '../components/kpi/KpiCard'
import AreaChart from '../components/charts/AreaChart'
import Donut from '../components/charts/Donut'
import { useKpi } from '../hooks/useKpi'
import { fmtInt, fmtMoney, fmtNum, periodLabel } from '../utils/format'

const SEVERITY = { trend: 'var(--ok)', attention: 'var(--warn)', info: 'var(--info)' }

function display(kpi, currency) {
  if (kpi.value == null) return { value: null }
  switch (kpi.unit) {
    case 'count': return { value: kpi.value, format: fmtInt }
    case 'percent': return { value: kpi.value, format: (v) => fmtNum(v), unit: '%' }
    case 'days': return { value: kpi.value, format: (v) => fmtNum(v), unit: 'days' }
    case 'currency': return { value: kpi.value, format: (v) => fmtMoney(v, currency) }
    default: return { value: kpi.value, format: (v) => fmtNum(v) }
  }
}

export default function ExecutiveSummary({ filters, onMeta }) {
  const summary = useKpi('summary', filters)
  const volume = useKpi('volume', filters)
  const outcomes = useKpi('outcomes', filters)
  const closure = useKpi('closure', filters)

  useEffect(() => { if (summary.data) onMeta(summary.data.meta) }, [summary.data, onMeta])

  const sparks = {
    visas_processed: volume.data?.data.months.map((m) => m.total),
    closure_rate: closure.data?.data.months.map((m) => m.closurePercent),
  }

  return (
    <>
      <QueryState query={summary} height={118}>
        {(s, meta) => (
          <div className="kpis">
            {s.kpis.map((k) => (
              <KpiCard key={k.key} label={k.label} {...display(k, meta.currency)} note={periodLabel(filters)} spark={sparks[k.key]} />
            ))}
          </div>
        )}
      </QueryState>
      <div className="grid">
        <Panel span={8} title="Monthly visa volumes" sub="Applications processed per month, digital vs centre journey"
          actions={<div className="legend"><span><i style={{ background: 'var(--gold)' }} />Centre</span><span><i style={{ background: 'var(--info)' }} />Digital</span></div>}>
          <QueryState query={volume} height={240} isEmpty={(v) => v.total === 0}>{(v) => <AreaChart months={v.months} />}</QueryState>
        </Panel>
        <Panel span={4} title="Visa outcomes" sub="Share of all decisions in the period">
          <QueryState query={outcomes} height={240} isEmpty={(o) => o.total === 0}>{(o) => <Donut outcomes={o.outcomes} />}</QueryState>
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
        <Panel span={6} title="Coming soon" sub="Outside the Phase 1 VAS scope">
          <div className="insights">
            {(summary.data?.data.comingSoon || ['CSAT', 'Operations', 'Finance', 'Alfit']).map((name) => (
              <div className="ins" key={name}><span className="sev" style={{ background: 'var(--line2)' }} />
                <div><b>{name}</b><span>{name === 'CSAT' ? 'Customer satisfaction reporting will be added in a later phase.' : 'This tab is reserved for a future phase.'}</span></div></div>
            ))}
          </div>
        </Panel>
      </div>
    </>
  )
}
