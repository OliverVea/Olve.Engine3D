// The synth's patch, heard live through Web Audio: each module is a few native audio nodes, each connection one from an
// output node into an input node or an AudioParam (which adds the signal to the parameter's own value). The C# side owns
// the patch and calls these functions as it changes; nothing is rendered ahead. Every connection is a cable.
//
// Pitch signals are in octaves from middle C. Inputs that take pitch scale it to cents and drive a detune parameter, so
// one octave of signal moves an oscillator or a filter cutoff by one octave.
//
// Every knob is a signal too: its turn, from -1 all the way left to 1 all the way right, plus whatever is plugged into its
// CV input, looked up in a table of the knob's values that the C# side sends (Knobs.Curve). The table stops at its ends, so
// a knob turned past them by a signal stays at its minimum or maximum. The knob's value then drives an AudioParam, or, for
// a switch like a waveform, crossfades between the choices, so a signal sweeps smoothly from one to the next. A module may
// take a knob's CV input another way, such as the filter's cutoff, swept by its MOD in octaves.
//
// A voiced module (Voicing.cs) is built once per voice, knobs and all, so the keyboard plays chords. A cable joins voice
// to voice between voiced modules, fans a shared module's signal out to every voice, and sums the voices into a shared one.
//
// From the phaser on the signal is stereo: a connection carries as many channels as its output has, and a mono signal into
// a stereo stage is heard on both sides. The output takes the left side from IN L and the right from IN R.
//
// A sequencer, and RANDOM on its own clock, keep time on the audio clock: a timer looks a little ahead and schedules each
// step at the exact time it falls, so the steps keep time however the page's frames and timers stutter.
//
// What native nodes can't do, a slew that rises and falls at different rates, runs in an audio worklet (synth-worklets.js),
// loaded before any module is built. Without worklets (an insecure page) SLEW passes its input through and FOLLOW smooths
// it with a low-pass instead.

const MiddleC = 261.6256;
const CentsPerOctave = 1200;
const CentsPerSemitone = 100;
const Smoothing = 0.015;
const GateSmoothing = 0.003;
// How long a knob's glide takes to all but finish, so what it turns out can stop after it
const FadeOut = 10 * Smoothing;
const LimiterThreshold = 0.89;
// After the limiter, which can let the start of a sudden peak through, a soft clip: straight up to its knee, then
// bending smoothly towards full scale, which a peak of twice full scale only just reaches
const SoftClipKnee = 0.8;
const SoftClipSpan = 2;
const Waves = ['sine', 'square', 'sawtooth', 'triangle'];
const FilterTypes = ['lowpass', 'highpass'];
// Odd, so the middle of a table is exact
const TablePoints = 1025;
// The drive's clipping curve, how much the folder's table spans (the most the amount pushes), the table's points for the
// folds to stay smooth, and how far it turns the shaped signal down to sit level with the dry
const DriveCurveSharpness = 3;
const FoldSpan = 40;
const FoldPoints = 8193;
const DriveTrim = 0.6;
const DriveDarken = 9000;
// The VCA's level table spans this either way, so an envelope on a turned-up knob isn't clipped
const LevelSpan = 4;
// The phaser's allpass stages, the centre of their sweep, and how far its LFO sweeps them either way, in cents
const PhaserStages = 4;
const PhaserCentre = 800;
const PhaserSweep = 2400;
// The delay's longest time, its mod LFO's rate, and how far it sweeps the time at full MOD, in seconds
const MaxDelay = 2;
const DelayModRate = 0.6;
const DelayModSweep = 0.006;
// The repeats of an echo lose their highs, as on tape
const DelayDamping = 4500;
// The reverb's two rooms, by how long their tails take to die away, in seconds
const ShortRoom = 0.8;
const LongRoom = 3.5;
// RANDOM's noise, looped, in seconds
const NoiseLength = 2;
// FOLLOW's smoothing without worklets, in hertz
const FollowFallback = 12;

// The sequencer's steps are sixteenth notes; how far ahead of the audio and how often the timer schedules them
const StepsPerBeat = 4;
const SecondsPerMinute = 60;
const SemitonesPerOctave = 12;
const LookAhead = 0.1;
const SchedulerInterval = 25;

// How far the quantizer's tables reach either way of middle C, in octaves (Quantizing.Span)
const QuantizeSpan = 5;

// Narrower than this, in CSS pixels, a screen is narrow
const NarrowWidth = 1000;

// The line input chosen last, kept in the browser
const LineInDevice = 'synth.lineIn.device';

// A gate input is not an audio connection: the gate plugged into it opens and closes it, calling open(time) and
// close(time) on its voice, or on the voice's handler for that input in `gateInputs` (the sequencer's RESET)
const GateInput = Symbol('gate');

const canHold = typeof AudioParam !== 'undefined' && 'cancelAndHoldAtTime' in AudioParam.prototype;

const context = new AudioContext({ latencyHint: 'interactive' });
const modules = new Map();
const cables = new Map();
// The sources started while a voice is built, to stop when it is built again
let building = null;
const analyser = new AnalyserNode(context, { fftSize: 1024 });
const samples = new Float32Array(analyser.fftSize);
// The speakers' levels, left and right, as the output sends them
const SpeakerWindow = 1024;
const speakerSplit = new ChannelSplitterNode(context, { numberOfOutputs: 2 });
const speakers = [0, 1].map(channel => {
    const meter = new AnalyserNode(context, { fftSize: SpeakerWindow });
    speakerSplit.connect(meter, channel);
    return meter;
});

async function loadWorklets() {
    try {
        await context.audioWorklet.addModule(new URL('synth-worklets.js', import.meta.url));
        return true;
    } catch {
        // No worklets on an insecure page: SLEW and FOLLOW make do
        return false;
    }
}

const hasWorklets = await loadWorklets();

/** For checks: whether the audio worklets loaded. */
export const workletsLoaded = hasWorklets;

// Browsers start audio only from a user gesture. A touch's pointerdown doesn't count as one (its pointerup, touchend and
// click do), so every one of these tries until the context runs. iOS mutes Web Audio while the ring switch is on silent,
// unless the page says it plays media.
const GestureEvents = ['pointerdown', 'pointerup', 'touchend', 'click', 'keydown'];

if (navigator.audioSession) {
    try {
        navigator.audioSession.type = 'playback';
    } catch {
        // Older Safari: the ring switch keeps muting it
    }
}

function onGesture() {
    if (context.state === 'running') {
        stopListening();
        return;
    }
    context.resume().then(() => {
        if (context.state === 'running') {
            stopListening();
        }
    }, () => {});
}

function stopListening() {
    for (const type of GestureEvents) {
        window.removeEventListener(type, onGesture, true);
    }
}

for (const type of GestureEvents) {
    window.addEventListener(type, onGesture, true);
}

function glide(param, value) {
    param.setTargetAtTime(value, context.currentTime, Smoothing);
}

// Holds the parameter where it is now, dropping what was scheduled after
function hold(param, time) {
    if (canHold) {
        param.cancelAndHoldAtTime(time);
    } else {
        const current = param.value;
        param.cancelScheduledValues(time);
        param.setValueAtTime(current, time);
    }
}

function started(source, time) {
    source.start(time);
    building?.push(source);
    return source;
}

function constant(offset = 0) {
    return started(new ConstantSourceNode(context, { offset }));
}

function gain(value = 1) {
    return new GainNode(context, { gain: value });
}

// A gain that hears a mono signal on both sides and a stereo one as it is
function stereo() {
    return new GainNode(context, { channelCount: 2, channelCountMode: 'explicit', channelInterpretation: 'speakers' });
}

// A gain that hears a stereo signal as one, both sides together
function mono() {
    return new GainNode(context, { channelCount: 1, channelCountMode: 'explicit', channelInterpretation: 'speakers' });
}

// A table of a function over -1 to 1
function table(f, points = TablePoints) {
    return Float32Array.from({ length: points }, (_, i) => f(2 * i / (points - 1) - 1));
}

