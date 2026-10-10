import { useState } from 'react'

export const narratorGuideLimit = 30_000
export const narratorGuideUploadByteLimit = 120_000
export const narratorGuideHelp = 'Paste the full adventure: hidden truth, NPC motivations, connected clues, scene guidance and possible endings. This is sent to the AI narrator, not shown directly to players. It does not override safety or game rules.'

interface NarratorGuideTextEditorProps {
  value: string
  onChange: (value: string) => void
  disabled?: boolean
}

export function NarratorGuideTextEditor({ value, onChange, disabled = false }: NarratorGuideTextEditorProps) {
  const [fileError, setFileError] = useState('')

  const importFile = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    event.target.value = ''
    setFileError('')
    if (!file) return
    if (!/\.(md|txt)$/i.test(file.name)) {
      setFileError('Choose a UTF-8 .md or .txt file. Other document types are not supported.')
      return
    }
    if (file.size > narratorGuideUploadByteLimit) {
      setFileError(`The file is too large. Uploads are limited to ${narratorGuideUploadByteLimit.toLocaleString()} bytes.`)
      return
    }

    let imported: string
    try {
      imported = new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode(await file.arrayBuffer())
    } catch {
      setFileError('The file is not valid UTF-8 text; the editor was not changed.')
      return
    }
    if (imported.length > narratorGuideLimit) {
      setFileError(`The guide is too long. The limit is ${narratorGuideLimit.toLocaleString()} characters; the editor was not changed.`)
      return
    }
    onChange(imported)
  }

  return (
    <div className="narrator-guide-editor">
      <p>{narratorGuideHelp}</p>
      <p className="narrator-guide-privacy">
        Private means hidden from players, not hidden from the configured AI provider. Hosted providers receive this guide.
      </p>
      <label htmlFor="narrator-guide-text">Full narrator guide (private)</label>
      <textarea
        id="narrator-guide-text"
        rows={18}
        value={value}
        disabled={disabled}
        aria-describedby="narrator-guide-count narrator-guide-help"
        onChange={(event) => onChange(event.target.value)}
      />
      <span id="narrator-guide-count" className="narrator-guide-count" aria-live="polite">
        {value.length.toLocaleString()} / {narratorGuideLimit.toLocaleString()} characters
      </span>
      <p id="narrator-guide-help" className="field-hint">
        Long guides are resent on every story turn and increase AI token usage; 30,000 characters is not a fixed token count.
      </p>
      <label className="narrator-guide-file">
        Import UTF-8 .md or .txt
        <input type="file" accept=".md,.txt,text/plain,text/markdown" disabled={disabled} onChange={(event) => void importFile(event)} />
      </label>
      {fileError && <p className="alert" role="alert">{fileError}</p>}
    </div>
  )
}
