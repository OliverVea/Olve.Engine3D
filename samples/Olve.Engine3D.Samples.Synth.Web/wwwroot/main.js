import { dotnet } from './_framework/dotnet.js';
import { createPixelRatioPolicy } from './pixel-ratio.js';

const Stopped = -1;
const Stepped = 1;

const canvas = document.querySelector('#canvas');
let host;
let synthInput;
let debug = null;
window.olveFrames = 0;
window.olveStopped = false;

function stop(error) {
    window.olveStopped = true;
    if (error) {
        throw error;
    }
}

// The canvas is drawn at least twice as dense as the page, up to the host's cap: on a screen of one pixel per CSS pixel the
// browser scales it down, which smooths the panels' small text and the thin cables as supersampling would
const MinPixelRatio = 2;

// A device whose GPU can't keep up draws fewer pixels (pixel-ratio.js)
let pixelRatio = 1;
const policy = createPixelRatioPolicy({
    fullRatio: () => Math.min(Math.max(devicePixelRatio, MinPixelRatio), host.MaxPixelRatio()),
    screen: () => devicePixelRatio,
    log: message => console.info(message),
    show: frame => debug?.show(frame),
});

function fitToWindow() {
    pixelRatio = policy.ratio();
    const width = Math.max(1, Math.round(canvas.clientWidth * pixelRatio));
    const height = Math.max(1, Math.round(canvas.clientHeight * pixelRatio));
    if (canvas.width !== width || canvas.height !== height) {
        canvas.width = width;
        canvas.height = height;
    }
}

// Coming back to the page starts measuring afresh, so the time away doesn't count as a slow frame
document.addEventListener('visibilitychange', () => policy.away(performance.now()));

function loop(timestamp) {
    policy.gap(timestamp);
    fitToWindow();

    let state;
    const started = performance.now();
    try {
        state = host.Frame(timestamp, canvas.width, canvas.height);
    } catch (error) {
        stop(error);
    }

    if (state === Stopped) {
        stop();
        return;
    }

    if (state === Stepped) {
        window.olveFrames++;
        policy.frame(timestamp, performance.now() - started);
    }

    requestAnimationFrame(loop);
}

// ?debug: a readout of what makes a note late, to read on a phone: the frames, the canvas they fill, the audio output's
// own latency, how long each key or touch took from the event to its note starting in synth.js, and what reading the
// level meters costs a frame
function createDebug(synth) {
    const readout = document.createElement('pre');
    readout.id = 'debug';
    readout.style.cssText = 'position:fixed;left:0;top:0;margin:0;padding:6px 8px;font:12px/1.35 monospace;color:#cfe;'
        + 'background:rgba(0,0,0,0.65);pointer-events:none;z-index:1;white-space:pre';
    document.body.append(readout);
    // Per input that started a note: from the event to its handler running, and on to the note starting
    const delays = [];
    const metered = { calls: 0, total: 0 };
    const ms = seconds => Number.isFinite(seconds) ? `${(seconds * 1000).toFixed(0)} ms` : '?';
    return {
        // After an input handler that began at `handled`: the note it started, if any, timed from when the event happened
        noted(event, handled) {
            if (synth.timing.noteOn >= handled) {
                delays.push([handled - event.timeStamp, synth.timing.noteOn - handled]);
                delays.splice(0, delays.length - 10);
            }
        },
        show({ interval, work, longestInterval, longestWork, refresh }) {
            // The level meters' reading, a frame's worth on average since the last readout, and the longest
            const meters = () => {
                const { calls, total: spent, longest } = synth.meterTiming;
                const line = calls > metered.calls
                    ? `${((spent - metered.total) / (calls - metered.calls)).toFixed(2)} ms a frame, max ${longest.toFixed(1)}`
                    : '-';
                Object.assign(metered, { calls, total: spent });
                return line;
            };
            const [base, output] = synth.latencies();
            const last = delays.at(-1);
            const total = ([reached, played]) => reached + played;
            readout.textContent = [
                `frame ${interval.toFixed(1)} ms (${(1000 / interval).toFixed(0)} fps, refresh ${refresh.toFixed(1)}), max ${longestInterval.toFixed(0)}`,
                `work  ${work.toFixed(1)} ms, max ${longestWork.toFixed(0)}`,
                `canvas ${canvas.width}x${canvas.height} at ${pixelRatio.toFixed(2)} (dpr ${devicePixelRatio.toFixed(2)})`,
                `audio ${synth.state()}, base ${ms(base)}, output ${ms(output)}`,
                `input->note ${last === undefined ? '-' : `${total(last).toFixed(1)} ms (to the page ${last[0].toFixed(1)}, `
                    + `to the note ${last[1].toFixed(1)}), max ${Math.max(...delays.map(total)).toFixed(1)} of ${delays.length}`}`,
                `meters ${meters()}`,
            ].join('\n');
        },
    };
}

