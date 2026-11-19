const LimiterThreshold = 0.95;
const LimiterRelease = 0.1;
const OpenCutoff = 20000;
const ButterworthQ = -3.0103;

const Play = 0;
const Release = 1;
const SetVoice = 2;
const StopVoice = 3;
const SetBus = 4;
const RemoveBus = 5;

const Gain = 0;
const Pan = 1;
const Pitch = 2;
const Cutoff = 1;

const sounds = new Map();
const outputs = new Map();
let nextOutput = 1;
let started = 0;
let gestured = false;

const canHold = typeof AudioParam !== 'undefined' && 'cancelAndHoldAtTime' in AudioParam.prototype;

function onGesture() {
    gestured = true;
    window.removeEventListener('pointerdown', onGesture, true);
    window.removeEventListener('keydown', onGesture, true);
    for (const output of outputs.values()) {
        output.context.resume();
    }
}

window.addEventListener('pointerdown', onGesture, true);
window.addEventListener('keydown', onGesture, true);

export function isAvailable() {
    return typeof AudioContext !== 'undefined';
}

export function createSound(handle, bytes, sampleRate) {
    const copy = bytes.slice();
    const samples = new Float32Array(copy.buffer, 0, copy.byteLength / 4);
    const buffer = new AudioBuffer({ length: Math.max(1, samples.length), sampleRate, numberOfChannels: 1 });
    buffer.copyToChannel(samples, 0);
    sounds.set(handle, buffer);
}

export function deleteSound(handle) {
    sounds.delete(handle);
}

export function open(sampleRate, latency, channels) {
    const options = { latencyHint: latency > 0 ? latency : 'interactive' };
    if (sampleRate > 0) {
        options.sampleRate = sampleRate;
    }

    const context = new AudioContext(options);
    if (channels < context.destination.channelCount) {
        context.destination.channelCount = channels;
    }
    const limiter = new DynamicsCompressorNode(context, {
        threshold: 20 * Math.log10(LimiterThreshold),
        knee: 0,
        ratio: 20,
        attack: 0,
        release: LimiterRelease,
    });
    limiter.connect(context.destination);

    const handle = nextOutput++;
    outputs.set(handle, { context, limiter, buses: new Map(), voices: new Map(), ended: [] });
    if (gestured) {
        context.resume();
    }

    return handle;
}

export function close(handle) {
    const output = outputs.get(handle);
    if (output) {
        outputs.delete(handle);
        output.context.close();
    }
}

export function state(handle) {
    return outputs.get(handle)?.context.state ?? 'closed';
}

export function sampleRate(handle) {
    return outputs.get(handle)?.context.sampleRate ?? 0;
}

export function channels(handle) {
    return outputs.get(handle)?.context.destination.channelCount ?? 0;
}

export function submit(handle, commands) {
    const output = outputs.get(handle);
    if (!output) {
        return [];
    }

    const time = output.context.currentTime;
    for (let i = 0; i < commands.length;) {
        switch (commands[i]) {
            case Play:
                play(output, commands[i + 1], commands[i + 2], commands[i + 3], commands[i + 4], commands[i + 5], commands[i + 6], commands[i + 7] !== 0);
                i += 8;
                break;
            case Release:
                release(output, commands[i + 1]);
                i += 2;
                break;
            case SetVoice:
                setVoice(output, commands[i + 1], commands[i + 2], commands[i + 3], time, commands[i + 4]);
                i += 5;
                break;
            case StopVoice:
                stopVoice(output, commands[i + 1], time, commands[i + 2]);
                i += 3;
                break;
            case SetBus:
                setBus(output, commands[i + 1], commands[i + 2], commands[i + 3], time, commands[i + 4]);
                i += 5;
                break;
            case RemoveBus:
                removeBus(output, commands[i + 1]);
                i += 2;
                break;
            default:
                throw new Error(`Unknown audio command ${commands[i]}`);
        }
    }

    const ended = output.ended;
    output.ended = [];
    return ended;
}

