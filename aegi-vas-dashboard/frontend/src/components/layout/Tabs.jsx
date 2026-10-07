import { motion } from 'framer-motion'

export const TABS = [
  { id: 'exec', label: 'Executive Summary' },
  { id: 'vas', label: 'VAS' },
  { id: 'ops', label: 'Operations', soon: true },
  { id: 'fin', label: 'Finance', soon: true },
  { id: 'alfit', label: 'Alfit', soon: true },
]

export default function Tabs({ active, onChange }) {
  const onKey = (e) => {
    const i = TABS.findIndex((t) => t.id === active)
    if (e.key === 'ArrowRight') onChange(TABS[(i + 1) % TABS.length].id)
    if (e.key === 'ArrowLeft') onChange(TABS[(i - 1 + TABS.length) % TABS.length].id)
  }
  return (
    <nav className="tabs" role="tablist" aria-label="Dashboard sections" onKeyDown={onKey}>
      {TABS.map((t) => (
        <button key={t.id} role="tab" className="tab" aria-selected={active === t.id} tabIndex={active === t.id ? 0 : -1} onClick={() => onChange(t.id)}>
          {t.label}{t.soon && <span className="soon">Soon</span>}
          {active === t.id && <motion.span layoutId="ink" className="ink" transition={{ type: 'spring', stiffness: 380, damping: 32 }} />}
        </button>
      ))}
    </nav>
  )
}
