// ClassroomTwin - deterministic fixed-step digital-twin simulator (pure C#).
// One call to Step() advances exactly TwinConfig.StepMinutes simulated minutes.
// The Unity layer (SimulationEngine) and the 24-h batch evaluator both use this class,
// so the live demo and the reported numbers can never disagree.
using System;

namespace ClassroomTwin.Core
{
    public sealed class DayResult
    {
        public int Scenario;
        public Weather Weather;
        public double LightingKWh, CoolingKWh, TotalKWh;
        public int OccupiedMinutes, ComfortMinutes, HeldSteps, Reversals;
        public double ComfortBandPct, FcAvg, UAvgPct, ReducedArtificialPct, TPeakC, DegreeHours28, TravelDeg;
    }

    public sealed class TwinSimulator
    {
        // ---------- configuration ----------
        public int Scenario;
        public Weather Weather;
        public readonly bool PaperSlots;
        readonly MamdaniFIS lightFis;
        readonly MamdaniFIS curtainFis;

        // ---------- state ----------
        public int MinuteOfDay;            // 0..1439, time at the START of the next step
        public double ThetaDeg;            // modelled curtain slat angle (servo output)
        public double TinC;                // indoor temperature (free-running, fan-ventilated room)
        public double Aqi;                 // indoor AQI
        int lastOccupiedMinute = -100000;

        // ---------- last-step telemetry (read by HUD) ----------
        public double HourOfDay, SunFactor, EdiffLux, LdrCurtainPct, PowerW, TauBlind, ToutC;
        public bool Occupied, PirActive, CurtainHeld;
        public readonly double[] La = new double[TwinConfig.Rows];          // 0..1 per row
        public readonly double[] EdayLux = new double[TwinConfig.Rows];
        public readonly double[] EtaskLux = new double[TwinConfig.Rows];
        public readonly double[] LightActs = new double[3];                  // last evaluated row (diagnostics)
        public readonly double[] CurtainActs = new double[3];
        public double FcNow;

        // ---------- KPI accumulators (since last ResetKpis) ----------
        public double LightingWh, CoolingWh, BaselineWh, ComfortSum, UtilSum, TPeak, DegreeHours, Travel;
        public int OccMin, ComfortMin, Held, Reversals;
        double prevTheta; int lastDir;

        public TwinSimulator(int scenario, Weather weather, bool paperSlots = false, bool curtainExtensions = true)
        {
            Scenario = scenario; Weather = weather; PaperSlots = paperSlots;
            lightFis = TwinControllers.BuildLightFis(paperSlots);
            curtainFis = TwinControllers.BuildCurtainFis(paperSlots, curtainExtensions);
            ResetState();
        }

        // ================= environment model (static, unit-testable) =================
        public static double Sun(double h) { return (h >= 6.0 && h <= 18.0) ? Math.Sin(Math.PI * (h - 6.0) / 12.0) : 0.0; }

        public static double Tout(double h, WeatherProfile w) { return w.TMeanC + w.TAmpC * Math.Cos(2.0 * Math.PI * (h - 15.0) / 24.0); }

        public static double Coverage(double thetaDeg)
        {
            double c = TwinConfig.SlatDepthM * Math.Sin(thetaDeg * Math.PI / 180.0) / TwinConfig.SlatPitchM;
            return c > 1.0 ? 1.0 : c;
        }

        public static double Tau(double thetaDeg) { return 1.0 - Coverage(thetaDeg) * (1.0 - TwinConfig.SlatTransmittance); }

        public static bool IsOccupied(double h) { return (h >= 9.0 && h < 13.0) || (h >= 14.0 && h < 18.0); }

        // ================= lifecycle =================
        public void ResetState()
        {
            WeatherProfile w = TwinConfig.Profile(Weather);
            MinuteOfDay = 0;
            ThetaDeg = (Scenario == 6) ? TwinConfig.ClosedAngleDeg : TwinConfig.StaticBlindAngleDeg;
            TinC = Tout(0.0, w);
            Aqi = w.AqiOut;
            lastOccupiedMinute = -100000;
            ResetKpis();
        }

        public void ResetKpis()
        {
            LightingWh = CoolingWh = BaselineWh = ComfortSum = UtilSum = DegreeHours = Travel = 0.0;
            TPeak = double.NegativeInfinity;
            OccMin = ComfortMin = Held = Reversals = 0;
            prevTheta = ThetaDeg; lastDir = 0;
        }

        /// <summary>Switch scenario while running (live demo). Non-curtain scenarios snap blinds to the static angle.</summary>
        public void SetScenario(int s)
        {
            Scenario = s;
            if (s != 6) ThetaDeg = TwinConfig.StaticBlindAngleDeg;
            ResetKpis();
        }

