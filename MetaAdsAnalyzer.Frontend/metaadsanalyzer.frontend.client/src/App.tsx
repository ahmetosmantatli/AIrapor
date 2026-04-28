import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { PublicAuthLayout } from './components/PublicAuthLayout'
import { RequireAuth } from './components/RequireAuth'
import { UserProvider } from './context/UserContext'
import { Analysis } from './pages/Analysis'
import { Accounts } from './pages/Accounts'
import { CampaignMaps } from './pages/CampaignMaps'
import { ConnectMeta } from './pages/ConnectMeta'
import { Dashboard } from './pages/Dashboard'
import { LandingPage } from './pages/LandingPage'
import { Login } from './pages/Login'
import { ImpactTracking } from './pages/ImpactTracking'
import { ProductsSetup } from './pages/ProductsSetup'
import { Register } from './pages/Register'
import { Settings } from './pages/Settings'
import { Tools } from './pages/Tools'
import { VideoReport } from './pages/VideoReport'

export default function App() {
  return (
    <UserProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<LandingPage />} />

          <Route element={<PublicAuthLayout />}>
            <Route path="/login" element={<Login />} />
            <Route path="/register" element={<Register />} />
            <Route path="/connect" element={<ConnectMeta />} />
          </Route>

          <Route element={<RequireAuth />}>
            <Route path="/app" element={<Layout />}>
              <Route index element={<Navigate to="/app/accounts" replace />} />
              <Route path="accounts" element={<Accounts />} />
              <Route path="dashboard" element={<Dashboard />} />
              <Route path="analysis" element={<Analysis />} />
              <Route path="video-report" element={<VideoReport />} />
              <Route path="analyzed-ads" element={<Navigate to="/app/accounts" replace />} />
              <Route path="impact-tracking" element={<ImpactTracking />} />
              <Route path="creatives" element={<Navigate to="/app/accounts" replace />} />
              <Route path="products" element={<ProductsSetup />} />
              <Route path="campaigns" element={<CampaignMaps />} />
              <Route path="settings" element={<Settings />} />
              <Route path="tools" element={<Tools />} />
            </Route>
          </Route>

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </UserProvider>
  )
}
