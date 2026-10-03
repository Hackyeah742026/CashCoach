import { Navigate, Route, Routes } from 'react-router'
import { AppShell } from './layout/AppShell'
import { RequireOnboarded } from './layout/RequireOnboarded'
import { Chat } from './pages/Chat'
import { Goals } from './pages/Goals'
import { Home } from './pages/Home'
import { Onboarding } from './pages/Onboarding'
import { Settings } from './pages/Settings'
import { WrappedPage } from './pages/Wrapped'
import { WrappedStory } from './pages/WrappedStory'

export default function App() {
  return (
    <Routes>
      <Route path="/onboarding" element={<Onboarding />} />
      <Route element={<RequireOnboarded />}>
        {/* Full-screen story, outside the tab shell */}
        <Route path="/wrapped/:month" element={<WrappedStory />} />
        <Route element={<AppShell />}>
          <Route index element={<Home />} />
          <Route path="wrapped" element={<WrappedPage />} />
          <Route path="chat" element={<Chat />} />
          <Route path="goals" element={<Goals />} />
          <Route path="settings" element={<Settings />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
