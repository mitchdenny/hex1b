import { execFile } from 'node:child_process'
import { lstat, mkdtemp, readdir, readFile, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { setTimeout } from 'node:timers/promises'
import { promisify } from 'node:util'

const run = promisify(execFile)
const pendingStatuses = new Set([
  'deployment_in_progress', 'syncing_files', 'finished_file_sync', 'updating_pages',
  'purging_cdn', 'deployment_attempt_error',
])
const failedStatuses = new Set([
  'deployment_failed', 'deployment_perms_error', 'deployment_content_failed',
  'deployment_cancelled', 'deployment_lost',
])

export async function validateBundle(directory) {
  const root = await lstat(directory)
  if (!root.isDirectory() || root.isSymbolicLink()) {
    throw new Error('The Pages bundle must be a real directory.')
  }
  async function inspect(path) {
    for (const name of await readdir(path)) {
      const file = join(path, name)
      const stat = await lstat(file)
      if (stat.isDirectory()) {
        await inspect(file)
      } else if (!stat.isFile() || stat.nlink !== 1) {
        throw new Error(`The Pages bundle contains a link or special file: ${file}`)
      }
    }
  }
  await inspect(directory)
  const index = await lstat(join(directory, 'index.html'))
  if (!index.isFile() || index.size === 0) {
    throw new Error('The Pages bundle must contain a nonempty index.html.')
  }
}

export async function publicationContext(env, repository) {
  for (const name of [
    'GITHUB_TOKEN', 'GITHUB_SHA', 'GITHUB_EVENT_NAME', 'GITHUB_EVENT_PATH',
    'GITHUB_REPOSITORY', 'GITHUB_RUN_ID', 'GITHUB_RUN_ATTEMPT',
    'ACTIONS_RUNTIME_TOKEN', 'ACTIONS_RESULTS_URL',
    'ACTIONS_ID_TOKEN_REQUEST_TOKEN', 'ACTIONS_ID_TOKEN_REQUEST_URL',
    'HEX1B_PAGES_ID_TOKEN',
  ]) {
    if (!env[name]) throw new Error(`Missing ${name}; publish from a GitHub Actions JavaScript action context.`)
  }
  if (env.GITHUB_REPOSITORY !== repository) {
    throw new Error('The Pages destination must match the workflow repository.')
  }
  if (!/^[a-f0-9]{40}$/.test(env.GITHUB_SHA)) {
    throw new Error('GITHUB_SHA must identify the tested commit.')
  }
  if (!/^[1-9]\d*$/.test(env.GITHUB_RUN_ID) || !/^[1-9]\d*$/.test(env.GITHUB_RUN_ATTEMPT)) {
    throw new Error('GitHub workflow run and attempt IDs must be positive integers.')
  }
  if (!['push', 'pull_request', 'workflow_dispatch'].includes(env.GITHUB_EVENT_NAME)) {
    throw new Error(`Unsupported Pages publication event: ${env.GITHUB_EVENT_NAME}`)
  }
  const event = JSON.parse(await readFile(env.GITHUB_EVENT_PATH, 'utf8'))
  if (env.GITHUB_EVENT_NAME === 'pull_request' &&
      event.pull_request?.head?.repo?.full_name !== repository) {
    throw new Error('Fork pull requests cannot publish GitHub Pages.')
  }
}

export async function publishPages({
  bundleDirectory, repository, artifactName, env = process.env,
  artifactClient, getIDToken, fetchImpl = fetch,
  signal = AbortSignal.timeout(600_000), pollIntervalMs = 5000,
  log = console.log,
}) {
  await publicationContext(env, repository)
  const bundle = resolve(bundleDirectory)
  await validateBundle(bundle)
  signal.throwIfAborted()
  const api = 'https://api.github.com'
  const endpoint = `/repos/${repository}/pages`

  async function request(path, { method = 'GET', body, requestSignal = signal } = {}) {
    requestSignal.throwIfAborted()
    const response = await fetchImpl(`${api}${path}`, {
      method,
      headers: {
        Accept: 'application/vnd.github+json',
        Authorization: `Bearer ${env.GITHUB_TOKEN}`,
        'X-GitHub-Api-Version': '2022-11-28',
        'Content-Type': 'application/json',
      },
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: requestSignal,
    })
    requestSignal.throwIfAborted()
    if (!response.ok) {
      throw new Error(`GitHub Pages request failed (HTTP ${response.status}). Check Pages source, environment rules, and workflow permissions.`)
    }
    return response.status === 204 ? null : response.json()
  }

  const settings = await request(endpoint)
  if (settings.build_type !== 'workflow' || settings.cname) {
    throw new Error('Enable Pages with the GitHub Actions source and no custom domain before publishing.')
  }

  const temporaryDirectory = await mkdtemp(join(tmpdir(), 'hex1b-pages-'))
  let deploymentId
  let deploymentFinished = false
  try {
    const archive = join(temporaryDirectory, 'artifact.tar')
    await run('tar', ['-cf', archive, '-C', bundle, '.'], { signal })
    const artifact = await artifactClient.uploadArtifact(
      `${artifactName}-${env.GITHUB_RUN_ID}-${env.GITHUB_RUN_ATTEMPT}`,
      [archive], temporaryDirectory, { retentionDays: 1 },
    )
    if (!Number.isSafeInteger(artifact.id) || artifact.id <= 0) {
      throw new Error('GitHub did not return an uploaded artifact ID.')
    }
    signal.throwIfAborted()
    const idToken = await getIDToken()
    if (!idToken) throw new Error('GitHub did not issue an Actions OIDC token.')
    const deployment = await request(`${endpoint}/deployments`, {
      method: 'POST',
      body: {
        artifact_id: artifact.id,
        pages_build_version: env.GITHUB_SHA,
        oidc_token: idToken,
        environment: 'github-pages',
      },
    })
    if (!deployment.id || !['string', 'number'].includes(typeof deployment.id)) {
      throw new Error('GitHub did not return a deployment ID.')
    }
    deploymentId = encodeURIComponent(deployment.id)
    if (typeof deployment.page_url !== 'string' || !deployment.page_url) {
      throw new Error('GitHub did not return a Pages URL.')
    }
    const pageUrl = new URL(deployment.page_url)
    const [owner, repo] = repository.split('/')
    if (pageUrl.origin !== `https://${owner.toLowerCase()}.github.io` ||
        ![`/${repo}`, `/${repo}/`].includes(pageUrl.pathname) ||
        pageUrl.username || pageUrl.password || pageUrl.search || pageUrl.hash) {
      throw new Error('GitHub returned an unexpected Pages URL.')
    }
    pageUrl.pathname = `/${repo}/`
    for (;;) {
      const status = await request(`${endpoint}/deployments/${deploymentId}`)
      if (status.status === 'succeed') {
        deploymentFinished = true
        log(`Published ${env.GITHUB_SHA} to ${pageUrl.href}`)
        return pageUrl.href
      }
      if (!pendingStatuses.has(status.status)) {
        deploymentFinished = failedStatuses.has(status.status)
        throw new Error(`GitHub Pages deployment did not succeed: ${status.status}`)
      }
      log(`GitHub Pages deployment status: ${status.status}`)
      await setTimeout(pollIntervalMs, undefined, { signal })
    }
  } catch (error) {
    if (signal.aborted) error = signal.reason
    if (deploymentId && !deploymentFinished) {
      try {
        await request(`${endpoint}/deployments/${deploymentId}/cancel`, {
          method: 'POST', requestSignal: AbortSignal.timeout(10_000),
        })
      } catch (cancelError) {
        throw new AggregateError([error, cancelError], 'Pages publication failed and deployment cancellation also failed.')
      }
    }
    throw error
  } finally {
    await rm(temporaryDirectory, { recursive: true, force: true })
  }
}
