import { useEffect, useState } from 'react'
import { MsalProvider, useIsAuthenticated, useMsal } from '@azure/msal-react'
import { InteractionStatus } from '@azure/msal-browser'
import { auth, initAuth } from './msalConfig'

function SignInGate({ children }) {
  const { instance, inProgress } = useMsal()
  const signedIn = useIsAuthenticated()
  if (signedIn) return children
  if (inProgress !== InteractionStatus.None) return <div className="center-screen">Signing you in…</div>
  return (
    <div className="center-screen">
      <div className="signin">
        <div className="mark">VAS</div>
        <h1>AEGI Executive Dashboard</h1>
        <p>Sign in with your organisation account to view the dashboard.</p>
        <button className="btn" onClick={() => instance.loginRedirect({ scopes: auth.scopes })}>Sign in with Microsoft</button>
      </div>
    </div>
  )
}

export default function AuthProvider({ children }) {
  const [state, setState] = useState({ ready: false, error: null })

  useEffect(() => {
    initAuth().then(() => setState({ ready: true, error: null }), (e) => setState({ ready: false, error: e.message }))
  }, [])

  if (state.error) return <div className="center-screen"><div className="error" role="alert">{state.error} Refresh the page to try again.</div></div>
  if (!state.ready) return <div className="center-screen">Loading…</div>
  if (!auth.enabled) return children
  return (
    <MsalProvider instance={auth.instance}>
      <SignInGate>{children}</SignInGate>
    </MsalProvider>
  )
}
