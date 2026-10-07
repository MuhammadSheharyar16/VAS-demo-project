import { useQuery, keepPreviousData } from '@tanstack/react-query'
import { kpiApi } from '../api/kpis'

// Changing a filter changes the query key, which repeats the reporting request (roadmap step 9).
export function useKpi(name, filters) {
  return useQuery({
    queryKey: [name, filters.from, filters.to, filters.country ?? '', filters.centre ?? ''],
    queryFn: ({ signal }) => kpiApi[name](filters, signal),
    placeholderData: keepPreviousData,
  })
}

export function useFilterOptions() {
  return useQuery({ queryKey: ['filters'], queryFn: ({ signal }) => kpiApi.filters(null, signal), staleTime: 15 * 60_000 })
}
