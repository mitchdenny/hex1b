#!/usr/bin/env node
/**
 * Generates robots.txt and llms.txt files for the Hex1b documentation site.
 * 
 * This script scans all markdown files in the content directory and generates:
 * - robots.txt: Standard web crawler directives
 * - llms.txt: Structured documentation links for LLMs
 * 
 * Run this script before building the VitePress site to ensure these files
 * are up to date with the latest content.
 */

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const CONTENT_DIR = path.resolve(__dirname, '..');
const PUBLIC_DIR = path.join(CONTENT_DIR, 'public');
const SITE_URL = 'https://hex1b.dev';
const MAX_DESCRIPTION_LENGTH = 200;

/**
 * Represents a documentation page with its metadata.
 */
class DocPage {
    constructor(filePath, title, description, section) {
        this.filePath = filePath;
        this.title = title;
        this.description = description;
        this.section = section;
    }

    get url() {
        // Convert file path to URL path
        let urlPath = this.filePath
            .replace(CONTENT_DIR, '')
            .replace(/\.md$/, '')
            .replace(/\/index$/, '/');
        
        // Ensure path starts with /
        if (!urlPath.startsWith('/')) {
            urlPath = '/' + urlPath;
        }
        
        return `${SITE_URL}${urlPath}`;
    }
}

/**
 * Extracts the title from markdown content.
 * Looks for H1 header (# Title), frontmatter title, or hero name.
 */
function extractTitle(content, filePath) {
    // Try frontmatter title first (frontmatter must be at the very start of the file)
    // Format: ---\n...title: value...\n---
    const frontmatterMatch = content.match(/^---\r?\n([\s\S]*?)\r?\n---/);
    if (frontmatterMatch) {
        const frontmatter = frontmatterMatch[1];
        
        // Try title first
        const titleMatch = frontmatter.match(/^title:\s*["']?([^"'\n]+)["']?$/m);
        if (titleMatch) {
            return titleMatch[1].trim();
        }
        
        // Try hero.name for home page layout
        const heroNameMatch = frontmatter.match(/^\s*name:\s*["']?([^"'\n]+)["']?$/m);
        if (heroNameMatch) {
            return heroNameMatch[1].trim();
        }
    }

    // Try H1 header
    const h1Match = content.match(/^#\s+(.+)$/m);
    if (h1Match) {
        return h1Match[1].trim();
    }

    // Fall back to filename
    const basename = path.basename(filePath, '.md');
    return basename.charAt(0).toUpperCase() + basename.slice(1).replace(/-/g, ' ');
}

/**
 * Extracts a description from markdown content.
 * Looks for frontmatter description, hero tagline, or first paragraph.
 */
function extractDescription(content) {
    // Try frontmatter description first (frontmatter must be at the very start of the file)
    const frontmatterMatch = content.match(/^---\r?\n([\s\S]*?)\r?\n---/);
    if (frontmatterMatch) {
        const frontmatter = frontmatterMatch[1];
        const descMatch = frontmatter.match(/^description:\s*["']?([^"'\n]+)["']?$/m);
        if (descMatch) {
            return descMatch[1].trim();
        }
    }

    // Try hero tagline (for home page)
    const taglineMatch = content.match(/tagline:\s*(.+)$/m);
    if (taglineMatch) {
        return taglineMatch[1].trim();
    }

    // Try first paragraph after H1
    const paragraphMatch = content.match(/^#\s+.+\n\n([^#\n][^\n]+)/m);
    if (paragraphMatch) {
        // Clean up the paragraph
        let desc = paragraphMatch[1].trim();
        // Remove markdown links and formatting
        desc = desc.replace(/\[([^\]]+)\]\([^)]+\)/g, '$1');
        desc = desc.replace(/\*\*([^*]+)\*\*/g, '$1');
        desc = desc.replace(/\*([^*]+)\*/g, '$1');
        desc = desc.replace(/`([^`]+)`/g, '$1');
        return desc.substring(0, MAX_DESCRIPTION_LENGTH);
    }

    return '';
}

/**
 * Determines the section for a file based on its path.
 */
function getSection(filePath) {
    const relativePath = filePath.replace(CONTENT_DIR, '');
    
    if (relativePath === '/index.md') {
        return 'Home';
    }
    if (relativePath.startsWith('/guide/widgets/')) {
        return 'Widget Reference';
    }
    if (relativePath.startsWith('/guide/')) {
        return 'Guide';
    }
    if (relativePath.startsWith('/reference/')) {
        return 'API Reference';
    }
    return 'Documentation';
}

/**
 * Scans the content directory for markdown files.
 */
function scanMarkdownFiles(dir) {
    const pages = [];
    
    // Directories to skip
    const skipDirs = new Set([
        'node_modules',
        'snippets',
        'scripts',
        'dist',
        'cache'
    ]);
    
    function scan(currentDir) {
        const entries = fs.readdirSync(currentDir, { withFileTypes: true });
        
        for (const entry of entries) {
            const fullPath = path.join(currentDir, entry.name);
            
            if (entry.isDirectory()) {
                // Skip hidden directories and excluded directories
                if (!entry.name.startsWith('.') && !skipDirs.has(entry.name)) {
                    scan(fullPath);
                }
            } else if (entry.isFile() && entry.name.endsWith('.md')) {
                const content = fs.readFileSync(fullPath, 'utf-8');
                const title = extractTitle(content, fullPath);
                const description = extractDescription(content);
                const section = getSection(fullPath);
                
                pages.push(new DocPage(fullPath, title, description, section));
            }
        }
    }
    
    scan(dir);
    return pages;
}

/**
 * Generates the robots.txt content.
 */
function generateRobotsTxt(pages) {
    const lines = [
        '# Hex1b Documentation - robots.txt',
        '# https://hex1b.dev',
        '',
        '# Allow all crawlers full access',
        'User-agent: *',
        'Allow: /',
        '',
        '# Sitemap location',
        `Sitemap: ${SITE_URL}/sitemap.xml`,
        '',
        '# LLMs.txt for AI assistants',
        `# See: ${SITE_URL}/llms.txt`,
        ''
    ];
    
    return lines.join('\n');
}

