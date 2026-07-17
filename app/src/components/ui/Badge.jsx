import clsx from 'clsx'

const variants = {
  blue:   'bg-blue-500/15 text-blue-400 border border-blue-500/20',
  green:  'bg-green-500/15 text-green-400 border border-green-500/20',
  red:    'bg-red-500/15 text-red-400 border border-red-500/20',
  orange: 'bg-orange-500/15 text-orange-400 border border-orange-500/20',
  purple: 'bg-purple-500/15 text-purple-400 border border-purple-500/20',
  yellow: 'bg-yellow-500/15 text-yellow-400 border border-yellow-500/20',
  gray:   'bg-[#2e3350] text-[#94a3b8] border border-[#3d4466]',
}

export default function Badge({ children, variant = 'gray', className = '' }) {
  return (
    <span className={clsx('inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium', variants[variant], className)}>
      {children}
    </span>
  )
}
