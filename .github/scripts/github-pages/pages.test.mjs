import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { link, mkdir, mkdtemp, readFile, rm, symlink, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { test } from 'node:test'
import { promisify } from 'node:util'
import { checkBundle } from './check-bundle.mjs'
import { publicationContext, publishPages, validateBundle } from './pages.mjs'

const run = promisify(execFile)

async function fixture(t) {
  const directory = await mkdtemp(join(tmpdir(), 'hex1b-pages-test-'))
  t.after(() => rm(directory, { recursive: true, force: true }))
  const bundleDirectory = join(directory, 'dist')
  await mkdir(join(bundleDirectory, 'assets'), { recursive: true })
  await writeFile(join(bundleDirectory, 'index.html'),
    '<script src="/hex1b/assets/main-123.js"></script><link href="/hex1b/assets/main-123.css" rel="stylesheet">')
  await writeFile(join(bundleDirectory, 'assets', 'main-123.js'), 'console.log("Vite")')
  await writeFile(join(bundleDirectory, 'assets', 'main-123.css'), 'body { margin: 0 }')
  const eventPath = join(directory, 'event.json')
  await writeFile(eventPath, JSON.stringify({ pull_request: { head: { repo: { full_name: 'mitchdenny/hex1b' } } } }))
  const env = Object.fromEntries([
    'GITHUB_TOKEN', 'ACTIONS_RUNTIME_TOKEN', 'ACTIONS_RESULTS_URL',
    'ACTIONS_ID_TOKEN_REQUEST_TOKEN', 'ACTIONS_ID_TOKEN_REQUEST_URL',
    'HEX1B_PAGES_ID_TOKEN',
  ].map(name => [name, 'test-value']))
  Object.assign(env, {
    GITHUB_SHA: 'a'.repeat(40), GITHUB_REPOSITORY: 'mitchdenny/hex1b',
    GITHUB_EVENT_NAME: 'pull_request', GITHUB_EVENT_PATH: eventPath,
    GITHUB_RUN_ID: '987', GITHUB_RUN_ATTEMPT: '2',
  })
  const requests = []
  let archivePath
  let uploadCount = 0
  let statuses = ['deployment_in_progress', 'succeed']
  const options = {
    bundleDirectory, repository: 'mitchdenny/hex1b', artifactName: 'github-pages-new-site', env,
    pollIntervalMs: 0,
    log: () => {},
    getIDToken: async () => 'test-oidc-token',
    artifactClient: {
      async uploadArtifact(name, files, root, uploadOptions) {
        uploadCount++
        assert.equal(name, 'github-pages-new-site-987-2')
        assert.equal(files.length, 1)
        assert.equal(dirname(files[0]), root)
        assert.equal(uploadOptions.retentionDays, 1)
        archivePath = files[0]
        const { stdout } = await run('tar', ['-tf', archivePath])
        assert.match(stdout, /index.html/)
        assert.match(stdout, /assets\/main-123.js/)
        assert.doesNotMatch(stdout, /event.json|node_modules/)
        return { id: 123 }
      },
    },
    async fetchImpl(url, request) {
      requests.push({ url, ...request })
      if (url.endsWith('/cancel')) return new Response(null, { status: 204 })
      if (request.method === 'POST') {
        return Response.json({ id: '456', page_url: 'https://mitchdenny.github.io/hex1b/' })
      }
      if (url.endsWith('/pages')) return Response.json({ build_type: 'workflow', cname: null })
      return Response.json({ status: statuses.shift() })
    },
  }
  return {
    options, directory, requests,
    archivePath: () => archivePath,
    uploadCount: () => uploadCount,
    statuses: value => { statuses = [...value] },
  }
}

test('publish builds an artifact, uses its ID and tested SHA, and waits for success', async t => {
  const f = await fixture(t)
  assert.equal(await publishPages(f.options), 'https://mitchdenny.github.io/hex1b/')
  const deployment = f.requests.find(request => request.method === 'POST')
  assert.deepEqual(JSON.parse(deployment.body), {
    artifact_id: 123, pages_build_version: 'a'.repeat(40),
    oidc_token: 'test-oidc-token', environment: 'github-pages',
  })
  assert.equal(f.requests.filter(request => request.url.endsWith('/456')).length, 2)
  assert.equal(deployment.headers.Authorization, 'Bearer test-value')
  await assert.rejects(readFile(f.archivePath()), { code: 'ENOENT' })
})

test('missing Actions credentials fail before upload or API access', async t => {
  const f = await fixture(t)
  delete f.options.env.ACTIONS_RUNTIME_TOKEN
  await assert.rejects(publishPages(f.options), /Missing ACTIONS_RUNTIME_TOKEN/)
  assert.equal(f.uploadCount(), 0)
  assert.equal(f.requests.length, 0)
})

test('all documented intermediate deployment statuses are polled rather than canceled', async t => {
  const f = await fixture(t)
  f.statuses([
    'deployment_in_progress', 'syncing_files', 'finished_file_sync',
    'updating_pages', 'purging_cdn', 'deployment_attempt_error', 'succeed',
  ])
  assert.equal(await publishPages(f.options), 'https://mitchdenny.github.io/hex1b/')
  assert.equal(f.requests.filter(request => request.url.endsWith('/456')).length, 7)
  assert.ok(!f.requests.some(request => request.url.endsWith('/cancel')))
})

test('Pages URLs without a trailing slash are normalized', async t => {
  const f = await fixture(t)
  const fetch = f.options.fetchImpl
  f.options.fetchImpl = async (url, request) => request.method === 'POST'
    ? Response.json({ id: '456', page_url: 'https://mitchdenny.github.io/hex1b' })
    : fetch(url, request)
  assert.equal(await publishPages(f.options), 'https://mitchdenny.github.io/hex1b/')
})

test('workflow reruns use distinct immutable artifact names', async t => {
  const f = await fixture(t)
  const names = []
  f.options.artifactClient.uploadArtifact = async name => {
    names.push(name)
    return { id: names.length }
  }
  for (const attempt of ['2', '3']) {
    f.options.env.GITHUB_RUN_ATTEMPT = attempt
    f.statuses(['succeed'])
    await publishPages(f.options)
  }
  assert.deepEqual(names, ['github-pages-new-site-987-2', 'github-pages-new-site-987-3'])
})

test('fork PRs cannot publish', async t => {
  const f = await fixture(t)
  await writeFile(f.options.env.GITHUB_EVENT_PATH,
    JSON.stringify({ pull_request: { head: { repo: { full_name: 'fork/hex1b' } } } }))
  await assert.rejects(publishPages(f.options), /Fork pull requests/)
  assert.equal(f.requests.length, 0)
})

test('publication cannot target a different repository or pull_request_target', async t => {
  const f = await fixture(t)
  await assert.rejects(publicationContext(f.options.env, 'someone/else'), /must match/)
  f.options.env.GITHUB_EVENT_NAME = 'pull_request_target'
  await assert.rejects(publishPages(f.options), /Unsupported.*event/)
})

test('main pushes and manual branch runs can publish', async t => {
  const f = await fixture(t)
  for (const eventName of ['push', 'workflow_dispatch']) {
    await publicationContext({ ...f.options.env, GITHUB_EVENT_NAME: eventName }, f.options.repository)
  }
})

test('disabled Pages, branch-source Pages and custom domains fail before upload', async t => {
  const f = await fixture(t)
  f.options.fetchImpl = async () => new Response(null, { status: 404 })
  await assert.rejects(publishPages(f.options), /HTTP 404/)
  for (const settings of [{ build_type: 'legacy' }, { build_type: 'workflow', cname: 'hex1b.dev' }]) {
    f.options.fetchImpl = async () => Response.json(settings)
    await assert.rejects(publishPages(f.options), /Enable Pages/)
  }
  assert.equal(f.uploadCount(), 0)
})

test('an upload error propagates and removes the temporary archive', async t => {
  const f = await fixture(t)
  let archivePath
  f.options.artifactClient.uploadArtifact = async (_, files) => {
    archivePath = files[0]
    throw new Error('Upload failed')
  }
  await assert.rejects(publishPages(f.options), /Upload failed/)
  assert.equal(f.requests.length, 1)
  await assert.rejects(readFile(archivePath), { code: 'ENOENT' })
})

test('missing upload ID or OIDC token cannot create a deployment', async t => {
  const f = await fixture(t)
  f.options.artifactClient.uploadArtifact = async () => ({})
  await assert.rejects(publishPages(f.options), /artifact ID/)
  f.options.artifactClient.uploadArtifact = async () => ({ id: 123 })
  f.options.getIDToken = async () => ''
  await assert.rejects(publishPages(f.options), /OIDC token/)
  assert.equal(f.requests.filter(request => request.method === 'POST').length, 0)
})

test('failed deployment statuses are not reported as successful', async t => {
  const f = await fixture(t)
  for (const status of ['deployment_failed', 'deployment_content_failed', 'deployment_cancelled']) {
    f.statuses([status])
    await assert.rejects(publishPages(f.options), new RegExp(status))
  }
})

test('unknown status and polling API errors fail and cancel the created deployment', async t => {
  const f = await fixture(t)
  f.statuses(['unexpected'])
  await assert.rejects(publishPages(f.options), /unexpected/)
  assert.ok(f.requests.some(request => request.url.endsWith('/456/cancel')))
  const fetch = f.options.fetchImpl
  f.options.fetchImpl = async (url, request) => url.endsWith('/456')
    ? new Response(null, { status: 503 })
    : fetch(url, request)
  await assert.rejects(publishPages(f.options), /HTTP 503/)
})

test('timeout while polling cancels the deployment and preserves the timeout error', async t => {
  const f = await fixture(t)
  const controller = new AbortController()
  f.options.signal = controller.signal
  const fetch = f.options.fetchImpl
  f.options.fetchImpl = async (url, request) => {
    if (url.endsWith('/456')) controller.abort(new Error('Publication timeout'))
    return fetch(url, request)
  }
  await assert.rejects(publishPages(f.options), /Publication timeout/)
  assert.ok(f.requests.some(request => request.url.endsWith('/456/cancel')))
})

test('cancellation failure reports both errors', async t => {
  const f = await fixture(t)
  f.statuses(['unexpected'])
  const fetch = f.options.fetchImpl
  f.options.fetchImpl = async (url, request) => url.endsWith('/cancel')
    ? new Response(null, { status: 403 })
    : fetch(url, request)
  await assert.rejects(publishPages(f.options), error => {
    assert.ok(error instanceof AggregateError)
    assert.match(error.errors[0].message, /unexpected/)
    assert.match(error.errors[1].message, /HTTP 403/)
    return true
  })
})

test('invalid returned URL cannot be announced as the site', async t => {
  const f = await fixture(t)
  const fetch = f.options.fetchImpl
  f.options.fetchImpl = async (url, request) => request.method === 'POST' && !url.endsWith('/cancel')
    ? Response.json({ id: '456', page_url: 'https://other.example/' })
    : fetch(url, request)
  await assert.rejects(publishPages(f.options), /unexpected Pages URL/)
})

test('missing deployment metadata fails and cancels when an ID was returned', async t => {
  const f = await fixture(t)
  const fetch = f.options.fetchImpl
  for (const deployment of [{}, { id: '456' }]) {
    f.options.fetchImpl = async (url, request) => request.method === 'POST' && !url.endsWith('/cancel')
      ? Response.json(deployment)
      : fetch(url, request)
    await assert.rejects(publishPages(f.options), /did not return/)
  }
  assert.ok(f.requests.some(request => request.url.endsWith('/456/cancel')))
})

test('bundle validation rejects symbolic and hard links', async t => {
  const f = await fixture(t)
  const path = join(f.options.bundleDirectory, 'link')
  await symlink('index.html', path)
  await assert.rejects(validateBundle(f.options.bundleDirectory), /link or special/)
  await rm(path)
  await link(join(f.options.bundleDirectory, 'index.html'), path)
  await assert.rejects(validateBundle(f.options.bundleDirectory), /link or special/)
})

test('bundle validation requires nonempty HTML and real base-path-safe JS and CSS', async t => {
  const f = await fixture(t)
  await checkBundle(f.options.bundleDirectory)
  await writeFile(join(f.options.bundleDirectory, 'index.html'),
    '<script src="/assets/main-123.js"></script><link href="/hex1b/assets/main-123.css">')
  await assert.rejects(checkBundle(f.options.bundleDirectory), /does not use the Pages base/)
  await writeFile(join(f.options.bundleDirectory, 'index.html'), '')
  await assert.rejects(checkBundle(f.options.bundleDirectory), /nonempty index.html/)
})