// A worklet node, stopped with its voice
function worklet(name, parameterData, processorOptions = {}) {
    const node = new AudioWorkletNode(context, name, { numberOfInputs: 1, numberOfOutputs: 1, outputChannelCount: [1], parameterData, processorOptions });
    building?.push({ stop: () => node.port.postMessage('stop') });
    return node;
}

// A knob as a signal. `value` is the knob's value with its CV added, once defineKnob has sent its table; `sum` is its
// turn with the CV, from -1 to 1 within the table; `plugged` counts the cables in its CV input, and `base` is its value
// without them. `watchers` are told when its value or its cables change.
//
// The audio thread has a few milliseconds for each block of samples, and a patch of six voices has hundreds of knobs, so
// a knob costs as little as it can: while nothing is plugged into its CV, `value` just holds the knob's value, and only
// once a cable is does it add the table's look-up of the turn and the CV (`shaped`, run only then).
function makeKnob(name) {
    const turn = constant(-1);
    const cv = gain(0);
    const sum = gain();
    const shaped = new WaveShaperNode(context);
    const value = constant(0);
    turn.connect(sum);
    cv.connect(sum);
    sum.connect(shaped);
    const knob = { name, turn, cv, sum, shaped, value, tables: [shaped], base: 0, plugged: 0, defined: false, watchers: [], cabled: false };
    cv.knob = knob;
    return knob;
}

// After the knob's value or the cables in its CV input changed: its value from the table while a cable is in, held at its
// value while none is, and its watchers told
function settle(knob) {
    const now = context.currentTime;
    const cabled = knob.plugged > 0;
    if (cabled !== knob.cabled) {
        knob.cabled = cabled;
        knob.value.offset.cancelScheduledValues(now);
        knob.value.offset.setValueAtTime(cabled ? 0 : knob.base, now);
        if (cabled) {
            knob.shaped.connect(knob.value.offset);
        } else {
            knob.shaped.disconnect(knob.value.offset);
        }
    }

    knob.watchers.forEach(watch => watch(now));
}

// The knob's value, sent to an AudioParam that adds it to its own value of 0
function drive(knob, ...params) {
    for (const param of params) {
        param.value = 0;
        knob.value.connect(param);
    }
}

function scaled(knob, factor) {
    return knob.value.connect(gain(factor));
}

// What the knob's CV adds to its value: the value less the value the knob alone gives
function cvOffset(knob) {
    const alone = new WaveShaperNode(context);
    knob.turn.connect(alone);
    knob.tables.push(alone);
    const offset = gain();
    knob.shaped.connect(offset);
    alone.connect(gain(-1)).connect(offset);
    return offset;
}

// The connection from `from` to `to`, made only while the knob is above 0 or has a cable in its CV, as an effect's mix
// does: an effect turned all the way out then costs nothing. It is broken a moment after the knob reaches 0, once the
// effect has faded out.
function whileOn(knob, from, to) {
    let joined = false;
    let parting = null;
    knob.watchers.push(() => {
        const on = knob.base > 0 || knob.plugged > 0;
        clearTimeout(parting);
        if (on && !joined) {
            from.connect(to);
            joined = true;
        } else if (!on && joined) {
            parting = setTimeout(() => {
                from.disconnect(to);
                joined = false;
            }, FadeOut * 1000);
        }
    });
}

// For a switch-like knob with `count` positions: a weight per position, 1 at it and falling to 0 at its neighbours, so a
// CV sweeping the knob crossfades from one position to the next
function choices(knob, count) {
    return Array.from({ length: count }, (_, i) => knob.sum.connect(new WaveShaperNode(context, {
        curve: table(x => Math.max(0, 1 - Math.abs((x + 1) / 2 * (count - 1) - i))),
    })));
}

// The knob's value now, for what can't be an AudioParam: read from the signal while a CV is plugged in
function reading(knob) {
    const probe = new AnalyserNode(context, { fftSize: 32 });
    const buffer = new Float32Array(probe.fftSize);
    knob.value.connect(probe);
    return () => {
        if (knob.plugged === 0) {
            return knob.base;
        }

        probe.getFloatTimeDomainData(buffer);
        const value = buffer[buffer.length - 1];
        return Number.isFinite(value) ? value : knob.base;
    };
}

// `a` and `b` blended into one output by a knob from 0 to 1: all `a` at 0, all `b` at 1, and evenly in between
function blend(knob, a, b) {
    const out = gain();
    const aLevel = gain(1);
    const bLevel = gain(0);
    knob.value.connect(bLevel.gain);
    knob.value.connect(gain(-1)).connect(aLevel.gain);
    a.connect(aLevel).connect(out);
    b.connect(bLevel).connect(out);
    return out;
}

// An effect's dry and wet signals by its mix knob: the dry stays at full until the mix is halfway, then fades as the wet
// reaches full, so a little effect adds to the sound instead of turning it down
function mix(knob, dry, wet) {
    const out = gain();
    const law = [x => Math.min(1, 2 * (1 - x)), x => Math.min(1, 2 * x)];
    [dry, wet].forEach((signal, i) => {
        const level = gain(0);
        knob.value.connect(new WaveShaperNode(context, { curve: table(law[i]) })).connect(level.gain);
        signal.connect(level).connect(out);
    });
    return out;
}

// Decaying noise in two channels, to hear as a room
function impulse(seconds) {
    const length = Math.round(seconds * context.sampleRate);
    const buffer = new AudioBuffer({ numberOfChannels: 2, length, sampleRate: context.sampleRate });
    for (let channel = 0; channel < 2; channel++) {
        const data = buffer.getChannelData(channel);
        for (let i = 0; i < length; i++) {
            // Down 60 dB by the end
            data[i] = (Math.random() * 2 - 1) * Math.exp(-6.9 * i / length);
        }
    }

    return buffer;
}

let rooms;
let noise;

// White noise, one channel, to loop
function noiseBuffer() {
    const length = Math.round(NoiseLength * context.sampleRate);
    const buffer = new AudioBuffer({ numberOfChannels: 1, length, sampleRate: context.sampleRate });
    const data = buffer.getChannelData(0);
    for (let i = 0; i < length; i++) {
        data[i] = Math.random() * 2 - 1;
    }

    return buffer;
}

// Each oscillator is all four waves, its wave knob choosing how much of each is heard. Only the chosen wave runs while
// nothing is plugged into the knob's CV, and all four while something is, so a signal sweeps from one to the next; a
// wave turned away from runs on until it has faded out. `tune(oscillator)` joins each wave started to its pitch, and
// `restart(time)` starts the waves again from the top of their cycle at the time, as a sync does.
function waveBank(wave, frequencyFrom, tune = () => {}) {
    const out = gain();
    const weights = choices(wave, Waves.length);
    const sources = building;
    const levels = Waves.map((_, i) => {
        const level = gain(0);
        weights[i].connect(level.gain);
        level.connect(out);
        return level;
    });
    const running = Waves.map(() => null);
    const start = (i, time) => {
        const oscillator = new OscillatorNode(context, { type: Waves[i], frequency: frequencyFrom ? 0 : MiddleC });
        if (frequencyFrom) {
            drive(frequencyFrom, oscillator.frequency);
        }

        tune(oscillator);
        oscillator.connect(levels[i]);
        oscillator.start(time);
        sources.push(oscillator);
        running[i] = oscillator;
    };
    const stop = (i, time) => {
        const old = running[i];
        running[i] = null;
        old.stop(time);
        old.onended = () => {
            old.disconnect();
            frequencyFrom?.value.disconnect(old.frequency);
            sources.splice(sources.indexOf(old) >>> 0, 1);
        };
    };
    const wanted = i => wave.plugged > 0 || i === Math.min(Waves.length - 1, Math.max(0, Math.round(wave.base)));
    const bank = { out, running };
    // Starts the waves now wanted and stops the others once faded
    wave.watchers.push(now => Waves.forEach((_, i) => {
        if (wanted(i) && !running[i]) {
            start(i, now);
        } else if (!wanted(i) && running[i]) {
            stop(i, now + FadeOut);
        }
    }));
    bank.restart = (time) => Waves.forEach((_, i) => {
        if (running[i]) {
            stop(i, time);
        }

        if (wanted(i)) {
            start(i, time);
        }
    });
    return bank;
}

