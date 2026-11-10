// ClassroomTwin - Mamdani fuzzy inference core (pure C#, no UnityEngine dependency).
// Implements: trapezoidal/triangular membership functions with shoulders, min (AND),
// max (aggregation), clipped (min) implication, discrete centroid defuzzification.
using System;
using System.Collections.Generic;

namespace ClassroomTwin.Core
{
    /// <summary>Trapezoid (a,b,c,d). Triangle = (a,b,b,c). a==b => left shoulder, c==d => right shoulder.</summary>
    public sealed class FuzzySet
    {
        public readonly string Name;
        public readonly double A, B, C, D;

        public FuzzySet(string name, double a, double b, double c, double d)
        {
            if (!(a <= b && b <= c && c <= d)) throw new ArgumentException("FuzzySet " + name + ": need a<=b<=c<=d");
            Name = name; A = a; B = b; C = c; D = d;
        }

        public static FuzzySet Tri(string name, double a, double b, double c) { return new FuzzySet(name, a, b, b, c); }
        public static FuzzySet Trap(string name, double a, double b, double c, double d) { return new FuzzySet(name, a, b, c, d); }

        public double Mu(double x)
        {
            if (x < A || x > D) return 0.0;
            if (x >= B && x <= C) return 1.0;
            if (x < B) return (B == A) ? 1.0 : (x - A) / (B - A);
            return (D == C) ? 1.0 : (D - x) / (D - C);
        }
    }

    /// <summary>A linguistic variable. Crisp inputs are clamped to [Min, Max] before fuzzification.</summary>
    public sealed class FuzzyVariable
    {
        public readonly string Name;
        public readonly double Min, Max;
        public readonly List<FuzzySet> Sets = new List<FuzzySet>();

        public FuzzyVariable(string name, double min, double max) { Name = name; Min = min; Max = max; }

        public FuzzyVariable Add(FuzzySet s) { Sets.Add(s); return this; }

        public int IndexOf(string setName)
        {
            for (int i = 0; i < Sets.Count; i++) if (Sets[i].Name == setName) return i;
            throw new ArgumentException("Variable " + Name + " has no set " + setName);
        }

        public double Clamp(double x) { return x < Min ? Min : (x > Max ? Max : x); }
    }

    /// <summary>IF in0 is S0 AND in1 is S1 ... THEN out is O. Antecedent index -1 means "Any" (membership 1).</summary>
    public sealed class FuzzyRule
    {
        public readonly string Id;
        public readonly int[] Antecedents;
        public readonly int Consequent;
        public FuzzyRule(string id, int[] antecedents, int consequent) { Id = id; Antecedents = antecedents; Consequent = consequent; }
    }

    public sealed class MamdaniFIS
    {
        public readonly string Name;
        public readonly List<FuzzyVariable> Inputs = new List<FuzzyVariable>();
        public readonly FuzzyVariable Output;
        public readonly double UMin, UMax, UStep;     // discretised output universe
        public readonly double ClampMin, ClampMax;    // final crisp output clamp
        public readonly List<FuzzyRule> Rules = new List<FuzzyRule>();

        public MamdaniFIS(string name, FuzzyVariable output, double uMin, double uMax, double uStep, double clampMin, double clampMax)
        {
            Name = name; Output = output; UMin = uMin; UMax = uMax; UStep = uStep; ClampMin = clampMin; ClampMax = clampMax;
        }

        public MamdaniFIS AddInput(FuzzyVariable v) { Inputs.Add(v); return this; }

        /// <summary>Add a rule using set names; null or "Any" = don't care.</summary>
        public MamdaniFIS AddRule(string id, string[] antecedentNames, string consequentName)
        {
            if (antecedentNames.Length != Inputs.Count) throw new ArgumentException("Rule " + id + ": wrong antecedent count");
            int[] idx = new int[antecedentNames.Length];
            for (int i = 0; i < idx.Length; i++)
                idx[i] = (antecedentNames[i] == null || antecedentNames[i] == "Any") ? -1 : Inputs[i].IndexOf(antecedentNames[i]);
            Rules.Add(new FuzzyRule(id, idx, Output.IndexOf(consequentName)));
            return this;
        }

        /// <summary>
        /// Evaluates the FIS. Returns the clamped centroid, or double.NaN when no rule fires
        /// (caller must hold its previous output). activations (length = Output.Sets.Count) receives
        /// the max rule strength per output set; may be null.
        /// </summary>
        public double Evaluate(double[] crisp, double[] activations)
        {
            int nIn = Inputs.Count;
            double[][] mu = new double[nIn][];
            for (int i = 0; i < nIn; i++)
            {
                FuzzyVariable v = Inputs[i];
                double x = v.Clamp(crisp[i]);
                mu[i] = new double[v.Sets.Count];
                for (int j = 0; j < v.Sets.Count; j++) mu[i][j] = v.Sets[j].Mu(x);
            }

            int nOut = Output.Sets.Count;
            double[] act = activations ?? new double[nOut];
            for (int o = 0; o < nOut; o++) act[o] = 0.0;

            for (int r = 0; r < Rules.Count; r++)
            {
                FuzzyRule rule = Rules[r];
                double a = 1.0;
                for (int i = 0; i < nIn; i++)
                {
                    int s = rule.Antecedents[i];
                    double m = (s < 0) ? 1.0 : mu[i][s];
                    if (m < a) a = m;
                }
                if (a > act[rule.Consequent]) act[rule.Consequent] = a;
            }

            int n = (int)Math.Round((UMax - UMin) / UStep) + 1;
            double num = 0.0, den = 0.0;
            for (int k = 0; k < n; k++)
            {
                double y = UMin + k * UStep;
                double m = 0.0;
                for (int o = 0; o < nOut; o++)
                {
                    if (act[o] <= 0.0) continue;
                    double v = Output.Sets[o].Mu(y);
                    if (act[o] < v) v = act[o];
                    if (v > m) m = v;
                }
                num += y * m;
                den += m;
            }
            if (den <= 1e-12) return double.NaN;
            double c = num / den;
            return c < ClampMin ? ClampMin : (c > ClampMax ? ClampMax : c);
        }
    }
}

