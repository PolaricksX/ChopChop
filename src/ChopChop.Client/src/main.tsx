import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Auth0Provider } from '@auth0/auth0-react'
import './index.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Auth0Provider
      domain={import.meta.env.VITE_AUTH0_DOMAIN ?? 'dev-55ll2x7q7gclx8c2.ca.auth0.com'}
      clientId={import.meta.env.VITE_AUTH0_CLIENT_ID ?? '2WTazHwh53cl5B8JV7VeJu7uj414sJBk'}
      authorizationParams={{
        redirect_uri: `${window.location.origin}/`,
        scope: 'openid profile email',
      }}
    >
      <App />
    </Auth0Provider>
  </StrictMode>,
)
