import { useEffect, useRef, useState } from 'react'

interface SpeechResult {
  0: { transcript: string }
}

interface SpeechResultEvent {
  results: ArrayLike<SpeechResult>
}

interface SpeechErrorEvent {
  error: string
}

interface BrowserSpeechRecognition {
  lang: string
  interimResults: boolean
  continuous: boolean
  onresult: ((event: SpeechResultEvent) => void) | null
  onerror: ((event: SpeechErrorEvent) => void) | null
  onend: (() => void) | null
  start: () => void
  stop: () => void
  abort: () => void
}

type SpeechRecognitionConstructor = new () => BrowserSpeechRecognition
type SpeechWindow = Window & {
  SpeechRecognition?: SpeechRecognitionConstructor
  webkitSpeechRecognition?: SpeechRecognitionConstructor
}

function getSpeechRecognitionConstructor() {
  const speechWindow = window as SpeechWindow
  return speechWindow.SpeechRecognition ?? speechWindow.webkitSpeechRecognition
}

function recognitionErrorMessage(error: string) {
  if (error === 'not-allowed' || error === 'service-not-allowed') {
    return 'Microphone access was not allowed. Check your browser permission, or type your idea instead.'
  }
  if (error === 'no-speech') {
    return 'No speech was heard. Hold the microphone button while you speak, or type your idea.'
  }
  if (error === 'audio-capture') {
    return 'A microphone could not be found. You can type your idea instead.'
  }
  return 'Voice input did not work. You can type your idea instead.'
}

export function useVoiceInput(enabled: boolean, onTranscript: (transcript: string) => void) {
  const [listening, setListening] = useState(false)
  const [message, setMessage] = useState('')
  const recognitionRef = useRef<BrowserSpeechRecognition | null>(null)

  const stopListening = () => {
    const recognition = recognitionRef.current
    if (!recognition) return
    setListening(false)
    recognitionRef.current = null
    recognition.stop()
  }

  const abortListening = () => {
    const recognition = recognitionRef.current
    if (!recognition) return
    recognitionRef.current = null
    recognition.onresult = null
    recognition.onerror = null
    recognition.onend = null
    setListening(false)
    recognition.abort()
  }

  const startListening = () => {
    if (!enabled) return
    if (!window.isSecureContext) {
      setMessage('Voice input needs a secure page (HTTPS). You can type your idea instead.')
      return
    }

    const Recognition = getSpeechRecognitionConstructor()
    if (!Recognition) {
      setMessage('Voice input is not available in this browser. You can type your idea instead.')
      return
    }

    setMessage('')
    try {
      const recognition = new Recognition()
      recognition.lang = navigator.language || 'en-US'
      recognition.interimResults = true
      recognition.continuous = false
      recognition.onresult = (event) => {
        const transcript = Array.from(
          { length: event.results.length },
          (_, index) => event.results[index][0].transcript,
        ).join(' ').trim()
        if (transcript) onTranscript(transcript)
      }
      recognition.onerror = (event) => {
        setMessage(recognitionErrorMessage(event.error))
        setListening(false)
        recognitionRef.current = null
      }
      recognition.onend = () => {
        setListening(false)
        recognitionRef.current = null
      }
      recognitionRef.current = recognition
      recognition.start()
      setListening(true)
    } catch (startError) {
      recognitionRef.current = null
      setListening(false)
      setMessage(startError instanceof Error
        ? `Voice input could not start: ${startError.message} You can type your idea instead.`
        : 'Voice input could not start. You can type your idea instead.')
    }
  }

  useEffect(() => {
    if (!enabled) abortListening()
  }, [enabled])

  useEffect(() => () => {
    const recognition = recognitionRef.current
    if (!recognition) return
    recognitionRef.current = null
    recognition.onresult = null
    recognition.onerror = null
    recognition.onend = null
    recognition.abort()
  }, [])

  return { listening, message, startListening, stopListening, setMessage }
}

export function supportsNarrationPlayback() {
  return typeof window !== 'undefined' &&
    'speechSynthesis' in window &&
    'SpeechSynthesisUtterance' in window
}
