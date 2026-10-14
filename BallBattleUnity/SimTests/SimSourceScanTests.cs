using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// 6a.6b/c: the simulation is integer / Q16.16 only. Scans every .cs file under Assets/Scripts/Sim (comments and string
    /// literals removed) for float/double/decimal, Mathf, System.Math, bit-casts between floats and ints, and floating-point
    /// literals (1.5, 1e-3, 2f, 3d, 4m). The only allowed exception is the named View conversion helper Fix.ToFloat().
    /// </summary>
    public class SimSourceScanTests
    {
        /// <summary>Exact source text of the allowed View boundary helpers (removed before the scan).</summary>
        static readonly string[] Allowed = { "public float ToFloat() => Raw / (float)OneRaw;" };

        static readonly Regex Forbidden = new Regex(
            @"\b(float|double|decimal|Single|Double|Mathf|MathF|SingleToInt32Bits|Int32BitsToSingle|DoubleToInt64Bits|Int64BitsToDouble)\b" +
            @"|(?<![\w.])Math\.|\bSystem\.Math\b" +
            @"|(?<![\w.])\d+\.\d+|(?<![\w.])\d+(\.\d+)?[eE][-+]?\d+|(?<![\w.])\d+[fFdDmM]\b");

        public static string SimRoot()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            {
                var p = Path.Combine(d.FullName, "Assets", "Scripts", "Sim");
                if (Directory.Exists(p)) return p;
            }
            throw new DirectoryNotFoundException("Assets/Scripts/Sim not found above " + AppContext.BaseDirectory);
        }

        /// <summary>Comments and string/char literals blanked (line count kept, so reported line numbers match the file).</summary>
        public static string StripCommentsAndStrings(string s)
        {
            s = Regex.Replace(s, @"/\*.*?\*/", m => new string('\n', m.Value.Count(c => c == '\n')), RegexOptions.Singleline);
            s = Regex.Replace(s, @"//[^\n]*", "");
            s = Regex.Replace(s, "@\"(?:[^\"]|\"\")*\"|\\$?\"(?:\\\\.|[^\"\\\\\\n])*\"|'(?:\\\\.|[^'\\\\])'", "\"\"");
            return s;
        }

        public static List<string> Violations(string root)
        {
            var found = new List<string>();
            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                var text = File.ReadAllText(file);
                foreach (var a in Allowed) text = text.Replace(a, "");
                var lines = StripCommentsAndStrings(text).Split('\n');
                for (var i = 0; i < lines.Length; i++)
                    if (Forbidden.IsMatch(lines[i]))
                        found.Add($"{Path.GetRelativePath(root, file)}:{i + 1}: {lines[i].Trim()}");
            }
            return found;
        }

        [Test]
        public void SimHasNoFloatingPoint()
        {
            var v = Violations(SimRoot());
            Assert.That(v, Is.Empty, "floating point in the Sim:\n" + string.Join("\n", v));
        }

        [Test]
        public void ScannerCatchesFloatsButNotCommentsOrStrings()
        {
            bool Bad(string code) => Forbidden.IsMatch(StripCommentsAndStrings(code));
            Assert.That(Bad("var x = 1.5;"), Is.True);
            Assert.That(Bad("var x = 2f;"), Is.True);
            Assert.That(Bad("var x = 1e-3;"), Is.True);
            Assert.That(Bad("float x;"), Is.True);
            Assert.That(Bad("double x;"), Is.True);
            Assert.That(Bad("Math.Abs(a)"), Is.True);
            Assert.That(Bad("System.Math.Max(a, b)"), Is.True);
            Assert.That(Bad("UnityEngine.Mathf.Sin(a)"), Is.True);
            Assert.That(Bad("BitConverter.SingleToInt32Bits(f)"), Is.True);
            Assert.That(Bad("FixMath.Abs(a); var v = 0x1F; var r = 3;"), Is.False);
            Assert.That(Bad("// 1.5f float double Math.Max"), Is.False);
            Assert.That(Bad("/* float */ var s = \"2.5f double\";"), Is.False);
            Assert.That(Bad("var t = v.ToFloat();"), Is.False, "calling the named helper is fine");
        }
    }
}
