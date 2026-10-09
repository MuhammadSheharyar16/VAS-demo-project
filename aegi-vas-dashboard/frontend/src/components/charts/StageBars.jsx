import { motion } from 'framer-motion'
import { reducedMotion } from '../../theme/motion'
import { fmtInt, fmtNum } from '../../utils/format'

const JOURNEYS = [['centre', 'var(--gold)'], ['digital', 'var(--info)']]

// Processing stages as a funnel: each bar is the share of applications that reached the stage,
// split into the centre and digital journeys. Stage names, order and percentages come from the API.
export default function StageBars({ stages }) {
  const first = stages[0]?.total || 1
  return (
    <ol className="stages" aria-label="Applications by processing stage">
      {stages.map((s, i) => (
        <li key={s.stage}>
          <div className="st-head"><b>{s.stage}</b><span>{fmtInt(s.total)} · {fmtNum(s.percentOfFirst)}%</span></div>
          <div className="st-track" title={`Centre ${fmtInt(s.centre)} · Digital ${fmtInt(s.digital)}`}>
            {JOURNEYS.map(([journey, color]) => (
              <motion.i key={journey} style={{ background: color }}
                initial={{ width: reducedMotion ? `${(s[journey] / first) * 100}%` : 0 }} animate={{ width: `${(s[journey] / first) * 100}%` }}
                transition={{ duration: 0.8, delay: i * 0.08, ease: [0.2, 0.8, 0.2, 1] }} />
            ))}
          </div>
          <span className="sr-only">Centre {fmtInt(s.centre)}, digital {fmtInt(s.digital)}</span>
        </li>
      ))}
    </ol>
  )
}
