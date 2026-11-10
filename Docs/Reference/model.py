"""Reference model for the ClassroomTwin end-sem upgrade.
Every number in the spec PDF is produced by this file. The C# core must match it.
"""
import math

# ---------------- membership functions ----------------
def trap(x, a, b, c, d):
    # shoulders: a==b -> left shoulder (1 for x<=b), c==d -> right shoulder (1 for x>=c)
    if x < a or x > d:
        return 0.0
    if b <= x <= c:
        return 1.0
    if x < b:
        return 1.0 if b == a else (x - a) / (b - a)
    return 1.0 if d == c else (d - x) / (d - c)

def tri(x, a, b, c):
    return trap(x, a, b, b, c)

def mf(x, p):
    return tri(x, *p) if len(p) == 3 else trap(x, *p)

# ---------------- fuzzy variables ----------------
# Light FIS (CCIS 2024) inputs
LIGHT_IN = {"Low": (0, 0, 25, 45), "Medium": (35, 55, 75), "High": (60, 80, 100, 100)}
TEMP_LFIS = {"Cold": (0, 0, 10, 20), "Warm": (15, 22.5, 30), "Hot": (25, 30, 40, 40)}
SLOT_PAPER = {"Sleep": (0, 0, 5, 10), "Work": (8, 14, 20), "Relax": (18, 20, 24, 24)}
SLOT_CLASS = {"Sleep": (0, 0, 6, 8), "Work": (7, 9, 17, 19), "Relax": (17, 19, 24, 24)}
LIGHT_OUT = {"Low": (-10, 20, 50), "Medium": (20, 50, 80), "High": (50, 80, 110)}
LIGHT_OUT_U = (-10.0, 110.0, 0.5)   # min, max, step  -> 241 samples

# Curtain FIS (IoT-J 2026) inputs
TEMP_CFIS = {"Cold": (0, 0, 16, 22), "Warm": (18, 25, 32), "Hot": (28, 34, 40, 40)}
AQI_IN = {"Clean": (0, 0, 100, 200), "Poor": (100, 200, 500, 500)}
ANGLE = {"Open": (-30, 0, 30), "Partial": (15, 45, 75), "Closed": (45, 75, 105)}
ANGLE_U = (-30.0, 105.0, 0.5)       # 271 samples

# CCIS Table I, 27 rules: (light, slot, temp) -> output
LIGHT_RULES = []
_tab = {
    "Low":    {"Work": ("High", "Medium", "Medium"), "Relax": ("Medium", "Medium", "Low"), "Sleep": ("Low", "Low", "Low")},
    "Medium": {"Work": ("High", "High", "Medium"),   "Relax": ("Medium", "Medium", "Medium"), "Sleep": ("Low", "Low", "Low")},
    "High":   {"Work": ("Medium", "Low", "Low"),     "Relax": ("Medium", "Medium", "Low"), "Sleep": ("Medium", "Low", "Low")},
}
for L in ("Low", "Medium", "High"):
    for S in ("Work", "Relax", "Sleep"):
        for i, T in enumerate(("Cold", "Warm", "Hot")):
            LIGHT_RULES.append((L, S, T, _tab[L][S][i]))

# IoT-J Table I (R1-R11) + extensions (R12-R17). None = Any.
# (light, temp, aqi, slot, prevAngle, out)
CURTAIN_RULES = [
    ("Low", None, None, "Sleep", None, "Closed"),        # R1
    ("Medium", "Cold", "Clean", "Work", "Open", "Open"),  # R2
    ("Medium", "Warm", "Clean", "Work", None, "Partial"), # R3
    ("Medium", None, None, "Relax", None, "Partial"),     # R4
    ("High", "Cold", "Clean", "Work", "Open", "Open"),    # R5
    ("High", "Warm", "Clean", "Work", "Partial", "Open"), # R6
    ("High", "Hot", "Clean", "Relax", None, "Partial"),   # R7
    ("High", None, "Poor", None, None, "Partial"),        # R8
    ("Medium", None, "Poor", None, None, "Partial"),      # R9
    (None, None, None, "Sleep", None, "Closed"),          # R10
    (None, None, None, None, "Closed", "Partial"),        # R11
    # ---- extensions (fill coverage gaps of the paper's table) ----
    ("High", "Hot", None, "Work", None, "Closed"),        # R12 thermal/glare protection
    ("Low", None, "Clean", "Work", None, "Open"),         # R13 harvest scarce daylight
    ("Medium", "Hot", "Clean", "Work", None, "Partial"),  # R14
    ("High", "Cold", "Clean", "Work", None, "Open"),      # R15 (R5 without prev condition)
    ("Medium", "Cold", "Clean", "Work", None, "Open"),    # R16 (R2 without prev condition)
    ("High", "Warm", "Clean", "Work", None, "Open"),      # R17 (R6 without prev condition)
    ("Low", None, None, "Relax", None, "Closed"),         # R18 evening privacy
]

