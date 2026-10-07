import { InteractionRequiredAuthError } from '@azure/msal-browser'
import { auth } from '../auth/msalConfig'

export class ApiError extends Error {
  constructor(status, message, correlationId) {
    super(message)
    this.status = status
    this.correlationId = correlationId
  }
}

// The executive's API access token. It is only ever sent to this API, never to Fabric.
async function authHeader() {
  if (!auth.enabled) return {}
  const account = auth.instance.getActiveAccount()
  try {
    const { accessToken } = await auth.instance.acquireTokenSilent({ scopes: auth.scopes, account })
    return { Authorization: `Bearer ${accessToken}` }
  } catch (err) {
    if (err instanceof InteractionRequiredAuthError) {
      await auth.instance.acquireTokenRedirect({ scopes: auth.scopes, account })
    }
    throw err
  }
}

export async function apiGet(path, params = {}, signal) {
  const qs = new URLSearchParams(Object.entries(params).filter(([, v]) => v != null && v !== '')).toString()
  const res = await fetch(`${path}${qs ? `?${qs}` : ''}`, {
    headers: { Accept: 'application/json', ...(await authHeader()) },
    signal,
  })
  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    const detail = body.errors ? Object.values(body.errors).flat().join(' ') : body.title
    throw new ApiError(res.status, detail || `Request failed (${res.status}).`,
      body.correlationId || res.headers.get('X-Correlation-ID'))
  }
  return res.json()
}
