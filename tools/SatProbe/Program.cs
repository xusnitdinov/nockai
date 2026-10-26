using System.IO;
using System.Text.Json;
using QuickCalcAI.Services;

var secrets = JsonSerializer.Deserialize<AppSecrets>(
    File.ReadAllText(@"C:\Users\user\quickCalcAI\src\QuickCalcAI\Secrets.local.json"),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
using var groq = new GroqParser(secrets.GroqApiKey, secrets.GroqModel);
var calc = new CalculatorService(new MathEngine(), groq);

bool Close(string got, string want) {
  if (string.Equals(got.Trim(), want.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
  // allow fraction primary matching decimal want
  if (got.Contains('/') && double.TryParse(want, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w)) {
    var parts = got.Split('/');
    if (parts.Length==2 && double.TryParse(parts[0], out var n) && double.TryParse(parts[1], out var d) && d!=0)
      return Math.Abs(n/d - w) < 0.02;
  }
  if (double.TryParse(got, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var g) &&
      double.TryParse(want, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var e))
    return Math.Abs(g-e) < 0.02;
  return false;
}

var tests = new (string Q, string Want)[] {
  ("10 choose 3", "120"),
  ("5!", "120"),
  ("5 factorial", "120"),
  ("gcd of 48 and 18", "6"),
  ("lcm of 4 and 6", "12"),
  ("mode of 2, 3, 3, 4, 5, 5, 5, 6", "5"),
  ("range of 3, 18, 7, 22, 11", "19"),
  ("(8-3)/8", "5/8"),
  ("A pizza is cut into 8 equal slices. You eat 3. What fraction is left?", "5/8"),
  ("mean if 6 students scored 85 and 4 scored 95", "89"),
  ("nCr(10,3)", "120"),
  ("nPr(5,2)", "20"),
  ("variance(2,4,4,4,5,5,7,9)", "4"),
  ("If 3/4 of a number is 60, what is the number?", "80"),
  ("A price drops 20% then rises 25%. Start at 100, final?", "100"),
};

Console.WriteLine("=== feature + hard probe ===\n");
var pass=0; var fail=0;
foreach (var (q, want) in tests) {
  try {
    var r = await calc.EvaluateAsync(q, null, default);
    var ok = r.Ok && (Close(r.Display, want) || (!string.IsNullOrEmpty(r.SecondaryDisplay) && Close(r.SecondaryDisplay!, want)));
    if (ok) pass++; else fail++;
    Console.WriteLine($"[{(ok?"PASS":"FAIL")}] {q}");
    Console.WriteLine($"  => {r.Display}" + (r.SecondaryDisplay is null?"": $"  ({r.SecondaryDisplay})") + $"  want {want}  expr={r.ExpressionUsed}");
  } catch (Exception ex) {
    fail++;
    Console.WriteLine($"[FAIL] {q}\n  EX {ex.Message}");
  }
  Console.WriteLine();
}
Console.WriteLine($"DONE {pass} pass / {fail} fail");
Environment.Exit(fail==0?0:1);