const builders = {
    // A voice's key: its pitch, bent by the pitch wheel, and its gate; and the mod wheel, the same on every voice, so
    // its cables take it from the first. How many voices the keys play is the C# side's to decide.
    Keyboard(knob, module) {
        const gate = constant();
        const pitch = constant();
        cvOffset(knob('Octave')).connect(pitch.offset);
        // The pitch wheel's knob is in semitones
        scaled(knob('Bend'), 1 / SemitonesPerOctave).connect(pitch.offset);
        const mod = knob('Mod').value.connect(gain());
        knob('Voices');
        return {
            outputs: { Gate: gate, Pitch: pitch, Mod: mod },
            inputs: {},
            gate,
            pitch,
        };
    },

    // Three oscillators: each one's wave at full level from its own OUT, and all of them at their levels from MIX
    Oscillators(knob, module) {
        const outputs = { Mix: gain() };
        const inputs = {};
        for (let vco = 1; vco <= module.spec.count; vco++) {
            const name = part => `VCO ${vco} ${part}`;
            const tune = scaled(knob(name('Tune')), CentsPerSemitone);
            const fine = knob(name('Fine'));
            const pitch = gain(CentsPerOctave);
            // Pitch, tune and fine, in cents
            const cents = gain();
            pitch.connect(cents);
            tune.connect(cents);
            fine.value.connect(cents);
            const { out } = waveBank(knob(name('Wave')), null, oscillator => cents.connect(oscillator.detune));

            const level = gain(0);
            drive(knob(name('Level')), level.gain);
            out.connect(level).connect(outputs.Mix);
            outputs[name('Out')] = out;
            inputs[name('Pitch')] = pitch;
        }

        return { outputs, inputs };
    },

    Mixer(knob, module) {
        const out = gain();
        const inputs = {};
        for (let channel = 1; channel <= module.spec.count; channel++) {
            const level = gain(0);
            drive(knob(`Level ${channel}`), level.gain);
            level.connect(out);
            inputs[`In ${channel}`] = level;
        }

        return { outputs: { Out: out }, inputs };
    },

    // A gate into SYNC starts the waves again as it opens
    Lfo(knob) {
        const bank = waveBank(knob('Wave'), knob('Rate'));
        const depth = gain(0);
        drive(knob('Depth'), depth.gain);
        bank.out.connect(depth);
        return {
            outputs: { Out: depth },
            inputs: { Sync: GateInput },
            open(time = context.currentTime) {
                bank.restart(time);
            },
            close() {},
        };
    },

    // Noise looped; S&H and SMOOTH stepped by the scheduler (scheduleClocks) at the rate, or by a gate in CLOCK
    Random(knob) {
        noise ??= noiseBuffer();
        const hiss = started(new AudioBufferSourceNode(context, { buffer: noise, loop: true }));
        const held = constant();
        const smooth = constant();
        const random = { rate: reading(knob('Rate')), next: 0, last: -Infinity, steps: [] };
        // A new level at the time: held at once, glided to by SMOOTH over about the time since the last
        const step = time => {
            const level = Math.random() * 2 - 1;
            // The last few, for checks (randomSteps)
            random.steps.push([time, level]);
            random.steps.splice(0, random.steps.length - 16);
            const since = Math.min(1, Math.max(0.005, time - random.last));
            random.last = time;
            held.offset.setValueAtTime(level, time);
            smooth.offset.setTargetAtTime(level, time, since / 3);
        };
        random.step = step;
        return {
            outputs: { Noise: hiss.connect(gain()), 'S&H': held, Smooth: smooth },
            inputs: { Clock: GateInput },
            random,
            open(time = context.currentTime) {
                step(time);
            },
            close() {},
        };
    },

    Filter(knob) {
        const type = knob('Type');
        const cutoff = knob('Cutoff');
        const resonance = knob('Res');
        const input = gain();
        const out = gain();
        // CUTOFF's CV, in octaves times the mod knob, to cents
        const sweep = gain(0);
        scaled(knob('Mod'), CentsPerOctave).connect(sweep.gain);
        const weights = choices(type, FilterTypes.length);
        FilterTypes.forEach((filterType, i) => {
            const filter = new BiquadFilterNode(context, { type: filterType });
            drive(cutoff, filter.frequency);
            drive(resonance, filter.Q);
            sweep.connect(filter.detune);
            const level = gain(0);
            weights[i].connect(level.gain);
            input.connect(filter).connect(level).connect(out);
        });
        return {
            outputs: { Out: out },
            inputs: { In: input },
            cvInputs: { 'Cutoff CV': sweep },
        };
    },

    Envelope(knob) {
        const level = constant();
        const stage = Object.fromEntries(['Attack', 'Decay', 'Sustain', 'Release'].map(name => [name, reading(knob(name))]));
        return {
            outputs: { Out: level },
            inputs: { Gate: GateInput },
            open(time = context.currentTime) {
                // From wherever it is, up to full over the attack, then down towards the sustain level
                const attack = stage.Attack();
                hold(level.offset, time);
                level.offset.linearRampToValueAtTime(1, time + attack);
                level.offset.setTargetAtTime(stage.Sustain(), time + attack, stage.Decay() / 4);
            },
            close(time = context.currentTime) {
                hold(level.offset, time);
                level.offset.setTargetAtTime(0, time, stage.Release() / 4);
            },
        };
    },

    // The level is the knob plus its CV one for one, past the knob's ends; RING blends from that level stopped at 0 to
    // the level as it is, which turns the signal over where it is below 0
    Vca(knob) {
        const levelKnob = knob('Level');
        const level = gain();
        levelKnob.sum.connect(gain(0.5)).connect(level);
        constant(0.5).connect(level);
        const stopped = level.connect(gain(1 / LevelSpan)).connect(new WaveShaperNode(context, {
            curve: table(x => Math.max(0, x * LevelSpan)),
        }));
        const amplifier = gain(0);
        blend(knob('Ring'), stopped, level).connect(amplifier.gain);
        return {
            outputs: { Out: amplifier },
            inputs: { In: amplifier },
        };
    },

    // The amount pushes the signal into a soft clip or, by FOLD, a sine folder, both over the same span, oversampled
    Drive(knob) {
        const input = gain();
        const push = gain(0);
        const mixKnob = knob('Mix');
        drive(knob('Amount'), push.gain);
        // Turned all the way out (MIX at 0), the shapers hear nothing and so cost nothing
        whileOn(mixKnob, input, push);
        const scaledDown = push.connect(gain(1 / FoldSpan));
        const shaper = f => scaledDown.connect(new WaveShaperNode(context, { curve: table(f, FoldPoints), oversample: '4x' }));
        const clip = shaper(x => Math.min(1, Math.max(-1, Math.tanh(DriveCurveSharpness * x * FoldSpan) / Math.tanh(DriveCurveSharpness))));
        const fold = shaper(x => Math.sin(Math.PI / 2 * x * FoldSpan));
        const wet = blend(knob('Fold'), clip, fold)
            .connect(new BiquadFilterNode(context, { type: 'lowpass', frequency: DriveDarken, Q: 0.7 }))
            .connect(gain(DriveTrim));
        return {
            outputs: { Out: mix(mixKnob, input, wet) },
            inputs: { In: input },
        };
    },

    // Each side through a chain of allpass filters swept by the LFO, the right in opposite phase, fed back through a
    // short delay (a loop needs one), and blended with the dry side by the depth: half each at full depth, for the deepest
    // notches
    Phaser(knob) {
        const input = stereo();
        const split = input.connect(new ChannelSplitterNode(context, { numberOfOutputs: 2 }));
        const merged = new ChannelMergerNode(context, { numberOfInputs: 2 });
        const lfo = started(new OscillatorNode(context, { frequency: 0 }));
        drive(knob('Rate'), lfo.frequency);
        const depth = knob('Depth');
        const feedback = knob('Feedback');
        [1, -1].forEach((phase, side) => {
            const into = gain();
            split.connect(into, side);
            const sweep = lfo.connect(gain(phase * PhaserSweep));
            let stage = into;
            for (let i = 0; i < PhaserStages; i++) {
                const allpass = new BiquadFilterNode(context, { type: 'allpass', frequency: PhaserCentre, Q: 0.5 });
                sweep.connect(allpass.detune);
                stage = stage.connect(allpass);
            }

            const back = gain(0);
            drive(feedback, back.gain);
            stage.connect(back).connect(new DelayNode(context, { delayTime: 0 })).connect(into);
            const wet = gain(0);
            scaled(depth, 0.5).connect(wet.gain);
            const dry = gain(1);
            scaled(depth, -0.5).connect(dry.gain);
            stage.connect(wet).connect(merged, 0, side);
            into.connect(dry).connect(merged, 0, side);
        });
        return {
            outputs: { Out: merged.connect(gain()) },
            inputs: { In: input },
        };
    },

    // A ping-pong echo: both sides heard as one into the left line, the left line into the right, the right fed back
    // into the left; each line's time wobbled by the mod, the right a quarter cycle on from the left, so a short time
    // with some mod is a wide chorus
    Delay(knob) {
        const input = stereo();
        const time = knob('Time');
        const lines = [0, 1].map(() => new DelayNode(context, { maxDelayTime: MaxDelay }));
        drive(time, ...lines.map(line => line.delayTime));
        const wobble = scaled(knob('Mod'), DelayModSweep);
        const quarter = new PeriodicWave(context, { real: [0, 1], imag: [0, 0] });
        lines.forEach((line, side) => {
            const lfo = started(new OscillatorNode(context, { frequency: DelayModRate, ...(side === 1 ? { periodicWave: quarter } : {}) }));
            const depth = gain(0);
            wobble.connect(depth.gain);
            lfo.connect(depth).connect(line.delayTime);
        });
        const [left, right] = lines;
        const mixKnob = knob('Mix');
        const heard = mono();
        whileOn(mixKnob, input, heard);
        heard.connect(left);
        left.connect(right);
        const repeat = gain(0);
        drive(knob('Feedback'), repeat.gain);
        right.connect(new BiquadFilterNode(context, { type: 'lowpass', frequency: DelayDamping })).connect(repeat).connect(left);
        const wet = new ChannelMergerNode(context, { numberOfInputs: 2 });
        left.connect(wet, 0, 0);
        right.connect(wet, 0, 1);
        return {
            outputs: { Out: mix(mixKnob, input, wet) },
            inputs: { In: input },
        };
    },

    Reverb(knob) {
        rooms ??= [impulse(ShortRoom), impulse(LongRoom)];
        const input = stereo();
        const mixKnob = knob('Mix');
        const [short, long] = rooms.map(buffer => {
            const room = new ConvolverNode(context, { buffer });
            whileOn(mixKnob, input, room);
            return room;
        });
        const darken = new BiquadFilterNode(context, { type: 'lowpass' });
        const size = knob('Size');
        drive(knob('Tone'), darken.frequency);
        blend(size, short, long).connect(darken);
        return {
            outputs: { Out: mix(mixKnob, input, darken) },
            inputs: { In: input },
        };
    },

    // An input of the device, raw: no echo cancelling, noise suppression or gain control, which would colour a guitar and
    // add delay. It is asked for only once used (useLineIn), and plays through the gain knob (AUDIO); ENV follows how loud
    // it is.
    LineIn(knob) {
        const audio = gain(0);
        drive(knob('Gain'), audio.gain);
        const attack = knob('Attack');
        const release = knob('Release');
        let env;
        if (hasWorklets) {
            env = worklet('slew', { rise: 0, fall: 0, shape: 1 }, { rectify: true });
            drive(attack, env.parameters.get('rise'));
            drive(release, env.parameters.get('fall'));
            audio.connect(env);
        } else {
            env = audio.connect(new WaveShaperNode(context, { curve: table(Math.abs) }))
                .connect(new BiquadFilterNode(context, { type: 'lowpass', frequency: FollowFallback }));
        }

        return {
            outputs: { Env: env.connect(gain()), Audio: audio },
            inputs: {},
            line: { state: 'idle', stream: null, source: null, deviceId: null, devices: [], input: audio },
        };
    },

    // Follows its input at the rise and fall times, from a glide to a one-pole curve by the shape
    Slew(knob) {
        const input = gain();
        const rise = knob('Rise');
        const fall = knob('Fall');
        const shape = knob('Shape');
        let out = input;
        if (hasWorklets) {
            const slew = worklet('slew', { rise: 0, fall: 0, shape: 0 });
            drive(rise, slew.parameters.get('rise'));
            drive(fall, slew.parameters.get('fall'));
            drive(shape, slew.parameters.get('shape'));
            out = input.connect(slew);
        }

        return {
            outputs: { Out: out.connect(gain()) },
            inputs: { In: input },
        };
    },

    // IN times the scale, plus the offset
    Atten(knob) {
        const input = gain(0);
        drive(knob('Scale'), input.gain);
        const out = gain();
        input.connect(out);
        knob('Offset').value.connect(out);
        return {
            outputs: { Out: out },
            inputs: { In: input },
        };
    },

    // The steps are played by the scheduler (scheduleClocks); a gate in the clock input plays each one instead, and one in
    // RESET sends it back to step 1. The steps' knobs have no CV inputs, so they are read as set, with no probe on them.
    Sequencer(knob, module) {
        const read = name => reading(knob(name));
        const pitch = constant();
        const gate = constant();
        const clock = constant();
        const sequence = {
            run: read('Run'), tempo: read('Tempo'), length: read('Length'), steps: null,
            // The step played last, the time the next falls due, whether the step played last opened the gate, and
            // the steps scheduled as [time, step] pairs, to tell which one is heard now
            running: false, step: -1, next: 0, gated: false, heard: [], played: [], reset: false,
        };
        sequence.steps = Array.from({ length: module.spec.count }, (_, i) => {
            const [stepPitch, stepGate] = [knob(`Step ${i + 1}`), knob(`Gate ${i + 1}`)];
            return { pitch: () => stepPitch.base, gate: () => stepGate.base };
        });
        return {
            outputs: { Pitch: pitch, Gate: gate, Clock: clock },
            inputs: { 'Clock In': GateInput, Reset: GateInput },
            gateInputs: {
                Reset: {
                    // The next step played is step 1
                    open: () => {
                        sequence.reset = true;
                    },
                    close() {},
                },
            },
            pitch,
            gate,
            clock,
            sequence,
            open(time = context.currentTime) {
                if (this.sequence.running) {
                    playStep(module, time);
                }
            },
            close(time = context.currentTime) {
                if (this.sequence.running) {
                    endStep(module, time);
                }
            },
        };
    },

    // IN pulled to the nearest note of the scale: one wave shaper whose table is the scale's, swapped as the scale knob
    // turns (the tables come from the C# side, defineScales). Native, so it costs the audio thread next to nothing.
    Quantizer(knob, module) {
        const input = gain(1 / QuantizeSpan);
        const shaper = new WaveShaperNode(context);
        const scale = knob('Scale');
        const choose = () => {
            const table = module.scales?.[Math.round(scale.base)];
            if (table && shaper.curve !== table) {
                shaper.curve = table;
            }
        };
        scale.watchers.push(choose);
        input.connect(shaper);
        return {
            outputs: { Out: shaper.connect(gain()) },
            inputs: { In: input },
            choose,
        };
    },

    // The room's lights: knobs alone, read back each frame (readKnob) to colour the lights
    Lights(knob) {
        for (const side of ['Left', 'Right']) {
            for (const part of ['Intensity', 'Hue', 'Saturation']) {
                knob(`${side} ${part}`);
            }
        }

        return { outputs: {}, inputs: {} };
    },

    // A scope's input, kept for the C# side to read back (readScope) and trace
    Scope() {
        const input = gain();
        const analyser = new AnalyserNode(context, { fftSize: 2048 });
        input.connect(analyser);
        return { outputs: {}, inputs: { In: input }, analyser, buffer: new Float32Array(analyser.fftSize) };
    },

    // The left side of IN L and the right of IN R, each turned down by the pan towards the other, then the volume, a
    // limiter and a soft clip, to the speakers
    Output(knob) {
        const sides = [stereo(), stereo()];
        const merged = new ChannelMergerNode(context, { numberOfInputs: 2 });
        const pan = knob('Pan');
        const laws = [x => Math.min(1, 1 - x), x => Math.min(1, 1 + x)];
        sides.forEach((input, side) => {
            const level = gain(0);
            pan.value.connect(new WaveShaperNode(context, { curve: table(laws[side]) })).connect(level.gain);
            input.connect(new ChannelSplitterNode(context, { numberOfOutputs: 2 })).connect(level, side).connect(merged, 0, side);
        });
        const volume = gain(0);
        drive(knob('Volume'), volume.gain);
        const limiter = new DynamicsCompressorNode(context, {
            threshold: 20 * Math.log10(LimiterThreshold), knee: 0, ratio: 20, attack: 0.003, release: 0.1,
        });
        const soft = y => Math.abs(y) < SoftClipKnee
            ? y
            : Math.sign(y) * (SoftClipKnee + (1 - SoftClipKnee) * Math.tanh((Math.abs(y) - SoftClipKnee) / (1 - SoftClipKnee)));
        const clip = new WaveShaperNode(context, { curve: table(x => soft(x * SoftClipSpan), 4097), oversample: '2x' });
        merged.connect(volume).connect(limiter).connect(gain(1 / SoftClipSpan)).connect(clip).connect(context.destination);
        // For checks (outputHeadroom): what reaches the limiter, and how hard it works
        const reaching = new AnalyserNode(context, { fftSize: 2048 });
        volume.connect(reaching);
        output = { reaching, limiter };
        clip.connect(analyser);
        clip.connect(speakerSplit);
        return {
            outputs: {},
            inputs: { 'In L': sides[0], 'In R': sides[1] },
        };
    },
};

