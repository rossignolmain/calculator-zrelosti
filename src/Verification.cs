using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace MaturityApp
{
    public static class Verification
    {
        public sealed class ReferenceCase
        {
            public double[] a;
            public double p;
            public int level;
        }
        public sealed class ReferenceSet
        {
            public int caseCount;
            public List<ReferenceCase> cases;
        }
        public sealed class VerificationResult
        {
            public bool pass;
            public int referenceCaseCount, referenceAssertions, additionalTests, additionalAssertions, assertionCount;
            public string startedAtUtc, finishedAtUtc, referenceFile;
            public Dictionary<string, int> referenceLevelCounts = new Dictionary<string, int>();
            public List<string> errors = new List<string>();
            public List<string> completedTests = new List<string>();
            public List<string> generatedFiles = new List<string>();
        }

        static VerificationResult report;
        static string outputFolder;
        static readonly double[][] Representatives = {
            new double[] { 0, 0, 0, 0, 0, 0, 0, 0 },
            new double[] { .5, .5, .5, 0, 0, 0, 0, 0 },
            new double[] { .5, .5, .5, .5, .5, 0, 0, 0 },
            new double[] { 1, 1, .5, .5, 1, .5, 0, 0 },
            new double[] { 1, 1, .5, .5, 1, 1, 1, 1 }
        };

        public static int Run(string[] args)
        {
            outputFolder = args.Length > 2 ? Path.GetFullPath(args[2]) : Path.GetFullPath("work/verification");
            report = new VerificationResult { startedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) };
            try
            {
                Directory.CreateDirectory(outputFolder);
                if (args.Length < 2) throw new ArgumentException("Usage: --self-test reference-cases.json output-folder");
                report.referenceFile = Path.GetFullPath(args[1]);
                VerifyReferenceCases(report.referenceFile);
                VerifyCalculations();
                VerifyPersistence();
                VerifyValidation();
                Test("sample reports and saved assessment", CreateSampleReports);
            }
            catch (Exception ex) { report.errors.Add("Verification infrastructure: " + ex.GetType().Name + ": " + ex.Message); }
            report.pass = report.errors.Count == 0;
            report.additionalAssertions = report.assertionCount - report.referenceAssertions;
            report.finishedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            string resultPath = Path.Combine(outputFolder, "verification.json");
            try
            {
                Directory.CreateDirectory(outputFolder);
                string json = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 }.Serialize(report);
                File.WriteAllText(resultPath, json, new UTF8Encoding(false));
                Console.WriteLine((report.pass ? "PASS" : "FAIL") + ": " + report.referenceCaseCount + " reference cases, " + report.assertionCount + " assertions, " + report.errors.Count + " errors. " + resultPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Cannot write verification report: " + ex.Message);
                return 1;
            }
            return report.pass ? 0 : 1;
        }

        static void Check(bool condition, string message)
        {
            report.assertionCount++;
            if (!condition) report.errors.Add(message);
        }
        static void Equal(double actual, double expected, string message)
        {
            Check(!Double.IsNaN(actual) && Math.Abs(actual - expected) < 1e-10,
                message + ": expected " + expected.ToString("R", CultureInfo.InvariantCulture) + ", actual " + actual.ToString("R", CultureInfo.InvariantCulture));
        }
        static void Test(string name, Action action)
        {
            report.additionalTests++;
            int before = report.errors.Count;
            try { action(); }
            catch (Exception ex) { report.errors.Add(name + ": unexpected " + ex.GetType().Name + ": " + ex.Message); }
            if (before == report.errors.Count) report.completedTests.Add(name);
        }
        static double?[] NullableAnswers(double[] values) { return values.Select(x => (double?)x).ToArray(); }
        static DirectionAssessment Direction(double[] answers, int target = 1)
        {
            return new DirectionAssessment { Id = 1, Answers = NullableAnswers(answers), Target = target };
        }
        static Assessment WithLevels(params int[] levels)
        {
            Assessment assessment = Assessment.Create();
            foreach (DirectionAssessment d in assessment.Directions) d.Included = false;
            for (int i = 0; i < levels.Length; i++)
            {
                assessment.Directions[i].Included = true;
                assessment.Directions[i].Answers = NullableAnswers(Representatives[levels[i]]);
            }
            return assessment;
        }

        static void VerifyReferenceCases(string file)
        {
            ReferenceSet reference = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 }.Deserialize<ReferenceSet>(File.ReadAllText(file, Encoding.UTF8));
            if (reference == null || reference.cases == null) throw new InvalidDataException("Reference cases missing.");
            Check(reference.caseCount == 8748 && reference.cases.Count == 8748, "All 8748 exhaustive reference combinations must be present.");
            int assertionStart = report.assertionCount;
            HashSet<string> unique = new HashSet<string>();
            for (int i = 0; i < reference.cases.Count; i++)
            {
                ReferenceCase item = reference.cases[i];
                if (item.a == null || item.a.Length != 8) throw new InvalidDataException("Invalid reference answer vector at " + i);
                DirectionResult actual = Calc.Direction(Direction(item.a));
                Equal(actual.Score, item.p, "Reference score case " + i);
                Check(actual.Level == item.level, "Reference level case " + i + ": expected " + item.level + ", actual " + actual.Level);
                unique.Add(String.Join("|", item.a.Select(x => x.ToString("R", CultureInfo.InvariantCulture)).ToArray()));
                string levelKey = item.level.ToString(CultureInfo.InvariantCulture);
                if (!report.referenceLevelCounts.ContainsKey(levelKey)) report.referenceLevelCounts[levelKey] = 0;
                report.referenceLevelCounts[levelKey]++;
                report.referenceCaseCount++;
            }
            Check(unique.Count == 8748, "Reference combinations must be unique.");
            report.referenceAssertions = report.assertionCount - assertionStart;
        }

        static void VerifyCalculations()
        {
            Test("empty answers differ from explicit zeros", delegate
            {
                DirectionAssessment d = new DirectionAssessment { Id = 1 };
                DirectionResult empty = Calc.Direction(d);
                Equal(empty.Score, 0, "Empty score"); Check(empty.Level == 0 && empty.Answered == 0 && !empty.Complete, "Empty direction state");
                d.Answers[0] = 0;
                DirectionResult oneZero = Calc.Direction(d);
                Equal(oneZero.Score, empty.Score, "Explicit zero preserves score"); Check(oneZero.Answered == 1 && !oneZero.Complete, "Explicit zero counted as answered");
                d.Answers = new double?[] { 0, 0, 0, 0, 0, 0, 0, 0 };
                Check(Calc.Direction(d).Complete && Calc.Direction(d).Answered == 8, "All zeros form a complete direction");
            });
            Test("exact minimum boundaries for levels zero through four", delegate
            {
                for (int level = 0; level <= 4; level++)
                {
                    DirectionResult result = Calc.Direction(Direction(Representatives[level], 4));
                    Check(result.Level == level, "Representative level " + level);
                    Equal(result.Score, level == 0 ? 0 : Calc.Thresholds[level - 1], "Exact threshold " + level);
                    Check(result.Gap == 4 - level && result.Complete, "Gap and completion for level " + level);
                }
            });
            Test("high weighted total cannot replace mandatory documentation", delegate
            {
                DirectionResult r = Calc.Direction(Direction(new double[] { 0, 1, 1, 1, 1, 1, 1, 1 }, 4));
                Equal(r.Score, .85, "High score without documentation"); Check(r.Level == 0, "Missing documentation prevents level one");
            });
            Test("training 0.3 cannot satisfy level three minimum", delegate
            {
                DirectionAssessment d = Direction(new double[] { 1, 1, 1, 1, 1, .3, 1, 1 }, 3);
                DirectionResult r = Calc.Direction(d);
                Check(r.Level == 2 && r.Gap == 1, "Episodic training caps the attained level at two");
                Check(r.Recommendations.Any(x => x.Contains(Data.Criteria[5].Name)), "Training recommendation is present");
                d.Answers[5] = .5;
                Check(Calc.Direction(d).Level == 3, "Training 0.5 unlocks level three");
            });
            Test("regular external audit is required for target four", delegate
            {
                DirectionAssessment d = Direction(new double[] { 1, 1, 1, 1, 1, 1, .5, 1 }, 4);
                DirectionResult r = Calc.Direction(d);
                Equal(r.Score, .95, "Score with episodic audit"); Check(r.Level == 3 && r.Gap == 1, "Episodic external audit caps level at three");
                Check(r.Recommendations.Any(x => x.Contains(Data.Criteria[6].Name) && x.Contains(Data.Criteria[6].Options.Last().Text)), "Regular external audit recommendation");
                d.Answers[6] = 1;
                Check(Calc.Direction(d).Level == 4 && Calc.Direction(d).Gap == 0, "Regular external audit unlocks level four");
            });
            Test("sparse answers can establish a preliminary level", delegate
            {
                DirectionAssessment d = new DirectionAssessment { Id = 1, Answers = new double?[] { .5, .5, .5, null, null, null, null, null } };
                DirectionResult r = Calc.Direction(d);
                Check(r.Level == 1 && r.Answered == 3 && !r.Complete && r.Gap == 0, "Sparse minima retain the attained level and preliminary completion flag");
            });
            Test("empty summary and zero scope", delegate
            {
                Assessment a = Assessment.Create(); Summary fresh = Calc.Summary(a);
                Check(fresh.Count == 21 && fresh.Total == 168 && fresh.Answered == 0 && !fresh.Complete, "New assessment scope and progress");
                Equal(fresh.Average, 0, "New assessment Uzi");
                foreach (DirectionAssessment d in a.Directions) d.Included = false;
                Summary none = Calc.Summary(a);
                Check(none.Count == 0 && none.Total == 0 && none.Answered == 0 && none.Met == 0 && !none.Complete, "Zero scope has no computable complete result");
                Equal(none.Average, 0, "Zero scope internal average");
            });
            Test("excluded directions retain answers targets evidence", delegate
            {
                Assessment a = WithLevels(4, 0); DirectionAssessment d = a.Directions[0];
                d.Target = 4; d.Evidence = "Регламент № 7; журнал проверок";
                Equal(Calc.Summary(a).Average, 2, "Included high level affects mean");
                d.Included = false; Summary excluded = Calc.Summary(a);
                Equal(excluded.Average, 0, "Excluded high level omitted from mean");
                Check(excluded.Count == 1 && excluded.Answered == 8 && excluded.Total == 8, "Excluded direction omitted from denominator and progress");
                Check(d.Target == 4 && d.Answers[6] == 1 && d.Evidence.Contains("№ 7"), "Exclusion retains direction data");
                d.Included = true;
                Equal(Calc.Summary(a).Average, 2, "Reinclusion restores its contribution");
            });
            Test("summary rounding follows original two decimal average", delegate
            {
                Equal(Calc.Summary(WithLevels(1, 0, 0, 0, 0, 0, 0, 0)).Average, .13, "One eighth rounds upward");
                Equal(Calc.Summary(WithLevels(1, 1, 1, 0, 0, 0, 0, 0)).Average, .38, "Three eighths round upward");
                Equal(Calc.Summary(WithLevels(4, 4, 3)).Average, 3.67, "Eleven thirds");
                Equal(Calc.Summary(WithLevels(0, 1, 2)).Average, 1, "Exact level boundary");
                int[] levels = new int[21]; levels[0] = 1;
                Equal(Calc.Summary(WithLevels(levels)).Average, .05, "One twenty first");
            });
            Test("complete scope includes correctly counted goal achievements", delegate
            {
                Assessment a = WithLevels(1, 2, 3, 4); Summary s = Calc.Summary(a);
                Check(s.Complete && s.Count == 4 && s.Answered == 32 && s.Total == 32 && s.Met == 4, "Complete scope counters");
                Equal(s.Average, 2.5, "Complete scope mean");
                a.Directions[1].Target = 3;
                Check(Calc.Summary(a).Met == 3 && Calc.Direction(a.Directions[1]).Gap == 1, "Raised goal creates one gap");
            });
        }

        static void VerifyPersistence()
        {
            Test("JSON Unicode metadata evidence and exclusion roundtrip", delegate
            {
                Assessment a = Assessment.Create();
                a.Date = "2024-02-29"; a.Category = 2;
                a.Organization = "ООО «Орбита» & партнёры <тест> \"цитата\"";
                a.SystemName = "ГИС «Учёт» / объект № 7"; a.Assessor = "Иванов Пётр Ёжикович";
                a.Notes = "Строка 1\r\nСтрока 2: проверка Unicode ✓ 🔒";
                foreach (DirectionAssessment d in a.Directions) d.Target = 2;
                a.Directions[9].Included = false; a.Directions[9].Target = 4;
                a.Directions[9].Answers[5] = .3;
                a.Directions[9].Evidence = "Wi-Fi не используется.\nПриказ № 42 от 29.02.2024 <приложение> & журнал.";
                Assessment b = Store.Parse(Store.Serialize(a));
                Check(b.Organization == a.Organization && b.SystemName == a.SystemName && b.Assessor == a.Assessor && b.Notes == a.Notes, "Unicode metadata preserved");
                Check(b.Date == a.Date && b.Category == 2 && b.Version == 1, "Date category and version preserved");
                Check(!b.Directions[9].Included && b.Directions[9].Target == 4 && b.Directions[9].Answers[5] == .3 && !b.Directions[9].Answers[4].HasValue && b.Directions[9].Evidence == a.Directions[9].Evidence, "Excluded direction and null versus numeric answers preserved");
                string saved = Path.Combine(outputFolder, "roundtrip.uzi.json");
                Store.Save(a, saved); Store.Save(b, saved); Assessment loaded = Store.Load(saved);
                Check(loaded.Organization == a.Organization && loaded.Directions[9].Evidence == a.Directions[9].Evidence, "Save replacement and UTF8 load preserve content");
                Check(!Directory.GetFiles(outputFolder, ".roundtrip.uzi.json.*.tmp").Any(), "Atomic save leaves no temporary files");
                report.generatedFiles.Add(saved);
            });
            Test("validation normalizes direction order and nullable text", delegate
            {
                Assessment a = Assessment.Create(); a.Organization = a.SystemName = a.Assessor = a.Notes = null;
                a.Directions[0].Evidence = null; a.Directions = a.Directions.Reverse().ToArray();
                Store.Validate(a);
                Check(a.Directions.Select(x => x.Id).SequenceEqual(Enumerable.Range(1, 21)), "Direction IDs sorted to stable display order");
                Check(a.Organization == "" && a.SystemName == "" && a.Assessor == "" && a.Notes == "" && a.Directions[0].Evidence == "", "Nullable text normalized");
            });
            Test("invalid save preserves existing valid project", delegate
            {
                string path = Path.Combine(outputFolder, "atomic-preservation.uzi.json");
                Assessment valid = Assessment.Create(); valid.Organization = "Сохранённые данные"; Store.Save(valid, path);
                Assessment invalid = Assessment.Create(); invalid.Version = 42;
                bool rejected = false; try { Store.Save(invalid, path); } catch { rejected = true; }
                Check(rejected && Store.Load(path).Organization == valid.Organization, "Rejected save does not overwrite an existing assessment");
                report.generatedFiles.Add(path);
            });
        }

        static void Reject(string name, Action<Assessment> mutation)
        {
            Test("reject " + name, delegate
            {
                Assessment a = Assessment.Create(); mutation(a);
                bool rejected = false;
                try { Store.Validate(a); } catch { rejected = true; }
                Check(rejected, "Invalid input must be rejected: " + name);
            });
        }
        static void VerifyValidation()
        {
            Reject("version zero", a => a.Version = 0);
            Reject("future version", a => a.Version = 2);
            Reject("null directions", a => a.Directions = null);
            Reject("20 directions", a => a.Directions = a.Directions.Take(20).ToArray());
            Reject("22 directions", a => a.Directions = a.Directions.Concat(new[] { new DirectionAssessment { Id = 22 } }).ToArray());
            Reject("null direction", a => a.Directions[3] = null);
            Reject("duplicate direction ID", a => a.Directions[3].Id = 1);
            Reject("direction ID zero", a => a.Directions[3].Id = 0);
            Reject("direction ID 22", a => a.Directions[3].Id = 22);
            Reject("null answer vector", a => a.Directions[0].Answers = null);
            Reject("seven answers", a => a.Directions[0].Answers = new double?[7]);
            Reject("nine answers", a => a.Directions[0].Answers = new double?[9]);
            Reject("negative category", a => a.Category = -1);
            Reject("category four", a => a.Category = 4);
            Reject("target zero", a => a.Directions[0].Target = 0);
            Reject("target five", a => a.Directions[0].Target = 5);
            Reject("target below category minimum", delegate(Assessment a) { a.Category = 3; foreach (DirectionAssessment d in a.Directions) d.Target = 3; a.Directions[0].Target = 2; });
            Reject("negative answer", a => a.Directions[0].Answers[0] = -.5);
            Reject("answer above one", a => a.Directions[0].Answers[0] = 1.5);
            Reject("unsupported fractional answer", a => a.Directions[0].Answers[0] = .25);
            Reject("training grade on documentation", a => a.Directions[0].Answers[0] = .3);
            Reject("NaN answer", a => a.Directions[0].Answers[0] = Double.NaN);
            Reject("infinite answer", a => a.Directions[0].Answers[0] = Double.PositiveInfinity);
            Reject("impossible calendar date", a => a.Date = "2026-02-30");
            Reject("wrong date separator", a => a.Date = "16.10.2026");
            Reject("missing date padding", a => a.Date = "2026-1-6");
            Reject("null date", a => a.Date = null);
            Test("parser rejects invalid version date and malformed JSON", delegate
            {
                foreach (string json in new[] { "null", "{", "[]", "{\"Version\":2}", Store.Serialize(Assessment.Create()).Replace("\"Version\":1", "\"Version\":9"), Store.Serialize(Assessment.Create()).Replace("\"Date\":\"" + Assessment.Create().Date + "\"", "\"Date\":\"2026-13-01\"") })
                {
                    bool rejected = false; try { Store.Parse(json); } catch { rejected = true; }
                    Check(rejected, "Malformed or invalid JSON rejected");
                }
            });
            Test("unknown category and valid training grade are accepted", delegate
            {
                Assessment a = Assessment.Create(); a.Category = 0; a.Directions[0].Answers[5] = .3;
                Store.Validate(a);
                Check(a.Category == 0 && a.Directions[0].Answers[5] == .3, "Unknown category and episodic training are supported");
            });
        }

        static void CreateSampleReports()
        {
            Assessment sample = Assessment.Create(); sample.Date = "2026-10-16";
            sample.Organization = "ООО «Пример организации» & партнёры";
            sample.SystemName = "Информационная система «Учёт и аналитика»";
            sample.Assessor = "Ответственный за информационную безопасность — Иванов Пётр Сергеевич";
            sample.Category = 2;
            sample.Notes = "Проверочный пример. Оценка включает подтверждающие материалы, исключённые направления, разные уровни зрелости и незаполненные ответы.\nВнутренний комментарий: <проверка экранирования> & Unicode ✓.";
            for (int i = 0; i < sample.Directions.Length; i++)
            {
                DirectionAssessment d = sample.Directions[i];
                d.Target = i % 7 == 0 ? 4 : 2;
                d.Answers = NullableAnswers(Representatives[i % 5]);
                d.Evidence = "Положение о защите информации; приказ о назначении ответственных; журнал плановых проверок; протокол обучения № " + (i + 1) + ". Документы проверены в ходе внутренней оценки.";
            }
            foreach (int id in new[] { 10, 13, 19 })
            {
                sample.Directions[id - 1].Included = false;
                sample.Directions[id - 1].Evidence = "Исключено из области оценки: соответствующая технология не используется. Основание — п. 13 методики.";
            }
            sample.Directions[5].Answers[4] = null;
            sample.Directions[5].Answers[7] = null;
            string project = Path.Combine(outputFolder, "report-sample.uzi.json");
            string word = Path.Combine(outputFolder, "report-sample.docx");
            string excel = Path.Combine(outputFolder, "report-sample.xlsx");
            Store.Save(sample, project); Reports.ExportWord(sample, word); Reports.ExportExcel(sample, excel);
            foreach (string file in new[] { project, word, excel })
            {
                Check(File.Exists(file) && new FileInfo(file).Length > 100, "Generated sample file: " + Path.GetFileName(file));
                report.generatedFiles.Add(file);
            }
        }
    }
}
