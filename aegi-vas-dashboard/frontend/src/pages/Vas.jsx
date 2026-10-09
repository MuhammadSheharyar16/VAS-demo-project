import { lazy, Suspense, useEffect, useState } from 'react'
import Panel from '../components/common/Panel'
import QueryState from '../components/common/QueryState'
import KpiCard from '../components/kpi/KpiCard'
import { display } from '../components/kpi/display'
import AreaChart from '../components/charts/AreaChart'
import BarList from '../components/charts/BarList'
import Donut from '../components/charts/Donut'
import HBars from '../components/charts/HBars'
import StageBars from '../components/charts/StageBars'
import TrendLine from '../components/charts/TrendLine'
import { useKpi } from '../hooks/useKpi'
import { fmtInt, fmtMoney, fmtNum, periodLabel } from '../utils/format'

// Three.js is large, so it loads only when the VAS tab opens.
const RevenueBars3D = lazy(() => import('../components/three/RevenueBars3D'))

// Without WebGL (locked-down browsers, remote desktops) the revenue view falls back to plain bars.
const hasWebGL = (() => {
  try {
    const c = document.createElement('canvas')
    return Boolean(c.getContext('webgl2') || c.getContext('webgl'))
  } catch { return false }
})()

const JOURNEY_LEGEND = <div className="legend"><span><i style={{ background: 'var(--gold)' }} />Centre</span><span><i style={{ background: 'var(--info)' }} />Digital</span></div>
const pct = (v) => (v == null ? '–' : `${fmtNum(v)}%`)

