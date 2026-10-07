import { motion } from 'framer-motion'
import { itemVariants, reducedMotion } from '../../theme/motion'
import CountUp from './CountUp'
import Sparkline from './Sparkline'

export default function KpiCard({ label, value, format, unit, note, spark }) {
  const onMove = (e) => {
    const el = e.currentTarget, r = el.getBoundingClientRect()
    const x = (e.clientX - r.left) / r.width, y = (e.clientY - r.top) / r.height
    el.style.setProperty('--mx', `${x * 100}%`)
    el.style.setProperty('--my', `${y * 100}%`)
    if (!reducedMotion) el.style.transform = `rotateY(${(x - 0.5) * 10}deg) rotateX(${(0.5 - y) * 10}deg) translateZ(4px)`
  }
  return (
    <motion.div className="kpi" variants={itemVariants} onPointerMove={onMove} onPointerLeave={(e) => (e.currentTarget.style.transform = '')}>
      <div className="lab">{label}</div>
      <div className="val">
        {value == null ? <span className="na" title="No data for the selected filters">–</span>
          : <><CountUp value={value} format={format} />{unit && <span className="u">{unit}</span>}</>}
      </div>
      <div className="row"><span className="note">{note}</span><Sparkline values={spark} /></div>
    </motion.div>
  )
}
