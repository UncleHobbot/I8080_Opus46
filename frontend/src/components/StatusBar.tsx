export default function StatusBar() {
  return (
    <footer style={{
      padding: '4px 16px',
      backgroundColor: '#111',
      borderTop: '1px solid #333',
      fontSize: '12px',
      color: '#888',
      display: 'flex',
      justifyContent: 'space-between',
      flexShrink: 0,
    }}>
      <span>HELP - System help | ED - Editor | ASM - Assembler | MBASIC - BASIC | DIR - Files</span>
      <span>64K RAM | Drive A:</span>
    </footer>
  )
}
