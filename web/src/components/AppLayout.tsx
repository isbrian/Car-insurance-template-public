import { Layout } from 'antd'
import { useEffect, useRef, type ReactNode } from 'react'
import { useLocation } from 'react-router-dom'
import PageHeader from './PageHeader'
import SideNavigation from './SideNavigation'

type AppLayoutProps = {
  children: ReactNode
  selectedKey?: string
}

function focusMainContent() {
  const mainContent = document.getElementById('main-content')
  if (!(mainContent instanceof HTMLElement)) return

  mainContent.scrollIntoView({ block: 'start' })
  mainContent.focus({ preventScroll: true })
}

function AppLayout({ children, selectedKey = 'dashboard' }: AppLayoutProps) {
  const location = useLocation()
  const previousPathname = useRef(location.pathname)

  useEffect(() => {
    if (previousPathname.current === location.pathname) return
    previousPathname.current = location.pathname

    const frame = window.requestAnimationFrame(focusMainContent)
    return () => window.cancelAnimationFrame(frame)
  }, [location.pathname])

  return (
    <Layout className="app-layout">
      <a
        className="skip-link"
        href="#main-content"
        onClick={(event) => {
          event.preventDefault()
          focusMainContent()
        }}
      >
        跳至主要內容
      </a>
      <SideNavigation selectedKey={selectedKey} />
      <Layout>
        <PageHeader />
        <Layout.Content id="main-content" className="app-content" tabIndex={-1}>
          {children}
        </Layout.Content>
      </Layout>
    </Layout>
  )
}

export default AppLayout
