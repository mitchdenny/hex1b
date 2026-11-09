import { DefaultArtifactClient } from '@actions/artifact'
import * as core from '@actions/core'
import { publishPages } from './pages.mjs'

const [bundleDirectory, repository, artifactName] = process.argv.slice(2)
if (!bundleDirectory || !repository || !artifactName) {
  throw new Error('Usage: node publish.mjs <bundle-directory> <owner/repository> <artifact-name>')
}

try {
  const url = await publishPages({
    bundleDirectory, repository, artifactName,
    artifactClient: new DefaultArtifactClient(),
    getIDToken: async () => process.env.HEX1B_PAGES_ID_TOKEN,
    log: message => core.info(message),
  })
  core.setOutput('page_url', url)
} catch (error) {
  core.setFailed(error instanceof AggregateError
    ? `${error.message}: ${error.errors.map(item => item.message).join('; ')}`
    : error.message)
}
