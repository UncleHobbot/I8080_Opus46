import { useState } from 'react'
import Terminal from './components/Terminal'
import StatusBar from './components/StatusBar'

function App() {
  const [connected, setConnected] = useState(false)

  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      height: '100vh',
      backgroundColor: '#000',
      color: '#0f0',
      fontFamily: 'monospace',
    }}>
      <header style={{
        padding: '8px 16px',
        backgroundColor: '#111',
        borderBottom: '1px solid #333',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexShrink: 0,
      }}>
        <span style={{ fontSize: '14px', color: '#0f0' }}>
          Intel 8080 CP/M 2.2 Emulator
        </span>
        <span style={{
          fontSize: '12px',
          color: connected ? '#0f0' : '#f00',
        }}>
          {connected ? '● Connected' : '○ Disconnected'}
        </span>
      </header>
      <div style={{ flex: 1, overflow: 'hidden' }}>
        <Terminal onConnectionChange={setConnected} />
      </div>
      <StatusBar />
    </div>
  )
}

export default App
