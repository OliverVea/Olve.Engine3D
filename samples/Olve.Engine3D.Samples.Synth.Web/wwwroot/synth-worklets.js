// The synth's audio worklets, for what native Web Audio nodes can't do: synth.js loads them before it builds a module.

// How close an exponential slew gets to its target in its time: within e^-4, about 2%
const ExponentialSpan = 4;
const ShortestTime = 1e-4;

// Follows its input slowly: one unit up in `rise` seconds and one down in `fall`, in a straight line at a shape of 0,
// closing in ever slower (one pole) at 1, and in between at shapes between. With `rectify` it follows the input's
// magnitude, as an envelope follower.
class SlewProcessor extends AudioWorkletProcessor {
    static get parameterDescriptors() {
        return [
            { name: 'rise', defaultValue: 0, automationRate: 'k-rate' },
            { name: 'fall', defaultValue: 0, automationRate: 'k-rate' },
            { name: 'shape', defaultValue: 0, automationRate: 'k-rate' },
        ];
    }

    constructor(options) {
        super();
        this.rectify = options.processorOptions?.rectify ?? false;
        this.level = 0;
        this.running = true;
        this.port.onmessage = event => {
            if (event.data === 'stop') {
                this.running = false;
            }
        };
    }

    process(inputs, outputs, parameters) {
        const input = inputs[0][0];
        const output = outputs[0][0];
        const seconds = 1 / sampleRate;
        const rise = Math.max(ShortestTime, parameters.rise[0]);
        const fall = Math.max(ShortestTime, parameters.fall[0]);
        const shape = Math.min(1, Math.max(0, parameters.shape[0]));
        // Scalars, not arrays: anything allocated here, hundreds of times a second, is garbage the audio thread must
        // stop to collect
        const riseStep = seconds / rise;
        const fallStep = seconds / fall;
        const risePull = 1 - Math.exp(-ExponentialSpan * seconds / rise);
        const fallPull = 1 - Math.exp(-ExponentialSpan * seconds / fall);
        let level = this.level;
        for (let i = 0; i < output.length; i++) {
            const raw = input ? input[i] : 0;
            const target = this.rectify ? Math.abs(raw) : raw;
            const rising = target > level;
            const step = rising ? riseStep : fallStep;
            const gap = target - level;
            const straight = level + Math.max(-step, Math.min(step, gap));
            const curved = level + gap * (rising ? risePull : fallPull);
            level = straight + (curved - straight) * shape;
            output[i] = level;
        }

        this.level = Number.isFinite(level) ? level : 0;
        return this.running;
    }
}

registerProcessor('slew', SlewProcessor);
