import { dotnet } from './_framework/dotnet.js';

const status = document.getElementById('status');
const results = document.getElementById('results');
const rerun = document.getElementById('rerun');

function report(message, passed) {
    status.dataset.status = passed ? 'passed' : 'failed';
    status.textContent = passed ? 'PASS — all font cases' : 'FAIL — see details';
    results.textContent = message;
    document.title = `${passed ? 'PASS' : 'FAIL'} — Svg.Skia issue #565`;
}

try {
    const runtime = await dotnet.create();
    runtime.setModuleImports('fontDiagnostics', { report });
    const config = runtime.getConfig();
    const useInterDefault = new URLSearchParams(location.search).get('default') === 'inter';
    if (useInterDefault) {
        document.getElementById('font-setup').textContent = 'Issue #565 · WithInterFont() + explicit Avalonia Inter default';
    }
    await runtime.runMain(config.mainAssemblyName, useInterDefault ? ['inter-default'] : []);
    const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
    rerun.addEventListener('click', () => exports.AvaloniaSvgFontBrowser.Program.RunChecksAgain());
    rerun.disabled = false;
} catch (error) {
    report(error?.stack ?? String(error), false);
    console.error(error);
}
