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
const resvgTestsDir = path.join(repoRoot, 'externals', 'resvg', 'crates', 'resvg', 'tests');
const svgDir = path.join(resvgTestsDir, 'tests');
const outputDir = path.join(repoRoot, 'tests', 'Svg.Skia.UnitTests', 'ChromeReference', 'resvg');
const wrapperDir = path.join(repoRoot, 'output', 'playwright', 'resvg-capture');
const scale = 1.5;

const mimeTypes = new Map([
    ['.css', 'text/css; charset=utf-8'],
    ['.html', 'text/html; charset=utf-8'],
    ['.js', 'application/javascript; charset=utf-8'],
    ['.png', 'image/png'],
    ['.svg', 'image/svg+xml; charset=utf-8'],
    ['.ttf', 'font/ttf'],
    ['.txt', 'text/plain; charset=utf-8'],
]);

const fontCss = `
@font-face { font-family: "Noto Sans"; src: url("/externals/resvg/crates/resvg/tests/fonts/NotoSans-Regular.ttf") format("truetype"); }
@font-face { font-family: "Noto Sans"; font-weight: 300; src: url("/externals/resvg/crates/resvg/tests/fonts/NotoSans-Light.ttf") format("truetype"); }
@font-face { font-family: "Noto Sans"; font-weight: 700; src: url("/externals/resvg/crates/resvg/tests/fonts/NotoSans-Bold.ttf") format("truetype"); }
@font-face { font-family: "Noto Sans"; font-weight: 900; src: url("/externals/resvg/crates/resvg/tests/fonts/NotoSans-Black.ttf") format("truetype"); }
@font-face { font-family: "Noto Sans"; font-style: italic; src: url("/externals/resvg/crates/resvg/tests/fonts/NotoSans-Italic.ttf") format("truetype"); }
@font-face { font-family: "Noto Serif"; src: url("/externals/resvg/crates/resvg/tests/fonts/NotoSerif-Regular.ttf") format("truetype"); }
@font-face { font-family: "Noto Mono"; src: url("/externals/resvg/crates/resvg/tests/fonts/NotoMono-Regular.ttf") format("truetype"); }
@font-face { font-family: "Amiri"; src: url("/externals/resvg/crates/resvg/tests/fonts/Amiri-Regular.ttf") format("truetype"); }
@font-face { font-family: "M PLUS 1p"; src: url("/externals/resvg/crates/resvg/tests/fonts/MPLUS1p-Regular.ttf") format("truetype"); }
@font-face { font-family: "Noto Color Emoji"; src: url("/externals/resvg/crates/resvg/tests/fonts/NotoColorEmojiCOLR.subset.ttf") format("truetype"); }
@font-face { font-family: "Sedgwick Ave Display"; src: url("/externals/resvg/crates/resvg/tests/fonts/SedgwickAveDisplay-Regular.ttf") format("truetype"); }
@font-face { font-family: "Source Sans Pro"; src: url("/externals/resvg/crates/resvg/tests/fonts/SourceSansPro-Regular.ttf") format("truetype"); }
@font-face { font-family: "Yellowtail"; src: url("/externals/resvg/crates/resvg/tests/fonts/Yellowtail-Regular.ttf") format("truetype"); }
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
            const requestPath = new URL(req.url ?? '/', 'http://127.0.0.1').pathname;
            const safePath = requestPath === '/'
                ? path.join(rootPath, 'index.html')
                : path.join(rootPath, decodeURIComponent(requestPath));
            const normalizedPath = path.normalize(safePath);

            if (!normalizedPath.startsWith(rootPath))
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

async function getNames()
{
    const cliNames = process.argv.slice(2)
        .flatMap(arg => arg.split(','))
        .map(name => name.trim())
        .filter(Boolean)
        .map(name => name.endsWith('.png') ? name.slice(0, -4) : name);

    if (cliNames.length > 0)
    {
        return cliNames;
    }

    const entries = await fs.readdir(outputDir, { withFileTypes: true });
    return entries
        .filter(entry => entry.isFile() && entry.name.endsWith('.png'))
        .map(entry => entry.name.slice(0, -4))
        .sort((left, right) => left.localeCompare(right, 'en'));
}

function parseFirstFloat(value)
{
    if (!value)
    {
        return null;
    }

    const match = value.match(/[+-]?(?:\d+\.\d*|\d+|\.\d+)(?:[eE][+-]?\d+)?/);
    return match ? Number.parseFloat(match[0]) : null;
}

function getViewport(svgMarkup)
{
    const rootTagMatch = svgMarkup.match(/<svg\b([^>]*)>/i);
    const rootTag = rootTagMatch?.[1] ?? '';
    const widthMatch = rootTag.match(/\bwidth\s*=\s*["']([^"']+)["']/i);
    const heightMatch = rootTag.match(/\bheight\s*=\s*["']([^"']+)["']/i);
    const width = parseFirstFloat(widthMatch?.[1]);
    const height = parseFirstFloat(heightMatch?.[1]);
    if (width && height)
    {
        return { width, height };
    }

    const viewBoxMatch = rootTag.match(/\bviewBox\s*=\s*["']([^"']+)["']/i);
    if (viewBoxMatch)
    {
        const parts = viewBoxMatch[1]
            .split(/[\s,]+/)
            .map(part => Number.parseFloat(part))
            .filter(Number.isFinite);
        if (parts.length === 4 && parts[2] > 0 && parts[3] > 0)
        {
            return { width: parts[2], height: parts[3] };
        }
    }

    return { width: 200, height: 200 };
}

function injectFonts(svgMarkup)
{
    return svgMarkup.replace(/<svg\b[^>]*>/i, match => `${match}\n<style>${fontCss}</style>`);
}

async function writeWrapper(name)
{
    const svgPath = getSvgPath(name);
    const rawSvg = await fs.readFile(svgPath, 'utf8');
    const svgMarkup = injectFonts(rawSvg);
    const viewport = getViewport(svgMarkup);
    const width = Math.max(1, Math.ceil(viewport.width * scale));
    const height = Math.max(1, Math.ceil(viewport.height * scale));
    const wrapperPath = path.join(wrapperDir, `${getSafeName(name)}.html`);
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

    await fs.mkdir(wrapperDir, { recursive: true });
    await fs.writeFile(wrapperPath, html);
    return { wrapperPath, width, height };
}

async function captureOverride(baseUrl, name)
{
    const svgPath = getSvgPath(name);
    const outputPath = path.join(outputDir, `${name}.png`);
    await fs.access(svgPath);

    const { wrapperPath, width, height } = await writeWrapper(name);
    const wrapperUrl = `${baseUrl}/${path.relative(repoRoot, wrapperPath).split(path.sep).map(encodeURIComponent).join('/')}`;

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

function getSvgPath(name)
{
    return name.startsWith('extra/')
        ? path.join(resvgTestsDir, `${name}.svg`)
        : path.join(svgDir, `${name}.svg`);
}

function getSafeName(name)
{
    return name.replace(/[\\/:%=]/g, '_');
}

async function main()
{
    const names = await getNames();
    if (names.length < 1)
    {
        throw new Error(`No resvg Chrome override targets found in ${outputDir}. Pass names explicitly.`);
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
            const outputPath = await captureOverride(baseUrl, name);
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
