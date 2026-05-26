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
const w3cTestSuiteTestsPath = path.join(repoRoot, 'tests', 'Svg.Skia.UnitTests', 'W3CTestSuiteTests.cs');
const animationSeekOverrides = await readAnimationSeekOverrides();
const preSeekInteractionScripts = new Map([
    ['animate-elem-52-t', `
      dispatchMouseEvent(doc, win, 'A', 'click');
      dispatchMouseEvent(doc, win, 'B', 'click');
      dispatchMouseEvent(doc, win, 'C', 'click');
    `],
]);
const interactionScripts = new Map([
    ['interact-dom-01-b', `
      dispatchMouseEvent(doc, win, 'startButton', 'click');
    `],
    ['script-handle-01-b', `
      dispatchMouseEvent(doc, win, 'target', 'click');
    `],
    ['script-handle-02-b', `
      dispatchEvent(doc, win, 'target', 'focusin');
      dispatchEvent(doc, win, 'target', 'activate');
      dispatchEvent(doc, win, 'target', 'focusout');
    `],
    ['script-handle-03-b', `
      dispatchMouseEvent(doc, win, 'target', 'mousedown');
      dispatchMouseEvent(doc, win, 'target', 'mouseup');
      dispatchMouseEvent(doc, win, 'target', 'click');
    `],
    ['script-handle-04-b', `
      dispatchMouseEvent(doc, win, 'target', 'mouseover');
      dispatchMouseEvent(doc, win, 'target', 'mousemove');
      dispatchMouseEvent(doc, win, 'target', 'mouseout');
    `],
]);

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

async function readAnimationSeekOverrides()
{
    const source = await fs.readFile(w3cTestSuiteTestsPath, 'utf8');
    const startMarker = '// W3C_ANIMATION_SEEK_TIMES_BEGIN';
    const endMarker = '// W3C_ANIMATION_SEEK_TIMES_END';
    const start = source.indexOf(startMarker);
    const end = source.indexOf(endMarker);

    if (start < 0 || end < 0 || end <= start)
    {
        throw new Error(`Unable to find W3C animation seek-time markers in ${w3cTestSuiteTestsPath}.`);
    }

    const tableSource = source.slice(start + startMarker.length, end);
    const seekTimes = new Map();
    const rowPattern = /^\s*\["([^"]+)"\]\s*=\s*([0-9]+(?:\.[0-9]+)?)/gm;
    let match;

    while ((match = rowPattern.exec(tableSource)) !== null)
    {
        const name = match[1];
        const seconds = Number(match[2]);

        if (seekTimes.has(name))
        {
            throw new Error(`Duplicate W3C animation seek time for ${name}.`);
        }

        seekTimes.set(name, seconds);
    }

    if (seekTimes.size < 1)
    {
        throw new Error(`No W3C animation seek times found in ${w3cTestSuiteTestsPath}.`);
    }

    return seekTimes;
}

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
    const animationSeekTime = animationSeekOverrides.get(name) ?? null;
    const preSeekInteractionScript = preSeekInteractionScripts.get(name) ?? '';
    const interactionScript = interactionScripts.get(name) ?? '';
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
  <script>
    const animationSeekTime = ${animationSeekTime === null ? 'null' : JSON.stringify(animationSeekTime)};
    function dispatchEvent(doc, win, elementId, eventType) {
      const target = doc.getElementById(elementId);
      if (!target) {
        throw new Error('Missing target: ' + elementId);
      }

      target.dispatchEvent(new win.Event(eventType, { bubbles: true, cancelable: true }));
    }

    function dispatchMouseEvent(doc, win, elementId, eventType) {
      const target = doc.getElementById(elementId);
      if (!target) {
        throw new Error('Missing target: ' + elementId);
      }

      target.dispatchEvent(new win.MouseEvent(eventType, {
        bubbles: true,
        cancelable: true,
        view: win,
        detail: 1,
        button: 0
      }));
    }

    const frame = document.getElementById('capture');
    function runWhenFrameReady(callback) {
      let didRun = false;
      const run = () => {
        if (didRun) {
          return;
        }

        didRun = true;
        callback();
      };
      const tryRun = () => {
        try {
          const doc = frame.contentDocument;
          const href = frame.contentWindow?.location?.href;
          if (href && href !== 'about:blank' && doc?.documentElement && (doc.readyState === undefined || doc.readyState === 'interactive' || doc.readyState === 'complete')) {
            run();
            return true;
          }
        } catch {
        }

        return false;
      };

      if (tryRun()) {
        return;
      }

      frame.addEventListener('load', () => {
        tryRun();
      }, { once: true });
      let attempts = 0;
      const retry = () => {
        if (tryRun() || ++attempts >= 40) {
          return;
        }

        setTimeout(retry, 50);
      };
      setTimeout(retry, 50);
    }

    let pendingCaptureReady = animationSeekTime !== null ? 2 : 1;
    function completeCaptureReadyPart() {
      pendingCaptureReady -= 1;
      if (pendingCaptureReady > 0 || document.getElementById('capture-ready')) {
        return;
      }

      const ready = document.createElement('div');
      ready.id = 'capture-ready';
      ready.style.position = 'absolute';
      ready.style.left = '-9999px';
      ready.style.top = '-9999px';
      ready.style.width = '1px';
      ready.style.height = '1px';
      document.body.appendChild(ready);
    }

    if (animationSeekTime !== null) {
      runWhenFrameReady(() => {
        try {
          const win = frame.contentWindow;
          const doc = frame.contentDocument;
          if (!win || !doc) {
            completeCaptureReadyPart();
            return;
          }

          ${preSeekInteractionScript}

          const svg = frame.contentDocument?.documentElement;
          if (svg && typeof svg.setCurrentTime === 'function') {
            const seek = () => {
              try {
                if (typeof svg.pauseAnimations === 'function') {
                  svg.pauseAnimations();
                }
                svg.setCurrentTime(animationSeekTime);
              } catch {
              }
            };

            seek();
            frame.contentWindow?.requestAnimationFrame(() => {
              seek();
              frame.contentWindow?.setTimeout(seek, 100);
              frame.contentWindow?.setTimeout(() => {
                seek();
                completeCaptureReadyPart();
              }, 500);
            });
          }
          else {
            completeCaptureReadyPart();
          }
        } catch {
          completeCaptureReadyPart();
        }
      });
    }

    runWhenFrameReady(() => {
      try {
        const win = frame.contentWindow;
        const doc = frame.contentDocument;
        if (!win || !doc) {
          completeCaptureReadyPart();
          return;
        }

        ${interactionScript}
        frame.contentWindow?.requestAnimationFrame(() => {
          frame.contentWindow?.setTimeout(completeCaptureReadyPart, 0);
        });
      } catch (error) {
        console.error(error);
        completeCaptureReadyPart();
      }
    });
  </script>
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
        '--wait-for-selector',
        '#capture-ready',
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
