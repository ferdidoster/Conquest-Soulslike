"""
Synthesises the placeholder combat sounds (whooshes, flesh hits, clangs, energy shield hits, parry ring)
and the nano-injector's (cartridge draw, slam, nanite repair shimmer, empty click, refill chime).
Pure numpy, deterministic, so they can be regenerated or tweaked at any time:

    python Tools/Audio/make_combat_sfx.py Assets/_Project/Audio/Combat
"""

import os
import sys
import wave

import numpy as np

RATE = 44100


def t_axis(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def env(seconds, attack, decay_rate):
    t = t_axis(seconds)
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    return a * np.exp(-t * decay_rate)


def lowpass(x, cutoff):
    """One-pole low-pass; cutoff may be an array (sweeps)."""
    cutoff = np.broadcast_to(np.asarray(cutoff, dtype=float), x.shape)
    alpha = 1 - np.exp(-2 * np.pi * cutoff / RATE)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += alpha[i] * (x[i] - acc)
        y[i] = acc
    return y


def bandpass(x, centre, q=2.0):
    """State-variable band-pass; centre may sweep."""
    centre = np.broadcast_to(np.asarray(centre, dtype=float), x.shape)
    low = band = 0.0
    y = np.empty_like(x)
    damp = 1.0 / q
    for i in range(len(x)):
        f = 2 * np.sin(np.pi * min(centre[i], RATE / 6) / RATE)
        low += f * band
        high = x[i] - low - damp * band
        band += f * high
        y[i] = band
    return y


def normalise(x, peak=0.9):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 0 else x


def fade_out(x, seconds=0.01):
    n = min(len(x), int(RATE * seconds))
    x[-n:] *= np.linspace(1, 0, n)
    return x


def save(folder, name, x):
    x = fade_out(normalise(x))
    data = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(os.path.join(folder, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())


def whoosh(rng, seconds, f_lo, f_hi):
    t = t_axis(seconds)
    s = t / seconds
    centre = f_lo + (f_hi - f_lo) * np.sin(np.pi * np.clip(s * 1.3, 0, 1)) ** 2
    noise = rng.standard_normal(len(t))
    swell = np.sin(np.pi * s) ** 1.5 * (1 - s) ** 0.3
    return bandpass(noise, centre, q=1.4) * swell


def flesh_hit(rng, seconds=0.45, body=1.0):
    t = t_axis(seconds)
    # Low body thump with a falling pitch.
    pitch = 95 * np.exp(-t * 9) + 42
    thump = np.sin(2 * np.pi * np.cumsum(pitch) / RATE) * env(seconds, 0.002, 16) * body
    # Wet, crunchy noise burst.
    crunch = lowpass(rng.standard_normal(len(t)), 2200 * np.exp(-t * 14) + 300) * env(seconds, 0.001, 30)
    # A short slap at the very start.
    slap = bandpass(rng.standard_normal(len(t)), 3200, q=3) * env(seconds, 0.0005, 90)
    return thump * 0.9 + crunch * 1.4 + slap * 0.6


def clang(rng, seconds, base, partials, decay, brightness=1.0):
    t = t_axis(seconds)
    x = np.zeros(len(t))
    for i, ratio in enumerate(partials):
        f = base * ratio * (1 + rng.uniform(-0.01, 0.01))
        d = decay * (1 + i * 0.6)
        x += np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * np.exp(-t * d) / (1 + i * 0.5 / brightness)
    hit = lowpass(rng.standard_normal(len(t)), 5000) * env(seconds, 0.0005, 70)
    return x + hit * 0.8


def energy_hit(rng, seconds=0.65, f_start=1200, f_end=150, wobble=34, sub=1.0):
    """
    A blow absorbed by an energy shield: an electric zap, a resonant "vwomm" that sweeps down
    with a wobble, a detuned shimmer pulsing like a power field, sparse crackle and a soft sub thud.
    """
    t = t_axis(seconds)
    sweep = f_end + (f_start - f_end) * np.exp(-t * 13)
    phase = 2 * np.pi * np.cumsum(sweep * (1 + 0.2 * np.sin(2 * np.pi * wobble * t))) / RATE
    tone = (np.sin(phase) + 0.45 * np.sin(2.01 * phase) + 0.2 * np.sin(3.98 * phase)) * env(seconds, 0.001, 6.5)
    shimmer = (np.sin(2 * np.pi * 2300 * t) + np.sin(2 * np.pi * 2341 * t)) \
        * (0.5 + 0.5 * np.sin(2 * np.pi * 55 * t)) * env(seconds, 0.002, 9)
    sparks = (rng.random(len(t)) < 0.006 * np.exp(-t * 7)) * rng.choice([-1.0, 1.0], len(t))
    crackle = bandpass(sparks, 4800, q=1.2) * 4
    zap = bandpass(rng.standard_normal(len(t)), 7000 * np.exp(-t * 35) + 900, q=2.5) * env(seconds, 0.0003, 45)
    thud = np.sin(2 * np.pi * np.cumsum(60 * np.exp(-t * 6) + 38) / RATE) * env(seconds, 0.003, 10) * sub
    return tone * 0.8 + shimmer * 0.22 + crackle * 0.5 + zap * 0.9 + thud * 0.9


def energy_break(rng, seconds=1.2):
    """The shield giving way: a heavy energy hit, a stuttering power-down sweep and a burst of crackle."""
    t = t_axis(seconds)
    hit = np.concatenate([energy_hit(rng, 0.8, 1500, 110, 22, sub=1.6), np.zeros(len(t))])[:len(t)]
    sweep = 900 * np.exp(-t * 2.2) + 40
    stutter = (np.sin(2 * np.pi * 17 * t) > -0.2).astype(float)
    power_down = np.sign(np.sin(2 * np.pi * np.cumsum(sweep) / RATE)) * stutter * env(seconds, 0.05, 2.6)
    power_down = lowpass(power_down, 2500)
    sparks = (rng.random(len(t)) < 0.02 * np.exp(-t * 3)) * rng.choice([-1.0, 1.0], len(t))
    crackle = bandpass(sparks, 3800, q=1.0) * 4
    return hit + power_down * 0.35 + crackle * 0.6


def energy_raise(seconds=0.28):
    """A short rising hum as the shield switches on."""
    t = t_axis(seconds)
    sweep = 140 + 520 * (t / seconds) ** 0.6
    phase = 2 * np.pi * np.cumsum(sweep) / RATE
    tone = np.sin(phase) + 0.35 * np.sin(2 * phase) + 0.15 * np.sin(3 * phase)
    shape = np.clip(t / 0.03, 0, 1) * np.exp(-np.clip(t - 0.12, 0, None) * 18)
    return lowpass(tone * shape, 3000)


def gun_shot(rng, seconds=0.9):
    """A black-powder pistol: a hard crack, a chest thump and a rolling tail of smoke and echo."""
    t = t_axis(seconds)
    noise = rng.standard_normal(len(t))
    crack = bandpass(noise, 2600, q=0.7) * np.exp(-t * 90) * 3
    thump = np.sin(2 * np.pi * np.cumsum(55 + 140 * np.exp(-t * 40)) / RATE) * env(seconds, 0.001, 14) * 1.4
    tail = lowpass(noise, 700) * env(seconds, 0.004, 5.5) * 1.6
    echo = np.concatenate([np.zeros(int(RATE * 0.11)), lowpass(noise, 1200) * np.exp(-t * 9)])[:len(t)] * 0.35
    click = np.concatenate([np.zeros(int(RATE * 0.002)), bandpass(noise, 5000, q=3) * np.exp(-t * 900)])[:len(t)]
    return fade_out(crack + thump + tail + echo + click * 0.5, 0.05)


def injector_draw(rng, seconds=0.22):
    """The cartridge sliding out of its belt clip: a short metal scrape and a latch tick."""
    t = t_axis(seconds)
    noise = rng.standard_normal(len(t))
    scrape = bandpass(noise, np.linspace(2400, 4200, len(t)), q=3) * env(seconds, 0.02, 14)
    tick = np.concatenate([np.zeros(int(RATE * 0.12)), bandpass(noise, 6000, q=4) * np.exp(-t * 400)])[:len(t)]
    return fade_out(scrape + tick * 2.0, 0.02)


def injector_slam(rng, seconds=0.75):
    """The cartridge rammed into the gauntlet port: a heavy clunk, a lock click and a pneumatic hiss."""
    t = t_axis(seconds)
    noise = rng.standard_normal(len(t))
    clunk = clang(rng, seconds, 210, [1.0, 2.3, 4.1], 18, brightness=0.6) * 0.8
    thump = np.sin(2 * np.pi * np.cumsum(70 + 120 * np.exp(-t * 35)) / RATE) * env(seconds, 0.001, 18) * 1.2
    click = np.concatenate([np.zeros(int(RATE * 0.035)), bandpass(noise, 5200, q=5) * np.exp(-t * 600)])[:len(t)]
    hiss_env = np.clip((t - 0.06) / 0.05, 0, 1) * np.exp(-np.clip(t - 0.06, 0, None) * 5.5)
    hiss = bandpass(noise, np.linspace(5200, 2600, len(t)), q=0.8) * hiss_env * 0.9
    return fade_out(clunk + thump + click * 1.5 + hiss, 0.05)


def nanite_repair(rng, seconds=1.3):
    """The nanites at work: a rising glassy shimmer with a soft hum under it."""
    t = t_axis(seconds)
    shape = np.sin(np.pi * np.clip(t / seconds, 0, 1)) ** 1.5
    hum = np.sin(2 * np.pi * np.cumsum(180 + 120 * t / seconds) / RATE) * 0.35
    sparkle = np.zeros(len(t))
    for _ in range(60):  # little glassy pings scattered through the swell
        start = int(rng.uniform(0, seconds - 0.12) * RATE)
        f = rng.uniform(1800, 5200) * (1 + 0.4 * start / len(t))
        n = int(RATE * 0.1)
        ping = np.sin(2 * np.pi * f * np.arange(n) / RATE) * np.exp(-np.arange(n) / RATE * 45)
        sparkle[start:start + n] += ping[:len(t) - start] * rng.uniform(0.2, 0.5)
    sweep = np.sin(2 * np.pi * np.cumsum(600 + 900 * (t / seconds) ** 2) / RATE) * 0.15
    return fade_out((hum + sparkle + sweep) * shape, 0.08)


def injector_empty(rng, seconds=0.18):
    """Pressing an empty injector: a dry double click."""
    t = t_axis(seconds)
    noise = rng.standard_normal(len(t))
    click = bandpass(noise, 3200, q=4) * np.exp(-t * 500)
    second = np.concatenate([np.zeros(int(RATE * 0.06)), click])[:len(t)] * 0.7
    return fade_out(click + second, 0.02)


def injector_refill(seconds=1.4):
    """Resting at a checkpoint: a soft rising three-note chime."""
    t = t_axis(seconds)
    out = np.zeros(len(t))
    for i, f in enumerate((523.25, 659.25, 987.77)):
        start = int(RATE * 0.12 * i)
        n = len(t) - start
        tt = np.arange(n) / RATE
        tone = (np.sin(2 * np.pi * f * tt) + 0.3 * np.sin(2 * np.pi * 2 * f * tt)) * np.exp(-tt * 3.2)
        out[start:] += tone * np.clip(tt / 0.01, 0, 1)
    return fade_out(out, 0.1)


def main(folder):
    os.makedirs(folder, exist_ok=True)
    rng = np.random.default_rng(7)

    save(folder, "Swing_Light_1", whoosh(rng, 0.3, 350, 1900))
    save(folder, "Swing_Light_2", whoosh(rng, 0.28, 420, 2200))
    save(folder, "Swing_Heavy_1", whoosh(rng, 0.5, 180, 1100))

    save(folder, "Hit_Flesh_1", flesh_hit(rng))
    save(folder, "Hit_Flesh_2", flesh_hit(rng, body=1.2))
    save(folder, "Hit_Flesh_3", flesh_hit(rng, body=0.8))
    heavy = flesh_hit(rng, 0.7, body=1.8)
    heavy += np.concatenate([np.zeros(int(RATE * 0.03)), flesh_hit(rng, 0.67, body=0.6)])[:len(heavy)] * 0.7
    save(folder, "Hit_Flesh_Heavy", heavy)

    metal = [1.0, 2.76, 5.4, 8.93, 13.34]
    save(folder, "Block_1", clang(rng, 0.7, 410, metal, 7))
    save(folder, "Block_2", clang(rng, 0.7, 365, metal, 7))
    guard = clang(rng, 0.9, 300, metal, 5) + flesh_hit(rng, 0.9, body=1.5) * 0.8
    save(folder, "Guard_Break", guard)
    save(folder, "Parry", clang(rng, 1.4, 640, [1.0, 2.0, 3.01, 4.17, 5.43, 6.8], 3.2, brightness=2.0))

    save(folder, "Shield_Hit_1", energy_hit(rng))
    save(folder, "Shield_Hit_2", energy_hit(rng, f_start=1000, f_end=130, wobble=41))
    save(folder, "Shield_Break", energy_break(rng))
    save(folder, "Shield_Up", energy_raise())

    save(folder, "Gun_Shot", gun_shot(rng))

    save(folder, "Injector_Draw", injector_draw(rng))
    save(folder, "Injector_Slam", injector_slam(rng))
    save(folder, "Nanite_Repair", nanite_repair(rng))
    save(folder, "Injector_Empty", injector_empty(rng))
    save(folder, "Injector_Refill", injector_refill())


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Combat")