/**
 * Generates the llms.txt content following the llms.txt specification.
 */
function generateLlmsTxt(pages) {
    const lines = [
        '# Hex1b',
        '',
        '> Hex1b is a .NET library for building terminal user interfaces (TUI) with a React-inspired declarative API. It provides a terminal emulator, widget system, layout engine, theming, and testing utilities.',
        '',
    ];

    // Group pages by section
    const sections = new Map();
    for (const page of pages) {
        if (!sections.has(page.section)) {
            sections.set(page.section, []);
        }
        sections.get(page.section).push(page);
    }

    // Define section order for consistent output
    const sectionOrder = ['Home', 'Guide', 'Widget Reference', 'API Reference', 'Documentation'];
    
    for (const sectionName of sectionOrder) {
        const sectionPages = sections.get(sectionName);
        if (!sectionPages || sectionPages.length === 0) continue;

        lines.push(`## ${sectionName}`);
        lines.push('');

        // Sort pages within section
        sectionPages.sort((a, b) => {
            // index.md should come first in each section
            if (a.filePath.endsWith('/index.md') && !b.filePath.endsWith('/index.md')) return -1;
            if (!a.filePath.endsWith('/index.md') && b.filePath.endsWith('/index.md')) return 1;
            return a.title.localeCompare(b.title);
        });

        for (const page of sectionPages) {
            const desc = page.description ? `: ${page.description}` : '';
            lines.push(`- [${page.title}](${page.url})${desc}`);
        }
        lines.push('');
    }

    // Add contact and metadata
    lines.push('## Links');
    lines.push('');
    lines.push(`- [GitHub Repository](https://github.com/mitchdenny/hex1b)`);
    lines.push(`- [NuGet Package](https://www.nuget.org/packages/Hex1b)`);
    lines.push('');

    return lines.join('\n');
}

/**
 * Main function to generate all SEO files.
 */
function main() {
    console.log('Generating SEO files for Hex1b documentation...');
    console.log(`Content directory: ${CONTENT_DIR}`);
    console.log(`Public directory: ${PUBLIC_DIR}`);

    // Ensure public directory exists
    if (!fs.existsSync(PUBLIC_DIR)) {
        fs.mkdirSync(PUBLIC_DIR, { recursive: true });
    }

    // Scan for markdown files
    const pages = scanMarkdownFiles(CONTENT_DIR);
    console.log(`Found ${pages.length} documentation pages`);

    // Generate robots.txt
    const robotsTxt = generateRobotsTxt(pages);
    const robotsPath = path.join(PUBLIC_DIR, 'robots.txt');
    fs.writeFileSync(robotsPath, robotsTxt);
    console.log(`Generated: ${robotsPath}`);

    // Generate llms.txt
    const llmsTxt = generateLlmsTxt(pages);
    const llmsPath = path.join(PUBLIC_DIR, 'llms.txt');
    fs.writeFileSync(llmsPath, llmsTxt);
    console.log(`Generated: ${llmsPath}`);

    console.log('SEO files generated successfully!');
}

main();
