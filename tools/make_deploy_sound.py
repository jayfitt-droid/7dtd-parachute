#!/usr/bin/env python3
"""Procedurally create the parachute deploy sound.

Output: 44.1 kHz, 16-bit mono WAV, about 1.45 s, peak -1 dBFS. The result is
deterministic: the same RNG seed always produces the same file, so it can be
regenerated and committed. Layers:

  1. 0.00-0.25 s  pack release: band-passed noise (0.7-2.5 kHz) with a
                  slow amplitude flutter.
  2. 0.15-0.85 s  fabric unfurl: pink noise through a low-pass that sweeps
                  2.5 kHz down to 300 Hz, with a rise-then-fall envelope and a
                  light random tremolo.
  3. ~0.80 s      canopy snap and inflation thump: a 55->32 Hz sine (plus a
                  quiet octave above so small speakers still carry it) with a
                  200 ms exponential decay, a short filtered noise transient,
                  and a 400 ms fabric tail.
"""

import argparse
import math
import os
import struct
import wave

import numpy as np

SAMPLE_RATE = 44100
SEED = 20261012


def one_pole_lowpass(x, cutoff_hz, sample_rate=SAMPLE_RATE):
    """Single-pole RC low-pass, cutoff in Hz (may be an array for a sweep)."""
    cutoff = np.asarray(cutoff_hz, dtype=np.float64)
    if cutoff.ndim == 0:
        cutoff = np.full(x.shape, float(cutoff))
    alpha = 1.0 - np.exp(-2.0 * math.pi * cutoff / sample_rate)
    y = np.empty_like(x)
    state = 0.0
    for i in range(x.shape[0]):
        state += alpha[i] * (x[i] - state)
        y[i] = state
    return y


def bandpass(x, low_hz, high_hz):
    low = one_pole_lowpass(x, high_hz)
    high = one_pole_lowpass(x, low_hz)
    return low - high


def pink_noise(rng, length):
    """1/f-ish noise: sum a few octaves of white noise."""
    out = np.zeros(length)
    octaves = 6
    for octave in range(octaves):
        step = 2 ** octave
        count = length // step + 2
        coarse = rng.standard_normal(count)
        upsampled = np.interp(
            np.arange(length), np.arange(count) * step, coarse
        )
        out += upsampled / (octave + 1)
    return out


def envelope(length, start, attack, release):
    """A simple attack/release envelope over [start, start+length) samples."""
    t = np.arange(length) / SAMPLE_RATE
    env = np.ones(length)
    env = np.minimum(1.0, t / max(attack, 1e-6))
    env *= np.minimum(1.0, (t[-1] - t) / max(release, 1e-6)) if length else env
    env = np.clip(env, 0.0, 1.0)
    # Move into place
    full = np.zeros(int(start * SAMPLE_RATE) + length)
    full[int(start * SAMPLE_RATE):] = env
    return full


def add_at(track, start_s, segment):
    start = int(round(start_s * SAMPLE_RATE))
    end = start + segment.shape[0]
    if end > track.shape[0]:
        end = track.shape[0]
        segment = segment[: end - start]
    track[start:end] += segment


def build():
    rng = np.random.default_rng(SEED)
    duration = 1.45
    total = int(duration * SAMPLE_RATE)
    track = np.zeros(total)

    # --- 1. Pack release -------------------------------------------------
    release_len = int(0.25 * SAMPLE_RATE)
    noise = rng.standard_normal(release_len)
    rip = bandpass(noise, 700.0, 2500.0)
    t = np.arange(release_len) / SAMPLE_RATE
    flutter = 0.6 + 0.4 * np.sin(2.0 * math.pi * 38.0 * t)
    rip *= flutter
    rip *= np.exp(-t * 14.0)
    rip /= max(np.max(np.abs(rip)), 1e-9)
    add_at(track, 0.0, rip * 0.45)

    # --- 2. Fabric unfurl -------------------------------------------------
    unfurl_start = 0.15
    unfurl_len = int(0.70 * SAMPLE_RATE)
    pink = pink_noise(rng, unfurl_len)
    t = np.arange(unfurl_len) / SAMPLE_RATE
    sweep = np.linspace(2500.0, 300.0, unfurl_len)
    whoosh = one_pole_lowpass(pink, sweep)
    rise_fall = np.sin(math.pi * np.clip(t / 0.70, 0.0, 1.0)) ** 1.2
    tremolo = 1.0 + 0.12 * np.sin(2.0 * math.pi * 16.0 * t + 1.1) * rng.standard_normal(1)
    whoosh *= rise_fall * tremolo
    whoosh /= max(np.max(np.abs(whoosh)), 1e-9)
    add_at(track, unfurl_start, whoosh * 0.55)

    # --- 3. Canopy snap and inflation thump ------------------------------
    snap_start = 0.80
    thump_len = int(0.60 * SAMPLE_RATE)
    t = np.arange(thump_len) / SAMPLE_RATE
    freq = np.linspace(55.0, 32.0, thump_len)
    phase = 2.0 * math.pi * np.cumsum(freq) / SAMPLE_RATE
    thump = (np.sin(phase) + 0.35 * np.sin(2.0 * phase)) * np.exp(-t * (1.0 / 0.20))
    # 10 ms filtered noise transient on top of the snap
    transient_len = int(0.010 * SAMPLE_RATE)
    transient = bandpass(rng.standard_normal(transient_len), 200.0, 1500.0)
    transient *= np.exp(-np.arange(transient_len) / (0.25 * transient_len))
    transient /= max(np.max(np.abs(transient)), 1e-9)
    thump[:transient_len] += transient * 0.6
    thump /= max(np.max(np.abs(thump)), 1e-9)
    add_at(track, snap_start, thump * 0.9)

    # 400 ms fabric tail after the snap
    tail_len = int(0.40 * SAMPLE_RATE)
    tail = pink_noise(rng, tail_len)
    tail = one_pole_lowpass(tail, 1200.0)
    tail *= np.exp(-np.arange(tail_len) / (0.14 * SAMPLE_RATE))
    tail /= max(np.max(np.abs(tail)), 1e-9)
    add_at(track, snap_start + 0.03, tail * 0.30)

    # --- Normalize and fade ----------------------------------------------
    peak = np.max(np.abs(track))
    if peak > 0:
        track *= (10 ** (-1.0 / 20.0)) / peak
    fade = int(0.005 * SAMPLE_RATE)
    track[:fade] *= np.linspace(0.0, 1.0, fade)
    track[-fade:] *= np.linspace(1.0, 0.0, fade)
    return track


def write_wav(path, samples):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    clipped = np.clip(samples, -1.0, 1.0)
    pcm = (clipped * 32767.0).astype("<i2")
    with wave.open(path, "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(SAMPLE_RATE)
        handle.writeframes(pcm.tobytes())


def main():
    repo = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    default_out = os.path.join(
        repo, "unity", "ParachuteAssets", "Assets", "Parachute", "Audio", "ParachuteDeploy.wav"
    )
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", default=default_out, help="output WAV path")
    args = parser.parse_args()
    samples = build()
    write_wav(args.out, samples)
    print("wrote %s (%.2f s, peak %.1f dBFS)" % (
        args.out, samples.shape[0] / SAMPLE_RATE,
        20.0 * math.log10(max(float(np.max(np.abs(samples))), 1e-9)),
    ))


if __name__ == "__main__":
    main()
