import { useEffect, useRef } from 'react'
import { Terminal as XTerm } from '@xterm/xterm'
import { FitAddon } from '@xterm/addon-fit'
import { WebLinksAddon } from '@xterm/addon-web-links'
import '@xterm/xterm/css/xterm.css'
import * as signalR from '@microsoft/signalr'

interface TerminalProps {
  onConnectionChange: (connected: boolean) => void
}

export default function Terminal({ onConnectionChange }: TerminalProps) {
  const termRef = useRef<HTMLDivElement>(null)
  const xtermRef = useRef<XTerm | null>(null)
  const connectionRef = useRef<signalR.HubConnection | null>(null)

  useEffect(() => {
    if (!termRef.current) return

    const term = new XTerm({
      theme: {
        background: '#000000',
        foreground: '#00ff00',
        cursor: '#00ff00',
        cursorAccent: '#000000',
        selectionBackground: '#00ff0040',
        black: '#000000',
        red: '#ff0000',
        green: '#00ff00',
        yellow: '#ffff00',
        blue: '#0080ff',
        magenta: '#ff00ff',
        cyan: '#00ffff',
        white: '#ffffff',
        brightBlack: '#808080',
        brightRed: '#ff4040',
        brightGreen: '#40ff40',
        brightYellow: '#ffff40',
        brightBlue: '#4080ff',
        brightMagenta: '#ff40ff',
        brightCyan: '#40ffff',
        brightWhite: '#ffffff',
      },
      fontFamily: '"Courier New", "Lucida Console", monospace',
      fontSize: 16,
      cursorBlink: true,
      cursorStyle: 'block',
      scrollback: 1000,
      convertEol: false,
      disableStdin: false,
    })

    const fitAddon = new FitAddon()
    term.loadAddon(fitAddon)
    term.loadAddon(new WebLinksAddon())

    term.open(termRef.current)

    // Initial fit
    setTimeout(() => fitAddon.fit(), 100)

    // Handle resize
    const resizeObserver = new ResizeObserver(() => {
      setTimeout(() => fitAddon.fit(), 50)
    })
    resizeObserver.observe(termRef.current)

    xtermRef.current = term

    // Connect to SignalR
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/terminal')
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connectionRef.current = connection

    connection.on('output', (text: string) => {
      term.write(text)
    })

    connection.onreconnecting(() => {
      onConnectionChange(false)
      term.write('\r\n[Reconnecting...]\r\n')
    })

    connection.onreconnected(() => {
      onConnectionChange(true)
      term.write('\r\n[Reconnected]\r\n')
    })

    connection.onclose(() => {
      onConnectionChange(false)
    })

    connection.start()
      .then(() => {
        onConnectionChange(true)
        term.write('Connected to Intel 8080 CP/M emulator.\r\n')
        term.write('Initializing system...\r\n\r\n')
      })
      .catch(err => {
        term.write(`Connection failed: ${err.message}\r\n`)
        term.write('Make sure the backend is running on http://localhost:5026\r\n')
        onConnectionChange(false)
      })

    // Handle input
    term.onData((data) => {
      if (connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke('Input', data).catch(() => {})
      }
    })

    return () => {
      resizeObserver.disconnect()
      connection.stop()
      term.dispose()
    }
  }, [])

  return (
    <div
      ref={termRef}
      style={{
        width: '100%',
        height: '100%',
        padding: '4px',
      }}
    />
  )
}
