import { execFileSync, spawnSync } from 'node:child_process';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export const packageName = '@hex1b/web-terminal';
export const repositoryUrl = 'git+https://github.com/mitchdenny/hex1b.git';


export function validateVersion(version, kind, prNumber) {
    const match = /^(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)(?:-([0-9A-Za-z.-]+))?$/.exec(version ?? '');
    if (!match) {
        throw new Error('Expected the exact package version from the workflow version job.');
    }

    if (kind === 'release' && !match[1]) {
        return;
    }
    if ((kind === 'alpha' || kind === 'beta') && match[1]?.startsWith(`${kind}.`)) {
        return;
    }
    if (kind === 'pr' && /^[1-9]\d*$/.test(prNumber ?? '') && match[1]?.startsWith(`pr.${prNumber}.`)) {
        return;
    }
    throw new Error(`Package version '${version}' does not match publication kind '${kind}'.`);
}

export function publicationTarget(version, kind) {
    if (kind === 'pr') {
        throw new Error('PR npm packages are build artifacts only and cannot be published.');
    }
    validateVersion(version, kind);
    return { registry: 'https://registry.npmjs.org', tag: kind === 'release' ? 'latest' : kind };
}

export function validateManifest(manifest, version) {
    if (manifest.name !== packageName || manifest.version !== version) {
        throw new Error(`Tarball must contain ${packageName}@${version}.`);
    }
    if (manifest.repository?.url !== repositoryUrl) {
        throw new Error(`Tarball repository.url must be ${repositoryUrl}.`);
    }
    if (manifest.publishConfig?.registry || manifest.publishConfig?.tag) {
        throw new Error('Tarball must not override the workflow registry or dist-tag.');
    }
}

function runNpm(args, options) {
    return spawnSync('npm', args, { encoding: 'utf8', ...options });
}

export function versionExists(version, registry, npm = runNpm) {
    const result = npm([
        'view', `${packageName}@${version}`, 'version', '--json', '--registry', registry,
    ]);
    if (result.error) throw result.error;
    if (result.status === null) {
        throw new Error(`npm view was terminated: ${result.signal}`);
    }

    let body;
    try {
        body = JSON.parse(result.stdout);
    } catch {
        throw new Error(`npm view returned invalid JSON (exit ${result.status}). ${result.stderr ?? ''}`);
    }

    if (result.status === 0) {
        if (body !== version) {
            throw new Error(`npm view did not return the exact requested version '${version}'.`);
        }
        return true;
    }

    // npm reports missing packages AND missing exact versions as E404. Never
    // interpret authentication, rate limiting, network or server errors as absent.
    if (body?.error?.code === 'E404' || body?.error?.code === '404' || body?.error?.code === 404) {
        return false;
    }
    throw new Error(`npm view failed (exit ${result.status}): ${JSON.stringify(body)} ${result.stderr ?? ''}`);
}

export function publishPackage({ version, kind, tarball, manifest }, npm = runNpm) {
    const target = publicationTarget(version, kind);
    validateManifest(manifest, version);
    if (versionExists(version, target.registry, npm)) {
        console.log(`${packageName}@${version} already exists on ${target.registry}; skipping publish.`);
        return false;
    }

    const args = [
        'publish', tarball, '--ignore-scripts', '--registry', target.registry, '--tag', target.tag, '--access', 'public',
    ];
    const result = npm(args, { stdio: 'inherit' });
    if (result.error) throw result.error;
    if (result.status !== 0) {
        throw new Error(`npm publish failed (exit ${result.status}, signal ${result.signal ?? 'none'}).`);
    }
    return true;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
    if (process.argv.length > 3 || (process.argv[2] && process.argv[2] !== '--verify')) {
        throw new Error('Usage: node .github/scripts/publish-web-terminal.mjs [--verify]');
    }
    const version = process.env.PACKAGE_VERSION;
    const kind = process.env.PACKAGE_KIND;
    const prNumber = process.env.PR_NUMBER;
    validateVersion(version, kind, prNumber);
    const tarball = resolve(`artifacts/npm/hex1b-web-terminal-${version}.tgz`);
    const manifest = JSON.parse(execFileSync('tar', ['-xOf', tarball, 'package/package.json'], { encoding: 'utf8' }));
    if (process.argv[2] === '--verify') {
        validateManifest(manifest, version);
        console.log(`Verified ${packageName}@${version} in ${tarball}.`);
    } else {
        publishPackage({ version, kind, tarball, manifest });
    }
}
