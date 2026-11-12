export const MAX_INPUT_BYTES = 64 * 1024 * 1024

export type InspectErrorCode = 'invalid-save' | 'unsupported-format' | 'format-choice-required' | 'unsupported-pokedex' | 'input-too-large' | 'parser-failure'
export interface InspectOptions { fileName?: string; formatId?: string }
export interface InspectSuccess {
  status: 'ok'
  schemaVersion: 1
  parserRevision: string
  /** Saved adventure-start calendar date (YYYY-MM-DD), when verified and initialized. */
  startDate?: string
  game: { version: string; versions: string[]; formatId: string; name: string }
  pokedex: {
    status: 'available'; coverage: 'complete' | 'partial'; speciesNamespace: string
    trackedSpecies: number[]; seenSpecies: number[]; caughtSpecies: number[]; warnings: string[]
  }
}
export interface InspectError {
  status: 'error'; schemaVersion: 1; parserRevision: string
  code: InspectErrorCode; message: string
  candidateFormats?: { id: string; name: string }[]
  pkhexGame?: string
}
export type InspectResult = InspectSuccess | InspectError
export interface ReaderProgress { stage: 'preparing'; loaded?: number; total?: number }
export interface CreateReaderOptions {
  /** Directory containing worker.js and _framework/. Use a versioned, same-origin URL. */
  baseUrl: string
  onProgress?: (progress: ReaderProgress) => void
  /** Aborting terminates the worker during preparation or inspection. */
  signal?: AbortSignal
}
export interface SaveReader {
  /** Reads a copy of the input, without uploading or rewriting it. */
  inspect(bytes: Uint8Array, options?: InspectOptions): Promise<InspectResult>
  /** Terminates the runtime and rejects pending work with AbortError. */
  dispose(): void
}

/** Starts an isolated .NET runtime in a dedicated worker. No runtime is loaded before this call. */
export async function createSaveReader({ baseUrl, onProgress, signal }: CreateReaderOptions): Promise<SaveReader> {
  signal?.throwIfAborted()
  const base = new URL(baseUrl.endsWith('/') ? baseUrl : `${baseUrl}/`, globalThis.location.href)
  const worker = new Worker(new URL('worker.js', base), { type: 'module', name: 'pokemon-save-reader' })
  let disposed = false
  let nextId = 0
  const pending = new Map<number, { resolve(value: InspectResult): void; reject(error: Error): void }>()
  let readyResolve!: () => void
  let readyReject!: (error: Error) => void
  const ready = new Promise<void>((resolve, reject) => { readyResolve = resolve; readyReject = reject })
  const stop = (error: Error) => {
    if (disposed) return
    disposed = true
    worker.terminate()
    signal?.removeEventListener('abort', abort)
    readyReject(error)
    for (const request of pending.values()) request.reject(error)
    pending.clear()
  }
  const abort = () => stop(new DOMException('Save reading cancelled.', 'AbortError'))
  signal?.addEventListener('abort', abort, { once: true })
  worker.onmessage = ({ data }) => {
    if (disposed) return
    if (data.type === 'progress') onProgress?.(data.progress)
    else if (data.type === 'ready') readyResolve()
    else if (data.type === 'failure') stop(new Error('The save reader could not start.'))
    else if (data.type === 'result') {
      pending.get(data.id)?.resolve(data.result)
      pending.delete(data.id)
    }
  }
  worker.onerror = () => stop(new Error('The save reader worker failed.'))
  onProgress?.({ stage: 'preparing' })
  worker.postMessage({ type: 'init', baseUrl: base.href })
  await ready
  return {
    inspect(bytes, options = {}) {
      if (disposed) return Promise.reject(new DOMException('Save reader disposed.', 'AbortError'))
      if (!(bytes instanceof Uint8Array)) return Promise.reject(new TypeError('Expected Uint8Array save bytes.'))
      if (bytes.byteLength > MAX_INPUT_BYTES) return Promise.resolve({
        status: 'error', schemaVersion: 1, parserRevision: 'serabi-reader-0.1.2',
        code: 'input-too-large', message: 'The save exceeds the 64 MiB input limit.',
      })
      const copy = bytes.slice()
      const id = ++nextId
      return new Promise((resolve, reject) => {
        pending.set(id, { resolve, reject })
        worker.postMessage({ type: 'inspect', id, bytes: copy, options }, [copy.buffer])
      })
    },
    dispose: abort,
  }
}
