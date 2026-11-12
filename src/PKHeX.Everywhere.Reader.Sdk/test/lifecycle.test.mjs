import { test, beforeEach } from 'node:test'
import assert from 'node:assert/strict'
import { createSaveReader, MAX_INPUT_BYTES } from '../dist/index.js'

globalThis.location = new URL('https://example.test/import')
let workers = []
let boot = true
class TestWorker {
  constructor(url, options) { this.url = url; this.options = options; this.messages = []; workers.push(this) }
  postMessage(message) {
    this.messages.push(message)
    if (message.type === 'init' && boot) queueMicrotask(() => this.onmessage({ data: { type: 'ready' } }))
  }
  terminate() { this.terminated = true }
  result(id, result) { this.onmessage({ data: { type: 'result', id, result } }) }
}
globalThis.Worker = TestWorker
beforeEach(() => { workers = []; boot = true })

test('loads only when explicitly created and resolves a versioned worker URL', async () => {
  assert.equal(workers.length, 0)
  const reader = await createSaveReader({ baseUrl: '/reader/0.1.0' })
  assert.equal(workers[0].url.href, 'https://example.test/reader/0.1.0/worker.js')
  assert.equal(workers[0].options.type, 'module')
  reader.dispose()
  assert.equal(workers[0].terminated, true)
})

test('aborting preparation rejects and terminates the runtime', async () => {
  boot = false
  const controller = new AbortController()
  const preparing = createSaveReader({ baseUrl: '/reader/0.1.0/', signal: controller.signal })
  controller.abort()
  await assert.rejects(preparing, { name: 'AbortError' })
  assert.equal(workers[0].terminated, true)
})

test('reads a transferable copy and disposal cancels pending inspection', async () => {
  const reader = await createSaveReader({ baseUrl: '/reader/0.1.0/' })
  const bytes = new Uint8Array([1, 2, 3])
  const inspected = reader.inspect(bytes)
  const message = workers[0].messages.at(-1)
  assert.notEqual(message.bytes.buffer, bytes.buffer)
  assert.deepEqual(message.bytes, bytes)
  reader.dispose()
  await assert.rejects(inspected, { name: 'AbortError' })
  await assert.rejects(reader.inspect(bytes), { name: 'AbortError' })
  assert.deepEqual(bytes, new Uint8Array([1, 2, 3]))
})

test('rejects an oversized file without sending its bytes to the worker', async () => {
  const reader = await createSaveReader({ baseUrl: '/reader/0.1.0/' })
  const result = await reader.inspect(new Uint8Array(MAX_INPUT_BYTES + 1))
  assert.equal(result.code, 'input-too-large')
  assert.equal(workers[0].messages.length, 1)
  reader.dispose()
})

test('boot failure terminates and rejects', async () => {
  boot = false
  const preparing = createSaveReader({ baseUrl: '/reader/0.1.0/' })
  workers[0].onerror()
  await assert.rejects(preparing, /worker failed/)
  assert.equal(workers[0].terminated, true)
})
