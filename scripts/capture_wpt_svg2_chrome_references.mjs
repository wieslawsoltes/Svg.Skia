#!/usr/bin/env node

import fs from 'node:fs/promises';
import path from 'node:path';
import http from 'node:http';
import { fileURLToPath } from 'node:url';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const repoRoot = path.resolve(__dirname, '..');
const corpusRoot = path.join(repoRoot, 'externals', 'WPT_SVG_2');
const svgRoot = path.join(corpusRoot, 'svg');
const outputRoot = path.join(repoRoot, 'tests', 'Svg.Skia.UnitTests', 'ChromeReference', 'WPT', 'svg');
const wrapperRoot = path.join(repoRoot, 'output', 'playwright', 'wpt-svg2-capture');

const mimeTypes = new Map([
    ['.css', 'text/css; charset=utf-8'],
    ['.html', 'text/html; charset=utf-8'],
    ['.js', 'application/javascript; charset=utf-8'],
    ['.png', 'image/png'],
    ['.svg', 'image/svg+xml; charset=utf-8'],
    ['.ttf', 'font/ttf'],
    ['.woff', 'font/woff'],
    ['.txt', 'text/plain; charset=utf-8'],
]);

const fontCss = `
@font-face { font-family: "Ahem"; src: url("/externals/WPT_SVG_2/fonts/Ahem.ttf") format("truetype"); }
`;

function getContentType(filePath)
{
    return mimeTypes.get(path.extname(filePath).toLowerCase()) ?? 'application/octet-stream';
}

function createStaticServer(rootPath)
{
    return http.createServer(async (req, res) =>
    {
        try
        {
            const requestUrl = new URL(req.url ?? '/', 'http://127.0.0.1');
            const requestPath = decodeURIComponent(requestUrl.pathname);
            const mappedPath = requestPath.startsWith('/fonts/')
                ? path.join(corpusRoot, requestPath.slice(1))
                : path.join(rootPath, requestPath === '/' ? 'index.html' : requestPath);
            const normalizedPath = path.normalize(mappedPath);

            if (!normalizedPath.startsWith(rootPath) && !normalizedPath.startsWith(corpusRoot))
            {
                res.writeHead(403);
                res.end('Forbidden');
                return;
            }

            const stats = await fs.stat(normalizedPath);
            const filePath = stats.isDirectory()
                ? path.join(normalizedPath, 'index.html')
                : normalizedPath;
            const body = await fs.readFile(filePath);
            res.writeHead(200, { 'Content-Type': getContentType(filePath) });
            res.end(body);
        }
        catch
        {
            res.writeHead(404);
            res.end('Not Found');
        }
    });
}

async function collectSvgFiles(directory)
{
    const entries = await fs.readdir(directory, { withFileTypes: true });
    const result = [];
    for (const entry of entries)
    {
        const entryPath = path.join(directory, entry.name);
        if (entry.isDirectory())
        {
            result.push(...await collectSvgFiles(entryPath));
            continue;
        }

        if (!entry.isFile() || !entry.name.endsWith('.svg'))
        {
            continue;
        }

        const relativePath = path.relative(svgRoot, entryPath).split(path.sep).join('/');
        if (relativePath.includes('/reference/') || relativePath.includes('/support/') || relativePath.endsWith('-ref.svg'))
        {
            continue;
        }

        result.push(relativePath);
    }

    return result;
}

async function getNames()
{
    const cliNames = process.argv.slice(2)
        .flatMap(arg => arg.split(','))
        .map(name => name.trim())
        .filter(Boolean)
        .map(name => name.startsWith('svg/') ? name.slice(4) : name)
        .map(name => name.endsWith('.png') ? `${name.slice(0, -4)}.svg` : name)
        .map(name => name.endsWith('.svg') ? name : `${name}.svg`);

    if (cliNames.length > 0)
    {
        return cliNames;
    }

    return (await collectSvgFiles(svgRoot)).sort((left, right) => left.localeCompare(right, 'en'));
}

function parseFirstFloat(value)
{
    if (!value || value.includes('%'))
    {
        return null;
    }

    const match = value.match(/[+-]?(?:\d+\.\d*|\d+|\.\d+)(?:[eE][+-]?\d+)?/);
    return match ? Number.parseFloat(match[0]) : null;
}