def samples(u):
    lo, hi, st = u
    n = int(round((hi - lo) / st)) + 1
    return [lo + i * st for i in range(n)]

def defuzz(acts, outsets, u):
    # acts: dict out_label -> activation (max over rules)
    num = den = 0.0
    for y in samples(u):
        m = 0.0
        for lab, a in acts.items():
            if a > 0:
                v = min(a, mf(y, outsets[lab]))
                if v > m:
                    m = v
        num += y * m
        den += m
    if den <= 1e-12:
        return None
    return num / den

def clamp(x, lo, hi):
    return lo if x < lo else hi if x > hi else x

def light_fis(light_pct, hour, temp, slots, rules=None):
    rules = LIGHT_RULES if rules is None else rules
    light_pct = clamp(light_pct, 0.0, 100.0); hour = clamp(hour, 0.0, 24.0); temp = clamp(temp, 0.0, 40.0)
    mL = {k: mf(light_pct, p) for k, p in LIGHT_IN.items()}
    mS = {k: mf(hour, p) for k, p in slots.items()}
    mT = {k: mf(temp, p) for k, p in TEMP_LFIS.items()}
    acts = {"Low": 0.0, "Medium": 0.0, "High": 0.0}
    for L, S, T, o in rules:
        a = min(mL[L], mS[S], mT[T])
        if a > acts[o]:
            acts[o] = a
    y = defuzz(acts, LIGHT_OUT, LIGHT_OUT_U)
    return (None if y is None else max(0.0, min(100.0, y))), acts

def curtain_fis(light_pct, temp, aqi, hour, prev_angle, slots, use_ext=True):
    light_pct = clamp(light_pct, 0.0, 100.0); temp = clamp(temp, 0.0, 40.0); aqi = clamp(aqi, 0.0, 500.0)
    hour = clamp(hour, 0.0, 24.0); prev_angle = clamp(prev_angle, 0.0, 75.0)
    mL = {k: mf(light_pct, p) for k, p in LIGHT_IN.items()}
    mT = {k: mf(temp, p) for k, p in TEMP_CFIS.items()}
    mA = {k: mf(aqi, p) for k, p in AQI_IN.items()}
    mS = {k: mf(hour, p) for k, p in slots.items()}
    mP = {k: mf(prev_angle, p) for k, p in ANGLE.items()}
    acts = {"Open": 0.0, "Partial": 0.0, "Closed": 0.0}
    rules = CURTAIN_RULES if use_ext else CURTAIN_RULES[:11]
    for (L, T, A, S, P, o) in rules:
        a = min(
            1.0 if L is None else mL[L],
            1.0 if T is None else mT[T],
            1.0 if A is None else mA[A],
            1.0 if S is None else mS[S],
            1.0 if P is None else mP[P],
        )
        if a > acts[o]:
            acts[o] = a
    y = defuzz(acts, ANGLE, ANGLE_U)
    if y is None:
        return prev_angle, acts, False   # hold
    return max(0.0, min(75.0, y)), acts, True