// One voice of a module: its knobs, the nodes its builder made, and the knobs' tables and settings as they are now
function buildVoice(module) {
    const knobs = new Map();
    const knob = name => {
        const made = makeKnob(name);
        knobs.set(name, made);
        return made;
    };
    const sources = building = [];
    let voice;
    try {
        voice = { knobs, sources, ...builders[module.kind](knob, module) };
    } finally {
        building = null;
    }

    for (const [name, definition] of module.definitions) {
        define(voice, name, definition);
    }

    for (const [name, setting] of module.settings) {
        const made = knobs.get(name);
        made.base = setting.value;
        made.turn.offset.value = 2 * setting.turn - 1;
        made.value.offset.value = setting.value;
    }

    knobs.forEach(settle);
    return voice;
}

function define(voice, name, { cvJack, cvGain, values }) {
    const knob = voice.knobs.get(name);
    knob.tables.forEach(shaper => shaper.curve = values);
    knob.cv.gain.value = cvGain;
    knob.defined = true;
    if (cvJack) {
        voice.inputs[cvJack] = voice.cvInputs?.[cvJack] ?? knob.cv;
    }
}

function teardown(voice) {
    voice.sources.forEach(source => source.stop());
    Object.values(voice.outputs).forEach(node => node.disconnect());
}

