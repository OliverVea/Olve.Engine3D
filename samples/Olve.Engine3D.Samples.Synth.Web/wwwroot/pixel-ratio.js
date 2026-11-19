// A device whose GPU can't keep up draws fewer pixels: once the page has settled, whenever frames come well under the
// screen's rate while the frame's own work leaves time over (so the GPU, not the code, holds it back), the pixel ratio
// steps down, as far as one canvas pixel per CSS pixel. A step down that brings no faster frames shows the screen itself
// refreshes slower than thought (90 Hz to 60 Hz on some phones): the step is undone and the screen's rate taken anew.
// After a long run of frames at the screen's rate it steps back up, but not to a ratio found too slow in the last
// minute, twice as long each time that ratio is found too slow again, so the picture doesn't pump; a stall that stepped
// it down passes. Pure, with time only from the timestamps it is given, so a check can drive it with a made-up clock.
const SettleMilliseconds = 2000;
const WindowMilliseconds = 1000;
const SlowFrames = 1.4;
const IdleWork = 0.6;
const PixelRatioStep = 0.25;
const SteadyFrames = 1.15;
const RecoverMilliseconds = 10_000;
const DistrustMilliseconds = 60_000;
// A step down that leaves frames at least this slow brought nothing
const NoGain = 0.92;

// A gap longer than this is the page hidden or stalled, not a slow frame
const GapMilliseconds = 1000;

// The refresh is the least, over the windows, of a low percentile of each window's times between frames: one early or
// jittery frame doesn't set it, and frames slowed by the GPU don't raise it. It starts afresh on another screen (a new
// device pixel ratio), and when a step down shows the screen slowed.
const RefreshPercentile = 0.1;
const RefreshGaps = 10;

/**
 * The pixel ratio policy. `fullRatio()` is the ratio the page draws at when the GPU keeps up, `screen()` the device's
 * pixel ratio (a new one is another screen). Call `gap` on every animation frame, `frame` on each frame the page steps,
 * `away` when the page is hidden or shown, and read `ratio()` before sizing the canvas.
 */
export function createPixelRatioPolicy({ fullRatio, screen, log = () => {}, show = () => {} }) {
    let cap = Infinity;
    // When each pixel ratio was last found too slow and how many times, and since when frames have kept up with the screen
    const slowAt = new Map();
    const slowTimes = new Map();
    let steadySince = null;
    // The last step down, until the window after it says whether it brought faster frames
    let stepped = null;

    // The frames in the current window: how many, the time between them and spent in them, the longest of each; and the
    // screen's refresh, from the times between animation frames
    const frames = { started: null, since: 0, count: 0, interval: 0, work: 0, longestInterval: 0, longestWork: 0, refresh: Infinity, last: null, any: null };
    const gaps = [];
    let windowRefresh = Infinity;
    let refreshScreen = screen();

    const ratio = () => Math.min(fullRatio(), cap);

    function estimateRefresh() {
        if (screen() !== refreshScreen) {
            refreshScreen = screen();
            frames.refresh = Infinity;
        }

        if (gaps.length >= RefreshGaps) {
            gaps.sort((a, b) => a - b);
            windowRefresh = gaps[Math.floor((gaps.length - 1) * RefreshPercentile)];
            frames.refresh = Math.min(frames.refresh, windowRefresh);
            gaps.length = 0;
        }
    }

    function restartWindow(timestamp) {
        Object.assign(frames, { since: timestamp, count: 0, interval: 0, work: 0, longestInterval: 0, longestWork: 0 });
    }

    return {
        ratio,

        /** The cap on the pixel ratio, Infinity when there is none, and the screen's refresh as estimated, for checks. */
        state: () => ({ cap, refresh: frames.refresh }),

        /** Every animation frame, whether or not the page steps in it. */
        gap(timestamp) {
            const gap = frames.any === null ? null : timestamp - frames.any;
            frames.any = timestamp;
            if (gap !== null && gap >= 4 && gap < GapMilliseconds) {
                gaps.push(gap);
            }
        },

        /** Coming back to the page starts measuring afresh, so the time away doesn't count as a slow frame. */
        away(timestamp) {
            frames.last = null;
            frames.any = null;
            restartWindow(timestamp);
        },

        /** A frame the page stepped, and how long its work took. */
        frame(timestamp, work) {
            const since = frames.last === null ? null : timestamp - frames.last;
            if (since !== null && since < GapMilliseconds) {
                frames.count++;
                frames.interval += since;
                frames.work += work;
                frames.longestInterval = Math.max(frames.longestInterval, since);
                frames.longestWork = Math.max(frames.longestWork, work);
            }

            frames.last = timestamp;
            frames.started ??= timestamp;
            if (timestamp - frames.since < WindowMilliseconds || frames.count === 0) {
                return;
            }

            const interval = frames.interval / frames.count;
            const meanWork = frames.work / frames.count;
            estimateRefresh();
            const step = stepped;
            stepped = null;
            const now = ratio();
            if (step && interval >= step.interval * NoGain) {
                // Fewer pixels, no faster: the screen refreshes slower than thought
                frames.refresh = windowRefresh;
                cap = step.ratio >= fullRatio() ? Infinity : step.ratio;
                slowTimes.set(step.ratio, (slowTimes.get(step.ratio) ?? 1) - 1);
                slowAt.delete(step.ratio);
                log(`Frames take ${interval.toFixed(1)} ms at fewer pixels too: the screen refreshes every ${frames.refresh.toFixed(1)} ms, drawing at ${ratio()} pixels per CSS pixel`);
            } else if (timestamp - frames.started > SettleMilliseconds && interval > frames.refresh * SlowFrames && meanWork < interval * IdleWork
                && now > 1) {
                slowAt.set(now, timestamp);
                slowTimes.set(now, (slowTimes.get(now) ?? 0) + 1);
                cap = Math.max(1, now - PixelRatioStep);
                stepped = { ratio: now, interval };
                steadySince = null;
                log(`Frames take ${interval.toFixed(1)} ms at ${frames.refresh.toFixed(1)} ms a refresh: drawing at ${cap} pixels per CSS pixel`);
            } else if (cap !== Infinity && interval <= frames.refresh * SteadyFrames) {
                steadySince ??= timestamp;
                const up = cap + PixelRatioStep;
                const distrust = DistrustMilliseconds * 2 ** Math.max(0, (slowTimes.get(up) ?? 0) - 1);
                if (timestamp - steadySince > RecoverMilliseconds && !(timestamp - (slowAt.get(up) ?? -Infinity) < distrust)) {
                    cap = up >= fullRatio() ? Infinity : up;
                    steadySince = null;
                    log(`Frames keep up at ${frames.refresh.toFixed(1)} ms a refresh: drawing at ${cap} pixels per CSS pixel`);
                }
            } else {
                steadySince = null;
            }

            show({ interval, work: meanWork, longestInterval: frames.longestInterval, longestWork: frames.longestWork, refresh: frames.refresh });
            restartWindow(timestamp);
        },
    };
}