export function stats() {
    return { started, states: [...outputs.values()].map(output => output.context.state) };
}

function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
}

function rampTo(param, value, time, ramp) {
    if (ramp <= 0) {
        param.cancelScheduledValues(time);
        param.setValueAtTime(value, time);
        return;
    }

    if (canHold) {
        param.cancelAndHoldAtTime(time);
    } else {
        const current = param.value;
        param.cancelScheduledValues(time);
        param.setValueAtTime(current, time);
    }

    param.linearRampToValueAtTime(value, time + ramp);
}

function busFor(output, handle) {
    let bus = output.buses.get(handle);
    if (!bus) {
        const context = output.context;
        const input = new GainNode(context);
        const filter = new BiquadFilterNode(context, { type: 'lowpass', frequency: context.sampleRate / 2, Q: ButterworthQ });
        input.connect(filter).connect(output.limiter);
        bus = { input, filter, voices: new Set(), removing: false };
        output.buses.set(handle, bus);
    }

    bus.removing = false;
    return bus;
}

function removeBus(output, handle) {
    const bus = output.buses.get(handle);
    if (!bus) {
        return;
    }

    bus.removing = true;
    disposeBusIfDone(output, handle, bus);
}

function disposeBusIfDone(output, handle, bus) {
    if (bus.removing && bus.voices.size === 0) {
        bus.filter.disconnect();
        output.buses.delete(handle);
    }
}

function play(output, handle, soundHandle, busHandle, gain, pan, pitch, loop) {
    const bus = busFor(output, busHandle);
    const buffer = sounds.get(soundHandle);
    if (!buffer || output.context.state !== 'running') {
        output.ended.push(handle);
        return;
    }

    const context = output.context;
    const source = new AudioBufferSourceNode(context, { buffer, loop, playbackRate: Math.max(pitch, 0) });
    const amplifier = new GainNode(context, { gain });
    const panner = new StereoPannerNode(context, { pan: clamp(pan, -1, 1) });
    source.connect(amplifier).connect(panner).connect(bus.input);

    const voice = { source, gain: amplifier, panner, bus, busHandle, stopping: false };
    output.voices.set(handle, voice);
    bus.voices.add(handle);
    source.onended = () => end(output, handle);
    source.start();
    started++;
}

function end(output, handle) {
    const voice = output.voices.get(handle);
    if (!voice) {
        return;
    }

    output.voices.delete(handle);
    voice.panner.disconnect();
    voice.bus.voices.delete(handle);
    output.ended.push(handle);
    disposeBusIfDone(output, voice.busHandle, voice.bus);
}

function release(output, handle) {
    const voice = output.voices.get(handle);
    if (voice) {
        voice.source.loop = false;
    }
}

function setVoice(output, handle, parameter, value, time, ramp) {
    const voice = output.voices.get(handle);
    if (!voice) {
        return;
    }

    switch (parameter) {
        case Gain:
            if (!voice.stopping) {
                rampTo(voice.gain.gain, value, time, ramp);
            }
            break;
        case Pan:
            rampTo(voice.panner.pan, clamp(value, -1, 1), time, ramp);
            break;
        case Pitch:
            rampTo(voice.source.playbackRate, Math.max(value, 0), time, ramp);
            break;
    }
}

function stopVoice(output, handle, time, fade) {
    const voice = output.voices.get(handle);
    if (!voice) {
        return;
    }

    voice.stopping = true;
    rampTo(voice.gain.gain, 0, time, fade);
    voice.source.stop(time + Math.max(fade, 0));
}

function setBus(output, handle, parameter, value, time, ramp) {
    const bus = busFor(output, handle);
    if (parameter === Cutoff) {
        const cutoff = clamp(value, 10, OpenCutoff);
        rampTo(bus.filter.frequency, cutoff >= OpenCutoff ? output.context.sampleRate / 2 : cutoff, time, ramp);
    } else {
        rampTo(bus.input.gain, Math.max(value, 0), time, ramp);
    }
}
