// ClassroomTwin - the two Mamdani controllers.
//  * Light FIS   : IEEE CCIS 2024 (Singhai et al.), Table I, 27 rules, 3 inputs.
//  * Curtain FIS : IEEE IoT-J 2026 (Singhai et al.), Table I rules R1-R11 + documented extensions R12-R18.
using System;

namespace ClassroomTwin.Core
{
    public static class TwinControllers
    {
        // ---------------- shared input variables ----------------
        public static FuzzyVariable LightInput()
        {
            return new FuzzyVariable("Light", 0, 100)
                .Add(FuzzySet.Trap("Low", 0, 0, 25, 45))
                .Add(FuzzySet.Tri("Medium", 35, 55, 75))
                .Add(FuzzySet.Trap("High", 60, 80, 100, 100));
        }

        /// <summary>Classroom time slots (adapted from the paper's home slots). paperSlots=true uses Fig. 6(b) of CCIS 2024.</summary>
        public static FuzzyVariable SlotInput(bool paperSlots)
        {
            FuzzyVariable v = new FuzzyVariable("Slot", 0, 24);
            if (paperSlots)
            {
                v.Add(FuzzySet.Trap("Sleep", 0, 0, 5, 10))
                 .Add(FuzzySet.Tri("Work", 8, 14, 20))
                 .Add(FuzzySet.Trap("Relax", 18, 20, 24, 24));
            }
            else
            {
                v.Add(FuzzySet.Trap("Sleep", 0, 0, 6, 8))
                 .Add(FuzzySet.Trap("Work", 7, 9, 17, 19))
                 .Add(FuzzySet.Trap("Relax", 17, 19, 24, 24));
            }
            return v;
        }

        // ---------------- Light FIS (CCIS 2024) ----------------
        public static MamdaniFIS BuildLightFis(bool paperSlots)
        {
            FuzzyVariable temp = new FuzzyVariable("Temp", 0, 40)
                .Add(FuzzySet.Trap("Cold", 0, 0, 10, 20))
                .Add(FuzzySet.Tri("Warm", 15, 22.5, 30))
                .Add(FuzzySet.Trap("Hot", 25, 30, 40, 40));

            FuzzyVariable output = new FuzzyVariable("Artificial", 0, 100)
                .Add(FuzzySet.Tri("Low", -10, 20, 50))
                .Add(FuzzySet.Tri("Medium", 20, 50, 80))
                .Add(FuzzySet.Tri("High", 50, 80, 110));

            MamdaniFIS fis = new MamdaniFIS("LightFIS_CCIS2024", output, -10.0, 110.0, 0.5, 0.0, 100.0);
            fis.AddInput(LightInput()).AddInput(SlotInput(paperSlots)).AddInput(temp);

            // CCIS 2024 Table I.  Order of temperature columns: Cold, Warm, Hot.
            string[] L = { "Low", "Medium", "High" };
            string[] S = { "Work", "Relax", "Sleep" };
            string[] T = { "Cold", "Warm", "Hot" };
            string[,,] table =
            {
                { { "High", "Medium", "Medium" }, { "Medium", "Medium", "Low" }, { "Low", "Low", "Low" } },      // Light Low
                { { "High", "High", "Medium" },   { "Medium", "Medium", "Medium" }, { "Low", "Low", "Low" } },   // Light Medium
                { { "Medium", "Low", "Low" },     { "Medium", "Medium", "Low" }, { "Medium", "Low", "Low" } },   // Light High
            };
            int n = 1;
            for (int l = 0; l < 3; l++)
                for (int s = 0; s < 3; s++)
                    for (int t = 0; t < 3; t++)
                        fis.AddRule("L" + (n++), new[] { L[l], S[s], T[t] }, table[l, s, t]);
            return fis;
        }

        // ---------------- Curtain FIS (IoT-J 2026) ----------------
        public static FuzzyVariable AngleVariable()
        {
            return new FuzzyVariable("Angle", 0, 75)
                .Add(FuzzySet.Tri("Open", -30, 0, 30))
                .Add(FuzzySet.Tri("Partial", 15, 45, 75))
                .Add(FuzzySet.Tri("Closed", 45, 75, 105));
        }

        public static MamdaniFIS BuildCurtainFis(bool paperSlots, bool includeExtensions)
        {
            FuzzyVariable temp = new FuzzyVariable("Temp", 0, 40)
                .Add(FuzzySet.Trap("Cold", 0, 0, 16, 22))
                .Add(FuzzySet.Tri("Warm", 18, 25, 32))
                .Add(FuzzySet.Trap("Hot", 28, 34, 40, 40));
            FuzzyVariable aqi = new FuzzyVariable("AQI", 0, 500)
                .Add(FuzzySet.Trap("Clean", 0, 0, 100, 200))
                .Add(FuzzySet.Trap("Poor", 100, 200, 500, 500));

            MamdaniFIS fis = new MamdaniFIS("CurtainFIS_IoTJ2026", AngleVariable(), -30.0, 105.0, 0.5, 0.0, 75.0);
            fis.AddInput(LightInput()).AddInput(temp).AddInput(aqi).AddInput(SlotInput(paperSlots)).AddInput(AngleVariable());

            //            Light     Temp    AQI      Slot     PrevAngle   ->  NewAngle
            fis.AddRule("R1",  new[] { "Low",    null,   null,    "Sleep", null },      "Closed");
            fis.AddRule("R2",  new[] { "Medium", "Cold", "Clean", "Work",  "Open" },    "Open");
            fis.AddRule("R3",  new[] { "Medium", "Warm", "Clean", "Work",  null },      "Partial");
            fis.AddRule("R4",  new[] { "Medium", null,   null,    "Relax", null },      "Partial");
            fis.AddRule("R5",  new[] { "High",   "Cold", "Clean", "Work",  "Open" },    "Open");
            fis.AddRule("R6",  new[] { "High",   "Warm", "Clean", "Work",  "Partial" }, "Open");
            fis.AddRule("R7",  new[] { "High",   "Hot",  "Clean", "Relax", null },      "Partial");
            fis.AddRule("R8",  new[] { "High",   null,   "Poor",  null,    null },      "Partial");
            fis.AddRule("R9",  new[] { "Medium", null,   "Poor",  null,    null },      "Partial");
            fis.AddRule("R10", new[] { null,     null,   null,    "Sleep", null },      "Closed");
            fis.AddRule("R11", new[] { null,     null,   null,    null,    "Closed" },  "Partial");
            if (includeExtensions)
            {
                fis.AddRule("R12", new[] { "High",   "Hot",  null,    "Work",  null },  "Closed");  // thermal / glare protection
                fis.AddRule("R13", new[] { "Low",    null,   "Clean", "Work",  null },  "Open");    // harvest scarce daylight
                fis.AddRule("R14", new[] { "Medium", "Hot",  "Clean", "Work",  null },  "Partial");
                fis.AddRule("R15", new[] { "High",   "Cold", "Clean", "Work",  null },  "Open");    // R5 without prev-angle condition
                fis.AddRule("R16", new[] { "Medium", "Cold", "Clean", "Work",  null },  "Open");    // R2 without prev-angle condition
                fis.AddRule("R17", new[] { "High",   "Warm", "Clean", "Work",  null },  "Open");    // R6 without prev-angle condition
                fis.AddRule("R18", new[] { "Low",    null,   null,    "Relax", null },  "Closed");  // evening privacy
            }
            return fis;
        }
    }
}

