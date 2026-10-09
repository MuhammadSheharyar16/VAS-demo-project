import { apiGet } from './client'

// Every reporting call returns { data, meta }. meta = applied filters, currency, freshness, correlationId.
const q = (f) => ({ from: f.from, to: f.to, country: f.country, centre: f.centre, visaType: f.visaType })

export const kpiApi = {
  filters: (_, signal) => apiGet('/api/v1/filters', {}, signal),
  summary: (f, signal) => apiGet('/api/v1/executive-summary', q(f), signal),
  volume: (f, signal) => apiGet('/api/v1/vas/volume', q(f), signal),
  outcomes: (f, signal) => apiGet('/api/v1/vas/outcomes', q(f), signal),
  closure: (f, signal) => apiGet('/api/v1/vas/closure', q(f), signal),
  stages: (f, signal) => apiGet('/api/v1/vas/stages', q(f), signal),
  turnaroundDigital: (f, signal) => apiGet('/api/v1/vas/turnaround', { ...q(f), journey: 'digital' }, signal),
  turnaroundCentre: (f, signal) => apiGet('/api/v1/vas/turnaround', { ...q(f), journey: 'centre' }, signal),
  revenue: (f, signal) => apiGet('/api/v1/vas/revenue', q(f), signal),
  visaTypes: (f, signal) => apiGet('/api/v1/vas/visa-types', q(f), signal),
  centres: (f, signal) => apiGet('/api/v1/vas/centres', q(f), signal),
}
