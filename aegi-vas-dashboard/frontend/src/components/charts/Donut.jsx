import { motion } from 'framer-motion'
import { reducedMotion } from '../../theme/motion'
import { fmtNum } from '../../utils/format'

const COLORS = { approved: 'var(--ok)', rejected: 'var(--bad)', pending: 'var(--warn)' }
const colour = (outcome) => COLORS[outcome.toLowerCase()] || 'var(--faint)'

// approvalPercent comes from the API, so the centre figure always matches the headline KPI.
export default function Donut({ outcomes, approvalPercent }) {
  const cx = 130, cy = 112, r = 84, sw = 22, C = 2 * Math.PI * r
  let offset = 0
  return (
    <>
      <svg viewBox="0 0 260 230" style={{ width: '100%', height: 'auto', maxHeight: 260 }} role="img"
        aria-label={`Application outcomes: ${outcomes.map((o) => `${o.outcome} ${fmtNum(o.percent)}%`).join(', ')}`}>
        <circle cx={cx} cy={cy} r={r} fill="none" stroke="var(--line)" strokeWidth={sw} />
        {outcomes.map((o, i) => {
          const len = (o.percent / 100) * C, start = offset
          offset += len
          return (
            <motion.circle key={o.outcome} cx={cx} cy={cy} r={r} fill="none" stroke={colour(o.outcome)} strokeWidth={sw}
              strokeDashoffset={-start} transform={`rotate(-90 ${cx} ${cy})`}
              initial={{ strokeDasharray: `${reducedMotion ? Math.max(0, len - 2) : 0} ${C}` }}
              animate={{ strokeDasharray: `${Math.max(0, len - 2)} ${C}` }}
              transition={{ duration: 1, delay: 0.2 + i * 0.15, ease: [0.2, 0.8, 0.2, 1] }} />
          )
        })}
        <text x={cx} y={cy - 2} textAnchor="middle" style={{ fill: 'var(--fg)', fontFamily: 'var(--display)', fontSize: 30 }}>
          {approvalPercent != null ? `${fmtNum(approvalPercent)}%` : '–'}
        </text>
        <text x={cx} y={cy + 20} textAnchor="middle">approved</text>
      </svg>
      <div className="legend" style={{ justifyContent: 'center' }}>
        {outcomes.map((o) => <span key={o.outcome}><i style={{ background: colour(o.outcome) }} />{o.outcome} {fmtNum(o.percent)}%</span>)}
      </div>
    </>
  )
}
