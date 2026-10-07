const COPY = {
  ops: ['Operations', 'Centre capacity, queue times and staffing views will appear here once operational data is onboarded.'],
  fin: ['Finance', 'Cost, margin and billing reporting is planned for a later phase.'],
  alfit: ['Alfit', 'Alfit reporting will join the dashboard when its data source is brought into scope.'],
}

export default function ComingSoon({ tab }) {
  const [title, text] = COPY[tab]
  return (
    <div className="soonv">
      <div>
        <div className="orb" />
        <h2>{title}</h2>
        <p>{text}</p>
        <p style={{ marginTop: 14 }}><span className="chip">Coming soon</span></p>
      </div>
    </div>
  )
}
