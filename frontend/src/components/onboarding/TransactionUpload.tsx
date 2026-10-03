import clsx from 'clsx'
import { FileUp } from 'lucide-react'
import { useId, useRef, useState, type DragEvent } from 'react'
import { useT } from '../../i18n/context'

interface TransactionUploadProps {
  file: File | null
  onFile: (file: File | null) => void
  error?: string | null
}

/** Drag & drop + file picker for a bank CSV export. */
export function TransactionUpload({ file, onFile, error }: TransactionUploadProps) {
  const t = useT()
  const inputId = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)

  function onDrop(e: DragEvent) {
    e.preventDefault()
    setDragging(false)
    onFile(e.dataTransfer.files[0] ?? null)
  }

  return (
    <div
      className={clsx('dropzone', dragging && 'dropzone--active')}
      onDragOver={(e) => {
        e.preventDefault()
        setDragging(true)
      }}
      onDragLeave={() => setDragging(false)}
      onDrop={onDrop}
    >
      <span className="dropzone__icon" aria-hidden>
        <FileUp />
      </span>
      <strong>{t.onboarding.dropHere}</strong>
      <span className="text-sm text-muted">{t.onboarding.or}</span>
      <input
        ref={inputRef}
        id={inputId}
        type="file"
        accept=".csv,text/csv"
        className="visually-hidden"
        onChange={(e) => onFile(e.target.files?.[0] ?? null)}
      />
      <button type="button" className="btn btn--secondary" onClick={() => inputRef.current?.click()}>
        {t.onboarding.chooseFile}
      </button>
      {file && <span className="text-sm">{t.onboarding.selected(file.name)}</span>}
      {error && (
        <span className="field__error" role="alert">
          {error}
        </span>
      )}
    </div>
  )
}
