using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace MaturityApp
{
    public class AnswerOption { public double Value; public string Text; }
    public class Criterion { public string Name, Description; public double Weight; public AnswerOption[] Options; }
    public class DirectionAssessment
    {
        public int Id;
        public bool Included = true;
        public int Target = 1;
        public double?[] Answers = new double?[8];
        public string Evidence = "";
    }
    public class Assessment
    {
        public int Version = 1;
        public string Organization = "", SystemName = "", Assessor = "", Notes = "";
        public string OrganizationShort = "", OrganizationAddress = "", City = "";
        public string AssessorPosition = "", ApproverName = "", ApproverPosition = "", DocumentNumber = "";
        public string Date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public int Category = 1;
        public SystemPassport Passport = null;
        public DirectionAssessment[] Directions;
        // Report data required by section VI of the methodology (see ReportData.cs).
        public string AssessmentKind = "internal", ExternalLicense = "";
        public string AssessorOrganization = "", AssessorContact = "";
        public ReportPerson[] Participants = new ReportPerson[0];
        public string[] Requirements = new string[0], SourceData = new string[0], Methods = new string[0];
        public string[] Instruments = { "calculator" };
        public string ToolsNote = "", SourceDataNote = "";
        public PreviousResult Previous = null;
        public string PlanResponsible = "", PlanDeadline = "";
        public static Assessment Create()
        {
            Assessment a = new Assessment();
            a.Directions = Enumerable.Range(1, 21).Select(i => new DirectionAssessment { Id = i }).ToArray();
            return a;
        }
    }
    public class DirectionResult
    {
        public double Score;
        public int Level, Answered, Gap;
        public bool Complete;
        public List<string> Recommendations = new List<string>();
    }
    public class Summary
    {
        public int Count, Answered, Total, Met;
        public double Average, TargetAverage;
        public bool Complete;
    }
    public static class Calc
    {
        // Weighted minima of table 3, ordered by level, then criterion.
        public static readonly double[] Thresholds = { .225, .35, .65, .9 };
        public static readonly double[][] Minima = {
            new double[] { .075, .1, .05, 0, 0, 0, 0, 0 },
            new double[] { .075, .1, .05, .05, .075, 0, 0, 0 },
            new double[] { .15, .2, .05, .05, .15, .05, 0, 0 },
            new double[] { .15, .2, .05, .05, .15, .1, .1, .1 }
        };
        public static string LevelName(int level)
        {
            return new[] { "Нулевой (отсутствует)", "Начальный", "Системный", "Контролируемый", "Верифицируемый" }[Math.Max(0, Math.Min(4, level))];
        }
        public static DirectionResult Direction(DirectionAssessment d)
        {
            DirectionResult r = new DirectionResult();
            double[] weighted = new double[8];
            for (int j = 0; j < 8; j++)
            {
                if (d.Answers[j].HasValue) r.Answered++;
                weighted[j] = d.Answers[j].GetValueOrDefault() * Data.Criteria[j].Weight;
                r.Score += weighted[j];
            }
            r.Complete = r.Answered == 8;
            for (int level = 4; level >= 1; level--)
            {
                if (r.Score + 1e-9 < Thresholds[level - 1]) continue;
                bool met = true;
                for (int j = 0; j < 8; j++) if (weighted[j] + 1e-9 < Minima[level - 1][j]) met = false;
                if (met) { r.Level = level; break; }
            }
            r.Gap = Math.Max(0, d.Target - r.Level);
            if (!r.Complete) r.Recommendations.Add("Заполните оставшиеся критерии: " + (8 - r.Answered) + ". До этого результат предварительный.");
            if (r.Level < d.Target)
            {
                double[] need = Minima[d.Target - 1];
                for (int j = 0; j < 8; j++)
                {
                    if (weighted[j] + 1e-9 >= need[j]) continue;
                    double requiredAnswer = need[j] / Data.Criteria[j].Weight;
                    AnswerOption option = Data.Criteria[j].Options.First(x => x.Value + 1e-9 >= requiredAnswer);
                    r.Recommendations.Add(Data.Criteria[j].Name + ": требуется оценка не ниже " + F(option.Value) + ". " + option.Text);
                }
                if (!r.Recommendations.Any(x => x.Contains(": требуется оценка")) && r.Score + 1e-9 < Thresholds[d.Target - 1])
                    r.Recommendations.Add("Увеличьте взвешенную сумму P до " + F(Thresholds[d.Target - 1], "0.000") + " (сейчас " + F(r.Score, "0.000") + ").");
            }
            if (r.Complete && r.Level >= d.Target) r.Recommendations.Add("Целевой уровень достигнут. Поддерживайте процессы и сохраняйте подтверждающие материалы.");
            return r;
        }
        public static Summary Summary(Assessment a)
        {
            Summary s = new Summary();
            foreach (DirectionAssessment d in a.Directions.Where(x => x.Included))
            {
                DirectionResult r = Direction(d);
                s.Count++; s.Answered += r.Answered; s.Average += r.Level; s.TargetAverage += d.Target;
                if (r.Complete && r.Level >= d.Target) s.Met++;
            }
            s.Total = s.Count * 8;
            s.Complete = s.Count > 0 && s.Answered == s.Total;
            if (s.Count > 0)
            {
                s.Average = Math.Round(s.Average / s.Count, 2, MidpointRounding.AwayFromZero);
                s.TargetAverage = Math.Round(s.TargetAverage / s.Count, 2, MidpointRounding.AwayFromZero);
            }
            return s;
        }
        public static string F(double value, string format = "0.##") { return value.ToString(format, CultureInfo.GetCultureInfo("ru-RU")); }
    }
    public static class Store
    {
        public static string Serialize(Assessment a) { return new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Serialize(a); }
        public static Assessment Parse(string json)
        {
            Assessment a = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Deserialize<Assessment>(json);
            Validate(a);
            return a;
        }
        public static void Validate(Assessment a)
        {
            if (a == null || a.Version != 1) throw new InvalidDataException("Неподдерживаемый формат проекта. Нужен файл оценки версии 1.");
            if (a.Category < 0 || a.Category > 3) throw new InvalidDataException("Некорректная категория системы.");
            if (a.Directions == null || a.Directions.Length != 21) throw new InvalidDataException("В проекте должны быть данные по 21 направлению.");
            DateTime date;
            if (!DateTime.TryParseExact(a.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) throw new InvalidDataException("Некорректная дата оценки.");
            SystemPassports.Validate(a.Passport);
            ReportFacts.Normalize(a);
            int minimum = Math.Max(1, a.Category);
            HashSet<int> ids = new HashSet<int>();
            foreach (DirectionAssessment d in a.Directions)
            {
                if (d == null || d.Id < 1 || d.Id > 21 || !ids.Add(d.Id)) throw new InvalidDataException("Некорректные номера направлений.");
                if (d.Target < minimum || d.Target > 4) throw new InvalidDataException("Целевой уровень должен находиться между минимумом категории и 4.");
                if (d.Answers == null || d.Answers.Length != 8) throw new InvalidDataException("В направлении должно быть 8 критериев.");
                for (int j = 0; j < 8; j++)
                    if (d.Answers[j].HasValue && !Data.Criteria[j].Options.Any(o => Math.Abs(o.Value - d.Answers[j].Value) < 1e-10)) throw new InvalidDataException("Недопустимое значение ответа.");
                d.Evidence = d.Evidence ?? "";
            }
            a.Directions = a.Directions.OrderBy(x => x.Id).ToArray();
            a.Organization = a.Organization ?? ""; a.SystemName = a.SystemName ?? "";
            a.Assessor = a.Assessor ?? ""; a.Notes = a.Notes ?? "";
            a.OrganizationShort = a.OrganizationShort ?? ""; a.OrganizationAddress = a.OrganizationAddress ?? ""; a.City = a.City ?? "";
            a.AssessorPosition = a.AssessorPosition ?? ""; a.ApproverName = a.ApproverName ?? "";
            a.ApproverPosition = a.ApproverPosition ?? ""; a.DocumentNumber = a.DocumentNumber ?? "";
        }
        public static Assessment Load(string path)
        {
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Файл слишком большой для проекта оценки.");
            return Parse(File.ReadAllText(path, Encoding.UTF8));
        }
        public static void Save(Assessment a, string path)
        {
            Validate(a);
            string full = Path.GetFullPath(path);
            string folder = Path.GetDirectoryName(full);
            Directory.CreateDirectory(folder);
            string temp = Path.Combine(folder, "." + Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temp, Serialize(a), new UTF8Encoding(false));
                if (File.Exists(full)) File.Replace(temp, full, null); else File.Move(temp, full);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
