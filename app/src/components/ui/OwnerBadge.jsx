export const SHARED_OWNER = { id: 'shared', name: 'Compartido', color: '#64748b', emoji: '🤝' }

export default function OwnerBadge({ person, size = 'sm' }) {
  if (!person) return null
  const dim = size === 'xs' ? 'w-4 h-4 text-[9px]' : 'w-5 h-5 text-[10px]'
  const isShared = person.id === 'shared'
  return (
    <span
      className={`inline-flex items-center justify-center ${dim} rounded-full font-bold shrink-0`}
      style={{
        backgroundColor: `${person.color}33`,
        color: person.color,
        border: `1px solid ${person.color}66`,
      }}
      title={person.name}
    >
      {isShared ? '🤝' : person.name.charAt(0).toUpperCase()}
    </span>
  )
}
