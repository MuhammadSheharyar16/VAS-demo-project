import { auth } from '../../auth/msalConfig'
import { fmtDateTime, monthYear } from '../../utils/format'

// Period presets are built from the latest month available in the reporting data.
export function periodPresets(maxMonth) {
  if (!maxMonth) return []
  const year = maxMonth.slice(0, 4), last = Number(maxMonth.slice(5, 7))
  const presets = [{ id: 'ytd', label: `Year to date (${monthYear(`${year}-01`).slice(0, 3)} – ${monthYear(maxMonth)})`, from: `${year}-01`, to: maxMonth }]
  for (let q = Math.ceil(last / 3); q >= 1; q--) {
    const start = (q - 1) * 3 + 1, end = Math.min(q * 3, last)
    presets.push({ id: `q${q}`, label: `Q${q} ${year}${end < q * 3 ? ' (so far)' : ''}`, from: `${year}-${String(start).padStart(2, '0')}`, to: `${year}-${String(end).padStart(2, '0')}` })
  }
  return presets
}

export default function Header({ options, presets, filters, onChange, meta, children }) {
  const account = auth.enabled ? auth.instance.getActiveAccount() : null
  const country = options?.countries.find((c) => c.country === filters.country)
  const fresh = meta?.freshness
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
          <select id="period" className="select" value={filters.presetId} onChange={(e) => {
            const p = presets.find((x) => x.id === e.target.value)
            onChange({ presetId: p.id, from: p.from, to: p.to })
          }}>
            {presets.map((p) => <option key={p.id} value={p.id}>{p.label}</option>)}
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
      </div>
      {children}
    </header>
  )
}