        /// <summary>Fresh day, silently simulated from 00:00 up to startMinute so physical state is warm.</summary>
        public void ResetToMinute(int startMinute)
        {
            ResetState();
            for (int i = 0; i < startMinute; i++) Step();
            ResetKpis();
        }

        // ================= one fixed step =================
        public void Step()
        {
            WeatherProfile w = TwinConfig.Profile(Weather);
            double dt = TwinConfig.StepHours;
            int k = MinuteOfDay;
            double h = k / 60.0;
            HourOfDay = h;

            // 1. exogenous inputs
            SunFactor = Sun(h);
            ToutC = Tout(h, w);
            Occupied = IsOccupied(h);
            if (Occupied) lastOccupiedMinute = k;
            PirActive = Occupied || (k - lastOccupiedMinute) <= TwinConfig.PirHoldMinutes;
            EdiffLux = w.EdiffMaxLux * SunFactor;
            LdrCurtainPct = Math.Min(100.0, TwinConfig.DaylightFactor[0] * EdiffLux / TwinConfig.LdrFullScaleLux * 100.0);

            // 2. curtain decision (S6 only)
            CurtainHeld = false;
            if (Scenario == 6)
            {
                if (PirActive)
                {
                    double y = curtainFis.Evaluate(new[] { LdrCurtainPct, TinC, Aqi, h, ThetaDeg }, CurtainActs);
                    if (double.IsNaN(y)) { CurtainHeld = true; Held++; }   // no rule fired: hold position
                    else ThetaDeg = y;
                }
                else ThetaDeg = TwinConfig.ClosedAngleDeg;                  // room empty: close (heat gain / privacy)
            }
            TauBlind = Tau(ThetaDeg);

            // 3. lighting decision
            for (int r = 0; r < TwinConfig.Rows; r++)
            {
                EdayLux[r] = TwinConfig.DaylightFactor[r] * EdiffLux * TauBlind;
                La[r] = LightingLevel(r, h);
            }

            // 4. energy
            PowerW = 0.0;
            for (int r = 0; r < TwinConfig.Rows; r++) PowerW += TwinConfig.FixturesPerRow * TwinConfig.FixtureFullW * La[r];
            LightingWh += PowerW * dt;
            BaselineWh += TwinConfig.BaselinePowerW * dt;
            if (Occupied)
            {
                double qSolar = TwinConfig.Shgc * TwinConfig.WindowAreaM2 * w.IFacadePeak * SunFactor * TauBlind;
                CoolingWh += (qSolar + PowerW) / TwinConfig.AcCop * dt;
            }

            // 5. comfort + KPIs
            double fcSum = 0.0; bool allInBand = true; double laSum = 0.0;
            for (int r = 0; r < TwinConfig.Rows; r++)
            {
                EtaskLux[r] = EdayLux[r] + TwinConfig.ArtificialLuxFull * La[r];
                fcSum += TwinConfig.ComfortMembership.Mu(EtaskLux[r]);
                if (EtaskLux[r] < TwinConfig.ComfortLowLux || EtaskLux[r] > TwinConfig.ComfortHighLux) allInBand = false;
                laSum += La[r];
            }
            FcNow = fcSum / TwinConfig.Rows;
            if (Occupied)
            {
                if (TinC > TPeak) TPeak = TinC;
                DegreeHours += Math.Max(0.0, TinC - TwinConfig.DegreeHourBaseC) * dt;
                OccMin += TwinConfig.StepMinutes;
                ComfortSum += FcNow * TwinConfig.StepMinutes;
                if (allInBand) ComfortMin += TwinConfig.StepMinutes;
                UtilSum += 100.0 * laSum / TwinConfig.Rows * TwinConfig.StepMinutes;
            }
            double d = ThetaDeg - prevTheta;
            Travel += Math.Abs(d);
            if (Math.Abs(d) > 0.5)
            {
                int dir = d > 0 ? 1 : -1;
                if (lastDir != 0 && dir != lastDir) Reversals++;
                lastDir = dir;
            }
            prevTheta = ThetaDeg;

            // 6. physics integration (explicit Euler, after decisions)
            double teq = ToutC + w.DTGainC * SunFactor * TauBlind;
            TinC += (teq - TinC) * (dt / TwinConfig.ThermalTauH);
            double aqEq = w.AqiOut + (Occupied ? TwinConfig.AqiOccupancyGain : 0.0);
            Aqi += (aqEq - Aqi) * (dt / TwinConfig.AirTauH);

            // 7. advance clock
            MinuteOfDay += TwinConfig.StepMinutes;
            if (MinuteOfDay >= TwinConfig.MinutesPerDay)
            {
                MinuteOfDay -= TwinConfig.MinutesPerDay;
                lastOccupiedMinute -= TwinConfig.MinutesPerDay;
            }
        }

