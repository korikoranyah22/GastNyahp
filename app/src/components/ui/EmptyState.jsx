export default function EmptyState({ icon: Icon, title, description, action }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4 text-center">
      {Icon && (
        <div className="w-14 h-14 rounded-2xl bg-[#1c2030] border border-[#2e3350] flex items-center justify-center mb-4">
          <Icon size={24} className="text-[#3d4466]" />
        </div>
      )}
      <h3 className="text-base font-semibold text-white mb-1">{title}</h3>
      {description && <p className="text-sm text-[#64748b] max-w-xs mb-4">{description}</p>}
      {action && action}
    </div>
  )
}
