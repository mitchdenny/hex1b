import { readFile, stat } from 'node:fs/promises'
import { resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import { validateBundle } from './pages.mjs'

export async function checkBundle(directory, base = '/hex1b/') {
  const root = resolve(directory)
  await validateBundle(root)
  const html = await readFile(resolve(root, 'index.html'), 'utf8')
  const assets = [...html.matchAll(/(?:src|href)="([^"]+\.(?:js|css))"/g)].map(match => match[1])
  if (!assets.some(asset => asset.endsWith('.js')) || !assets.some(asset => asset.endsWith('.css'))) {
    throw new Error('The Vite placeholder must emit JavaScript and CSS assets.')
  }
  for (const asset of assets) {
    const path = resolve(root, asset.slice(base.length))
    if (!asset.startsWith(base) || !path.startsWith(`${root}${sep}`)) {
      throw new Error(`The bundle asset does not use the Pages base ${base}: ${asset}`)
    }
    const file = await stat(path)
    if (!file.isFile() || file.size === 0) throw new Error(`Missing or empty bundle asset: ${asset}`)
  }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  await checkBundle(process.argv[2], process.argv[3])
  console.log('Pages production bundle and asset paths verified.')
}
