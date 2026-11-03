#!/usr/bin/env node

// Runs the published WebAssembly application in CI or a local development browser.
// Install the pinned Playwright version documented in the sample README first.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import http from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const options = new Map();
for (let i = 2; i < process.argv.length; i += 2) {
    assert.ok(['--root', '--output', '--timeout'].includes(process.argv[i]), `Unknown option: ${process.argv[i]}`);
    assert.ok(process.argv[i + 1], `Missing value for ${process.argv[i]}`);
    options.set(process.argv[i], process.argv[i + 1]);
}

const root = path.resolve(repoRoot, options.get('--root') ?? 'artifacts/avalonia-browser-fonts/publish/wwwroot');
const output = path.resolve(repoRoot, options.get('--output') ?? 'artifacts/avalonia-browser-fonts/results');
const timeout = Number(options.get('--timeout') ?? 120_000);
assert.ok(Number.isFinite(timeout) && timeout > 0, '--timeout must be a positive number of milliseconds');
await fs.mkdir(output, { recursive: true });

const contentTypes = new Map([
    ['.html', 'text/html; charset=utf-8'],
    ['.css', 'text/css; charset=utf-8'],
    ['.js', 'text/javascript; charset=utf-8'],
    ['.mjs', 'text/javascript; charset=utf-8'],
    ['.json', 'application/json'],
    ['.wasm', 'application/wasm'],
    ['.svg', 'image/svg+xml'],
    ['.png', 'image/png'],
]);

const server = http.createServer(async (request, response) => {
    try {
        if (request.method !== 'GET' && request.method !== 'HEAD') {
            response.writeHead(405).end();
            return;
        }
        const url = new URL(request.url ?? '/', 'http://localhost');
        const requestedPath = decodeURIComponent(url.pathname);
        const file = path.resolve(root, `.${requestedPath === '/' ? '/index.html' : requestedPath}`);
        const relative = path.relative(root, file);
        if (relative.startsWith(`..${path.sep}`) || relative === '..' || path.isAbsolute(relative)) {
            response.writeHead(403).end();
            return;
        }
        const data = await fs.readFile(file);
        response.writeHead(200, {
            'Content-Type': contentTypes.get(path.extname(file)) ?? 'application/octet-stream',
            'Content-Length': data.length,
            'Cache-Control': 'no-store',
        });
        response.end(request.method === 'HEAD' ? undefined : data);
    } catch (error) {
        response.writeHead(error.code === 'ENOENT' || error.code === 'EISDIR' ? 404 : 500).end();
    }
});

const report = { startedAt: new Date().toISOString(), root, runs: [], errors: [] };
let browser;
let failed = false;

try {
    await fs.access(path.join(root, 'index.html'));
    await new Promise((resolve, reject) => {
        server.once('error', reject);
        server.listen(0, '127.0.0.1', resolve);
    });
    const baseUrl = `http://127.0.0.1:${server.address().port}/`;
    browser = await chromium.launch({ headless: true });
    report.browserVersion = browser.version();

    for (const mode of [{ name: 'registered-inter', query: '' }, { name: 'inter-default', query: '?default=inter' }]) {
        const page = await browser.newPage({ viewport: { width: 1440, height: 960 }, deviceScaleFactor: 1 });
        page.setDefaultTimeout(timeout);
        const result = { mode: mode.name, console: [], pageErrors: [], requestErrors: [], snapshots: [] };
        report.runs.push(result);
        page.on('console', message => result.console.push({ level: message.type(), text: message.text() }));
        page.on('pageerror', error => result.pageErrors.push(error.stack ?? String(error)));
        page.on('requestfailed', request => result.requestErrors.push({ url: request.url(), failure: request.failure() }));
        page.on('response', response => {
            if (response.status() >= 400) {
                result.requestErrors.push({ url: response.url(), status: response.status() });
            }
        });

        try {
            await page.goto(baseUrl + mode.query, { waitUntil: 'domcontentloaded', timeout });
            await page.locator('#status[data-status="passed"], #status[data-status="failed"]').waitFor();
            await checkResult(page, result, 1);
            await page.locator('#out canvas').first().waitFor({ state: 'visible' });
            await settleFrames(page);
            await page.screenshot({ path: path.join(output, `${mode.name}.png`), fullPage: true });

            await page.getByRole('button', { name: 'Run font checks again' }).click();
            await page.waitForFunction(() => document.getElementById('results')?.textContent?.startsWith('Run 2:'), null, { timeout });
            await checkResult(page, result, 2);
            await settleFrames(page);
            await page.screenshot({ path: path.join(output, `${mode.name}-reload.png`), fullPage: true });
            assert.equal(result.pageErrors.length, 0, `Unhandled browser errors: ${result.pageErrors.join('\n')}`);
            assert.equal(result.requestErrors.length, 0, `Failed browser requests: ${JSON.stringify(result.requestErrors)}`);
            result.passed = true;
            console.log(`PASS ${mode.name}: initial rendering and disposal/reload`);
        } catch (error) {
            failed = true;
            result.passed = false;
            result.error = error.stack ?? String(error);
            result.failureDom = await readDom(page).catch(() => null);
            await page.screenshot({ path: path.join(output, `${mode.name}-failure.png`), fullPage: true }).catch(() => {});
            console.error(`FAIL ${mode.name}: ${error.message}`);
        } finally {
            await page.close();
        }
    }
} catch (error) {
    failed = true;
    report.errors.push(error.stack ?? String(error));
    console.error(error);
} finally {
    await browser?.close().catch(error => {
        failed = true;
        report.errors.push(error.stack ?? String(error));
    });
    server.closeAllConnections();
    if (server.listening) {
        await new Promise(resolve => server.close(resolve));
    }
    report.passed = !failed;
    report.finishedAt = new Date().toISOString();
    await fs.writeFile(path.join(output, 'browser-font-results.json'), JSON.stringify(report, null, 2) + '\n');
}

process.exitCode = failed ? 1 : 0;

async function readDom(page) {
    return page.evaluate(() => ({
        url: location.href,
        title: document.title,
        status: document.getElementById('status')?.dataset.status ?? null,
        text: document.getElementById('results')?.innerText ?? '',
        setup: document.getElementById('font-setup')?.innerText ?? '',
    }));
}

async function checkResult(page, result, expectedRun) {
    const dom = await readDom(page);
    result.snapshots.push(dom);
    assert.equal(dom.status, 'passed', dom.text);
    const counts = dom.text.match(/^Run (\d+): (\d+)\/(\d+) font cases passed in WebAssembly\./);
    assert.ok(counts, `Missing managed validation counts: ${dom.text}`);
    assert.equal(Number(counts[1]), expectedRun);
    assert.equal(counts[2], counts[3], dom.text);
    assert.ok(Number(counts[3]) >= 8, `Expected at least eight font cases: ${dom.text}`);
    assert.equal((dom.text.match(/^PASS  /gm) ?? []).length, Number(counts[3]), dom.text);
    assert.ok(!/^FAIL  /m.test(dom.text), dom.text);
    if (result.mode === 'inter-default') {
        assert.match(dom.text, /Avalonia default: .*Inter/, 'The explicit Inter default was not applied.');
    }
}

async function settleFrames(page) {
    await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
}
