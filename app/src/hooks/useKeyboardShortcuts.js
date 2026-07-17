import { useEffect, useCallback } from 'react'
import useStore from '../store/useStore'
import { addMonthsToYM } from '../lib/dateUtils'

/**
 * Hook global de atajos de teclado. Montar una sola vez en Shell.jsx.
 *
 *   ←  → : mes anterior / siguiente
 *   Ctrl+S / ⌘+S : descargar backup JSON (los datos viven en el servidor)
 *
 * No dispara si el foco está en un input/textarea/select.
 */
export function useKeyboardShortcuts() {
  const currentMonth    = useStore((s) => s.currentMonth)
  const setCurrentMonth = useStore((s) => s.setCurrentMonth)
  const exportData      = useStore((s) => s.exportData)

  const prevMonth = useCallback(() => {
    setCurrentMonth(addMonthsToYM(currentMonth, -1))
  }, [currentMonth, setCurrentMonth])

  const nextMonth = useCallback(() => {
    setCurrentMonth(addMonthsToYM(currentMonth, 1))
  }, [currentMonth, setCurrentMonth])

  const handleSave = useCallback(() => {
    const json = exportData()
    const blob = new Blob([json], { type: 'application/json' })
    const url  = URL.createObjectURL(blob)
    const a    = document.createElement('a')
    a.href     = url
    a.download = `gastnyahp-${new Date().toISOString().slice(0, 10)}.json`
    a.click()
    URL.revokeObjectURL(url)
  }, [exportData])

  useEffect(() => {
    const handler = (e) => {
      const tag = e.target?.tagName
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return
      if (e.target?.isContentEditable) return

      if (e.key === 'ArrowLeft' && !e.metaKey && !e.ctrlKey && !e.altKey) {
        e.preventDefault()
        prevMonth()
      }
      if (e.key === 'ArrowRight' && !e.metaKey && !e.ctrlKey && !e.altKey) {
        e.preventDefault()
        nextMonth()
      }
      if ((e.ctrlKey || e.metaKey) && e.key === 's') {
        e.preventDefault()
        handleSave()
      }
    }

    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [prevMonth, nextMonth, handleSave])
}
