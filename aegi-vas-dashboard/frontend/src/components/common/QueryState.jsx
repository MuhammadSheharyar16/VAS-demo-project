// Loading, error and empty states for one reporting request (roadmap step 9).
export default function QueryState({ query, height = 220, isEmpty, children }) {
  if (query.isPending) return <div className="skeleton" style={{ height }} aria-label="Loading" />
  if (query.isError) {
    const { status, correlationId, message } = query.error ?? {}
    const msg = status === 401 ? 'Your session has expired. Refresh the page to sign in again.'
      : status === 403 ? "Your account doesn't have access to this dashboard."
      : status === 503 ? 'Reporting data is temporarily unavailable.'
      : message || "Couldn't load this data."
    return (
      <div className="error" role="alert">
        <span>{msg}{correlationId && <small className="ref"> Reference: {correlationId}</small>}</span>
        {status !== 403 && status !== 400 && <button className="btn ghost" onClick={() => query.refetch()}>Try again</button>}
      </div>
    )
  }
  // Reporting endpoints return { data, meta }; /filters returns its payload directly.
  const enveloped = query.data && 'meta' in query.data
  const data = enveloped ? query.data.data : query.data
  const meta = enveloped ? query.data.meta : undefined
  if (isEmpty?.(data)) return <div className="empty" style={{ minHeight: Math.min(height, 160) }}>No data for the selected filters.</div>
  return children(data, meta)
}
