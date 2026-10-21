using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MaturityApp
{
    // Data required by section VI (items 34-36) of the methodology that the
    // calculation itself does not need. Every field is optional on load so
    // projects from 1.0-1.5 keep opening.
    public class ReportPerson
    {
        // Role: "assessor" (п. 35в, additional to the main assessor), "participant" or "source" (п. 35г).
        public string Role = "participant", Name = "", Position = "", Organization = "", Contact = "";
    }

    public class PreviousDirection
    {
        public int Id;
        public bool Included = true;
        public int Level;
        public double?[] Answers; // null when only the level is known
    }

    public class PreviousResult
    {
        public string Date = "", Source = "";
        public double Uzi;
        public PreviousDirection[] Directions; // null when only the overall value was entered
    }

    public class CatalogItem
    {
        public string Key, Text;
        public CatalogItem(string key, string text) { Key = key; Text = text; }
    }

    public class DirectionChange
    {
        public int Id, Before, After;
        public bool BeforeIncluded, AfterIncluded;
    }

    public class DynamicsResult
    {
        public string Kind = "", Title = "", Explanation = "";
        public double PreviousUzi, CurrentUzi;
        public List<DirectionChange> Directions = new List<DirectionChange>();
        public List<string> CriterionDecreases = new List<string>();
    }

    // Owner and deadline of one plan row (item 35 н). Empty values fall back to the plan defaults.
    public class PlanItem { public string Key = "", Responsible = "", Deadline = ""; }

    // One protected object type inside the assessed scope, e.g. "ГИС, К3" or "ЗОКИИ, 2 категория".
    public class ProtectionObject
    {
        public string Kind = "", Name = "", Document = "";
        public int Level; // класс ГИС 1-3, уровень защищённости ПДн 1-4, категория значимости 1-3; 0 — не установлен
        // ИСПДн only (ПП РФ от 01.11.2012 № 1119): categories, whose data, how many subjects. Empty = not specified.
        public string[] PdCategories = new string[0];
        public string Subjects = "", Volume = "";
    }

    public class ObjectKind
    {
        public string Key, Text, LevelLabel, Requirement;
        public string[] Levels; // index + 1 = Level
        public ObjectKind(string key, string text, string levelLabel, string requirement, params string[] levels) { Key = key; Text = text; LevelLabel = levelLabel; Requirement = requirement; Levels = levels; }
    }

    public static class ObjectCatalog
    {
        public static readonly ObjectKind[] Kinds = {
            new ObjectKind("gis", "Государственная информационная система (ГИС)", "Класс защищённости", "fstec117", "К1", "К2", "К3"),
            new ObjectKind("ispdn", "Информационная система персональных данных (ИСПДн)", "Уровень защищённости ПДн", "fstec21", "УЗ-1", "УЗ-2", "УЗ-3", "УЗ-4"),
            new ObjectKind("kii", "Значимый объект КИИ", "Категория значимости", "fstec239", "1 категория", "2 категория", "3 категория"),
            new ObjectKind("asutp", "АСУ ТП", "Класс защищённости", "fstec31asutp", "К1", "К2", "К3"), // п. 11 Методики не называет АСУ ТП: класс не меняет цель
            new ObjectKind("opk", "ИС управления производством ОПК", "", "fstec31opk"),
            new ObjectKind("other", "Иная информационная система", "", null)
        };
        public static ObjectKind Find(string key) { return Kinds.FirstOrDefault(k => k.Key == key); }

        // Item 11 of the methodology: recommended minimum target per object type and class.
        public static int MinimumTarget(ProtectionObject o)
        {
            if (o.Level <= 0) return 1;
            switch (o.Kind)
            {
                case "gis": return 4 - o.Level;                       // К1 → 3, К2 → 2, К3 → 1
                case "ispdn": return o.Level >= 3 ? 1 : 4 - o.Level;  // УЗ-1 → 3, УЗ-2 → 2, УЗ-3/4 → 1
                case "kii": return 4 - o.Level;                       // 1 → 3, 2 → 2, 3 → 1
                default: return 1;
            }
        }
        public static string Text(ProtectionObject o)
        {
            ObjectKind k = Find(o.Kind); if (k == null) return o.Kind;
            string level = o.Level > 0 && o.Level <= k.Levels.Length ? ", " + k.Levels[o.Level - 1] : k.Levels.Length > 0 ? ", " + k.LevelLabel.ToLower() + " не установлен" : "";
            return (o.Name.Length > 0 ? o.Name + " — " : "") + k.Text + level + (o.Document.Length > 0 ? " (" + o.Document + ")" : "") + PdText(o);
        }

        public static readonly CatalogItem[] PdCategories = {
            new CatalogItem("special", "специальные"), new CatalogItem("biometric", "биометрические"),
            new CatalogItem("public", "общедоступные"), new CatalogItem("other", "иные")
        };
        public static readonly CatalogItem[] Subjects = {
            new CatalogItem("", "Субъекты ПДн: не указано"), new CatalogItem("staff", "Работники оператора"),
            new CatalogItem("nonstaff", "Не являющиеся работниками"), new CatalogItem("both", "Работники и иные лица")
        };
        public static readonly CatalogItem[] Volumes = {
            new CatalogItem("", "Число субъектов: не указано"), new CatalogItem("lt100k", "Менее 100 000 субъектов"), new CatalogItem("gt100k", "100 000 субъектов и более")
        };
        public static string PdText(ProtectionObject o)
        {
            if (o.Kind != "ispdn") return "";
            List<string> parts = new List<string>();
            if (o.PdCategories.Length > 0) parts.Add("категории ПДн: " + String.Join(", ", ReportCatalog.Texts(PdCategories, o.PdCategories)));
            if (o.Subjects.Length > 0) parts.Add("субъекты: " + ReportCatalog.Text(Subjects, o.Subjects).ToLower());
            if (o.Volume.Length > 0) parts.Add(ReportCatalog.Text(Volumes, o.Volume).ToLower());
            return parts.Count == 0 ? "" : "; " + String.Join("; ", parts);
        }
        // Category (= minimum target) follows the strictest object; matching requirements are ticked.
        // Unticking a requirement afterwards is the user's decision and is kept until objects change.
        public static void ApplyToAssessment(Assessment a)
        {
            if (a.Objects.Length == 0) return;
            bool anyLevel = a.Objects.Any(o => o.Level > 0);
            a.Category = anyLevel ? a.Objects.Max(o => MinimumTarget(o)) : 0;
            foreach (DirectionAssessment d in a.Directions) d.Target = Math.Max(Math.Max(1, a.Category), d.Target);
            string[] add = a.Objects.Select(o => Find(o.Kind).Requirement).Where(r => r != null).ToArray();
            a.Requirements = ReportCatalog.Requirements.Where(r => add.Contains(r.Key) || a.Requirements.Contains(r.Key)).Select(r => r.Key).ToArray();
        }
    }

    public static class ReportCatalog
    {
        // Footnote 1 of the methodology: requirements the assessed activity follows (п. 35а).
        public static readonly CatalogItem[] Requirements = {
            new CatalogItem("fstec117", "Требования о защите информации, содержащейся в ГИС (приказ ФСТЭК России от 11.04.2025 № 117)"),
            new CatalogItem("fstec21", "Состав и содержание мер по обеспечению безопасности ПДн в ИСПДн (приказ ФСТЭК России от 18.02.2013 № 21)"),
            new CatalogItem("fstec239", "Требования по обеспечению безопасности значимых объектов КИИ (приказ ФСТЭК России от 25.12.2017 № 239)"),
            new CatalogItem("fstec31asutp", "Требования к защите информации в АСУ ТП (приказ ФСТЭК России от 14.03.2013 № 31)"),
            new CatalogItem("fstec31opk", "Требования к защите информации в ИС управления производством предприятий ОПК (приказ ФСТЭК России от 28.02.2017 № 31)")
        };

        // Item 19, letters а-н: source data used for the assessment (п. 35е).
        public static readonly CatalogItem[] SourceData = {
            new CatalogItem("a", "Политика защиты информации"),
            new CatalogItem("b", "Приказы и иные документы о полномочиях лиц, ответственных за защиту информации"),
            new CatalogItem("v", "Приказы и иные документы о полномочиях подразделений"),
            new CatalogItem("g", "Внутренние стандарты, регламенты и иные документы по защите информации и эксплуатации ИС"),
            new CatalogItem("d", "План мероприятий по совершенствованию защиты информации"),
            new CatalogItem("e", "Отчёты и протоколы по результатам мероприятий и мер по защите информации"),
            new CatalogItem("zh", "Результаты последней оценки уровня зрелости"),
            new CatalogItem("z", "Отчёты по результатам контроля и анализа защищённости ИС"),
            new CatalogItem("i", "Акты и протоколы государственного контроля в области защиты информации"),
            new CatalogItem("k", "Результаты опроса (интервьюирования) работников"),
            new CatalogItem("l", "Результаты наблюдения за деятельностью работников в ИС"),
            new CatalogItem("m", "Результаты наблюдения за функционированием программных и программно-аппаратных средств"),
            new CatalogItem("n", "Результаты работы инструментальных средств анализа защищённости и мониторинга ИБ")
        };

        // Item 23: how source data were collected and analysed (п. 35ж, methods).
        public static readonly CatalogItem[] Methods = {
            new CatalogItem("documents", "Запрос и анализ документов и материалов структурных подразделений"),
            new CatalogItem("interview", "Опрос (интервьюирование) работников с документированием результатов"),
            new CatalogItem("previous", "Анализ отчётов о результатах предыдущей оценки уровня зрелости"),
            new CatalogItem("observation", "Наблюдение за деятельностью работников и функционированием средств, в том числе СЗИ")
        };

        // Item 20: instruments (п. 35ж, tools).
        public static readonly CatalogItem[] Instruments = {
            new CatalogItem("calculator", "Программа «Калькулятор зрелости» — расчёт по таблицам 2 и 3 Методики"),
            new CatalogItem("grc", "GRC-система"),
            new CatalogItem("soar", "Платформа оркестрации и реагирования (SOAR, SecOps)"),
            new CatalogItem("ea", "Инструмент проектирования корпоративной архитектуры (Enterprise Architecture Tools)"),
            new CatalogItem("llm", "Языковая модель, адаптированная для работы с исходными данными"),
            new CatalogItem("other", "Иные инструментальные средства (сканеры, средства мониторинга и т. п. — укажите ниже)")
        };

        public static string RoleText(string role)
        {
            return role == "assessor" ? "Проводил оценку" : role == "source" ? "Предоставлял исходные данные" : "Участвовал в оценке";
        }
        public static string Text(CatalogItem[] catalog, string key)
        {
            CatalogItem item = catalog.FirstOrDefault(x => x.Key == key);
            return item == null ? key : item.Text;
        }
        public static string[] Texts(CatalogItem[] catalog, string[] keys)
        {
            if (keys == null) return new string[0];
            return catalog.Where(x => keys.Contains(x.Key)).Select(x => x.Text).ToArray();
        }
    }

    public static class ReportFacts
    {
        static readonly string[] Roles = { "assessor", "participant", "source" };

        public static void Normalize(Assessment a)
        {
            a.AssessmentKind = a.AssessmentKind == "external" ? "external" : "internal";
            a.AssessorOrganization = a.AssessorOrganization ?? ""; a.AssessorContact = a.AssessorContact ?? "";
            a.ExternalLicense = a.ExternalLicense ?? ""; a.ToolsNote = a.ToolsNote ?? ""; a.SourceDataNote = a.SourceDataNote ?? "";
            a.PlanResponsible = a.PlanResponsible ?? ""; a.PlanDeadline = a.PlanDeadline ?? "";
            a.Requirements = Known(a.Requirements, ReportCatalog.Requirements, "основание требований");
            a.SourceData = Known(a.SourceData, ReportCatalog.SourceData, "вид исходных данных");
            a.Methods = Known(a.Methods, ReportCatalog.Methods, "метод оценки");
            a.Instruments = Known(a.Instruments, ReportCatalog.Instruments, "инструмент оценки");
            a.Participants = (a.Participants ?? new ReportPerson[0]).Where(p => p != null).ToArray();
            if (a.Participants.Length > 200) throw new InvalidDataException("Слишком много участников оценки.");
            foreach (ReportPerson p in a.Participants)
            {
                if (!Roles.Contains(p.Role)) throw new InvalidDataException("Неизвестная роль участника оценки.");
                p.Name = p.Name ?? ""; p.Position = p.Position ?? ""; p.Organization = p.Organization ?? ""; p.Contact = p.Contact ?? "";
            }
            if (a.Previous != null) ValidatePrevious(a.Previous);
            a.Objects = (a.Objects ?? new ProtectionObject[0]).Where(o => o != null).ToArray();
            if (a.Objects.Length > 50) throw new InvalidDataException("Слишком много объектов защиты.");
            foreach (ProtectionObject o in a.Objects)
            {
                ObjectKind k = ObjectCatalog.Find(o.Kind);
                if (k == null) throw new InvalidDataException("Неизвестный тип объекта защиты.");
                if (o.Level < 0 || o.Level > k.Levels.Length) throw new InvalidDataException("Некорректный класс или категория объекта защиты.");
                o.Name = o.Name ?? ""; o.Document = o.Document ?? "";
                o.Subjects = o.Subjects ?? ""; o.Volume = o.Volume ?? "";
                if (!ObjectCatalog.Subjects.Any(x => x.Key == o.Subjects) || !ObjectCatalog.Volumes.Any(x => x.Key == o.Volume)) throw new InvalidDataException("Некорректные сведения о субъектах персональных данных.");
                o.PdCategories = Known(o.PdCategories, ObjectCatalog.PdCategories, "категория персональных данных");
                if (o.Kind != "ispdn" && (o.PdCategories.Length > 0 || o.Subjects.Length > 0 || o.Volume.Length > 0)) throw new InvalidDataException("Сведения о персональных данных указываются только для ИСПДн.");
            }
            a.PlanItems = (a.PlanItems ?? new PlanItem[0]).Where(x => x != null && !String.IsNullOrEmpty(x.Key)).ToArray();
            foreach (PlanItem item in a.PlanItems) { item.Responsible = item.Responsible ?? ""; item.Deadline = item.Deadline ?? ""; }
        }
        // Rows of the improvement plan (п. 35 н): real measures only. Unanswered criteria are
        // not measures; the P threshold matters only once a direction is fully answered.
        public static DeficitRecord[] PlanRows(Assessment a, AssessmentAnalysis analysis)
        {
            return analysis.Deficits.Where(d => d.Type == "improve" || d.Type == "score" && Calc.Direction(a.Directions.First(x => x.Id == d.DirectionId)).Complete).ToArray();
        }
        public static PlanItem PlanFor(Assessment a, string key)
        {
            PlanItem item = a.PlanItems.FirstOrDefault(x => x.Key == key);
            return new PlanItem { Key = key, Responsible = item != null && item.Responsible.Length > 0 ? item.Responsible : a.PlanResponsible, Deadline = item != null && item.Deadline.Length > 0 ? item.Deadline : a.PlanDeadline };
        }

        static string[] Known(string[] keys, CatalogItem[] catalog, string what)
        {
            if (keys == null) return new string[0];
            foreach (string key in keys)
                if (!catalog.Any(x => x.Key == key)) throw new InvalidDataException("Неизвестный " + what + ": " + key + ".");
            return catalog.Where(x => keys.Contains(x.Key)).Select(x => x.Key).ToArray(); // catalog order, no duplicates
        }

        public static void ValidatePrevious(PreviousResult p)
        {
            DateTime date;
            if (!DateTime.TryParseExact(p.Date ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) throw new InvalidDataException("Некорректная дата предыдущей оценки.");
            if (p.Uzi < 0 || p.Uzi > 4 || Double.IsNaN(p.Uzi)) throw new InvalidDataException("Узи предыдущей оценки должен быть от 0 до 4.");
            p.Source = p.Source ?? "";
            if (p.Directions == null) return;
            if (p.Directions.Length != 21 || p.Directions.Any(d => d == null) || p.Directions.Select(d => d.Id).Distinct().Count() != 21 || p.Directions.Any(d => d.Id < 1 || d.Id > 21))
                throw new InvalidDataException("В предыдущей оценке должны быть данные по 21 направлению.");
            foreach (PreviousDirection d in p.Directions)
            {
                if (d.Level < 0 || d.Level > 4) throw new InvalidDataException("Некорректный уровень направления в предыдущей оценке.");
                if (d.Answers != null && d.Answers.Length != 8) throw new InvalidDataException("В предыдущей оценке направление должно иметь 8 критериев.");
            }
            p.Directions = p.Directions.OrderBy(d => d.Id).ToArray();
        }

        // Builds the previous result from an earlier project or bundle (п. 35д).
        public static PreviousResult FromAssessment(Assessment old, string source)
        {
            Summary s = Calc.Summary(old);
            if (s.Count == 0) throw new InvalidDataException("В предыдущей оценке нет включённых направлений.");
            return new PreviousResult {
                Date = old.Date, Uzi = s.Average, Source = source ?? "",
                Directions = old.Directions.OrderBy(d => d.Id).Select(d => new PreviousDirection { Id = d.Id, Included = d.Included, Level = Calc.Direction(d).Level, Answers = (double?[])d.Answers.Clone() }).ToArray()
            };
        }

        // Item 33. Negative wins when anything decreased; otherwise growth of Uzi is
        // "позитивная", growth of individual directions only is "положительная".
        public static DynamicsResult Dynamics(Assessment a)
        {
            if (a.Previous == null) return null;
            Summary s = Calc.Summary(a);
            DynamicsResult r = new DynamicsResult { PreviousUzi = a.Previous.Uzi, CurrentUzi = s.Average };
            bool directionDown = false, directionUp = false;
            if (a.Previous.Directions != null)
                foreach (PreviousDirection before in a.Previous.Directions)
                {
                    DirectionAssessment now = a.Directions.First(d => d.Id == before.Id);
                    int after = Calc.Direction(now).Level;
                    if (before.Included && now.Included)
                    {
                        if (after < before.Level) directionDown = true;
                        if (after > before.Level) directionUp = true;
                        if (before.Answers != null)
                            for (int j = 0; j < 8; j++)
                                if (before.Answers[j].HasValue && now.Answers[j].GetValueOrDefault() + 1e-9 < before.Answers[j].Value)
                                    r.CriterionDecreases.Add(before.Id + ". " + Data.DirectionNames[before.Id - 1] + " — " + Data.Criteria[j].Name + ": " + Calc.F(before.Answers[j].Value) + " → " + (now.Answers[j].HasValue ? Calc.F(now.Answers[j].Value) : "не заполнено"));
                    }
                    if (before.Included != now.Included || before.Included && after != before.Level)
                        r.Directions.Add(new DirectionChange { Id = before.Id, Before = before.Level, After = after, BeforeIncluded = before.Included, AfterIncluded = now.Included });
                }
            bool uziUp = s.Average > a.Previous.Uzi + 1e-9, uziDown = s.Average + 1e-9 < a.Previous.Uzi;
            if (uziDown || directionDown || r.CriterionDecreases.Count > 0)
            {
                r.Kind = "negative"; r.Title = "Негативная динамика";
                r.Explanation = "Зафиксировано снижение результатов оценки по одному или более показателю (п. 33 г Методики).";
            }
            else if (uziUp)
            {
                r.Kind = "positive"; r.Title = "Позитивная динамика";
                r.Explanation = "Зафиксирован рост значения уровня зрелости Узи (п. 33 а Методики).";
            }
            else if (directionUp)
            {
                r.Kind = "growth"; r.Title = "Положительная динамика";
                r.Explanation = "Зафиксирован рост оценок по отдельным направлениям (п. 33 б Методики).";
            }
            else
            {
                r.Kind = "zero"; r.Title = "Нулевая динамика";
                r.Explanation = a.Previous.Directions == null ? "Значение Узи не изменилось. Сведения по направлениям предыдущей оценки не загружены." : "Изменения по показателям оценки отсутствуют (п. 33 в Методики).";
            }
            return r;
        }

        // Remaining gaps against item 35; shown before export and in the preflight list.
        public static string[] Missing(Assessment a)
        {
            List<string> m = new List<string>();
            if (a.Objects == null || a.Objects.Length == 0) m.Add("п. 35а: не указан тип объекта защиты (ГИС, ИСПДн, ЗОКИИ и т. п.) и его класс или категория.");
            if (a.Requirements == null || a.Requirements.Length == 0) m.Add("п. 35а: не отмечены действующие требования по защите информации.");
            if (String.IsNullOrWhiteSpace(a.AssessorOrganization) || String.IsNullOrWhiteSpace(a.AssessorContact)) m.Add("п. 35в: не указаны организация или контакты лица, проводившего оценку.");
            if (a.Participants == null || !a.Participants.Any(p => p.Role != "assessor" && !String.IsNullOrWhiteSpace(p.Name))) m.Add("п. 35г: не указаны лица, участвовавшие в оценке и предоставлявшие исходные данные.");
            if (a.SourceData == null || a.SourceData.Length == 0) m.Add("п. 35е: не отмечены исходные данные.");
            if (a.Methods == null || a.Methods.Length == 0) m.Add("п. 35ж: не отмечены методы оценки.");
            if (a.Instruments == null || a.Instruments.Length == 0) m.Add("п. 35ж: не отмечены инструменты оценки.");
            int unassigned = PlanRows(a, Analysis.Build(a)).Count(d => { PlanItem p = PlanFor(a, d.Key); return p.Responsible.Trim().Length == 0 || p.Deadline.Trim().Length == 0; });
            if (unassigned > 0) m.Add("п. 35н: у мероприятий плана не указаны ответственный или срок (строк: " + unassigned + ").");
            if (a.AssessmentKind == "external" && String.IsNullOrWhiteSpace(a.ExternalLicense)) m.Add("п. 6: для внешней оценки не указана лицензия организации.");
            return m.ToArray();
        }
    }
}