function cableKey(fromModule, fromJack, toModule, toJack) {
    return `${fromModule} ${fromJack} -> ${toModule} ${toJack}`;
}

// Joins the cable's nodes: voice to voice, one to all, or all into one
function link(cable) {
    const from = modules.get(cable.fromModule);
    const to = modules.get(cable.toModule);
    if (!from?.voices[0].outputs[cable.fromJack] || !to?.voices[0].inputs[cable.toJack]) {
        throw new Error(`No cable can join module ${cable.fromModule} ${cable.fromJack} to module ${cable.toModule} ${cable.toJack}`);
    }

    cable.links = [];
    if (to.voices[0].inputs[cable.toJack] === GateInput) {
        const key = gateKey(cable.fromModule, cable.fromJack);
        to.gates.set(key, (to.gates.get(key) ?? new Set()).add(cable.toJack));
        return;
    }

    // An output the same on every voice is taken from the first, as one signal
    const sources = from.spec.shared.includes(cable.fromJack) ? 1 : from.voices.length;
    const count = Math.max(sources, to.voices.length);
    for (let i = 0; i < count; i++) {
        const node = from.voices[Math.min(i, sources - 1)].outputs[cable.fromJack];
        const input = to.voices[Math.min(i, to.voices.length - 1)].inputs[cable.toJack];
        node.connect(input);
        if (input.knob) {
            input.knob.plugged++;
            settle(input.knob);
        }

        cable.links.push([node, input]);
    }
}

function unlink(cable) {
    const to = modules.get(cable.toModule);
    if (to.voices[0].inputs[cable.toJack] === GateInput) {
        const key = gateKey(cable.fromModule, cable.fromJack);
        to.gates.get(key)?.delete(cable.toJack);
        if (to.gates.get(key)?.size === 0) {
            to.gates.delete(key);
        }

        to.voices.forEach(voice => gateHandler(voice, cable.toJack).close());
    }

    for (const [node, input] of cable.links) {
        node.disconnect(input);
        if (input.knob) {
            input.knob.plugged--;
            settle(input.knob);
        }
    }

    cable.links = [];
}

/**
 * Builds a module of the kind with this many voices. Its jacks, the outputs that are the same on every voice, and how many
 * of its repeated parts it has (oscillators, channels, steps) come from the C# side's definition of it.
 */
export function addModule(id, kind, voices, inputs, outputs, shared, count) {
    if (!builders[kind]) {
        throw new Error(`Unknown module kind ${kind}`);
    }

    // `gates` maps each gate output plugged into the module's gate inputs to the inputs it is in; the arrays may come as
    // typed arrays
    const spec = { inputs: Array.from(inputs), outputs: Array.from(outputs), shared: Array.from(shared), count };
    const module = { id, kind, spec, definitions: new Map(), settings: new Map(), gates: new Map(), voices: [] };
    module.voices = Array.from({ length: voices }, () => buildVoice(module));
    modules.set(id, module);
}

/** Builds the module again with this many voices, unplugging its cables first and plugging them back after. */
export function setVoices(id, voices) {
    const module = modules.get(id);
    const touching = [...cables.values()].filter(cable => cable.fromModule === id || cable.toModule === id);
    touching.forEach(unlink);
    // Tearing the voices down disconnects their outputs from everything, the meters included
    const metering = meters.filter(meter => meter.module === id);
    metering.forEach(meter => meter.sources = []);
    module.voices.forEach(teardown);
    module.voices = Array.from({ length: voices }, () => buildVoice(module));
    touching.forEach(link);
    metering.forEach(listen);
}

// The outputs metered for their level (meterOutputs), in order: each an analyser hearing every voice of the output, its
// window about a frame long so a peak between two frames is still caught
const MeterWindow = 1024;
let meters = [];

/** For the page's ?debug readout and for checks: how many times the level meters were read and how long it took, in milliseconds. */
export const meterTiming = { calls: 0, total: 0, longest: 0 };

// Connects the meter to its output on every voice (or the first, for an output shared by them all), afresh
function listen(meter) {
    const module = modules.get(meter.module);
    meter.sources?.forEach(node => node.disconnect(meter.analyser));
    const voices = module.spec.shared.includes(meter.jack) ? module.voices.slice(0, 1) : module.voices;
    meter.sources = voices.map(voice => voice.outputs[meter.jack]).filter(node => node);
    meter.sources.forEach(node => node.connect(meter.analyser));
}

