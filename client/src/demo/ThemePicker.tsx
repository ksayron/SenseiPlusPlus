import { Check, Palette } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import { themes, type ThemeId } from './themes'

export function ThemePicker({ themeId, onChange }: { themeId: ThemeId; onChange: (id: ThemeId) => void }) {
  const current = themes.find((theme) => theme.id === themeId) ?? themes[0]
  return <Dialog>
    <DialogTrigger render={<Button variant="outline" className="demo-theme-trigger" aria-label={`Choose color theme. Current: ${current.name}`} />}>
      <Palette size={16} /><span>{current.name}</span>
    </DialogTrigger>
    <DialogContent className="demo-theme-dialog"><DialogHeader>
      <p className="demo-eyebrow">A different atmosphere</p>
      <DialogTitle>Find your campus colors.</DialogTitle>
      <DialogDescription>Three light palettes. Three dark palettes. Preview them across the studio and gallery.</DialogDescription>
    </DialogHeader>
      {[false, true].map((dark) => <section key={String(dark)} className="demo-theme-group" aria-label={dark ? 'Dark themes' : 'Light themes'}>
        <h3>{dark ? 'Dark' : 'Light'}<span>3 palettes</span></h3>
        <div className="demo-theme-options">{themes.filter((theme) => theme.dark === dark).map((theme) => <Button key={theme.id} variant="outline" className="demo-theme-option" aria-pressed={themeId === theme.id} onClick={() => onChange(theme.id)}>
        <span className="demo-theme-preview" aria-hidden="true" style={{ background: theme.tokens.background }}>
          <span style={{ background: theme.tokens.sidebar }} />
          <span style={{ background: theme.tokens.card }}><i style={{ background: theme.tokens.foreground }} /><i style={{ background: theme.tokens.border }} /><i style={{ background: theme.tokens.primary }} /></span>
        </span>
        <span className="demo-theme-option-label"><strong>{theme.name}</strong>{themeId === theme.id && <Check size={15} />}</span>
        <span className="demo-theme-description">{theme.description}</span>
      </Button>)}</div></section>)}
    </DialogContent>
  </Dialog>
}
