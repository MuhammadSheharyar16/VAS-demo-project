import { useRef, useState } from 'react'
import { motion } from 'framer-motion'
import { fmtInt, monthLabel, monthYear } from '../../utils/format'
import { reducedMotion } from '../../theme/motion'

// Stacked area: digital at the bottom, centre stacked on top. Data comes from /vas/volume.
export default function AreaChart({ months }) {
  const W = 720, H = 260, L = 48, R = 12, T = 14, B = 28
  const [hover, setHover] = useState(null)
  const svgRef = useRef(null)
  if (!months.length) return <p className="sub">No data for this period.</p>

  const top = Math.max(...months.map((m) => m.total))
  // Round gridlines: pick a 1/2/2.5/5 x 10^k step giving about four intervals.
  const raw = (top * 1.05) / 4 || 1, mag = Math.pow(10, Math.floor(Math.log10(raw)))
  const step = [1, 2, 2.5, 5, 10].map((f) => f * mag).find((v) => v >= raw)
  const max = Math.ceil((top * 1.05) / step) * step
  const ticks = Array.from({ length: Math.round(max / step) + 1 }, (_, i) => i * step)
  const n = months.length
  // A range that crosses a year end shows the year on the first month and on each January, and thins the labels.
  const multiYear = months[0].month.slice(0, 4) !== months[n - 1].month.slice(0, 4)
  const every = Math.ceil(n / 12)
  const tick = (m, i) => (multiYear && (i === 0 || m.month.endsWith('-01')) ? `${monthLabel(m.month)} ${m.month.slice(2, 4)}` : monthLabel(m.month))
  const x = (i) => (n === 1 ? (L + W - R) / 2 : L + (i * (W - L - R)) / (n - 1))
  const y = (v) => T + (1 - v / max) * (H - T - B)
  const line = (vals) => 'M' + vals.map((v, i) => `${x(i)},${y(v)}`).join('L')
  const totals = months.map((m) => m.total), digital = months.map((m) => m.digital)
  const centreFill = line(totals) + 'L' + [...digital].map((v, i) => `${x(i)},${y(v)}`).reverse().join('L') + 'Z'
  const digitalFill = line(digital) + `L${x(n - 1)},${y(0)}L${x(0)},${y(0)}Z`
  const draw = { initial: { pathLength: reducedMotion ? 1 : 0 }, animate: { pathLength: 1 }, transition: { duration: 1.6, ease: [0.2, 0.8, 0.2, 1] } }
  const fade = { initial: { opacity: reducedMotion ? 1 : 0 }, animate: { opacity: 1 }, transition: { duration: 1.2, delay: 0.4 } }

  const onMove = (e) => {
    const pt = svgRef.current.createSVGPoint(); pt.x = e.clientX; pt.y = e.clientY
    const q = pt.matrixTransform(svgRef.current.getScreenCTM().inverse())
    setHover(Math.max(0, Math.min(n - 1, Math.round(((q.x - L) / (W - L - R)) * (n - 1)))))
  }
  const hi = hover, bx = hi != null && x(hi) > W - 160 ? x(hi) - 152 : (hi != null ? x(hi) + 10 : 0)

  return (
    <svg ref={svgRef} viewBox={`0 0 ${W} ${H}`} style={{ width: '100%', height: 'auto' }} role="img"
      aria-label={`Monthly application volumes: ${months.map((m) => `${monthYear(m.month)} ${fmtInt(m.total)}`).join(', ')}`}>
      <defs>
        <linearGradient id="gC" x1="0" x2="0" y1="0" y2="1"><stop offset="0" stopColor="#d6b25e" stopOpacity=".45" /><stop offset="1" stopColor="#d6b25e" stopOpacity="0" /></linearGradient>
        <linearGradient id="gD" x1="0" x2="0" y1="0" y2="1"><stop offset="0" stopColor="#6aa8e4" stopOpacity=".45" /><stop offset="1" stopColor="#6aa8e4" stopOpacity="0" /></linearGradient>
      </defs>
      {ticks.map((v) => (
        <g key={v}>
          <line x1={L} x2={W - R} y1={y(v)} y2={y(v)} stroke="var(--line)" strokeDasharray={v ? '3 4' : undefined} />
          <text x={L - 8} y={y(v) + 4} textAnchor="end">{v >= 1000 ? `${Math.round(v / 1000)}k` : Math.round(v)}</text>
        </g>
      ))}
      {months.map((m, i) => i % every === 0 && <text key={m.month} x={x(i)} y={H - 8} textAnchor="middle">{tick(m, i)}</text>)}
      <motion.path d={centreFill} fill="url(#gC)" {...fade} />
      <motion.path d={digitalFill} fill="url(#gD)" {...fade} />
      <motion.path d={line(totals)} fill="none" stroke="var(--gold)" strokeWidth="2.2" {...draw} />
      <motion.path d={line(digital)} fill="none" stroke="var(--info)" strokeWidth="2.2" {...draw} />
      <circle cx={x(n - 1)} cy={y(totals[n - 1])} r="4" fill="var(--gold2)" stroke="var(--bg)" strokeWidth="2" />
      {hi != null && (
        <g pointerEvents="none">
          <line x1={x(hi)} x2={x(hi)} y1={T} y2={H - B} stroke="var(--gold2)" strokeOpacity=".5" />
          <rect x={bx} y={T + 4} width="142" height="54" rx="8" fill="#0b1730" stroke="var(--line)" />
          <text x={bx + 10} y={T + 22} style={{ fill: 'var(--fg)', fontWeight: 600 }}>{monthYear(months[hi].month)} · {fmtInt(totals[hi])}</text>
          <text x={bx + 10} y={T + 37}>Centre {fmtInt(months[hi].centre)}</text>
          <text x={bx + 10} y={T + 51}>Digital {fmtInt(months[hi].digital)}</text>
        </g>
      )}
      <rect x={L} y={T} width={W - L - R} height={H - T - B} fill="transparent" onPointerMove={onMove} onPointerLeave={() => setHover(null)} />
    </svg>
  )
}