try {
    const { runMain, getAssemblyExports } = await dotnet.create();
    host = (await getAssemblyExports('Olve.Engine3D.Platform.Web')).Olve.Engine3D.Platform.Web.WebHost;
    const sample = (await getAssemblyExports('Olve.Engine3D.Samples.Synth.Web')).Olve.Engine3D.Samples.Synth.Web;
    synthInput = sample.SynthInput;
    // For scripts/check-web.py --synth
    window.synthChecks = sample.SynthChecks;
    await runMain();
    if (new URLSearchParams(location.search).has('debug')) {
        debug = createDebug(await import('./synth.js'));
    }
} catch (error) {
    stop(error);
}

function toCanvasPixels(event) {
    return [event.offsetX * canvas.width / canvas.clientWidth, event.offsetY * canvas.height / canvas.clientHeight];
}

const WheelPixelsPerNotch = 100;
const WheelLinesPerNotch = 3;

function wheelNotches(delta, mode) {
    return delta / (mode === WheelEvent.DOM_DELTA_PIXEL ? WheelPixelsPerNotch : mode === WheelEvent.DOM_DELTA_LINE ? WheelLinesPerNotch : 1);
}

// Pointer events cover the mouse, touch and pens alike. Each finger is a pointer of its own, from touching to lifting,
// so several play at once; the mouse and a pen drive the main pointer. A pointer is captured, so a drag that leaves the
// canvas still ends there. Presses, releases and keys also go to SynthInput, which plays their notes now rather than at
// the next frame.
const isTouch = event => event.pointerType === 'touch';
const MainButton = 0;

canvas.addEventListener('pointerdown', event => {
    const handled = performance.now();
    if (isTouch(event)) {
        canvas.setPointerCapture(event.pointerId);
        const at = toCanvasPixels(event);
        host.TouchStart(event.pointerId, ...at);
        synthInput.Press(true, event.pointerId, ...at);
        debug?.noted(event, handled);
        return;
    }

    if (!event.isPrimary) {
        return;
    }

    canvas.setPointerCapture(event.pointerId);
    const at = toCanvasPixels(event);
    host.PointerMove(...at);
    host.MouseButton(event.button, true);
    if (event.button === MainButton) {
        synthInput.Press(false, 0, ...at);
        debug?.noted(event, handled);
    }
});
canvas.addEventListener('pointermove', event => {
    if (isTouch(event)) {
        host.TouchMove(event.pointerId, ...toCanvasPixels(event));
    } else if (event.isPrimary) {
        host.PointerMove(...toCanvasPixels(event));
    }
});
for (const type of ['pointerup', 'pointercancel']) {
    canvas.addEventListener(type, event => {
        if (isTouch(event)) {
            host.TouchEnd(event.pointerId);
            synthInput.Release(true, event.pointerId);
        } else if (event.isPrimary) {
            const button = Math.max(0, event.button);
            host.MouseButton(button, false);
            if (button === MainButton) {
                synthInput.Release(false, 0);
            }
        }
    });
}
canvas.addEventListener('wheel', event => {
    event.preventDefault();
    host.Wheel(-wheelNotches(event.deltaX, event.deltaMode), -wheelNotches(event.deltaY, event.deltaMode));
}, { passive: false });
canvas.addEventListener('contextmenu', event => event.preventDefault());
window.addEventListener('keydown', event => {
    const handled = performance.now();
    if (event.code === 'Tab') {
        event.preventDefault();
    }

    if (!event.repeat) {
        host.Key(event.code, true);
        synthInput.Key(event.code, true);
        debug?.noted(event, handled);
    }
});
window.addEventListener('keyup', event => {
    host.Key(event.code, false);
    synthInput.Key(event.code, false);
});
window.addEventListener('blur', () => host.Blur());

requestAnimationFrame(loop);