export default function Vas({ filters, onMeta }) {
  const [journey, setJourney] = useState('digital')
  const outcomes = useKpi('outcomes', filters)
  const volume = useKpi('volume', filters)
  const stages = useKpi('stages', filters)
  const closure = useKpi('closure', filters)
  const revenue = useKpi('revenue', filters)
  const digital = useKpi('turnaroundDigital', filters)
  const centre = useKpi('turnaroundCentre', filters)
  const types = useKpi('visaTypes', filters)
  const centres = useKpi('centres', filters)
  const tat = journey === 'digital' ? digital : centre

  useEffect(() => { if (outcomes.data) onMeta(outcomes.data.meta) }, [outcomes.data, onMeta])

  return (
    <>
      <QueryState query={outcomes} height={118}>
        {(o, meta) => (
          <div className="kpis">
            {[
              ['Applications received', o.received, 'count'], ['Applications processed', o.processed, 'count'],
              ['Applications pending', o.pending, 'count'], ['Approval rate', o.approvalPercent, 'percent'],
              ['Rejection rate', o.rejectionPercent, 'percent'],
            ].map(([label, value, unit]) => <KpiCard key={label} label={label} {...display(value, unit)} note={periodLabel(meta.filters)} />)}
          </div>
        )}
      </QueryState>
      <div className="grid">
        <Panel span={8} title="Monthly application volumes" sub="Applications received per month, digital vs centre journey" actions={JOURNEY_LEGEND}>
          <QueryState query={volume} height={240} isEmpty={(v) => v.total === 0}>{(v) => <AreaChart months={v.months} />}</QueryState>
        </Panel>
        <Panel span={4} title="Application outcomes" sub="Share of applications received in the period">
          <QueryState query={outcomes} height={240} isEmpty={(o) => o.received === 0}>
            {(o) => <Donut outcomes={o.outcomes} approvalPercent={o.approvalPercent} />}
          </QueryState>
        </Panel>

        <Panel span={6} title="Processing stages" sub="Applications that reached each stage, as a share of the first" actions={JOURNEY_LEGEND}>
          <QueryState query={stages} height={220} isEmpty={(s) => s.stages.length === 0 || s.stages[0].total === 0}>
            {(s) => <StageBars stages={s.stages} />}
          </QueryState>
        </Panel>
        <Panel span={6} title="Visa closure by month" sub="Closed applications as a share of all applications">
          <QueryState query={closure} height={220} isEmpty={(c) => c.months.length === 0}>
            {(c) => <TrendLine label="Visa closure by month" points={c.months.map((m) => ({ month: m.month, value: m.closurePercent }))} />}
          </QueryState>
        </Panel>

        <Panel span={8} title={filters.country ? `Revenue by centre in ${filters.country}` : 'Revenue by country'} sub="VAS revenue for the period">
          <QueryState query={revenue} height={380} isEmpty={(r) => r.countries.length === 0}>
            {(r, meta) => {
              const bars = filters.country
                ? (r.countries[0]?.centres ?? []).map((c) => ({ label: c.centre, revenue: c.revenue }))
                : r.countries.map((c) => ({ label: c.country, revenue: c.revenue, sharePercent: c.sharePercent, centres: c.centres }))
              return (
                <>
                  {hasWebGL
                    ? <Suspense fallback={<div className="skeleton" style={{ height: 380 }} />}><RevenueBars3D currency={meta.currency} bars={bars} /></Suspense>
                    : <BarList label="Revenue" items={bars.map((b) => ({ label: b.label, value: b.revenue, display: fmtMoney(b.revenue, meta.currency), note: b.sharePercent != null ? `${fmtNum(b.sharePercent)}%` : null }))} />}
                  {/* The 3D scene is a canvas, so the same figures are also given as a table for screen readers. */}
                  <table className="sr-only">
                    <caption>{filters.country ? `Revenue by centre in ${filters.country}` : 'Revenue by country'}</caption>
                    <thead><tr><th scope="col">{filters.country ? 'Centre' : 'Country'}</th><th scope="col">Revenue</th></tr></thead>
                    <tbody>{bars.map((b) => <tr key={b.label}><th scope="row">{b.label}</th><td>{fmtMoney(b.revenue, meta.currency)}</td></tr>)}</tbody>
                  </table>
                </>
              )
            }}
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

        <Panel span={8} title="Visa type performance" sub="Volumes and outcomes by visa type">
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
                          <th scope="row" className="rowhead">{r.visaType}</th>
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
        <Panel span={4} title="Revenue by visa type" sub="VAS revenue for the period">
          <QueryState query={revenue} height={260} isEmpty={(r) => r.byVisaType.length === 0}>
            {(r, meta) => <BarList label="Revenue by visa type" items={r.byVisaType.map((t) => ({ label: t.visaType, value: t.revenue, display: fmtMoney(t.revenue, meta.currency), note: `${fmtNum(t.sharePercent)}%` }))} />}
          </QueryState>
        </Panel>

        <Panel span={12} title="Centre performance" sub="Activity, journey mix, turnaround, revenue and capacity use by centre">
          <QueryState query={centres} height={260} isEmpty={(c) => c.centres.length === 0}>
            {(c, meta) => (
              <div className="scroll">
                <table>
                  <caption className="sr-only">Centre performance</caption>
                  <thead><tr><th scope="col">Centre</th><th scope="col">Country</th><th scope="col" className="n">Applications</th><th scope="col" className="n">In centre</th><th scope="col" className="n">Digital</th><th scope="col" className="n">Processed</th><th scope="col" className="n">Approval</th><th scope="col" className="n">Turnaround</th><th scope="col" className="n">Revenue</th><th scope="col">Capacity used</th></tr></thead>
                  <tbody>
                    {c.centres.map((r) => (
                      <tr key={`${r.country}|${r.centre}`}>
                        <th scope="row" className="rowhead">{r.centre}</th>
                        <td>{r.country}</td>
                        <td className="n">{fmtInt(r.applications)}</td>
                        <td className="n">{fmtInt(r.centreJourney)}</td>
                        <td className="n">{fmtInt(r.digital)}</td>
                        <td className="n">{fmtInt(r.processed)}</td>
                        <td className="n">{pct(r.approvalPercent)}</td>
                        <td className="n">{r.avgTurnaroundDays == null ? '–' : `${fmtNum(r.avgTurnaroundDays)} d`}</td>
                        <td className="n">{fmtMoney(r.revenue, meta.currency)}</td>
                        <td>{r.utilisationPercent == null
                          ? <span className="na" title="Capacity is not recorded per visa type">–</span>
                          : <div className="share"><div className="bar"><i style={{ width: `${Math.min(100, r.utilisationPercent)}%` }} /></div><span>{fmtNum(r.utilisationPercent)}%</span></div>}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </QueryState>
        </Panel>
      </div>
    </>
  )
}
