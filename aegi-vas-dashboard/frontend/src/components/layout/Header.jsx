import { auth } from '../../auth/msalConfig'
import { addMonths, fmtDateTime, monthLabel, monthRange, monthYear } from '../../utils/format'

// Period presets are built from the months available in the reporting data. Any other range is "Custom range".
export function periodPresets(minMonth, maxMonth) {
  if (!maxMonth) return []
  const year = maxMonth.slice(0, 4), last = Number(maxMonth.slice(5, 7))
  const clamp = (m) => (minMonth && m < minMonth ? minMonth : m)
  const presets = [{ id: 'ytd', label: `Year to date (${monthLabel(`${year}-01`)} – ${monthYear(maxMonth)})`, from: clamp(`${year}-01`), to: maxMonth }]
  for (let q = Math.ceil(last / 3); q >= 1; q--) {
    const start = (q - 1) * 3 + 1, end = Math.min(q * 3, last)
    const from = `${year}-${String(start).padStart(2, '0')}`, to = `${year}-${String(end).padStart(2, '0')}`
    if (minMonth && to < minMonth) continue
    presets.push({ id: `q${q}`, label: `Q${q} ${year}${end < q * 3 ? ' (so far)' : ''}`, from: clamp(from), to })
  }
  if (minMonth && minMonth < `${year}-01`) {
    presets.push({ id: 'last12', label: 'Last 12 months', from: clamp(addMonths(maxMonth, -11)), to: maxMonth })
    presets.push({ id: 'prevyear', label: `${Number(year) - 1}`, from: clamp(`${Number(year) - 1}-01`), to: `${Number(year) - 1}-12` })
  }
  return presets
}

export default function Header({ options, presets, filters, onChange, meta, children }) {
  const account = auth.enabled ? auth.instance.getActiveAccount() : null
  const country = options?.countries.find((c) => c.country === filters.country)
  const months = options?.minMonth && options?.maxMonth ? monthRange(options.minMonth, options.maxMonth) : []
  const fresh = meta?.freshness

  // A hand-picked range that happens to equal a preset is shown as that preset.
  const setRange = (from, to) => {
    const preset = presets.find((p) => p.from === from && p.to === to)
    onChange({ presetId: preset?.id ?? 'custom', from, to })
  }

  return (
    <header className="header">
      <div className="hrow">
        <div className="brand">
          <div className="mark">VAS</div>
          <div><small>AEGI · Executive Dashboard</small><h1>Visa Application Services</h1></div>
        </div>
        <div className="meta">
          {fresh && (
            <span className={`chip ${fresh.isStale ? 'stale' : fresh.source === 'Sample data' ? 'sample' : 'live'}`}
              title={fresh.lastRefreshedUtc ? `Gold layer last refreshed ${fmtDateTime(fresh.lastRefreshedUtc)}` : 'Refresh time not available'}>
              {fresh.source !== 'Sample data' && <span className="dot" />}
              {fresh.source}{fresh.lastRefreshedUtc ? ` · refreshed ${fmtDateTime(fresh.lastRefreshedUtc)}` : ''}{fresh.isStale ? ' · out of date' : ''}
            </span>
          )}
          {account && (
            <span className="user">{account.name}
              <button className="linkbtn" onClick={() => auth.instance.logoutRedirect()}>Sign out</button>
            </span>
          )}
        </div>
      </div>
      <div className="frow">
        <label className="field"><span>Period</span>
          <select id="period" className="select" value={filters.presetId ?? ''} onChange={(e) => {
            const p = presets.find((x) => x.id === e.target.value)
            if (p) onChange({ presetId: p.id, from: p.from, to: p.to })
          }}>
            {presets.map((p) => <option key={p.id} value={p.id}>{p.label}</option>)}
            {filters.presetId === 'custom' && <option value="custom">Custom range</option>}
          </select>
        </label>
        {/* Picking a start after the end (or an end before the start) moves the other side to match. */}
        <label className="field"><span>From</span>
          <select id="from" className="select" value={filters.from ?? ''} disabled={!months.length}
            onChange={(e) => setRange(e.target.value, e.target.value > filters.to ? e.target.value : filters.to)}>
            {months.map((m) => <option key={m} value={m}>{monthYear(m)}</option>)}
          </select>
        </label>
        <label className="field"><span>To</span>
          <select id="to" className="select" value={filters.to ?? ''} disabled={!months.length}
            onChange={(e) => setRange(e.target.value < filters.from ? e.target.value : filters.from, e.target.value)}>
            {months.map((m) => <option key={m} value={m}>{monthYear(m)}</option>)}
          </select>
        </label>
        <label className="field"><span>Country</span>
          <select id="country" className="select" value={filters.country ?? ''} onChange={(e) => onChange({ country: e.target.value || null, centre: null })}>
            <option value="">All countries</option>
            {options?.countries.map((c) => <option key={c.country} value={c.country}>{c.country}</option>)}
          </select>
        </label>
        <label className="field"><span>Centre</span>
          <select id="centre" className="select" value={filters.centre ?? ''} disabled={!country} onChange={(e) => onChange({ centre: e.target.value || null })}>
            <option value="">{country ? 'All centres' : 'Choose a country first'}</option>
            {country?.centres.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
        </label>
        <label className="field"><span>Visa type</span>
          <select id="visaType" className="select" value={filters.visaType ?? ''} onChange={(e) => onChange({ visaType: e.target.value || null })}>
            <option value="">All visa types</option>
            {options?.visaTypes.map((v) => <option key={v} value={v}>{v}</option>)}
          </select>
        </label>
      </div>
      {children}
    </header>
  )
}