        double LightingLevel(int row, double h)
        {
            switch (Scenario)
            {
                case 0: return 1.0;
                case 1: return (h >= 9.0 && h < 18.0) ? 1.0 : 0.0;
                case 2: return PirActive ? 1.0 : 0.0;
                case 3: return TwinConfig.FixtureDimW / TwinConfig.FixtureFullW;
                case 4: return (row == 0 && h >= 9.0 && h < 13.0) ? 0.0 : 1.0;
                case 5:
                case 6:
                    if (!PirActive) return 0.0;
                    double lpct = Math.Min(100.0, EdayLux[row] / TwinConfig.LdrFullScaleLux * 100.0);
                    double y = lightFis.Evaluate(new[] { lpct, h, TinC }, LightActs);
                    return double.IsNaN(y) ? 0.0 : y / 100.0;
                default: return 1.0;
            }
        }

        // ================= results =================
        public DayResult Snapshot()
        {
            DayResult r = new DayResult();
            r.Scenario = Scenario; r.Weather = Weather;
            r.LightingKWh = LightingWh / 1000.0;
            r.CoolingKWh = CoolingWh / 1000.0;
            r.TotalKWh = r.LightingKWh + r.CoolingKWh;
            r.OccupiedMinutes = OccMin; r.ComfortMinutes = ComfortMin; r.HeldSteps = Held; r.Reversals = Reversals;
            r.ComfortBandPct = OccMin > 0 ? 100.0 * ComfortMin / OccMin : 0.0;
            r.FcAvg = OccMin > 0 ? ComfortSum / OccMin : 0.0;
            r.UAvgPct = OccMin > 0 ? UtilSum / OccMin : 0.0;
            r.ReducedArtificialPct = 100.0 - r.UAvgPct;          // IoT-J 2026 Eq. (12)
            r.TPeakC = TPeak; r.DegreeHours28 = DegreeHours; r.TravelDeg = Travel;
            return r;
        }

        /// <summary>Deterministic 24-h run (00:00 -> 24:00) from a fresh state.</summary>
        public static DayResult RunDay(int scenario, Weather weather, bool paperSlots = false)
        {
            TwinSimulator s = new TwinSimulator(scenario, weather, paperSlots);
            for (int i = 0; i < TwinConfig.MinutesPerDay / TwinConfig.StepMinutes; i++) s.Step();
            return s.Snapshot();
        }

        public static DayResult[,] RunAll()
        {
            int nw = TwinConfig.Profiles.Length;
            DayResult[,] res = new DayResult[nw, 7];
            for (int w = 0; w < nw; w++)
                for (int s = 0; s < 7; s++)
                    res[w, s] = RunDay(s, (Weather)w);
            return res;
        }

        public static string FormatTable(DayResult[,] res)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
            sb.AppendLine("weather,scenario,lighting_kWh,cooling_kWh,total_kWh,save_vs_S0_pct,save_vs_S2_pct,"
                        + "total_save_vs_S2_pct,comfort_band_pct,Fc_avg,U_avg_pct,reduced_artificial_pct,"
                        + "Tpeak_C,degree_hours_28,reversals,travel_deg,held_steps");
            for (int w = 0; w < res.GetLength(0); w++)
            {
                double e0 = res[w, 0].LightingKWh, e2 = res[w, 2].LightingKWh, t2 = res[w, 2].TotalKWh;
                for (int s = 0; s < res.GetLength(1); s++)
                {
                    DayResult r = res[w, s];
                    sb.AppendLine(string.Format(ci, "{0},S{1},{2:F4},{3:F4},{4:F4},{5:F2},{6:F2},{7:F2},{8:F1},{9:F3},{10:F2},{11:F2},{12:F2},{13:F2},{14},{15:F1},{16}",
                        TwinConfig.Profiles[w].Name, s, r.LightingKWh, r.CoolingKWh, r.TotalKWh,
                        100.0 * (1.0 - r.LightingKWh / e0), 100.0 * (1.0 - r.LightingKWh / e2), 100.0 * (1.0 - r.TotalKWh / t2),
                        r.ComfortBandPct, r.FcAvg, r.UAvgPct, r.ReducedArtificialPct, r.TPeakC, r.DegreeHours28, r.Reversals, r.TravelDeg, r.HeldSteps));
                }
            }
            return sb.ToString();
        }
    }
}