# ---------------- environment ----------------
P_FIX = 65.0
N_FIX = 12
P_DIM = 34.0
E_ART_FULL = 500.0 * P_FIX / P_DIM      # 955.882 lux per zone at 100 %
E_REF = 1000.0                          # lux that maps to 100 % LDR reading
DF = {"Window": 0.050, "Center": 0.020, "Corridor": 0.008}
ROWS = ("Window", "Center", "Corridor")
SLAT_DEPTH, SLAT_PITCH, TAU_SLAT = 0.17, 0.16, 0.05
STATIC_ANGLE = 0.0

COMFORT = (400.0, 2000.0)
PIR_HOLD_MIN = 10

WEATHER = {
    "Normal":   dict(Ediff=20000.0, Tmean=24.0, Tamp=4.0, dTgain=5.0, AQIout=45.0),
    "Heatwave": dict(Ediff=25000.0, Tmean=34.0, Tamp=4.0, dTgain=5.0, AQIout=45.0),
    "Pollution": dict(Ediff=20000.0, Tmean=24.0, Tamp=4.0, dTgain=5.0, AQIout=250.0),
}
A_WIN = 3 * 3.5 * 1.6      # 16.8 m2 glazing
SHGC = 0.60
COP = 3.0
I_PEAK = {"Normal": 400.0, "Heatwave": 550.0, "Pollution": 400.0}   # W/m2 on facade
TAU_TH_H = 1.0     # thermal time constant [h]
TAU_AQ_H = 0.5     # air time constant [h]
AQ_OCC_GAIN = 60.0

def sun_factor(t):
    return math.sin(math.pi * (t - 6.0) / 12.0) if 6.0 <= t <= 18.0 else 0.0

def coverage(theta):
    return min(1.0, SLAT_DEPTH * math.sin(math.radians(theta)) / SLAT_PITCH)

def tau_blind(theta):
    return 1.0 - coverage(theta) * (1.0 - TAU_SLAT)

def t_out(t, w):
    return w["Tmean"] + w["Tamp"] * math.cos(2 * math.pi * (t - 15.0) / 24.0)

def occupied(t):
    return (9.0 <= t < 13.0) or (14.0 <= t < 18.0)

def comfort_mu(E):
    return trap(E, 300.0, 400.0, 2000.0, 3000.0)

# ---------------- simulation ----------------
SCEN = {
    0: "Baseline (24/7 ON)",
    1: "Manager (09:00-18:00)",
    2: "PIR occupancy (10-min hold)",
    3: "Uniform dimming 34 W (24/7)",
    4: "Static daylight (window row OFF 09-13)",
    5: "Fuzzy lighting, CCIS 2024 (blinds static 45 deg)",
    6: "Fuzzy win-curtain co-regulation, IoT-J 2026",
}