/** Meters these outputs from now on, module by module and jack by jack, for readFrame; the ones metered before stop. */
export function meterOutputs(ids, jacks) {
    meters.forEach(meter => meter.sources.forEach(node => node.disconnect(meter.analyser)));
    meters.forEach(meter => meter.sources = []);
    // The ids may come as a typed array, whose map would keep only numbers
    meters = Array.from(ids, (module, i) => {
        const meter = { module, jack: jacks[i], analyser: new AnalyserNode(context, { fftSize: MeterWindow }) };
        listen(meter);
        return meter;
    });
}

const meterBuffer = new Float32Array(MeterWindow);

// Writes the first `count` metered outputs' peak magnitudes over their windows into `into`, from `at` on
function readPeaks(into, at, count) {
    const started = performance.now();
    for (let i = 0; i < count; i++) {
        meters[i].analyser.getFloatTimeDomainData(meterBuffer);
        let peak = 0;
        for (let j = 0; j < MeterWindow; j++) {
            const magnitude = Math.abs(meterBuffer[j]);
            if (magnitude > peak) {
                peak = magnitude;
            }
        }

        into[at + i] = peak;
    }

    const took = performance.now() - started;
    meterTiming.calls++;
    meterTiming.total += took;
    meterTiming.longest = Math.max(meterTiming.longest, took);
}

// What readFrame fills, before it is copied into .NET's view in one go
let readBack = new Float64Array(64);

function readBackOf(length) {
    if (readBack.length < length) {
        readBack = new Float64Array(Math.max(length, readBack.length * 2));
    }

    return readBack;
}

// The knobs readFrame reads back, each as [module, name], in order
let watched = [];

/** From now on readFrame also reads back these knobs, the module's by id and the knob's by name, in order. */
export function watchKnobs(ids, names) {
    // The ids may come as a typed array, whose map would keep only numbers
    watched = Array.from(ids, (id, i) => [id, names[i]]);
}

// The sequencers whose step heard readFrame reads back, by id, in order
let stepping = [];

/** From now on readFrame also reads back these sequencers' steps heard (as sequencerStep), after the knobs. */
export function watchSteps(ids) {
    stepping = Array.from(ids);
}

// Where readFrame puts the metered outputs' count, after the state, rate, level and the two speakers' peaks; AudioFrame.cs reads it so
const FrameMeters = 5;

/**
 * What .NET reads back each frame, in one call, into the view (a .NET Span<double>): whether the audio runs (1 or 0), its
 * sample rate, the output's level, the left and right speakers' peaks (as readSpeakers), how many metered outputs' peaks
 * follow and the peaks, each the output's peak magnitude over its window, then each watched knob's value (as readKnob)
 * and each watched sequencer's step heard (as sequencerStep). Peaks come only while the audio runs, as many as the view
 * has room for.
 */
export function readFrame(view) {
    const running = context.state === 'running';
    const count = running ? Math.max(0, Math.min(meters.length, view.length - FrameMeters - 1 - watched.length - stepping.length)) : 0;
    const length = FrameMeters + 1 + count + watched.length + stepping.length;
    const frame = readBackOf(length);
    frame[0] = running ? 1 : 0;
    frame[1] = context.sampleRate;
    frame[2] = running ? level() : 0;
    frame[3] = running && speakers.length > 0 ? speakerPeak(0) : 0;
    frame[4] = running && speakers.length > 1 ? speakerPeak(1) : 0;
    frame[FrameMeters] = count;
    readPeaks(frame, FrameMeters + 1, count);
    for (let i = 0; i < watched.length; i++) {
        frame[FrameMeters + 1 + count + i] = readKnob(watched[i][0], watched[i][1]);
    }

    for (let i = 0; i < stepping.length; i++) {
        frame[FrameMeters + 1 + count + watched.length + i] = sequencerStep(stepping[i]);
    }

    view.set(frame.subarray(0, Math.min(length, view.length)));
}

/** Gives a knob its table of values and its CV input, under the name the patch knows it by: none for an empty name. */
export function defineKnob(id, name, cvJack, cvGain, curve) {
    const module = modules.get(id);
    if (!module?.voices[0].knobs.has(name)) {
        throw new Error(`Module ${id} (${module?.kind}) has no knob ${name}`);
    }

    const definition = { cvJack, cvGain, values: Float32Array.from(curve) };
    module.definitions.set(name, definition);
    module.voices.forEach(voice => define(voice, name, definition));
}

/** Gives a quantizer its scales' tables, `count` of them one after another in `tables` (Quantizing.Table), each chosen by its scale knob. */
export function defineScales(id, count, tables) {
    const module = modules.get(id);
    const points = tables.length / count;
    module.scales = Array.from({ length: count }, (_, i) => Float32Array.from(tables.slice(i * points, (i + 1) * points)));
    module.voices.forEach(voice => voice.choose());
}

/** Fails unless every voice of the module realises exactly the jacks it was added with and every knob has been defined. */
export function checkModule(id) {
    const module = modules.get(id);
    const { inputs, outputs } = module?.spec ?? { inputs: [], outputs: [] };
    const same = (have, want) => have.length === want.length && want.every(name => have.includes(name));
    for (const voice of module?.voices ?? [undefined]) {
        const undefinedKnobs = [...(voice?.knobs.values() ?? [])].filter(knob => !knob.defined).map(knob => knob.name);
        if (!voice || !same(Object.keys(voice.inputs), inputs) || !same(Object.keys(voice.outputs), outputs) || undefinedKnobs.length > 0) {
            throw new Error(`Module ${id} (${module?.kind}) has inputs [${Object.keys(voice?.inputs ?? {})}] and outputs `
                + `[${Object.keys(voice?.outputs ?? {})}], not [${inputs}] and [${outputs}]; knobs not defined: [${undefinedKnobs}]`);
        }
    }
}

export function setParameter(id, name, turn, value) {
    const module = modules.get(id);
    if (!module?.voices[0].knobs.has(name)) {
        return;
    }

    module.settings.set(name, { turn, value });
    for (const voice of module.voices) {
        const knob = voice.knobs.get(name);
        knob.base = value;
        glide(knob.turn.offset, 2 * turn - 1);
        if (!knob.cabled) {
            glide(knob.value.offset, value);
        }

        settle(knob);
    }
}

export function connect(fromModule, fromJack, toModule, toJack) {
    const cable = { fromModule, fromJack, toModule, toJack, links: [] };
    link(cable);
    cables.set(cableKey(fromModule, fromJack, toModule, toJack), cable);
}

export function disconnect(fromModule, fromJack, toModule, toJack) {
    const key = cableKey(fromModule, fromJack, toModule, toJack);
    const cable = cables.get(key);
    if (!cable) {
        throw new Error(`No cable ${key} to unplug`);
    }

    unlink(cable);
    cables.delete(key);
}

function gateKey(module, jack) {
    return `${module} ${jack}`;
}

// What a gate into the voice's input opens and closes: the input's own handler, or the voice
function gateHandler(voice, input) {
    return voice.gateInputs?.[input] ?? voice;
}

// What a gate output opens: of each module it is plugged into, the same voice as the gate's, or every voice for a gate
// that has no voice of its own, such as a sequencer's, each by the input it is in
function gatedBy(module, jack, voice) {
    const key = gateKey(module, jack);
    return [...modules.values()]
        .filter(target => target.gates.has(key))
        .flatMap(target => (voice === undefined ? target.voices : [target.voices[Math.min(voice, target.voices.length - 1)]])
            .flatMap(gated => [...target.gates.get(key)].map(input => gateHandler(gated, input))));
}

// Whether a gate is plugged into the module's input
function gatePlugged(module, input) {
    return [...module.gates.values()].some(inputs => inputs.has(input));
}

