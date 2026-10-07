// Display formatting only. KPI values, percentages and averages all come from the API.
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

export const fmtDateTime = (iso) =>
  new Date(iso).toLocaleString('en-GB', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit', timeZone: 'UTC', timeZoneName: 'short' })
