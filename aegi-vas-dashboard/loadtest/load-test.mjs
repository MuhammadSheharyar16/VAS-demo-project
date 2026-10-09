// UAT load test for the sizing baseline: N simultaneous executives using the dashboard.
// No dependencies; needs Node 20 or later.
//
//   node loadtest/load-test.mjs --url https://<uat-app> --users 10 --duration 300 --token <access-token>
//
// Each virtual user opens the Executive Summary, pauses, opens the VAS tab, pauses, changes a filter, and repeats.
// Run it against the release container and read CPU and memory from App Service metrics (or `docker stats` locally)
// for the same window. --token is an Entra access token for the API; leave it out against a Development build.
//
// The API limits each signed-in user to 120 requests a minute. One token means every virtual user shares that
// allowance, so for the test window either raise it (app setting RateLimiting__PermitsPerMinute=6000) or expect
// HTTP 429s, which are reported separately from real errors.

const args = Object.fromEntries(process.argv.slice(2).join(' ').split('--').filter(Boolean).map((a) => {
  const [k, ...v] = a.trim().split(' ')
  return [k, v.join(' ') || 'true']
}))
const base = (args.url || 'http://localhost:5080').replace(/\/$/, '')
const users = Number(args.users || 10)
const durationMs = Number(args.duration || 60) * 1000
const think = (args.think || '2-6').split('-').map(Number) // seconds between page views
const headers = { Accept: 'application/json', ...(args.token ? { Authorization: `Bearer ${args.token}` } : {}) }

const stats = new Map() // name -> { ms: [], errors: 0, limited: 0 }
const failures = new Map()
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))
const pick = (list) => list[Math.floor(Math.random() * list.length)]

async function call(name, path, query = {}) {
  const qs = new URLSearchParams(Object.entries(query).filter(([, v]) => v != null)).toString()
  const entry = stats.get(name) ?? stats.set(name, { ms: [], errors: 0, limited: 0 }).get(name)
  const started = performance.now()
  try {
    const res = await fetch(`${base}${path}${qs ? `?${qs}` : ''}`, { headers })
    const body = await res.text()
    if (res.status === 429) { entry.limited++; return null }
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    entry.ms.push(performance.now() - started)
    return JSON.parse(body)
  } catch (err) {
    entry.errors++
    failures.set(`${name}: ${err.message}`, (failures.get(`${name}: ${err.message}`) ?? 0) + 1)
    return null
  }
}

const VAS = [
  ['outcomes', '/api/v1/vas/outcomes'], ['volume', '/api/v1/vas/volume'], ['stages', '/api/v1/vas/stages'],
  ['closure', '/api/v1/vas/closure'], ['revenue', '/api/v1/vas/revenue'], ['visa-types', '/api/v1/vas/visa-types'],
  ['centres', '/api/v1/vas/centres'],
]

async function user(deadline) {
  const pause = () => sleep((think[0] + Math.random() * (think[1] - think[0])) * 1000)
  await sleep(Math.random() * 2000) // executives do not all arrive in the same instant
  await call('client-config', '/api/client-config')
  const options = await call('filters', '/api/v1/filters')
  let views = 0
  while (Date.now() < deadline) {
    // Most views are unfiltered; some narrow by country and visa type, as an executive would.
    const country = options && Math.random() < 0.4 ? pick(options.countries) : null
    const filter = {
      country: country?.country,
      centre: country && Math.random() < 0.3 ? pick(country.centres) : null,
      visaType: options && Math.random() < 0.25 ? pick(options.visaTypes) : null,
    }
    await call('executive-summary', '/api/v1/executive-summary', filter)
    views++
    await pause()
    if (Date.now() >= deadline) break
    // The VAS tab fires its requests together, like the browser does.
    await Promise.all([
      ...VAS.map(([name, path]) => call(name, path, filter)),
      call('turnaround', '/api/v1/vas/turnaround', { ...filter, journey: 'digital' }),
      call('turnaround', '/api/v1/vas/turnaround', { ...filter, journey: 'centre' }),
    ])
    views++
    await pause()
  }
  return views
}

const pct = (sorted, p) => sorted[Math.min(sorted.length - 1, Math.floor((p / 100) * sorted.length))]

console.log(`Target ${base} · ${users} users · ${durationMs / 1000}s · think time ${think[0]}-${think[1]}s`)
const started = Date.now()
const views = (await Promise.all(Array.from({ length: users }, () => user(started + durationMs)))).reduce((a, b) => a + b, 0)
const seconds = (Date.now() - started) / 1000

let requests = 0, errors = 0, limited = 0
const rows = [...stats].map(([name, s]) => {
  const sorted = [...s.ms].sort((a, b) => a - b)
  requests += sorted.length + s.errors + s.limited
  errors += s.errors
  limited += s.limited
  return {
    endpoint: name, ok: sorted.length, errors: s.errors, 'rate limited': s.limited,
    'p50 ms': Math.round(pct(sorted, 50) ?? 0), 'p95 ms': Math.round(pct(sorted, 95) ?? 0), 'max ms': Math.round(sorted.at(-1) ?? 0),
  }
})
console.table(rows)
console.log(`${requests} requests in ${seconds.toFixed(0)}s (${(requests / seconds).toFixed(1)}/s) · ${views} page views · ${errors} errors · ${limited} rate limited (429)`)
if (limited) console.log('Rate limiting was hit: raise RateLimiting__PermitsPerMinute for the test window, then run again.')
for (const [what, count] of failures) console.log(`  ${count} x ${what}`)
process.exitCode = errors || limited ? 1 : 0
