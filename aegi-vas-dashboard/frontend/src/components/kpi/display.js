import { fmtInt, fmtMoney, fmtNum } from '../../utils/format'

// Turns a KPI's unit (count, percent, days, currency) into KpiCard props. Formatting only.
export function display(value, unit, currency) {
  if (value == null) return { value: null }
  switch (unit) {
    case 'count': return { value, format: fmtInt }
    case 'percent': return { value, format: (v) => fmtNum(v), unit: '%' }
    case 'days': return { value, format: (v) => fmtNum(v), unit: 'days' }
    case 'currency': return { value, format: (v) => fmtMoney(v, currency) }
    default: return { value, format: (v) => fmtNum(v) }
  }
}