function getAttribute(rootTag, name)
{
    const pattern = new RegExp(`\\b${name}\\s*=\\s*["']([^"']+)["']`, 'i');
    return rootTag.match(pattern)?.[1] ?? null;
}

function getViewport(svgMarkup)
{
    const rootTagMatch = svgMarkup.match(/<svg\b([^>]*)>/i);
    const rootTag = rootTagMatch?.[1] ?? '';
    const width = parseFirstFloat(getAttribute(rootTag, 'width'));
    const height = parseFirstFloat(getAttribute(rootTag, 'height'));
    if (width && height)
    {
        return { width, height };
    }

    const viewBox = getAttribute(rootTag, 'viewBox');
    if (viewBox)
    {
        const parts = viewBox
            .split(/[\s,]+/)
            .map(part => Number.parseFloat(part))
            .filter(Number.isFinite);
        if (parts.length === 4 && parts[2] > 0 && parts[3] > 0)
        {
            return { width: parts[2], height: parts[3] };
        }
    }

    return { width: 300, height: 150 };
}

function injectFonts(svgMarkup)
{
    return svgMarkup.replace(/<svg\b[^>]*>/i, match => `${match}\n<style>${fontCss}</style>`);
}

async function writeWrapper(relativeSvgPath)
{
    const svgPath = path.join(svgRoot, relativeSvgPath);
    const rawSvg = await fs.readFile(svgPath, 'utf8');
    const svgMarkup = injectFonts(rawSvg);
    const viewport = getViewport(svgMarkup);
    const width = Math.max(1, Math.ceil(viewport.width));
    const height = Math.max(1, Math.ceil(viewport.height));
    const wrapperPath = path.join(wrapperRoot, `${relativeSvgPath}.html`);
    const html = `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <style>
    html, body {
      margin: 0;
      padding: 0;
      width: ${width}px;
      height: ${height}px;
      overflow: hidden;
      background: white;
    }
    #capture {
      width: ${width}px;
      height: ${height}px;
      display: block;
      background: white;
    }
    #capture > svg {
      width: ${width}px;
      height: ${height}px;
      display: block;
      background: white;
    }
  </style>
</head>
<body>
  <div id="capture">${svgMarkup}</div>
</body>
</html>`;

    await fs.mkdir(path.dirname(wrapperPath), { recursive: true });
    await fs.writeFile(wrapperPath, html);
    return { wrapperPath, width, height };
}

async function captureReference(baseUrl, relativeSvgPath)
{
    const svgPath = path.join(svgRoot, relativeSvgPath);
    await fs.access(svgPath);

    const { wrapperPath, width, height } = await writeWrapper(relativeSvgPath);
    const wrapperUrl = `${baseUrl}/${path.relative(repoRoot, wrapperPath).split(path.sep).map(encodeURIComponent).join('/')}`;
    const outputPath = path.join(outputRoot, relativeSvgPath.replace(/\.svg$/i, '.png'));

    await fs.mkdir(path.dirname(outputPath), { recursive: true });
    await execFileAsync(
        'npx',
        [
            'playwright',
            'screenshot',
            '--channel',
            'chrome',
            '--viewport-size',
            `${width},${height}`,
            '--wait-for-timeout',
            '1500',
            '--timeout',
            '30000',
            wrapperUrl,
            outputPath,
        ],
        { cwd: repoRoot });

    return outputPath;
}

async function main()
{
    const names = await getNames();
    if (names.length < 1)
    {
        throw new Error(`No WPT SVG 2 targets found in ${svgRoot}.`);
    }

    const server = createStaticServer(repoRoot);
    await new Promise((resolve, reject) =>
    {
        server.once('error', reject);
        server.listen(0, '127.0.0.1', resolve);
    });

    const address = server.address();
    if (!address || typeof address === 'string')
    {
        throw new Error('Unable to resolve local server address.');
    }

    const baseUrl = `http://127.0.0.1:${address.port}`;

    try
    {
        for (const name of names)
        {
            const outputPath = await captureReference(baseUrl, name);
            console.log(`Captured ${name} -> ${path.relative(repoRoot, outputPath)}`);
        }
    }
    finally
    {
        await new Promise(resolve => server.close(resolve));
    }
}

main().catch(error =>
{
    console.error(error);
    process.exitCode = 1;
});
