const COPY = {
  ops: ['Operations', 'Operational reporting will appear here in a later phase.'],
  fin: ['Finance', 'Financial reporting will appear here in a later phase.'],
  alfit: ['Alfit', "Reporting for AEGI's insurance business will appear here in a later phase."],
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
