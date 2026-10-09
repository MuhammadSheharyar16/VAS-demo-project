// Display formatting only. KPI values, percentages, averages and comparisons all come from the API.
export const fmtInt = (n) => Math.round(n).toLocaleString('en-US')
export const fmtNum = (n, d = 1) => Number(n).toLocaleString('en-US', { minimumFractionDigits: d, maximumFractionDigits: d })

export const fmtMoney = (amount, currency = 'USD') => {
  const sym = currency === 'USD' ? '$' : `${currency} `
  return amount >= 1e6 ? `${sym}${fmtNum(amount / 1e6, 2)}M` : amount >= 1e3 ? `${sym}${fmtNum(amount / 1e3, 1)}K` : `${sym}${fmtInt(amount)}`
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
export const monthLabel = (ym) => MONTHS[Number(ym.slice(5, 7)) - 1]
export const monthYear = (ym) => `${monthLabel(ym)} ${ym.slice(0, 4)}`
export const periodLabel = ({ from, to }) =>
  from === to ? monthYear(from) : from.slice(0, 4) === to.slice(0, 4) ? `${monthLabel(from)} – ${monthYear(to)}` : `${monthYear(from)} – ${monthYear(to)}`

// Month arithmetic on yyyy-MM strings, used only to build the period pickers.
export const addMonths = (ym, n) => {
  const total = Number(ym.slice(0, 4)) * 12 + Number(ym.slice(5, 7)) - 1 + n
  return `${Math.floor(total / 12)}-${String((total % 12) + 1).padStart(2, '0')}`
}
export const monthRange = (from, to) => {
  const out = []
  for (let m = from; m <= to && out.length < 240; m = addMonths(m, 1)) out.push(m)
  return out
}

// How a KPI moved since the comparison period, in the unit the API names: percent, points or days.
export const fmtChange = (change, unit) => {
  const sign = change > 0 ? '+' : change < 0 ? '−' : ''
  const size = fmtNum(Math.abs(change))
  return unit === 'points' ? `${sign}${size} pts` : unit === 'days' ? `${sign}${size} days` : `${sign}${size}%`
}

export const fmtDateTime = (iso) =>
  new Date(iso).toLocaleString('en-GB', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit', timeZone: 'UTC', timeZoneName: 'short' })
