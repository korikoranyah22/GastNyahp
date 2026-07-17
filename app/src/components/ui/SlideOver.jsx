import { X } from 'lucide-react'

export default function SlideOver({ open, onClose, title, children, width = 'max-w-md' }) {
  if (!open) return null
  return (
    <div className="fixed inset-0 z-50 flex">
      {/* Backdrop */}
      <div className="fixed inset-0 bg-black/60 fade-in" onClick={onClose} />
      {/* Panel */}
      <div className={`relative ml-auto h-full ${width} w-full bg-[#151820] border-l border-[#2e3350] shadow-2xl flex flex-col slide-in`}>
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-[#2e3350]">
          <h2 className="text-base font-semibold text-white">{title}</h2>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-[#94a3b8] hover:text-white hover:bg-[#2e3350] transition-colors"
          >
            <X size={16} />
          </button>
        </div>
        {/* Content */}
        <div className="flex-1 overflow-y-auto px-6 py-5">
          {children}
        </div>
      </div>
    </div>
  )
}
