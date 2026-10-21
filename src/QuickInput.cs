using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace MaturityApp
{
    public class AnswerChange
    {
        public int DirectionId;
        // Zero-based index into Data.Criteria and DirectionAssessment.Answers.
        public int Criterion;
        public double? Before, After;
    }

    public static class QuickInput
    {
        public static readonly string[] HeaderNames = {
            "Документирование", "Выполнение", "Инструменты", "Квалификация",
            "Контроль", "Обучение", "Внешний аудит", "Актуализация"
        };
        static readonly string[][] Labels = {
            new[] { "Не разработаны", "Есть недостатки", "Соответствуют и обновляются" },
            new[] { "Не реализуется", "Частично", "По регламентам" },
            new[] { "Вручную", "Отдельные инструменты", "Полностью с инструментами" },
            new[] { "Нет компетенций", "Обучены, без опыта", "Есть компетенции" },
            new[] { "Не проводится", "После инцидентов", "Плановые проверки" },
            new[] { "Не проводится", "Эпизодически, по инициативе", "Без системы и измерения", "Программа и контроль знаний" },
            new[] { "Не проводится", "Эпизодически", "Регулярно, с улучшениями" },
            new[] { "Не проводится", "Эпизодически", "Улучшения по плану" }
        };

        public static string ShortLabel(int criterion, double value)
        {
            if (criterion < 0 || criterion >= Data.Criteria.Length) throw new ArgumentOutOfRangeException("criterion");
            AnswerOption[] options = Data.Criteria[criterion].Options;
            for (int index = 0; index < options.Length; index++)
                if (Math.Abs(options[index].Value - value) < 1e-10) return Labels[criterion][index];
            throw new ArgumentOutOfRangeException("value", "Недопустимая оценка для выбранного вида требования.");
        }

        public static List<AnswerChange> CopyIntoEmpty(Assessment a, int sourceId)
        {
            if (sourceId < 1 || sourceId > 21) throw new ArgumentOutOfRangeException("sourceId");
            ValidateStructure(a);
            DirectionAssessment source = a.Directions.First(d => d.Id == sourceId);
            if (!source.Included) throw new InvalidOperationException("Источник ответов должен быть включён в область оценки.");
            List<AnswerChange> changes = new List<AnswerChange>();
            foreach (DirectionAssessment destination in a.Directions.Where(d => d.Included && d.Id != sourceId).OrderBy(d => d.Id))
                for (int criterion = 0; criterion < Data.Criteria.Length; criterion++)
                    if (!destination.Answers[criterion].HasValue && source.Answers[criterion].HasValue)
                        changes.Add(new AnswerChange {
                            DirectionId = destination.Id, Criterion = criterion,
                            Before = destination.Answers[criterion], After = source.Answers[criterion]
                        });
            // Plan every change before touching answers, so an invalid source or model never partially applies.
            Dictionary<int, DirectionAssessment> destinations = a.Directions.ToDictionary(d => d.Id);
            foreach (AnswerChange change in changes) destinations[change.DirectionId].Answers[change.Criterion] = change.After;
            return changes;
        }

        // criterion -1 means all eight criteria. A null scope means every included
        // direction; an explicit scope contains direction IDs, not array indices.
        // Duplicate IDs are harmless, and valid but excluded directions are skipped.
        // This method only plans changes, so updating an operation's parameters never
        // records answers. SetValue is the explicit command that applies this plan.
        public static List<AnswerChange> PreviewSetValue(Assessment a, int criterion, double value, bool overwrite, IEnumerable<int> directionIds = null)
        {
            if (criterion < -1 || criterion >= Data.Criteria.Length) throw new ArgumentOutOfRangeException("criterion");
            ValidateStructure(a);
            int[] criteria = criterion == -1 ? Enumerable.Range(0, Data.Criteria.Length).ToArray() : new[] { criterion };
            foreach (int selected in criteria)
                if (!Allowed(selected, value)) throw new ArgumentOutOfRangeException("value", "Оценка недопустима для одного из выбранных видов требований.");

            // Enumerate and validate the entire scope before constructing/applying
            // any mutation, including a lazily supplied sequence that may fail.
            int[] requested = directionIds == null ? a.Directions.Select(d => d.Id).ToArray() : directionIds.ToArray();
            HashSet<int> ids = new HashSet<int>();
            foreach (int id in requested)
            {
                if (id < 1 || id > 21) throw new ArgumentOutOfRangeException("directionIds", "Номер направления должен находиться между 1 и 21.");
                ids.Add(id);
            }

            Dictionary<int, double> canonicalValues = criteria.ToDictionary(selected => selected,
                selected => Data.Criteria[selected].Options.First(o => Math.Abs(o.Value - value) < 1e-10).Value);
            List<AnswerChange> changes = new List<AnswerChange>();
            foreach (DirectionAssessment destination in a.Directions.Where(d => d.Included && ids.Contains(d.Id)).OrderBy(d => d.Id))
                foreach (int selected in criteria)
                {
                    double? before = destination.Answers[selected];
                    if (!overwrite && before.HasValue) continue; // An explicit zero is filled.
                    double after = canonicalValues[selected];
                    if (before.HasValue && Math.Abs(before.Value - after) < 1e-10) continue;
                    changes.Add(new AnswerChange { DirectionId = destination.Id, Criterion = selected, Before = before, After = after });
                }
            return changes;
        }

        public static List<AnswerChange> SetValue(Assessment a, int criterion, double value, bool overwrite, IEnumerable<int> directionIds = null)
        {
            List<AnswerChange> changes = PreviewSetValue(a, criterion, value, overwrite, directionIds);
            Dictionary<int, DirectionAssessment> destinations = a.Directions.ToDictionary(d => d.Id);
            foreach (AnswerChange change in changes) destinations[change.DirectionId].Answers[change.Criterion] = change.After;
            return changes;
        }

        public static int Revert(Assessment a, IEnumerable<AnswerChange> changes)
        {
            ValidateStructure(a);
            if (changes == null) throw new ArgumentNullException("changes");
            AnswerChange[] snapshot = changes.ToArray();
            Dictionary<int, DirectionAssessment> directions = a.Directions.ToDictionary(d => d.Id);
            int count = 0;
            foreach (AnswerChange change in snapshot)
            {
                if (change == null || change.Criterion < 0 || change.Criterion >= Data.Criteria.Length) continue;
                DirectionAssessment direction;
                if (!directions.TryGetValue(change.DirectionId, out direction)) continue;
                if (!Allowed(change.Criterion, change.Before) || !Allowed(change.Criterion, change.After) || change.Before == change.After) continue;
                if (direction.Answers[change.Criterion] != change.After) continue;
                direction.Answers[change.Criterion] = change.Before;
                count++;
            }
            return count;
        }

        // The most frequent answer among included directions; ties go to the lower option.
        // Null when no included direction has an answer for this criterion.
        public static double? CommonValue(Assessment a, int criterion)
        {
            if (criterion < 0 || criterion >= Data.Criteria.Length) throw new ArgumentOutOfRangeException("criterion");
            var groups = a.Directions.Where(d => d.Included && d.Answers[criterion].HasValue)
                .GroupBy(d => d.Answers[criterion].Value).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).ToArray();
            return groups.Length == 0 ? (double?)null : groups[0].Key;
        }

        // Sets a criterion for every included direction that is empty or still follows the
        // current common value. Directions that were answered differently keep their grade.
        public static List<AnswerChange> SetCommonValue(Assessment a, int criterion, double value)
        {
            if (criterion < 0 || criterion >= Data.Criteria.Length) throw new ArgumentOutOfRangeException("criterion");
            ValidateStructure(a);
            if (!Allowed(criterion, value)) throw new ArgumentOutOfRangeException("value", "Оценка недопустима для выбранного вида требования.");
            double? common = CommonValue(a, criterion);
            List<AnswerChange> changes = new List<AnswerChange>();
            foreach (DirectionAssessment d in a.Directions.Where(x => x.Included).OrderBy(x => x.Id))
            {
                double? before = d.Answers[criterion];
                bool follows = !before.HasValue || common.HasValue && Math.Abs(before.Value - common.Value) < 1e-10;
                if (!follows || before.HasValue && Math.Abs(before.Value - value) < 1e-10) continue;
                changes.Add(new AnswerChange { DirectionId = d.Id, Criterion = criterion, Before = before, After = value });
            }
            Dictionary<int, DirectionAssessment> destinations = a.Directions.ToDictionary(d => d.Id);
            foreach (AnswerChange change in changes) destinations[change.DirectionId].Answers[change.Criterion] = change.After;
            return changes;
        }

        // Directions whose technology the passport explicitly marks as unused. Unknown never counts.
        public static int[] PassportInapplicable(SystemPassport p)
        {
            if (p == null) return new int[0];
            List<int> ids = new List<int>();
            if (p.Mobile == false) ids.Add(8);
            if (p.RemoteAdministration == false) ids.Add(9);
            if (p.Wireless == false) ids.Add(10);
            if (p.OwnDevelopment == false) ids.Add(13);
            if (p.ArtificialIntelligence == false) ids.Add(19);
            return ids.ToArray();
        }

        static bool Allowed(int criterion, double? answer)
        {
            return !answer.HasValue || Data.Criteria[criterion].Options.Any(o => Math.Abs(o.Value - answer.Value) < 1e-10);
        }
        static void ValidateStructure(Assessment a)
        {
            if (a == null) throw new ArgumentNullException("a");
            if (a.Directions == null || a.Directions.Length != 21) throw new InvalidDataException("В оценке должно быть 21 направление.");
            HashSet<int> ids = new HashSet<int>();
            foreach (DirectionAssessment direction in a.Directions)
            {
                if (direction == null || direction.Id < 1 || direction.Id > 21 || !ids.Add(direction.Id)) throw new InvalidDataException("Некорректные номера направлений.");
                if (direction.Answers == null || direction.Answers.Length != Data.Criteria.Length) throw new InvalidDataException("В направлении должно быть восемь видов требований.");
                for (int criterion = 0; criterion < Data.Criteria.Length; criterion++)
                    if (!Allowed(criterion, direction.Answers[criterion])) throw new InvalidDataException("Недопустимая оценка вида требования.");
            }
        }
    }

    public class TemplateDirection
    {
        public int Id;
        public bool Included = true;
        public int Target = 1;
        public double?[] Answers = new double?[8];
    }
    public class AnswerTemplate
    {
        public string Format = "", Name = "", Author = "", Created = "", Description = "";
        public int Version = 1, Category = 1;
        public TemplateDirection[] Directions;
        // Optional parts added in 1.6. Null means "not part of this template".
        public SystemPassport Passport;
        public string[] Requirements, SourceData, Methods, Instruments;
        public ProtectionObject[] Objects; // types and classes of the protected object (part of grades and goals)
    }

    [Flags]
    public enum TemplateParts { None = 0, Passport = 1, Grades = 2, Practice = 4, All = 7 }

    // A reusable starting point for an object of the same type in another organization.
    // Organization requisites, people, dates, evidence and previous results are per object
    // and never enter a template.
    public static class AnswerTemplates
    {
        const string TemplateFormat = "maturity-answers";
        const int MaximumFileLength = 1024 * 1024;

        public static AnswerTemplate Create(Assessment a, string name, TemplateParts parts = TemplateParts.All)
        {
            Store.Validate(a);
            AnswerTemplate t = new AnswerTemplate {
                Format = TemplateFormat, Name = name ?? "", Category = a.Category,
                Author = Environment.UserName ?? "", Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                Directions = a.Directions.Select(d => new TemplateDirection { Id = d.Id, Included = d.Included, Target = d.Target, Answers = (double?[])d.Answers.Clone() }).ToArray(),
                Objects = a.Objects.Select(o => new ProtectionObject { Kind = o.Kind, Level = o.Level, PdCategories = (string[])o.PdCategories.Clone(), Subjects = o.Subjects, Volume = o.Volume }).ToArray() // names and acts belong to the object, not the template
            };
            if ((parts & TemplateParts.Passport) != 0 && a.Passport != null) t.Passport = SystemPassports.Snapshot(a.Passport);
            if ((parts & TemplateParts.Practice) != 0)
            {
                t.Requirements = (string[])a.Requirements.Clone(); t.SourceData = (string[])a.SourceData.Clone();
                t.Methods = (string[])a.Methods.Clone(); t.Instruments = (string[])a.Instruments.Clone();
            }
            return t;
        }

        public static TemplateParts PartsOf(AnswerTemplate t)
        {
            TemplateParts parts = TemplateParts.Grades;
            if (t.Passport != null) parts |= TemplateParts.Passport;
            if (t.Requirements != null || t.SourceData != null || t.Methods != null || t.Instruments != null) parts |= TemplateParts.Practice;
            return parts;
        }
        public static string PartsText(TemplateParts parts)
        {
            List<string> names = new List<string>();
            if ((parts & TemplateParts.Passport) != 0) names.Add("описание объекта");
            if ((parts & TemplateParts.Grades) != 0) names.Add("оценки и область");
            if ((parts & TemplateParts.Practice) != 0) names.Add("требования, исходные данные, методы");
            return String.Join(", ", names);
        }

        // Validates through a scratch assessment first, so a bad template never partially applies.
        public static void Apply(Assessment a, AnswerTemplate t, TemplateParts parts = TemplateParts.All)
        {
            if (a == null) throw new ArgumentNullException("a");
            Assessment check = Assessment.Create(); check.Category = t.Category;
            if (t.Directions == null || t.Directions.Length != 21) throw new InvalidDataException("В шаблоне должны быть данные по 21 направлению.");
            check.Directions = t.Directions.Select(d => d == null ? null : new DirectionAssessment { Id = d.Id, Included = d.Included, Target = d.Target, Answers = d.Answers == null ? null : (double?[])d.Answers.Clone() }).ToArray();
            check.Passport = t.Passport == null ? null : SystemPassports.Snapshot(t.Passport);
            check.Requirements = t.Requirements ?? new string[0]; check.SourceData = t.SourceData ?? new string[0];
            check.Methods = t.Methods ?? new string[0]; check.Instruments = t.Instruments ?? new string[0];
            check.Objects = t.Objects ?? new ProtectionObject[0];
            Store.Validate(check);
            parts &= PartsOf(t);
            if ((parts & TemplateParts.Grades) != 0)
            {
                a.Category = check.Category;
                if (t.Objects != null) a.Objects = check.Objects;
                foreach (DirectionAssessment source in check.Directions)
                {
                    DirectionAssessment d = a.Directions.First(x => x.Id == source.Id);
                    d.Included = source.Included; d.Target = source.Target; d.Answers = source.Answers;
                }
            }
            if ((parts & TemplateParts.Passport) != 0) a.Passport = check.Passport;
            if ((parts & TemplateParts.Practice) != 0)
            {
                a.Requirements = check.Requirements; a.SourceData = check.SourceData; a.Methods = check.Methods; a.Instruments = check.Instruments;
            }
        }

        public static string Serialize(AnswerTemplate t) { return Serializer().Serialize(t); }
        public static AnswerTemplate Parse(string json)
        {
            if (String.IsNullOrWhiteSpace(json) || json.Length > MaximumFileLength) throw new InvalidDataException("Пустой или слишком большой файл шаблона оценок.");
            AnswerTemplate t;
            try { t = Serializer().Deserialize<AnswerTemplate>(json); }
            catch (Exception ex)
            {
                if (!(ex is ArgumentException) && !(ex is InvalidOperationException)) throw;
                throw new InvalidDataException("Не удалось прочитать шаблон оценок: некорректный JSON.", ex);
            }
            if (t == null || t.Format != TemplateFormat || t.Version != 1) throw new InvalidDataException("Нужен шаблон оценок версии 1 (.answers.json).");
            t.Name = t.Name ?? ""; t.Author = t.Author ?? ""; t.Created = t.Created ?? ""; t.Description = t.Description ?? "";
            Apply(Assessment.Create(), t); // validation only
            return t;
        }
        public static AnswerTemplate Load(string path)
        {
            if (new FileInfo(path).Length > MaximumFileLength) throw new InvalidDataException("Файл шаблона оценок слишком большой.");
            return Parse(File.ReadAllText(path, new UTF8Encoding(false, true)));
        }
        public static void Save(AnswerTemplate t, string path)
        {
            string json = Serialize(t);
            string full = Path.GetFullPath(path), folder = Path.GetDirectoryName(full);
            Directory.CreateDirectory(folder);
            string temp = Path.Combine(folder, "." + Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(full)) File.Replace(temp, full, null); else File.Move(temp, full);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = MaximumFileLength, RecursionLimit = 32 }; }
    }
}
