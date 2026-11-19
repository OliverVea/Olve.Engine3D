import { dotnet } from './_framework/dotnet.js';

const Stopped = -1;
const Stepped = 1;

const canvas = document.querySelector('#canvas');
let host;
window.olveFrames = 0;
window.olveStopped = false;

function stop(error) {
    window.olveStopped = true;
    if (error) {
        throw error;
    }
}

function fitToWindow() {
    const pixelRatio = Math.min(devicePixelRatio, host.MaxPixelRatio());
    const width = Math.max(1, Math.round(canvas.clientWidth * pixelRatio));
    const height = Math.max(1, Math.round(canvas.clientHeight * pixelRatio));
    if (canvas.width !== width || canvas.height !== height) {
        canvas.width = width;
        canvas.height = height;
    }
}

function loop(timestamp) {
    fitToWindow();

    let state;
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
    }

    requestAnimationFrame(loop);
}

try {
    const { runMain, getAssemblyExports } = await dotnet.create();
    host = (await getAssemblyExports('Olve.Engine3D.Platform.Web')).Olve.Engine3D.Platform.Web.WebHost;
    await runMain();
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

canvas.addEventListener('mousemove', event => host.PointerMove(...toCanvasPixels(event)));
canvas.addEventListener('mousedown', event => {
    host.PointerMove(...toCanvasPixels(event));
    host.MouseButton(event.button, true);
});
window.addEventListener('mouseup', event => host.MouseButton(event.button, false));
canvas.addEventListener('wheel', event => {
    event.preventDefault();
    host.Wheel(-wheelNotches(event.deltaX, event.deltaMode), -wheelNotches(event.deltaY, event.deltaMode));
}, { passive: false });
canvas.addEventListener('contextmenu', event => event.preventDefault());
window.addEventListener('keydown', event => {
    if (event.code === 'Tab') {
        event.preventDefault();
    }

    if (!event.repeat) {
        host.Key(event.code, true);
    }
});
window.addEventListener('keyup', event => host.Key(event.code, false));
window.addEventListener('blur', () => host.Blur());

requestAnimationFrame(loop);
