import { lazy, Suspense, useEffect, useState } from 'react'
import Panel from '../components/common/Panel'
import QueryState from '../components/common/QueryState'
import HBars from '../components/charts/HBars'
import { useKpi } from '../hooks/useKpi'
import { fmtInt, fmtNum } from '../utils/format'

// Three.js is large, so it loads only when the VAS tab opens.
const RevenueBars3D = lazy(() => import('../components/three/RevenueBars3D'))

export default function Vas({ filters, onMeta }) {
  const [journey, setJourney] = useState('digital')
  const revenue = useKpi('revenue', filters)
  const digital = useKpi('turnaroundDigital', filters)
  const centre = useKpi('turnaroundCentre', filters)
  const types = useKpi('visaTypes', filters)
  const tat = journey === 'digital' ? digital : centre

  useEffect(() => { if (revenue.data) onMeta(revenue.data.meta) }, [revenue.data, onMeta])

  return (
    <div className="grid" style={{ marginTop: 0 }}>
      <Panel span={8} title={filters.country ? `Revenue by centre in ${filters.country}, in 3D` : "Revenue by country, in 3D"} sub="VAS revenue for the period. Bar height is revenue.">
        <QueryState query={revenue} height={380} isEmpty={(r) => r.countries.length === 0}>
          {(r, meta) => <Suspense fallback={<div className="skeleton" style={{ height: 380 }} />}><RevenueBars3D currency={meta.currency} bars={filters.country
            ? (r.countries[0]?.centres ?? []).map((c) => ({ label: c.centre, revenue: c.revenue }))
            : r.countries.map((c) => ({ label: c.country, revenue: c.revenue, sharePercent: c.sharePercent, centres: c.centres }))} /></Suspense>}
        </QueryState>
      </Panel>
      <Panel span={4} title="Turnaround time" sub="Average days from application to decision"
        actions={
          <div className="seg" role="group" aria-label="Journey">
            {['digital', 'centre'].map((j) => (
              <button key={j} aria-pressed={journey === j} onClick={() => setJourney(j)}>{j[0].toUpperCase() + j.slice(1)}</button>
            ))}
          </div>
        }>
        <QueryState query={tat} height={300} isEmpty={(t) => t.byVisaType.length === 0}>
          {(t) => <HBars key={journey} items={t.byVisaType} average={t.averageDays} />}
        </QueryState>
      </Panel>
      <Panel span={12} title="Visa type performance" sub="Volumes and outcomes by visa type">
        <QueryState query={types} height={260} isEmpty={(v) => v.visaTypes.length === 0}>
          {(v) => {
            const maxShare = Math.max(...v.visaTypes.map((r) => r.sharePercent), 1)
            return (
              <div className="scroll">
                <table>
                  <caption className="sr-only">Visa type performance</caption>
                  <thead><tr><th scope="col">Visa type</th><th scope="col" className="n">Volume</th><th scope="col">Share</th><th scope="col" className="n">Approved</th><th scope="col" className="n">Rejected</th><th scope="col" className="n">Pending</th><th scope="col" className="n">Closure</th></tr></thead>
                  <tbody>
                    {v.visaTypes.map((r) => (
                      <tr key={r.visaType}>
                        <th scope="row" style={{ fontWeight: 600, color: 'var(--fg)', textTransform: 'none', letterSpacing: 0, fontSize: 14 }}>{r.visaType}</th>
                        <td className="n">{fmtInt(r.volume)}</td>
                        <td><div className="share"><div className="bar"><i style={{ width: `${(r.sharePercent / maxShare) * 100}%` }} /></div><span>{fmtNum(r.sharePercent)}%</span></div></td>
                        <td className="n" style={{ color: 'var(--ok)' }}>{fmtNum(r.approvedPercent)}%</td>
                        <td className="n" style={{ color: 'var(--bad)' }}>{fmtNum(r.rejectedPercent)}%</td>
                        <td className="n" style={{ color: 'var(--warn)' }}>{fmtNum(r.pendingPercent)}%</td>
                        <td className="n">{fmtNum(r.closurePercent)}%</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )
          }}
        </QueryState>
      </Panel>
    </div>
  )
}
