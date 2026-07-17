import { useState } from 'react'
import { Eye, EyeOff } from 'lucide-react'

// Campo de contraseña con ojito. Poder ver lo que escribís baja los errores de tipeo mucho más de lo que sube el
// riesgo de que alguien te mire la pantalla — y es lo que evita el clásico "no me entra y no sé por qué".
export default function PasswordField({ label, value, onChange, placeholder, hint, autoFocus, autoComplete }) {
  const [visible, setVisible] = useState(false)
  return (
    <div>
      <label className="block text-xs text-[#94a3b8] mb-1.5">{label}</label>
      <div className="relative">
        <input
          type={visible ? 'text' : 'password'}
          className="w-full pr-9 px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30 placeholder:text-[#3d4466]"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          autoFocus={autoFocus}
          autoComplete={autoComplete}
        />
        <button
          type="button"
          onClick={() => setVisible((v) => !v)}
          className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-[#64748b] hover:text-[#94a3b8] transition-colors"
          title={visible ? 'Ocultar' : 'Mostrar'}
        >
          {visible ? <EyeOff size={14} /> : <Eye size={14} />}
        </button>
      </div>
      {hint && <p className="mt-1.5 text-[11px] text-[#64748b]">{hint}</p>}
    </div>
  )
}
