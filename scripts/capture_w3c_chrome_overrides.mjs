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
const svgDir = path.join(repoRoot, 'externals', 'W3C_SVG_11_TestSuite', 'W3C_SVG_11_TestSuite', 'svg');
const outputDir = path.join(repoRoot, 'tests', 'Svg.Skia.UnitTests', 'ChromeReference', 'W3C');
const wrapperDir = path.join(repoRoot, 'output', 'playwright', 'w3c-capture');

const mimeTypes = new Map([
    ['.css', 'text/css; charset=utf-8'],
    ['.html', 'text/html; charset=utf-8'],
    ['.jpg', 'image/jpeg'],
    ['.jpeg', 'image/jpeg'],
    ['.js', 'application/javascript; charset=utf-8'],
    ['.png', 'image/png'],
    ['.svg', 'image/svg+xml; charset=utf-8'],
    ['.txt', 'text/plain; charset=utf-8'],
]);

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

async function writeWrapper(name)
{
    const wrapperPath = path.join(wrapperDir, `${name}.html`);
    const svgUrl = `/externals/W3C_SVG_11_TestSuite/W3C_SVG_11_TestSuite/svg/${encodeURIComponent(name)}.svg`;
    const html = `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <style>
    html, body {
      margin: 0;
      padding: 0;
      width: 480px;
      height: 360px;
      overflow: hidden;
      background: white;
    }
    #capture {
      width: 480px;
      height: 360px;
      border: 0;
      display: block;
      background: white;
    }
  </style>
</head>
<body>
  <iframe id="capture" src="${svgUrl}" title="${name}"></iframe>
</body>
</html>`;

    await fs.mkdir(wrapperDir, { recursive: true });
    await fs.writeFile(wrapperPath, html);
    return wrapperPath;
}

async function captureOverride(baseUrl, name)
{
    const svgPath = path.join(svgDir, `${name}.svg`);
    const outputPath = path.join(outputDir, `${name}.png`);
    await fs.access(svgPath);

    const wrapperPath = await writeWrapper(name);
    const wrapperUrl = `${baseUrl}/${path.relative(repoRoot, wrapperPath).split(path.sep).map(encodeURIComponent).join('/')}`;

    const args = [
        'playwright',
        'screenshot',
        '--channel',
        'chrome',
        '--viewport-size',
        '480,360',
        '--wait-for-timeout',
        '1500',
        '--timeout',
        '30000',
        wrapperUrl,
        outputPath,
    ];

    await execFileAsync('npx', args, { cwd: repoRoot });
    return outputPath;
}

async function main()
{
    const names = await getNames();
    if (names.length < 1)
    {
        throw new Error(`No Chrome override targets found in ${outputDir}.`);
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
