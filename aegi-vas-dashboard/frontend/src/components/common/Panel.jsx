import { motion } from 'framer-motion'
import { itemVariants } from '../../theme/motion'

export default function Panel({ title, sub, span = 12, actions, children }) {
  return (
    <motion.section className={`panel c${span}`} variants={itemVariants}>
      <div className="ph">
        <div><h3>{title}</h3>{sub && <div className="sub">{sub}</div>}</div>
        {actions}
      </div>
      {children}
    </motion.section>
  )
}
