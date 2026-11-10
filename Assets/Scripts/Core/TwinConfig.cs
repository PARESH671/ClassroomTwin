// ClassroomTwin - every physical constant and assumption in one place (pure C#).
// Changing a value here changes the batch results; the reference numbers in the spec
// were produced with exactly these values.
using System;

namespace ClassroomTwin.Core
{
    public enum Weather { Normal = 0, Heatwave = 1, Pollution = 2 }

    public sealed class WeatherProfile
    {
        public readonly string Name;
        public readonly double EdiffMaxLux;   // peak exterior diffuse horizontal illuminance
        public readonly double TMeanC;        // daily mean outdoor temperature
        public readonly double TAmpC;         // half peak-to-peak diurnal swing, peak at 15:00
        public readonly double DTGainC;       // max indoor solar temperature rise with blinds fully open
        public readonly double AqiOut;        // outdoor AQI
        public readonly double IFacadePeak;   // peak solar irradiance on the window facade [W/m2]

        public WeatherProfile(string name, double ediff, double tmean, double tamp, double dtgain, double aqiOut, double iPeak)
        {
            Name = name; EdiffMaxLux = ediff; TMeanC = tmean; TAmpC = tamp; DTGainC = dtgain; AqiOut = aqiOut; IFacadePeak = iPeak;
        }
    }

    public static class TwinConfig
    {
        // ---------- Simulation timing ----------
        public const int StepMinutes = 1;                     // fixed control/physics step (simulated minutes)
        public const int MinutesPerDay = 1440;
        public const double StepHours = StepMinutes / 60.0;

        // ---------- Lighting plant (MDPI 2022 mid-sem model) ----------
        public const int Rows = 3;                            // 0 = Window, 1 = Center, 2 = Corridor
        public const int FixturesPerRow = 4;
        public const int TotalFixtures = Rows * FixturesPerRow; // 12
        public const double FixtureFullW = 65.0;              // W at 100 %
        public const double FixtureDimW = 34.0;               // W that yields 500 lux (MDPI dimming case)
        public const double TargetLux = 500.0;
        public const double ArtificialLuxFull = TargetLux * FixtureFullW / FixtureDimW; // 955.882352941... lux per zone at 100 %
        public const double BaselinePowerW = TotalFixtures * FixtureFullW;               // 780 W
        public static readonly string[] RowNames = { "Window", "Center", "Corridor" };

        // ---------- Daylight (daylight-factor method) ----------
        public static readonly double[] DaylightFactor = { 0.050, 0.020, 0.008 }; // per row, blinds open
        public const double LdrFullScaleLux = 1000.0;          // lux that reads as 100 % on the LDR scale

        // ---------- Venetian blinds ----------
        public const double SlatDepthM = 0.17;
        public const double SlatPitchM = 0.16;
        public const double SlatTransmittance = 0.05;
        public const double StaticBlindAngleDeg = 0.0;         // S0-S5: legacy unshaded window (slats horizontal)
        public const double ClosedAngleDeg = 75.0;             // IoT-J Fig. 3 fully closed
        public const double PartialAngleDeg = 45.0;
        public const double OpenAngleDeg = 0.0;

        // ---------- Occupancy / PIR ----------
        public const int PirHoldMinutes = 10;                  // lights stay on 10 min after last motion

        // ---------- Thermal / air ----------
        public const double ThermalTauH = 1.0;                 // indoor temperature time constant
        public const double AirTauH = 0.5;                     // indoor AQI time constant
        public const double AqiOccupancyGain = 60.0;           // AQI rise caused by a full class
        public const double DegreeHourBaseC = 28.0;

        // ---------- Cooling-load estimate (KPI only, does not feed back) ----------
        public const double WindowAreaM2 = 3 * 3.5 * 1.6;      // 16.8 m2
        public const double Shgc = 0.60;
        public const double AcCop = 3.0;

        // ---------- Comfort (useful illuminance band) ----------
        public const double ComfortLowLux = 400.0;
        public const double ComfortHighLux = 2000.0;
        public static readonly FuzzySet ComfortMembership = FuzzySet.Trap("Comfort", 300.0, 400.0, 2000.0, 3000.0);

        // ---------- Weather profiles ----------
        public static readonly WeatherProfile[] Profiles =
        {
            new WeatherProfile("Normal",    20000.0, 24.0, 4.0, 5.0,  45.0, 400.0),
            new WeatherProfile("Heatwave",  25000.0, 34.0, 4.0, 5.0,  45.0, 550.0),
            new WeatherProfile("Pollution", 20000.0, 24.0, 4.0, 5.0, 250.0, 400.0),
        };

        public static WeatherProfile Profile(Weather w) { return Profiles[(int)w]; }

        public static readonly string[] ScenarioNames =
        {
            "S0 Baseline (24/7 ON)",
            "S1 Manager (ON 09:00-18:00)",
            "S2 PIR occupancy (10-min hold)",
            "S3 Uniform dimming 34 W (24/7)",
            "S4 Static daylight (window row OFF 09-13)",
            "S5 Fuzzy lighting - IEEE CCIS 2024",
            "S6 Fuzzy win-curtain co-regulation - IEEE IoT-J 2026",
        };
    }
}