def simulate(scen, weather="Normal", slots=SLOT_CLASS, step_min=1, log=False):
    lrules = LIGHT_RULES
    w = WEATHER[weather]
    n = int(24 * 60 / step_min)
    dt = step_min / 60.0
    theta = 75.0 if scen == 6 else STATIC_ANGLE
    Tin = t_out(0.0, w)  # start at equilibrium (night, S=0)
    aqi = w["AQIout"]
    last_occ = -10**9
    E_Wh = 0.0
    occ_min = comfort_min = 0
    comfort_sum = 0.0
    rows_log = []
    held = 0
    Ecool_Wh = 0.0
    util_sum = 0.0
    Tpeak = -1e9; dh28 = 0.0; prev_theta = theta; reversals = 0; last_dir = 0; travel = 0.0
    for k in range(n):
        t = k * dt
        S = sun_factor(t)
        occ = occupied(t)
        if occ:
            last_occ = k
        pir = occ or (last_occ >= 0 and (k - last_occ) * step_min <= PIR_HOLD_MIN)
        # sensors (state at start of step, uses theta from previous step)
        Ediff = w["Ediff"] * S
        E_win_unshaded = DF["Window"] * Ediff
        L_curtain = min(100.0, E_win_unshaded / E_REF * 100.0)
        # ---- curtain decision ----
        if scen == 6:
            if pir:
                theta, acts, ok = curtain_fis(L_curtain, Tin, aqi, t, theta, slots)
                if not ok:
                    held += 1
            else:
                theta = 75.0
        tau = tau_blind(theta)
        # ---- lighting decision ----
        La = {}
        Eday = {r: DF[r] * Ediff * tau for r in ROWS}
        for r in ROWS:
            if scen == 0:
                La[r] = 1.0
            elif scen == 1:
                La[r] = 1.0 if 9.0 <= t < 18.0 else 0.0
            elif scen == 2:
                La[r] = 1.0 if pir else 0.0
            elif scen == 3:
                La[r] = P_DIM / P_FIX
            elif scen == 4:
                La[r] = 0.0 if (r == "Window" and 9.0 <= t < 13.0) else 1.0
            else:
                if pir:
                    Lpct = min(100.0, Eday[r] / E_REF * 100.0)
                    y, _ = light_fis(Lpct, t, Tin, slots, lrules)
                    La[r] = (y if y is not None else 0.0) / 100.0
                else:
                    La[r] = 0.0
        P = sum(4 * P_FIX * La[r] for r in ROWS)
        E_Wh += P * dt
        if occ:
            Qs = SHGC * A_WIN * I_PEAK[weather] * S * tau
            Ecool_Wh += (Qs + P) / COP * dt
        Etask = {r: Eday[r] + E_ART_FULL * La[r] for r in ROWS}
        if occ:
            Tpeak = max(Tpeak, Tin)
            dh28 += max(0.0, Tin - 28.0) * dt
        d = theta - prev_theta
        travel += abs(d)
        if abs(d) > 0.5:
            dr = 1 if d > 0 else -1
            if last_dir and dr != last_dir: reversals += 1
            last_dir = dr
        prev_theta = theta
        if occ:
            occ_min += step_min
            c = sum(comfort_mu(Etask[r]) for r in ROWS) / 3.0
            comfort_sum += c * step_min
            util_sum += 100.0 * sum(La[r] for r in ROWS) / 3.0 * step_min
            if all(COMFORT[0] <= Etask[r] <= COMFORT[1] for r in ROWS):
                comfort_min += step_min
        if log:
            rows_log.append(dict(t=t, P=P, theta=theta, Tin=Tin, aqi=aqi, pir=pir,
                                 La={r: La[r] for r in ROWS}, Eday=Eday, Etask=Etask, Lc=L_curtain))
        # ---- physics integration (explicit Euler, after decisions) ----
        Teq = t_out(t, w) + w["dTgain"] * S * tau
        Tin += (Teq - Tin) * (dt / TAU_TH_H)
        aq_eq = w["AQIout"] + (AQ_OCC_GAIN if occ else 0.0)
        aqi += (aq_eq - aqi) * (dt / TAU_AQ_H)
    return dict(E_kWh=E_Wh / 1000.0, occ_min=occ_min, comfort_min=comfort_min,
                Fc_avg=(comfort_sum / occ_min if occ_min else 0.0), held=held, log=rows_log,
                Tpeak=Tpeak, U_avg=(util_sum / occ_min if occ_min else 0.0), Ecool_kWh=Ecool_Wh/1000.0, dh28=dh28, reversals=reversals, travel=travel)

if __name__ == "__main__":
    base = simulate(0)["E_kWh"]
    for wname in ("Normal", "Heatwave", "Pollution"):
        print("==", wname)
        e2 = simulate(2, wname)["E_kWh"]
        for s in range(7):
            r = simulate(s, wname)
            print(f"S{s} {r['E_kWh']:.4f} kWh  vsS0 {100*(1-r['E_kWh']/base):6.2f}%  vsS2 {100*(1-r['E_kWh']/e2):6.2f}%  "
                  f"cool {r['Ecool_kWh']:.3f} tot {r['E_kWh']+r['Ecool_kWh']:.3f} band {100*r['comfort_min']/max(1,r['occ_min']):5.1f}%  Fc {r['Fc_avg']:.3f} held {r['held']} Tpk {r['Tpeak']:.2f} dh28 {r['dh28']:.2f} rev {r['reversals']} trav {r['travel']:.0f}")