export function noteOn(keyboard, voice, pitch, retrigger) {
    const played = modules.get(keyboard)?.voices[voice];
    if (!played) {
        return;
    }

    timing.noteOn = performance.now();
    const now = context.currentTime;
    played.pitch.offset.setValueAtTime(pitch, now);
    if (retrigger) {
        // The gate opens again: down for a moment, so a module reading the gate sees a fresh edge
        played.gate.offset.cancelScheduledValues(now);
        played.gate.offset.setValueAtTime(0, now);
    }

    played.gate.offset.setTargetAtTime(1, now, GateSmoothing);
    played.held = true;
    if (retrigger) {
        gatedBy(keyboard, 'Gate', voice).forEach(gated => gated.open(now));
    }
}

export function noteOff(keyboard, voice) {
    const played = modules.get(keyboard)?.voices[voice];
    if (!played) {
        return;
    }

    played.gate.offset.setTargetAtTime(0, context.currentTime, GateSmoothing);
    played.held = false;
    gatedBy(keyboard, 'Gate', voice).forEach(gated => gated.close());
}

// Plays the sequencer's next step at the time: its pitch, the clock, and the gate if the step's is on
function playStep(module, time) {
    const voice = module.voices[0];
    const sequence = voice.sequence;
    const length = Math.min(sequence.steps.length, Math.max(1, Math.round(sequence.length())));
    // A gate into RESET since the last step sends it back to step 1
    const index = sequence.reset || sequence.step + 1 >= length ? 0 : sequence.step + 1;
    sequence.step = index;
    sequence.reset = false;
    const step = sequence.steps[index];
    voice.pitch.offset.setValueAtTime(Math.round(step.pitch()) / SemitonesPerOctave, time);
    voice.clock.offset.setValueAtTime(1, time);
    gatedBy(module.id, 'Clock').forEach(gated => gated.open(time));
    sequence.gated = step.gate() >= 0.5;
    if (sequence.gated) {
        voice.gate.offset.setValueAtTime(1, time);
        gatedBy(module.id, 'Gate').forEach(gated => gated.open(time));
    }

    forgetHeard(sequence, context.currentTime);
    sequence.heard.push([time, index]);
    // The last few, for checks (sequencerPlayed)
    sequence.played.push([time, index, sequence.gated]);
    sequence.played.splice(0, sequence.played.length - 32);
}

// Drops the steps scheduled before the one heard now
function forgetHeard(sequence, now) {
    while (sequence.heard.length > 1 && sequence.heard[1][0] <= now) {
        sequence.heard.shift();
    }
}

// Closes the step's clock and gate at the time, dropping anything scheduled after it, as when the sequencer stops
function endStep(module, time) {
    const voice = module.voices[0];
    const sequence = voice.sequence;
    for (const signal of [voice.clock, voice.gate]) {
        signal.offset.cancelScheduledValues(time);
        signal.offset.setValueAtTime(0, time);
    }

    gatedBy(module.id, 'Clock').forEach(gated => gated.close(time));
    if (sequence.gated) {
        gatedBy(module.id, 'Gate').forEach(gated => gated.close(time));
        sequence.gated = false;
    }

    sequence.heard = sequence.heard.filter(([at]) => at <= time);
    sequence.step = sequence.heard.at(-1)?.[1] ?? -1;
}

// Starts or stops the sequencer by its run switch, and while it runs on its own tempo, plays every step falling due
// before the look-ahead ends. After a stall, such as a hidden tab's slowed timers, it carries on from now instead of
// playing the missed steps at once.
function runSequencer(module, now) {
    const sequence = module.voices[0].sequence;
    if (sequence.run() < 0.5) {
        if (sequence.running) {
            endStep(module, now);
            sequence.running = false;
            sequence.step = -1;
            sequence.heard = [];
        }

        return;
    }

    if (!sequence.running) {
        sequence.running = true;
        sequence.step = -1;
        sequence.next = now;
    }

    // A gate in the clock input plays the steps
    if (gatePlugged(module, 'Clock In')) {
        return;
    }

    sequence.next = Math.max(sequence.next, now);
    while (sequence.next < now + LookAhead) {
        const seconds = SecondsPerMinute / Math.max(1, sequence.tempo()) / StepsPerBeat;
        playStep(module, sequence.next);
        endStep(module, sequence.next + seconds / 2);
        sequence.next += seconds;
    }
}

// While nothing is plugged into its clock, steps RANDOM at its rate: every step falling due before the look-ahead ends, and
// after a stall, on from now
function runRandom(module, now) {
    if (gatePlugged(module, 'Clock')) {
        return;
    }

    const random = module.voices[0].random;
    random.next = Math.max(random.next, now);
    while (random.next < now + LookAhead) {
        random.step(random.next);
        random.next += 1 / Math.max(0.01, random.rate());
    }
}

function scheduleClocks() {
    if (context.state !== 'running') {
        return;
    }

    const now = context.currentTime;
    for (const module of modules.values()) {
        if (module.kind === 'Sequencer') {
            runSequencer(module, now);
        } else if (module.kind === 'Random') {
            runRandom(module, now);
        }
    }
}

setInterval(scheduleClocks, SchedulerInterval);

/** The sequencer's step heard now, from 0, or -1 while it is stopped or before its first step sounds. */
export function sequencerStep(id) {
    const sequence = modules.get(id)?.voices[0].sequence;
    if (!sequence) {
        return -1;
    }

    const now = context.currentTime;
    forgetHeard(sequence, now);
    const [time, step] = sequence.heard[0] ?? [Infinity, -1];
    return time <= now ? step : -1;
}

function remembered() {
    try {
        return localStorage.getItem(LineInDevice);
    } catch {
        return null;
    }
}

function remember(deviceId) {
    try {
        localStorage.setItem(LineInDevice, deviceId);
    } catch {
        // Storage may be off; the choice just isn't kept
    }
}

function stopLine(line) {
    line.source?.disconnect();
    line.stream?.getTracks().forEach(track => track.stop());
    line.source = line.stream = null;
}

// The device's audio inputs; their labels show once the user has allowed one
async function listInputs(line) {
    const devices = await navigator.mediaDevices.enumerateDevices();
    line.devices = devices.filter(device => device.kind === 'audioinput');
}

// Starts the line input on a device: exactly that one when picked, else the one picked last if it's still there
async function openLine(voice, deviceId, exact) {
    const line = voice.line;
    if (!navigator.mediaDevices?.getUserMedia) {
        line.state = 'none';
        return;
    }

    line.state = 'asking';
    try {
        const stream = await navigator.mediaDevices.getUserMedia({
            audio: {
                ...(deviceId ? { deviceId: exact ? { exact: deviceId } : { ideal: deviceId } } : {}),
                echoCancellation: false,
                noiseSuppression: false,
                autoGainControl: false,
                channelCount: 1,
            },
        });
        stopLine(line);
        line.stream = stream;
        line.source = new MediaStreamAudioSourceNode(context, { mediaStream: stream });
        line.source.connect(line.input);
        const track = stream.getAudioTracks()[0];
        // An interface unplugged while it plays ends its track: the face then says there's no input
        track?.addEventListener('ended', () => {
            if (line.stream === stream) {
                stopLine(line);
                line.state = 'none';
            }
        });
        line.deviceId = track?.getSettings().deviceId ?? null;
        if (line.deviceId) {
            remember(line.deviceId);
        }

        await listInputs(line);
        line.state = 'live';
    } catch (error) {
        // A failed change of input leaves the one playing
        line.state = line.stream ? 'live' : error?.name === 'NotAllowedError' || error?.name === 'SecurityError' ? 'denied' : 'none';
    }
}

function lineOf(id) {
    return modules.get(id)?.voices[0].line;
}

navigator.mediaDevices?.addEventListener?.('devicechange', () => {
    for (const module of modules.values()) {
        const line = module.voices[0].line;
        if (line?.state === 'live') {
            listInputs(line).catch(() => {});
        }
    }
});

