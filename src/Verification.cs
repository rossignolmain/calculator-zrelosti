using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml;

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
            public int bulkTestCount, bulkAssertionCount;
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
                VerifyProjectTools();
                VerifyQuickInput();
                VerifyBundleLoading();
                Test("mass entry regression suite", BulkVerification.Run);
                report.bulkTestCount = BulkVerification.TestCount;
                report.bulkAssertionCount = BulkVerification.AssertionCount;
                report.assertionCount += report.bulkAssertionCount;
                VerifyAnalysis();
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
            sample.OrganizationShort = "ООО «Пример»";
            sample.OrganizationAddress = "620000, г. Екатеринбург, ул. Примерная, д. 7";
            sample.City = "Екатеринбург";
            sample.DocumentNumber = "УЗИ-2026/017";
            sample.SystemName = "Информационная система «Учёт и аналитика»";
            sample.Assessor = "Ответственный за информационную безопасность — Иванов Пётр Сергеевич";
            sample.AssessorPosition = "Ведущий специалист по защите информации";
            sample.ApproverName = "Петрова Анна Ёлкина";
            sample.ApproverPosition = "Директор";
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

        static void VerifyProjectTools()
        {
            Test("preflight distinguishes required metadata unanswered criteria and optional signatures", delegate
            {
                Assessment a = Assessment.Create();
                string before = Store.Serialize(a);
                string[] initial = ProjectTools.Preflight(a);
                Check(initial.Length == 24, "New assessment has three missing core metadata fields and 21 incomplete directions");
                Check(initial.Count(x => x.Contains("не заполнено 8 из 8")) == 21, "Preflight reports every included incomplete direction");
                Check(Store.Serialize(a) == before, "Preflight does not modify the original assessment");
                a.Organization = "Организация"; a.SystemName = "Система"; a.Assessor = "Оценщик";
                foreach (DirectionAssessment d in a.Directions) d.Answers = NullableAnswers(Representatives[0]);
                Check(ProjectTools.Preflight(a).Length == 0, "Optional approver position address and document number are not required");
                a.Directions[0].Answers[0] = null;
                string[] oneMissing = ProjectTools.Preflight(a);
                Check(oneMissing.Length == 1 && oneMissing[0].Contains("не заполнено 1 из 8"), "One null response is reported while explicit zeros count as answered");
                a.Directions[0].Included = false;
                string[] excluded = ProjectTools.Preflight(a);
                Check(excluded.Length == 1 && excluded[0].Contains("основание исключения"), "Missing excluded direction evidence is an advisory instead of an answer warning");
                a.Directions[0].Evidence = "Технология отсутствует.";
                Check(ProjectTools.Preflight(a).Length == 0, "Excluded direction with a reason needs no answers");
                foreach (DirectionAssessment d in a.Directions) { d.Included = false; d.Evidence = "Не применяется."; }
                Check(ProjectTools.Preflight(a).Length == 1 && ProjectTools.Preflight(a)[0].Contains("Область оценки пуста"), "Preflight identifies zero scope");
            });
            Test("organization profile roundtrip contains only reusable organization and people fields", delegate
            {
                Assessment a = Assessment.Create();
                a.Organization = "ООО «Орбита» & партнёры"; a.OrganizationShort = "Орбита";
                a.OrganizationAddress = "г. Екатеринбург, ул. Примерная, 17"; a.City = "Екатеринбург";
                a.Assessor = "Иванов Пётр Сергеевич"; a.AssessorPosition = "Эксперт";
                a.ApproverName = "Петрова Анна"; a.ApproverPosition = "Директор";
                a.SystemName = "НЕ ДОЛЖНО БЫТЬ В ПРОФИЛЕ"; a.Notes = "Заметки конкретной оценки";
                a.DocumentNumber = "ОТЧЁТ-007"; a.Directions[0].Evidence = "Доказательство"; a.Directions[0].Answers[0] = 1;
                string profilePath = Path.Combine(outputFolder, "organization-profile.json");
                ProjectTools.SaveProfile(a, profilePath); ProjectTools.SaveProfile(a, profilePath);
                OrganizationProfile profile = ProjectTools.LoadProfile(profilePath);
                Check(profile.Version == 1 && profile.Organization == a.Organization && profile.OrganizationShort == a.OrganizationShort && profile.OrganizationAddress == a.OrganizationAddress && profile.City == a.City, "Unicode organization fields survive profile save and load");
                Check(profile.Assessor == a.Assessor && profile.AssessorPosition == a.AssessorPosition && profile.ApproverName == a.ApproverName && profile.ApproverPosition == a.ApproverPosition, "People fields survive profile save and load");
                Dictionary<string, object> json = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(profilePath, Encoding.UTF8)) as Dictionary<string, object>;
                string[] expected = { "Version", "Organization", "OrganizationShort", "OrganizationAddress", "City", "Assessor", "AssessorPosition", "ApproverName", "ApproverPosition" };
                Check(json != null && json.Keys.OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)), "Profile serializes exactly the version and eight permitted fields");
                Check(!Directory.GetFiles(outputFolder, ".organization-profile.json.*.tmp").Any(), "Atomic profile replacement leaves no temporary file");
                Assessment target = WithLevels(3, 4, 1);
                target.SystemName = "Новая система"; target.Date = "2025-12-31"; target.Notes = "Сохранить примечания"; target.DocumentNumber = "НОМЕР-99";
                target.Category = 3; foreach (DirectionAssessment d in target.Directions) d.Target = 3;
                target.Directions[1].Target = 4; target.Directions[0].Evidence = "Сохранить подтверждение";
                string invariantBefore = AssessmentSpecificState(target);
                double scoreBefore = Calc.Summary(target).Average;
                ProjectTools.ApplyProfile(target, profile);
                Check(target.Organization == a.Organization && target.ApproverName == a.ApproverName && target.AssessorPosition == a.AssessorPosition, "Applying a profile transfers the requested metadata");
                Check(AssessmentSpecificState(target) == invariantBefore, "Profile application preserves system date category document number notes answers targets applicability and evidence");
                Equal(Calc.Summary(target).Average, scoreBefore, "Profile application preserves calculated maturity");
                report.generatedFiles.Add(profilePath);
            });
            Test("profile null normalization and invalid profiles preserve target state", delegate
            {
                string path = Path.Combine(outputFolder, "invalid-profile-test.json");
                try
                {
                    File.WriteAllText(path, "{\"Version\":1,\"Organization\":null,\"City\":null,\"ApproverName\":null}", new UTF8Encoding(false));
                    OrganizationProfile normalized = ProjectTools.LoadProfile(path);
                    Check(normalized.Organization == "" && normalized.City == "" && normalized.ApproverName == "" && normalized.Assessor == "", "Null and omitted optional profile text normalize to empty strings");
                    Assessment target = Assessment.Create(); target.Organization = "Сохранить организацию";
                    string original = Store.Serialize(target);
                    foreach (string json in new[] { "null", "{}", "[]", "{", "{\"Version\":2}", "{\"Version\":\"1\"}", "{\"Version\":1,\"Organization\":[1,2]}" })
                    {
                        File.WriteAllText(path, json, new UTF8Encoding(false));
                        bool rejected = false;
                        try { ProjectTools.ApplyProfile(target, ProjectTools.LoadProfile(path)); } catch { rejected = true; }
                        Check(rejected && Store.Serialize(target) == original, "Invalid loaded profile rejected without changing assessment: " + json);
                    }
                    bool invalidObject = false;
                    try { ProjectTools.ApplyProfile(target, new OrganizationProfile { Version = 2, Organization = "Не применять" }); } catch { invalidObject = true; }
                    Check(invalidObject && Store.Serialize(target) == original, "ApplyProfile validates version before changing any field");
                    using (FileStream oversized = new FileStream(path, FileMode.Create, FileAccess.Write)) oversized.SetLength(4 * 1024 * 1024 + 1);
                    bool oversizedRejected = false; try { ProjectTools.LoadProfile(path); } catch { oversizedRejected = true; }
                    Check(oversizedRejected, "Profile larger than four MiB is rejected before parsing");
                }
                finally { if (File.Exists(path)) File.Delete(path); }
            });
            Test("version one assessments from release one remain compatible", delegate
            {
                Assessment source = WithLevels(4, 2);
                source.Organization = "Старая оценка"; source.SystemName = "ГИС"; source.Assessor = "Оценщик";
                JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
                Dictionary<string, object> legacy = serializer.DeserializeObject(Store.Serialize(source)) as Dictionary<string, object>;
                foreach (string field in new[] { "OrganizationShort", "OrganizationAddress", "City", "AssessorPosition", "ApproverName", "ApproverPosition", "DocumentNumber" }) legacy.Remove(field);
                Assessment loaded = Store.Parse(serializer.Serialize(legacy));
                Check(loaded.Version == 1 && loaded.Organization == source.Organization && loaded.SystemName == source.SystemName, "Version one metadata remains readable without new fields");
                Check(loaded.OrganizationShort == "" && loaded.OrganizationAddress == "" && loaded.City == "" && loaded.AssessorPosition == "" && loaded.ApproverName == "" && loaded.ApproverPosition == "" && loaded.DocumentNumber == "", "New fields default to empty for old files");
                Equal(Calc.Summary(loaded).Average, 3, "Legacy project scoring is unchanged");
            });
            Test("bundle full project and valid nested reports share the same snapshot", delegate
            {
                Assessment a = WithLevels(4, 2, 1);
                a.Organization = "ООО «Комплект» & партнёры"; a.SystemName = "ГИС «Учёт»";
                a.Assessor = "Иванов Пётр"; a.AssessorPosition = "Эксперт";
                a.ApproverName = "Смирнова Анна"; a.ApproverPosition = "Директор";
                a.OrganizationShort = "Комплект"; a.OrganizationAddress = "ул. Примерная, 7"; a.City = "Екатеринбург";
                a.DocumentNumber = "УЗИ/2026-42"; a.Date = "2026-10-16";
                foreach (DirectionAssessment d in a.Directions.Where(x => !x.Included)) d.Evidence = "Направление не применяется.";
                a.Directions[2].Answers[7] = null;
                string original = Store.Serialize(a);
                string path = Path.Combine(outputFolder, "bundle-full.zip");
                ProjectTools.ExportBundle(a, path, true, true);
                using (ZipArchive zip = ZipFile.OpenRead(path))
                {
                    Check(zip.Entries.Count == 4, "Full bundle has project Word Excel and manifest");
                    Check(zip.GetEntry("01 Проект.uzi.json") != null && zip.GetEntry("02 Отчёт.docx") != null && zip.GetEntry("03 Таблица.xlsx") != null && zip.GetEntry("Состав комплекта.txt") != null, "Bundle entry names use the requested Unicode names");
                    Assessment bundled = Store.Parse(ReadZipText(zip.GetEntry("01 Проект.uzi.json")));
                    Check(Store.Serialize(bundled) == original, "Bundled JSON is the exact validated assessment snapshot");
                    string manifest = ReadZipText(zip.GetEntry("Состав комплекта.txt"));
                    Check(manifest.Contains(a.Organization) && manifest.Contains(a.SystemName) && manifest.Contains(a.DocumentNumber) && manifest.Contains(a.ApproverName) && manifest.Contains(Data.SourceUrl), "Manifest includes metadata and source");
                    Check(manifest.Contains("ПРЕДВАРИТЕЛЬНЫЙ") && manifest.Contains("не заполнено 1 из 8"), "Manifest reports preliminary status and preflight omissions");
                    ValidateNestedReport(zip.GetEntry("02 Отчёт.docx"), "word/document.xml", a.Organization);
                    ValidateNestedReport(zip.GetEntry("03 Таблица.xlsx"), "xl/workbook.xml", null);
                }
                Check(Store.Serialize(a) == original, "Bundle export does not change the current assessment");
                Check(!Directory.GetDirectories(outputFolder, ".maturity-bundle-*").Any(), "Successful bundle export removes its scoped scratch folder");
                report.generatedFiles.Add(path);
            });
            Test("bundle optional report flags and replacement", delegate
            {
                Assessment a = WithLevels(4); a.Organization = "Организация"; a.SystemName = "Система"; a.Assessor = "Оценщик";
                foreach (DirectionAssessment d in a.Directions.Where(x => !x.Included)) d.Evidence = "Не применяется.";
                foreach (bool word in new[] { false, true }) foreach (bool excel in new[] { false, true })
                {
                    if (word && excel) continue;
                    string path = Path.Combine(outputFolder, "bundle-options-" + (word ? "word" : excel ? "excel" : "project") + ".zip");
                    File.WriteAllText(path, "Заменяемый файл", new UTF8Encoding(false));
                    ProjectTools.ExportBundle(a, path, word, excel);
                    using (ZipArchive zip = ZipFile.OpenRead(path))
                    {
                        Check(zip.Entries.Count == 2 + (word ? 1 : 0) + (excel ? 1 : 0), "Optional flags determine entry count");
                        Check((zip.GetEntry("02 Отчёт.docx") != null) == word && (zip.GetEntry("03 Таблица.xlsx") != null) == excel, "Optional flags determine report membership");
                        Check(ReadZipText(zip.GetEntry("Состав комплекта.txt")).Contains("Заполнение критериев: полное"), "Complete bundle manifest reports full completion");
                        Check(Calc.Summary(Store.Parse(ReadZipText(zip.GetEntry("01 Проект.uzi.json")))).Average == 4, "Project is always included and loadable");
                    }
                    report.generatedFiles.Add(path);
                }
                Check(!Directory.GetDirectories(outputFolder, ".maturity-bundle-*").Any(), "Optional bundle exports remove all scoped scratch folders");
            });
            Test("zero scope and invalid assessments do not overwrite existing bundles", delegate
            {
                string path = Path.Combine(outputFolder, "bundle-preserve-test.zip");
                byte[] original = Encoding.UTF8.GetBytes("Existing target must survive.");
                File.WriteAllBytes(path, original);
                try
                {
                    Assessment empty = WithLevels();
                    bool rejected = false; try { ProjectTools.ExportBundle(empty, path, true, true); } catch { rejected = true; }
                    Check(rejected && File.ReadAllBytes(path).SequenceEqual(original), "Zero scope rejected before replacing the target");
                    Assessment invalid = Assessment.Create(); invalid.Directions[0].Answers[0] = .25;
                    rejected = false; try { ProjectTools.ExportBundle(invalid, path, true, true); } catch { rejected = true; }
                    Check(rejected && File.ReadAllBytes(path).SequenceEqual(original), "Invalid assessment rejected before replacing the target");
                    Check(!Directory.GetDirectories(outputFolder, ".maturity-bundle-*").Any(), "Rejected bundle exports leave no scratch directories");
                }
                finally { if (File.Exists(path)) File.Delete(path); }
            });
        }

        static string AssessmentSpecificState(Assessment a)
        {
            return new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Serialize(new {
                a.Version, a.SystemName, a.Date, a.Category, a.Notes, a.DocumentNumber, a.Directions
            });
        }
        static void VerifyQuickInput()
        {
            Test("quick input labels cover exact criterion grades without merging training levels", delegate
            {
                Check(QuickInput.HeaderNames.Length == 8, "Quick input has eight criterion headers");
                for (int criterion = 0; criterion < Data.Criteria.Length; criterion++)
                    foreach (AnswerOption option in Data.Criteria[criterion].Options)
                        Check(!String.IsNullOrWhiteSpace(QuickInput.ShortLabel(criterion, option.Value)), "Every allowed grade has a short label");
                Check(QuickInput.ShortLabel(5, .3) != QuickInput.ShortLabel(5, .5), "Training 0.3 and 0.5 have distinct meanings");
                bool badCriterion = false, badValue = false;
                try { QuickInput.ShortLabel(8, 0); } catch (ArgumentOutOfRangeException) { badCriterion = true; }
                try { QuickInput.ShortLabel(0, .3); } catch (ArgumentOutOfRangeException) { badValue = true; }
                Check(badCriterion && badValue, "Invalid criterion and nonapplicable grade are rejected");
            });
            Test("repeat command copies only known source facts into empty included cells", delegate
            {
                Assessment a = Assessment.Create();
                a.Organization = "Проверка повторения"; a.SystemName = "Система"; a.Assessor = "Оценщик";
                a.Date = "2026-10-16"; a.Notes = "Общие примечания"; a.DocumentNumber = "УЗИ-71";
                foreach (DirectionAssessment d in a.Directions) { d.Target = d.Id % 2 == 0 ? 4 : 1; d.Evidence = "Подтверждение " + d.Id; }
                DirectionAssessment source = a.Directions.Single(d => d.Id == 7);
                source.Answers = new double?[] { 1, null, .5, null, 0, .3, null, 1 };
                a.Directions[1].Included = false;
                a.Directions[2].Answers[0] = 0; a.Directions[2].Answers[2] = 1; a.Directions[2].Answers[5] = .5;
                a.Directions = a.Directions.Reverse().ToArray();
                string before = Store.Serialize(a), contextBefore = QuickInputNonAnswerState(a);
                List<AnswerChange> changes = QuickInput.CopyIntoEmpty(a, 7);
                Check(changes.Count == 92, "Five known source answers copied to 19 included destinations minus three existing answers");
                Check(changes.All(c => c.Before == null && c.After.HasValue && c.DirectionId != 7 && c.DirectionId != 2), "Change records contain only filled blanks in eligible destinations");
                Check(changes.Select(c => c.DirectionId + ":" + c.Criterion).Distinct().Count() == changes.Count, "Each changed cell appears exactly once");
                DirectionAssessment existing = a.Directions.Single(d => d.Id == 3);
                Check(existing.Answers[0] == 0 && existing.Answers[2] == 1 && existing.Answers[5] == .5, "Existing explicit zero and other answers are never overwritten");
                Check(a.Directions.Single(d => d.Id == 2).Answers.All(x => !x.HasValue), "Excluded destination answers stay untouched");
                Check(a.Directions.Where(d => d.Included && d.Id != 7).All(d => !d.Answers[1].HasValue && !d.Answers[3].HasValue && !d.Answers[6].HasValue), "Unknown source criteria remain unknown in destinations");
                Check(changes.Any(c => c.Criterion == 4 && c.After == 0) && changes.Any(c => c.Criterion == 5 && c.After == .3), "Explicit source zero and training 0.3 are copied as actual selected facts");
                Check(QuickInputNonAnswerState(a) == contextBefore, "Repeat preserves metadata date number scope goals notes and evidence");
                Check(QuickInput.CopyIntoEmpty(a, 7).Count == 0, "Repeating the same command again is a no-op");
                Check(QuickInput.Revert(a, changes) == changes.Count, "Undo restores every unchanged repeated cell");
                Check(Store.Serialize(a) == before, "Copy followed by undo restores exact original assessment including direction order");
            });
            Test("repeat undo preserves later manual values and unrelated changes", delegate
            {
                Assessment a = Assessment.Create();
                a.Directions[0].Answers = NullableAnswers(Representatives[4]);
                List<AnswerChange> changes = QuickInput.CopyIntoEmpty(a, 1);
                Check(changes.Count == 160, "Complete source can explicitly populate 20 empty directions");
                a.Directions[1].Answers[0] = 0;
                a.Directions[1].Answers[1] = null;
                a.Directions[2].Included = false;
                a.Directions[3].Target = 4; a.Directions[3].Evidence = "Позднее подтверждение";
                a.Directions[0].Answers[0] = .5;
                int reverted = QuickInput.Revert(a, changes);
                Check(reverted == 158, "Undo skips a changed and a cleared copied cell");
                Check(a.Directions[1].Answers[0] == 0 && !a.Directions[1].Answers[1].HasValue, "Later manual zero and cleared answer survive undo");
                Check(!a.Directions[2].Included && a.Directions[2].Answers.All(x => !x.HasValue), "Undo restores copied answers without reverting a later scope change");
                Check(a.Directions[3].Target == 4 && a.Directions[3].Evidence == "Позднее подтверждение", "Undo does not revert later goal or evidence changes");
                Check(a.Directions[0].Answers[0] == .5, "Undo never changes the source direction");
                Check(QuickInput.Revert(a, changes) == 0, "Repeated undo is harmless");
            });
            Test("empty source and fully assessed destinations do not invent answers", delegate
            {
                Assessment a = Assessment.Create(); string before = Store.Serialize(a);
                Check(QuickInput.CopyIntoEmpty(a, 1).Count == 0 && Store.Serialize(a) == before, "Blank source cannot fill any answer");
                foreach (DirectionAssessment d in a.Directions) d.Answers = NullableAnswers(Representatives[0]);
                a.Directions[0].Answers = NullableAnswers(Representatives[4]); before = Store.Serialize(a);
                Check(QuickInput.CopyIntoEmpty(a, 1).Count == 0 && Store.Serialize(a) == before, "Fully assessed zero destinations cannot be overwritten by a high-scoring source");
            });
            Test("repeat validates source and complete plan before any mutation", delegate
            {
                Assessment a = Assessment.Create(); a.Directions[0].Answers = NullableAnswers(Representatives[4]);
                foreach (int sourceId in new[] { 0, 22 })
                {
                    string before = Store.Serialize(a); bool rejected = false;
                    try { QuickInput.CopyIntoEmpty(a, sourceId); } catch { rejected = true; }
                    Check(rejected && Store.Serialize(a) == before, "Invalid source ID rejected without changes: " + sourceId);
                }
                a.Directions[0].Included = false; string excludedBefore = Store.Serialize(a); bool excludedRejected = false;
                try { QuickInput.CopyIntoEmpty(a, 1); } catch { excludedRejected = true; }
                Check(excludedRejected && Store.Serialize(a) == excludedBefore, "Excluded source rejected without changes");
                a.Directions[0].Included = true; a.Directions[20].Answers = new double?[7];
                string malformedBefore = Store.Serialize(a); bool malformedRejected = false;
                try { QuickInput.CopyIntoEmpty(a, 1); } catch { malformedRejected = true; }
                Check(malformedRejected && Store.Serialize(a) == malformedBefore, "Late invalid destination rejected before any earlier destination is modified");
            });
            Test("undo safely ignores malformed change records", delegate
            {
                Assessment a = Assessment.Create(); a.Directions[0].Answers[0] = 1;
                string before = Store.Serialize(a);
                AnswerChange[] malformed = {
                    null,
                    new AnswerChange { DirectionId = 1, Criterion = -1, Before = null, After = 1 },
                    new AnswerChange { DirectionId = 1, Criterion = 8, Before = null, After = 1 },
                    new AnswerChange { DirectionId = 42, Criterion = 0, Before = null, After = 1 },
                    new AnswerChange { DirectionId = 1, Criterion = 0, Before = .3, After = 1 },
                    new AnswerChange { DirectionId = 1, Criterion = 0, Before = null, After = Double.NaN },
                    new AnswerChange { DirectionId = 1, Criterion = 0, Before = 1, After = 1 }
                };
                Check(QuickInput.Revert(a, malformed) == 0 && Store.Serialize(a) == before, "Malformed undo records never change unrelated assessment data");
            });
        }
        static string QuickInputNonAnswerState(Assessment a)
        {
            return new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Serialize(new {
                a.Version, a.Organization, a.OrganizationShort, a.OrganizationAddress, a.City,
                a.Assessor, a.AssessorPosition, a.ApproverName, a.ApproverPosition,
                a.SystemName, a.Date, a.Category, a.Notes, a.DocumentNumber,
                Directions = a.Directions.Select(d => new { d.Id, d.Included, d.Target, d.Evidence }).ToArray()
            });
        }
        static void VerifyBundleLoading()
        {
            Test("bundle reopen preserves Unicode explicit zero training grade and unanswered cells", delegate
            {
                Assessment a = WithLevels(1, 2);
                a.Organization = "ООО «Возвращение» & партнёры 🔒"; a.SystemName = "ГИС «Учёт»";
                a.Assessor = "Иванов Пётр"; a.Date = "2026-10-16";
                a.OrganizationShort = "Возвращение"; a.City = "Екатеринбург";
                a.ApproverName = "Смирнова Анна"; a.DocumentNumber = "УЗИ/2026-Ё";
                a.Notes = "Первая строка\nВторая строка: <проверка> & Unicode.";
                a.Directions[0].Answers[0] = 0; a.Directions[0].Answers[1] = null; a.Directions[0].Answers[5] = .3;
                a.Directions[0].Evidence = "Протокол № 7, «подтверждение» & журнал.";
                string bundle = Path.Combine(outputFolder, "reopen-roundtrip.ZIP");
                ProjectTools.ExportBundle(a, bundle, false, false);
                Assessment loaded = ProjectTools.LoadAssessmentFile(bundle);
                Check(Store.Serialize(loaded) == Store.Serialize(a), "ZIP primary save format reopens the exact assessment data");
                Check(loaded.Directions[0].Answers[0] == 0 && loaded.Directions[0].Answers[5] == .3 && !loaded.Directions[0].Answers[1].HasValue, "Zero training 0.3 and blank remain distinct after bundle reopen");
                string json = Path.Combine(outputFolder, "reopen-roundtrip.uzi.json");
                Store.Save(a, json);
                Check(Store.Serialize(ProjectTools.LoadAssessmentFile(json)) == Store.Serialize(a), "Non-ZIP project input delegates to normal JSON loading");
                report.generatedFiles.Add(bundle); report.generatedFiles.Add(json);
            });
            Test("bundle loader ignores unrelated and traversal entries and never extracts files", delegate
            {
                Assessment a = WithLevels(3); a.Organization = "Правильный проект";
                string path = Path.Combine(outputFolder, "reopen-unrelated.zip");
                string escapeName = "maturity-loader-" + Guid.NewGuid().ToString("N") + ".txt";
                string escapeTarget = Path.Combine(Path.GetDirectoryName(outputFolder), escapeName);
                byte[] project = Encoding.UTF8.GetBytes(Store.Serialize(a));
                byte[] bomProject = new byte[project.Length + 3]; bomProject[0] = 0xEF; bomProject[1] = 0xBB; bomProject[2] = 0xBF;
                Buffer.BlockCopy(project, 0, bomProject, 3, project.Length);
                WriteTestZip(path,
                    ZipInput("../" + escapeName, Encoding.UTF8.GetBytes("Do not extract.")),
                    ZipInput("folder/01 Проект.uzi.json", Encoding.UTF8.GetBytes("INVALID JSON")),
                    ZipInput("./01 Проект.uzi.json", Encoding.UTF8.GetBytes("ALSO INVALID")),
                    ZipInput("unrelated-large.bin", new byte[4 * 1024 * 1024 + 1]),
                    ZipInput("01 Проект.uzi.json", bomProject));
                string[] beforeFiles = Directory.GetFileSystemEntries(outputFolder).OrderBy(x => x).ToArray();
                Assessment loaded = ProjectTools.LoadAssessmentFile(path);
                Check(Store.Serialize(loaded) == Store.Serialize(a), "Only the exact root project entry is read, supporting a UTF8 BOM");
                Check(!File.Exists(escapeTarget), "Traversal entry is never extracted outside the bundle directory");
                Check(Directory.GetFileSystemEntries(outputFolder).OrderBy(x => x).SequenceEqual(beforeFiles), "Loading a bundle creates no files or directories");
                report.generatedFiles.Add(path);
            });
            Test("invalid bundle formats reject before replacing current assessment state", delegate
            {
                Assessment current = WithLevels(4); current.Organization = "Текущая оценка не должна потеряться";
                string before = Store.Serialize(current);
                string path = Path.Combine(outputFolder, "reopen-invalid-test.zip");
                try
                {
                    byte[] good = Encoding.UTF8.GetBytes(Store.Serialize(Assessment.Create()));
                    WriteTestZip(path, ZipInput("Прочитайте.txt", Encoding.UTF8.GetBytes("No project")));
                    AssertBundleRejected(path, ref current, before, "Missing exact project entry");
                    WriteTestZip(path, ZipInput("01 Проект.uzi.json", good), ZipInput("01 Проект.uzi.json", good));
                    AssertBundleRejected(path, ref current, before, "Duplicate exact project entries");
                    foreach (byte[] invalid in new[] {
                        Encoding.UTF8.GetBytes("{"),
                        Encoding.UTF8.GetBytes("{\"Version\":2}"),
                        Encoding.UTF8.GetBytes(Store.Serialize(Assessment.Create()).Replace("\"Version\":1", "\"Version\":9")),
                        new byte[] { 0xFF, 0xFE, 0x00 }
                    })
                    {
                        WriteTestZip(path, ZipInput("01 Проект.uzi.json", invalid));
                        AssertBundleRejected(path, ref current, before, "Malformed invalid-version or non-UTF8 project");
                    }
                    Assessment invalidAnswers = Assessment.Create(); invalidAnswers.Directions[0].Answers[0] = .25;
                    WriteTestZip(path, ZipInput("01 Проект.uzi.json", Encoding.UTF8.GetBytes(Store.Serialize(invalidAnswers))));
                    AssertBundleRejected(path, ref current, before, "Project with an unsupported answer");
                    File.WriteAllText(path, "This is not a ZIP archive.", new UTF8Encoding(false));
                    AssertBundleRejected(path, ref current, before, "Non-ZIP bytes with ZIP extension");
                }
                finally { if (File.Exists(path)) File.Delete(path); }
            });
            Test("bundle loader enforces actual decompressed project size as well as entry length", delegate
            {
                Assessment current = Assessment.Create(); current.Organization = "Существующая оценка";
                string before = Store.Serialize(current);
                string path = Path.Combine(outputFolder, "reopen-size-limit-test.zip");
                try
                {
                    byte[] prefix = Encoding.UTF8.GetBytes(Store.Serialize(Assessment.Create()));
                    byte[] oversized = new byte[4 * 1024 * 1024 + 1];
                    Buffer.BlockCopy(prefix, 0, oversized, 0, prefix.Length);
                    for (int i = prefix.Length; i < oversized.Length; i++) oversized[i] = 0x20;
                    WriteTestZip(path, ZipInput("01 Проект.uzi.json", oversized));
                    AssertBundleRejected(path, ref current, before, "Project advertised as larger than four MiB");
                    byte[] zipBytes = File.ReadAllBytes(path); int central = -1;
                    for (int i = zipBytes.Length - 46; i >= 0; i--)
                        if (zipBytes[i] == 0x50 && zipBytes[i + 1] == 0x4B && zipBytes[i + 2] == 0x01 && zipBytes[i + 3] == 0x02) { central = i; break; }
                    if (central < 0) throw new InvalidDataException("Test ZIP central directory not found.");
                    zipBytes[central + 24] = 1; zipBytes[central + 25] = zipBytes[central + 26] = zipBytes[central + 27] = 0;
                    File.WriteAllBytes(path, zipBytes);
                    using (ZipArchive forged = ZipFile.OpenRead(path)) Check(forged.Entries.Single().Length == 1, "Forged ZIP test actually understates the uncompressed project length");
                    AssertBundleRejected(path, ref current, before, "Actual decompressed project exceeds the limit despite forged entry length");
                }
                finally { if (File.Exists(path)) File.Delete(path); }
            });
        }
        static KeyValuePair<string, byte[]> ZipInput(string name, byte[] content)
        {
            return new KeyValuePair<string, byte[]>(name, content);
        }
        static void WriteTestZip(string path, params KeyValuePair<string, byte[]>[] entries)
        {
            using (FileStream output = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (ZipArchive archive = new ZipArchive(output, ZipArchiveMode.Create, false, Encoding.UTF8))
                foreach (KeyValuePair<string, byte[]> item in entries)
                    using (Stream stream = archive.CreateEntry(item.Key, CompressionLevel.Optimal).Open()) stream.Write(item.Value, 0, item.Value.Length);
        }
        static void AssertBundleRejected(string path, ref Assessment current, string before, string label)
        {
            bool rejected = false;
            try { current = ProjectTools.LoadAssessmentFile(path); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, label + " must throw InvalidDataException");
            Check(Store.Serialize(current) == before, label + " must preserve current assessment state");
        }
        static void VerifyAnalysis()
        {
            Test("criterion profiles average answered values only and expose coverage", delegate
            {
                Assessment a = Assessment.Create(); foreach (DirectionAssessment d in a.Directions) d.Included = false;
                for (int i = 0; i < 3; i++) a.Directions[i].Included = true;
                a.Directions[0].Answers[0] = 0; a.Directions[1].Answers[0] = 1;
                a.Directions[0].Answers[5] = 0; a.Directions[1].Answers[5] = .3;
                a.Directions[3].Answers[0] = 1;
                AssessmentAnalysis analysis = Analysis.Build(a);
                CriterionProfile documentation = analysis.Criteria[0], training = analysis.Criteria[5];
                Check(analysis.Included == 3 && analysis.Answered == 4 && analysis.Total == 24 && !analysis.Complete && analysis.HasScope, "Analysis scope and completeness match actual included answers");
                Check(documentation.Answered == 2 && documentation.Missing == 1 && documentation.ZeroCount == 1, "Known zero counts as answered while missing and excluded cells do not");
                Equal(documentation.Average.Value, .5, "Documentation mean excludes unanswered and excluded directions");
                Equal(documentation.WeightedAverage.Value, .075, "Weighted criterion mean");
                Equal(documentation.Coverage, 2.0 / 3, "Criterion coverage");
                Equal(training.Average.Value, .15, "Training mean includes explicit zero and episodic 0.3");
                Check(!analysis.Criteria[2].Average.HasValue && !analysis.Criteria[2].WeightedAverage.HasValue && analysis.Criteria[2].Answered == 0, "An entirely unanswered criterion has no invented zero average");
            });
            Test("analysis separates factual target deficits unknown required data and optional unknowns", delegate
            {
                Assessment a = WithLevels(0); a.Directions[0].Target = 3;
                a.Directions[0].Answers[5] = null; a.Directions[0].Answers[6] = null;
                AssessmentAnalysis analysis = Analysis.Build(a);
                DeficitRecord training = analysis.Deficits.Single(d => d.CriterionIndex == 5);
                DeficitRecord audit = analysis.Deficits.Single(d => d.CriterionIndex == 6);
                Check(training.Type == "assess" && training.Action == "Оценить" && !training.Current.HasValue && training.Required == .5 && training.BlockingTarget, "Required unknown training is a request to assess rather than asserted absence");
                Check(audit.Type == "assess" && audit.Action == "Оценить" && !audit.Required.HasValue && !audit.BlockingTarget, "Unknown criterion with no target minimum does not recommend a zero answer");
                DeficitRecord documentation = analysis.Deficits.Single(d => d.CriterionIndex == 0);
                Check(documentation.Type == "improve" && documentation.Current == 0 && documentation.Required == 1 && documentation.BlockingTarget, "Confirmed low documentation is a factual target deficit");
                Check(analysis.Deficits.Count(d => d.Type == "score") == 1 && analysis.Deficits.Single(d => d.Type == "score").CriterionIndex == null, "Weighted score shortfall is a distinct diagnostic record");
                Check(analysis.GroupedActions.All(g => g.Type != "score"), "Score diagnostic is not counted as duplicate work alongside concrete criterion tasks");
                Check(analysis.Criteria[0].KnownDeficits == 1 && analysis.Criteria[5].RequiredUnknown == 1, "Profiles distinguish confirmed deficits from required data gaps");
                Check(analysis.Deficits.All(d => d.DirectionId == 1), "Excluded directions create no target or data tasks");
            });
            Test("strong totals cannot hide mandatory criterion deficits", delegate
            {
                Assessment a = WithLevels(4);
                a.Directions[0].Answers = new double?[] { 0, 1, 1, 1, 1, 1, 1, 1 }; a.Directions[0].Target = 1;
                AssessmentAnalysis analysis = Analysis.Build(a);
                Check(analysis.Deficits.Length == 1 && analysis.Deficits[0].CriterionIndex == 0 && analysis.Deficits[0].Required == .5, "High total with missing documentation still produces its mandatory deficit");
                Check(analysis.Directions[0].Next.ProjectedLevel == 2 && analysis.Directions[0].Next.ChangedCriteria == 1, "Minimal next-level correction can surpass the requested next level without lowering stronger answers");
                a.Directions[0].Answers = new double?[] { 1, 1, 1, 1, 1, .3, 1, 1 }; a.Directions[0].Target = 3;
                analysis = Analysis.Build(a);
                Check(analysis.Deficits.Length == 1 && analysis.Deficits[0].CriterionIndex == 5 && analysis.Deficits[0].Required == .5, "Training 0.3 requires 0.5 to reach target three despite a high total");
                a.Directions[0].Answers = new double?[] { 1, 1, 1, 1, 1, 1, .5, 1 }; a.Directions[0].Target = 4;
                analysis = Analysis.Build(a);
                Check(analysis.Deficits.Length == 1 && analysis.Deficits[0].CriterionIndex == 6 && analysis.Deficits[0].Required == 1, "Target four identifies regular external audit rather than generic score improvement");
            });
            Test("already attained target and incomplete data remain distinct", delegate
            {
                Assessment a = WithLevels(3); a.Directions[0].Target = 3;
                a.Directions[0].Answers[6] = null; a.Directions[0].Answers[7] = null;
                AssessmentAnalysis analysis = Analysis.Build(a); DirectionRoadmap direction = analysis.Directions[0];
                Check(direction.CurrentLevel == 3 && !direction.Complete && !analysis.Complete, "The provisional attained level remains three while data are incomplete");
                Check(direction.Target.ChangedCriteria == 0 && direction.Target.ProjectedLevel == 3, "Attained target does not trigger invented improvements");
                Check(analysis.Deficits.Length == 2 && analysis.Deficits.All(d => d.Type == "assess" && !d.BlockingTarget && !d.Required.HasValue), "Remaining optional unknowns are only requests to assess");
                Check(direction.Next.Level == 4 && direction.Next.ChangedCriteria == 3 && direction.Next.KnownChanges == 1 && direction.Next.UnknownChanges == 2, "Next level distinguishes the known training improvement and two unknown requirements");
            });
            Test("roadmaps preserve strong facts and do not assert unobserved improvements", delegate
            {
                DirectionAssessment unknown = new DirectionAssessment { Id = 1 };
                LevelRoadmap roadmap = Analysis.BuildRoadmap(unknown, 1);
                Check(roadmap.ChangedCriteria == 3 && roadmap.KnownChanges == 0 && roadmap.UnknownChanges == 3 && roadmap.Preliminary, "Blank source produces three required assessments rather than confirmed process failures");
                Check(roadmap.Changes.All(c => c.NeedsAssessment && !c.Current.HasValue && c.Required == .5), "Unknown required conditions are explicit hypotheses");
                Equal(roadmap.ProjectedScore, .225, "Conditional lower-bound score for level one");
                unknown.Answers[0] = 1;
                roadmap = Analysis.BuildRoadmap(unknown, 1);
                Check(roadmap.ChangedCriteria == 2 && roadmap.Changes.All(c => c.CriterionIndex != 0), "Already stronger documentation is preserved");
                Equal(roadmap.ProjectedScore, .3, "Projection retains the stronger existing documentation contribution");
                Check(unknown.Answers[0] == 1 && unknown.Answers.Skip(1).All(x => !x.HasValue), "Scenario calculation never applies its proposed answers");
            });
            Test("grouped actions use factual scope distinguish goals and leave owner deadline empty", delegate
            {
                Assessment a = WithLevels(0, 0, 4);
                AssessmentAnalysis analysis = Analysis.Build(a);
                Check(analysis.GroupedActions.Length == 3, "Identical concrete deficits group into three reusable actions");
                Check(analysis.GroupedActions.All(g => g.DirectionIds.SequenceEqual(new[] { 1, 2 }) && g.DirectionCount == 2 && g.Owner == "" && g.Deadline == ""), "Grouped scope is exact with no fictional responsible person or deadline");
                Check(analysis.GroupedActions[0].CriterionIndex == 1 && analysis.GroupedActions[0].Priority == 1, "Equal-coverage known deficits are ordered by criterion weight");
                Check(analysis.GroupedActions.Select(g => g.Priority).SequenceEqual(Enumerable.Range(1, 3)), "Priority ranks are deterministic and contiguous");
                a.Directions[1].Target = 3;
                analysis = Analysis.Build(a);
                GroupedAction[] documentation = analysis.GroupedActions.Where(g => g.CriterionIndex == 0).ToArray();
                Check(documentation.Length == 2 && documentation.Any(g => g.Required == .5) && documentation.Any(g => g.Required == 1), "Different required target grades are not collapsed into a misleading single condition");
                a.Directions[0].Answers[0] = null; a.Directions[0].Answers[6] = null;
                analysis = Analysis.Build(a);
                Check(analysis.GroupedActions[0].Type == "assess" && analysis.GroupedActions[0].BlockingTarget, "Unknown required conditions are reviewed before improvement decisions");
                Check(analysis.GroupedActions.Last().Type == "assess" && !analysis.GroupedActions.Last().BlockingTarget, "Nonblocking missing data remain distinct from mandatory improvement work");
            });
            Test("zero-scope analysis has no averages fabricated tasks or roadmaps", delegate
            {
                Assessment a = WithLevels(); a.Directions[0].Answers = NullableAnswers(Representatives[4]);
                AssessmentAnalysis analysis = Analysis.Build(a);
                Check(!analysis.HasScope && !analysis.Complete && analysis.Included == 0 && analysis.Answered == 0 && analysis.Total == 0, "Zero-scope aggregate state");
                Check(analysis.Criteria.All(c => !c.Average.HasValue && c.Answered == 0 && c.Coverage == 0), "Excluded answers do not create criterion averages");
                Check(analysis.Deficits.Length == 0 && analysis.GroupedActions.Length == 0 && analysis.Directions.All(d => d.Next == null && d.Target == null), "Zero scope produces no assess or improve tasks and no active roadmaps");
            });
            Test("analysis is read only including order null metadata answers and references", delegate
            {
                Assessment a = WithLevels(0, 2, 4); a.Organization = null; a.Directions = a.Directions.Reverse().ToArray();
                a.Directions[0].Evidence = null;
                DirectionAssessment[] directionReferences = a.Directions.ToArray(); double?[][] answerReferences = a.Directions.Select(d => d.Answers).ToArray();
                string before = Store.Serialize(a);
                Analysis.Build(a);
                Check(Store.Serialize(a) == before, "Analysis does not normalize or reorder the edited assessment");
                Check(a.Directions.Select((d, i) => Object.ReferenceEquals(d, directionReferences[i]) && Object.ReferenceEquals(d.Answers, answerReferences[i])).All(x => x), "Analysis never replaces original direction or answer objects");
                a.Directions[0].Answers[0] = .25; before = Store.Serialize(a); bool rejected = false;
                try { Analysis.Build(a); } catch { rejected = true; }
                Check(rejected && Store.Serialize(a) == before, "Invalid analytical input is rejected without altering it");
            });
            Test("minimal roadmaps agree with independent exhaustive original calculator cases", delegate
            {
                ReferenceSet reference = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 }.Deserialize<ReferenceSet>(File.ReadAllText(report.referenceFile, Encoding.UTF8));
                double[][] sources = {
                    Representatives[0], Representatives[1], Representatives[3],
                    new double[] { 0, 1, 1, 1, 1, 1, 1, 1 },
                    new double[] { 1, 1, 1, 1, 1, .3, 1, 1 },
                    new double[] { 1, 1, 1, 1, 1, 1, 1, 1 }
                };
                foreach (double[] source in sources) for (int target = 1; target <= 4; target++)
                {
                    LevelRoadmap roadmap = Analysis.BuildRoadmap(Direction(source), target);
                    int minimumCount = Int32.MaxValue; double minimumScore = Double.PositiveInfinity;
                    foreach (ReferenceCase candidate in reference.cases)
                    {
                        if (candidate.level < target) continue;
                        bool raisesOnly = true; int changed = 0;
                        for (int criterion = 0; criterion < 8; criterion++)
                        {
                            if (candidate.a[criterion] < source[criterion] - 1e-9) { raisesOnly = false; break; }
                            if (Math.Abs(candidate.a[criterion] - source[criterion]) > 1e-9) changed++;
                        }
                        if (!raisesOnly) continue;
                        if (changed < minimumCount) { minimumCount = changed; minimumScore = candidate.p; }
                        else if (changed == minimumCount && candidate.p < minimumScore) minimumScore = candidate.p;
                    }
                    Check(roadmap.ChangedCriteria == minimumCount, "Roadmap changes the minimum possible number of known criteria for target " + target);
                    Equal(roadmap.ProjectedScore, minimumScore, "Roadmap is the smallest upward grade adjustment among minimum-change independent solutions for target " + target);
                    Check(roadmap.ProjectedLevel >= target && roadmap.Changes.All(c => c.Current.HasValue && c.Proposed > c.Current.Value), "Complete-input roadmap reaches target by increasing facts only");
                }
            });
        }
        static string ReadZipText(ZipArchiveEntry entry)
        {
            if (entry == null) throw new InvalidDataException("Expected ZIP entry is missing.");
            using (Stream stream = entry.Open()) using (StreamReader reader = new StreamReader(stream, Encoding.UTF8)) return reader.ReadToEnd();
        }
        static void ValidateNestedReport(ZipArchiveEntry outerEntry, string requiredPart, string expectedText)
        {
            if (outerEntry == null) throw new InvalidDataException("Nested report is missing.");
            using (MemoryStream memory = new MemoryStream())
            {
                using (Stream input = outerEntry.Open()) input.CopyTo(memory);
                memory.Position = 0;
                using (ZipArchive reportZip = new ZipArchive(memory, ZipArchiveMode.Read, false, Encoding.UTF8))
                {
                    Check(reportZip.GetEntry("[Content_Types].xml") != null && reportZip.GetEntry(requiredPart) != null, "Nested Office report has required package parts");
                    foreach (ZipArchiveEntry part in reportZip.Entries.Where(x => x.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || x.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
                    {
                        XmlDocument xml = new XmlDocument(); xml.XmlResolver = null;
                        using (Stream stream = part.Open()) xml.Load(stream);
                        Check(xml.DocumentElement != null, "Nested Office XML is well formed: " + part.FullName);
                        if (part.FullName == requiredPart && expectedText != null) Check(xml.InnerText.Contains(expectedText), "Nested Word report uses the same Unicode organization metadata");
                    }
                }
            }
        }
    }
}
