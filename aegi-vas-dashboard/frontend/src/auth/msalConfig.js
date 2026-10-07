import { PublicClientApplication } from '@azure/msal-browser'

// Sign-in settings come from the API (/api/client-config) so one build works in Dev, UAT and Production.
// They are public identifiers only. MSAL uses the authorization code flow with PKCE.
export const auth = { enabled: false, scopes: [], instance: null }

export async function initAuth() {
  const res = await fetch('/api/client-config', { headers: { Accept: 'application/json' } })
  if (!res.ok) throw new Error(`Couldn't load sign-in settings (${res.status}).`)
  const cfg = await res.json()
  auth.enabled = cfg.authEnabled
  auth.dataSource = cfg.dataSource
  if (!cfg.authEnabled) return auth

  auth.scopes = [cfg.apiScope]
  auth.instance = new PublicClientApplication({
    auth: {
      clientId: cfg.clientId,
      authority: `https://login.microsoftonline.com/${cfg.tenantId}`,
      redirectUri: window.location.origin,
      postLogoutRedirectUri: window.location.origin,
    },
    // sessionStorage: tokens are cleared when the tab closes.
    cache: { cacheLocation: 'sessionStorage' },
  })
  await auth.instance.initialize()
  const result = await auth.instance.handleRedirectPromise()
  const account = result?.account || auth.instance.getAllAccounts()[0]
  if (account) auth.instance.setActiveAccount(account)
  return auth
}