/** Starts the line input, asking the user for an input the first time; again after it was refused or missing. */
export function useLineIn(id) {
    const line = lineOf(id);
    if (line && line.state !== 'live' && line.state !== 'asking') {
        openLine(modules.get(id).voices[0], remembered(), false);
    }
}

/** Changes the line input to the device's next input (step 1) or its previous (step -1), round from the last to the first. */
export function stepLineIn(id, step) {
    const line = lineOf(id);
    if (line?.state !== 'live' || line.devices.length < 2) {
        return;
    }

    const at = line.devices.findIndex(device => device.deviceId === line.deviceId);
    const count = line.devices.length;
    openLine(modules.get(id).voices[0], line.devices[(((at + step) % count) + count) % count].deviceId, true);
}

/** The line input's state (idle, asking, live, denied or none), its input's label, that input's index and how many there are. */
export function lineIn(id) {
    const line = lineOf(id);
    if (!line) {
        return ['idle', '', '0', '0'];
    }

    const at = Math.max(0, line.devices.findIndex(device => device.deviceId === line.deviceId));
    return [line.state, line.devices[at]?.label ?? '', String(at), String(line.devices.length)];
}

const speakerBuffer = new Float32Array(SpeakerWindow);

/**
 * Fills the view (a .NET Span<double>) with the left and then the right speaker's peak magnitude over its window and
 * returns how many it filled: none while the audio isn't running.
 */
export function readSpeakers(view) {
    if (context.state !== 'running') {
        return 0;
    }

    const count = Math.min(view.length, speakers.length);
    const peaks = new Float64Array(count);
    for (let i = 0; i < count; i++) {
        peaks[i] = speakerPeak(i);
    }

    view.set(peaks);
    return count;
}

// A speaker's peak magnitude over its window
function speakerPeak(index) {
    speakers[index].getFloatTimeDomainData(speakerBuffer);
    let peak = 0;
    for (const sample of speakerBuffer) {
        peak = Math.max(peak, Math.abs(sample));
    }

    return peak;
}

/** For the page's ?debug readout: when the last note started, on the page's clock (performance.now()). */
export const timing = { noteOn: -1 };

/** For the page's ?debug readout: the context's own latency and the output's after it, in seconds, if the browser says. */
export function latencies() {
    return [context.baseLatency ?? NaN, context.outputLatency ?? NaN];
}

/** Whether the page is on a narrow or touch screen, where the piano has three octaves rather than four. */
export function narrowScreen() {
    return matchMedia('(pointer: coarse)').matches || innerWidth < NarrowWidth;
}

/**
 * Which parts of the room's finish to draw, a bit each: the bloom, the vignette and the colour fringe, each on unless the page's
 * address turns it off (?bloom=0, ?vignette=0, ?fringe=0). A device with little memory goes without the bloom.
 */
export function finishOptions() {
    const query = new URLSearchParams(location.search);
    const on = name => query.get(name) !== '0';
    const lowEnd = (navigator.deviceMemory ?? 8) < 4;
    // One bit each, in that order
    return (on('bloom') && !lowEnd ? 1 : 0) | (on('vignette') ? 2 : 0) | (on('fringe') ? 4 : 0);
}

/** How many samples a pixel the room is drawn with: 4 (WebGPU's only multisampling), or 1 when the address says ?msaa=0. */
export function msaaSamples() {
    return new URLSearchParams(location.search).get('msaa') === '0' ? 1 : 4;
}

export function state() {
    return context.state;
}

/** The output's level over the last few milliseconds: its RMS, after the volume and the limiter. */
export function level() {
    analyser.getFloatTimeDomainData(samples);
    let sum = 0;
    for (const sample of samples) {
        sum += sample * sample;
    }

    return Math.sqrt(sum / samples.length);
}

/** For checks: the context's state, how many modules and cables it realises, the keyboard voices held and the output's level. */
export function stats() {
    const held = [...modules.values()].flatMap(module => module.voices).filter(voice => voice.held).length;
    return { state: context.state, modules: modules.size, connections: cables.size, held, level: level() };
}

/**
 * For checks: what the audio thread runs that grows with the patch, across every voice: the oscillators playing, and the
 * knobs looking their value up in their table because a cable is in their CV input.
 */
export function graphLoad() {
    const voices = [...modules.values()].flatMap(module => module.voices);
    const oscillators = voices.flatMap(voice => voice.sources).filter(source => source instanceof OscillatorNode).length;
    const tables = voices.flatMap(voice => [...voice.knobs.values()]).filter(knob => knob.cabled).length;
    return { oscillators, tables };
}

/** For checks: the sequencer's last few steps played, each its time on the audio's clock, the step and whether its gate opened, oldest first. */
export function sequencerPlayed(id) {
    return modules.get(id)?.voices[0].sequence?.played.map(entry => [...entry]) ?? [];
}

// The output's limiter and what reaches it, for checks
let output = null;

/**
 * For checks: the output's headroom now, over the last few thousand samples: the peak reaching the limiter (after the volume),
 * the peak leaving it, and how many decibels the limiter takes off now.
 */
export function outputHeadroom() {
    if (!output) {
        return null;
    }

    const peak = node => {
        const buffer = new Float32Array(node.fftSize);
        node.getFloatTimeDomainData(buffer);
        return buffer.reduce((most, sample) => Math.max(most, Math.abs(sample)), 0);
    };
    return { reaching: peak(output.reaching), leaving: peak(analyser), reduction: output.limiter.reduction };
}

/** For checks: the audio's clock, in seconds. */
export function audioTime() {
    return context.currentTime;
}

/** For checks: RANDOM's last few steps, each its time on the audio's clock and its S&H level, oldest first. */
export function randomSteps(id) {
    return modules.get(id)?.voices[0].random?.steps.map(([time, level]) => [time, level]) ?? [];
}

/** For checks: the ids of the modules of a kind. */
export function modulesOf(kind) {
    return [...modules].filter(([, module]) => module.kind === kind).map(([id]) => id);
}

/** For checks: an output's signal now, on the module's first voice. */
export function outputValue(id, jack) {
    const voice = modules.get(id)?.voices[0];
    const output = voice?.outputs[jack];
    if (!output) {
        return null;
    }

    voice.probes ??= {};
    voice.probes[jack] ??= reading({ value: output, plugged: 1 });
    return voice.probes[jack]();
}

export function sampleRate() {
    return context.sampleRate;
}

/**
 * Fills the view (a .NET Span<byte> over floats) with the latest stretch of the scope's input as 32-bit floats, oldest
 * first, and returns how many samples it filled: none while the audio isn't running. The scope keeps as many as it was
 * last asked for, up to 32768.
 */
export function readScope(id, view) {
    const voice = modules.get(id)?.voices[0];
    if (!voice?.analyser || context.state !== 'running') {
        return 0;
    }

    const wanted = Math.min(Math.floor(view.length / 4), 32768);
    const size = Math.max(32, 2 ** Math.ceil(Math.log2(wanted)));
    if (voice.analyser.fftSize !== size) {
        voice.analyser.fftSize = size;
        voice.buffer = new Float32Array(size);
    }

    voice.analyser.getFloatTimeDomainData(voice.buffer);
    view.set(new Uint8Array(voice.buffer.buffer, (size - wanted) * 4, wanted * 4));
    return wanted;
}

/** A knob's value as the audio has it now, CV and all: its own value while nothing is in its CV input. NaN if there's no such knob. */
export function readKnob(id, name) {
    const knob = modules.get(id)?.voices[0].knobs.get(name);
    if (!knob) {
        return NaN;
    }

    knob.reader ??= reading(knob);
    return knob.reader();
}

/** For checks: a knob's value as the audio has it now, CV and all. */
export function knobValue(id, name) {
    const knob = modules.get(id)?.voices[0].knobs.get(name);
    if (!knob) {
        return null;
    }

    knob.probe ??= reading({ ...knob, plugged: 1 });
    return knob.probe();
}
