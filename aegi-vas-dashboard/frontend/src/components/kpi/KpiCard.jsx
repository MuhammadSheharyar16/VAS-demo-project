import { motion } from 'framer-motion'
import { itemVariants, reducedMotion } from '../../theme/motion'
import { fmtChange } from '../../utils/format'
import CountUp from './CountUp'
import Sparkline from './Sparkline'

const STATUS = { good: 'On target', watch: 'Watch', poor: 'Off target' }

// change, changeUnit, higherIsBetter and status come from the API; the card only decides how to show them.
export default function KpiCard({ label, value, format, unit, note, spark, change, changeUnit, higherIsBetter, status, comparedWith }) {
  const onMove = (e) => {
    const el = e.currentTarget, r = el.getBoundingClientRect()
    const x = (e.clientX - r.left) / r.width, y = (e.clientY - r.top) / r.height
    el.style.setProperty('--mx', `${x * 100}%`)
    el.style.setProperty('--my', `${y * 100}%`)
    if (!reducedMotion) el.style.transform = `rotateY(${(x - 0.5) * 10}deg) rotateX(${(0.5 - y) * 10}deg) translateZ(4px)`
  }
  const tone = change == null || change === 0 || higherIsBetter == null ? 'flat' : (change > 0) === higherIsBetter ? 'good' : 'poor'
  return (
    <motion.div className="kpi" variants={itemVariants} onPointerMove={onMove} onPointerLeave={(e) => (e.currentTarget.style.transform = '')}>
      <div className="lab"><span>{label}</span>{status && <span className={`status ${status}`}>{STATUS[status]}</span>}</div>
      <div className="val">
        {value == null ? <span className="na" title="No data for the selected filters">–</span>
          : <><CountUp value={value} format={format} />{unit && <span className="u">{unit}</span>}</>}
      </div>
      {change != null && (
        <div className={`delta ${tone}`}>
          <span aria-hidden="true">{change > 0 ? '▲' : change < 0 ? '▼' : '■'}</span> {fmtChange(change, changeUnit)}
          {comparedWith && <span className="vs"> vs {comparedWith}</span>}
        </div>
      )}
      <div className="row"><span className="note">{note}</span><Sparkline values={spark} /></div>
    </motion.div>
  )
}
