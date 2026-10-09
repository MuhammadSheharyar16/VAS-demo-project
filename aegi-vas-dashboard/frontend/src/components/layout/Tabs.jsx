import { useRef } from 'react'
import { motion } from 'framer-motion'

export const TABS = [
  { id: 'exec', label: 'Executive Summary' },
  { id: 'vas', label: 'VAS' },
  { id: 'ops', label: 'Operations', soon: true },
  { id: 'fin', label: 'Finance', soon: true },
  { id: 'alfit', label: 'Alfit', soon: true },
]

export default function Tabs({ active, onChange }) {
  const refs = useRef({})
  // Arrow keys move both the selection and the keyboard focus, as the tab pattern expects.
  const onKey = (e) => {
    const i = TABS.findIndex((t) => t.id === active)
    const next = e.key === 'ArrowRight' ? TABS[(i + 1) % TABS.length]
      : e.key === 'ArrowLeft' ? TABS[(i - 1 + TABS.length) % TABS.length]
      : e.key === 'Home' ? TABS[0] : e.key === 'End' ? TABS[TABS.length - 1] : null
    if (!next) return
    e.preventDefault()
    onChange(next.id)
    refs.current[next.id]?.focus()
  }
  return (
    <nav className="tabs" role="tablist" aria-label="Dashboard sections" onKeyDown={onKey}>
      {TABS.map((t) => (
        <button key={t.id} id={`tab-${t.id}`} ref={(el) => { refs.current[t.id] = el }} role="tab" className="tab"
          aria-selected={active === t.id} aria-controls="tabpanel" tabIndex={active === t.id ? 0 : -1} onClick={() => onChange(t.id)}>
          {t.label}{t.soon && <span className="soon">Soon</span>}
          {active === t.id && <motion.span layoutId="ink" className="ink" transition={{ type: 'spring', stiffness: 380, damping: 32 }} />}
        </button>
      ))}
    </nav>
  )
}
