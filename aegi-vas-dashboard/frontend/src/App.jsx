import { useCallback, useEffect, useMemo, useState } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import Header, { periodPresets } from './components/layout/Header'
import Tabs, { TABS } from './components/layout/Tabs'
import QueryState from './components/common/QueryState'
import ExecutiveSummary from './pages/ExecutiveSummary'
import Vas from './pages/Vas'
import ComingSoon from './pages/ComingSoon'
import { useFilterOptions } from './hooks/useKpi'
import { pageVariants, reducedMotion } from './theme/motion'

const readTab = () => {
  const h = window.location.hash.slice(1)
  return TABS.some((t) => t.id === h) ? h : 'exec'
}

export default function App() {
  const [tab, setTab] = useState(readTab)
  const [meta, setMeta] = useState(null)
  const optionsQuery = useFilterOptions()
  const options = optionsQuery.data
  const presets = useMemo(() => periodPresets(options?.minMonth, options?.maxMonth), [options?.minMonth, options?.maxMonth])
  const [filters, setFilters] = useState(null)

  useEffect(() => {
    if (!filters && presets.length) setFilters({ presetId: 'ytd', from: presets[0].from, to: presets[0].to, country: null, centre: null, visaType: null })
  }, [presets, filters])
  useEffect(() => {
    if (window.location.hash.slice(1) !== tab) history.replaceState(null, '', `#${tab}`)
  }, [tab])

  const onMeta = useCallback((m) => setMeta(m), [])
  const update = (patch) => setFilters((f) => ({ ...f, ...patch }))

  let page
  if (!filters) page = <QueryState query={optionsQuery} height={420} isEmpty={(o) => !o.maxMonth}>{() => null}</QueryState>
  else if (tab === 'exec') page = <ExecutiveSummary filters={filters} onMeta={onMeta} />
  else if (tab === 'vas') page = <Vas filters={filters} onMeta={onMeta} />
  else page = <ComingSoon tab={tab} />

  return (
    <>
      <Header options={options} presets={presets} filters={filters ?? {}} onChange={update} meta={meta}>
        <Tabs active={tab} onChange={setTab} />
      </Header>
      <main className="wrap">
        <div className="stage" id="tabpanel" role="tabpanel" aria-labelledby={`tab-${tab}`}>
          <AnimatePresence mode="wait">
            <motion.div key={tab} className="view" variants={pageVariants}
              initial={reducedMotion ? false : 'initial'} animate="animate" exit={reducedMotion ? undefined : 'exit'}>
              {page}
            </motion.div>
          </AnimatePresence>
        </div>
        <footer>
          {meta && <>Figures in {meta.currency} · dates in {meta.timeZone}{meta.freshness.source === 'Sample data' ? ' · sample data for development, not AEGI results' : ''}</>}
        </footer>
      </main>
    </>
  )
}
