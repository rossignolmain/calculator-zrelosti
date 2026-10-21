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
            new CatalogItem("b", "ОРД о полномочиях и правах лиц, ответственных за защиту информации"),
            new CatalogItem("v", "ОРД о полномочиях и правах подразделений"),
            new CatalogItem("g", "Внутренние стандарты, регламенты и иные ОРД по защите информации и эксплуатации ИС"),
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
            new CatalogItem("llm", "Языковая модель, адаптированная для работы с исходными данными")
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
            if (a.Requirements == null || a.Requirements.Length == 0) m.Add("п. 35а: не отмечены действующие требования по защите информации.");
            if (String.IsNullOrWhiteSpace(a.AssessorOrganization) || String.IsNullOrWhiteSpace(a.AssessorContact)) m.Add("п. 35в: не указаны организация или контакты лица, проводившего оценку.");
            if (a.Participants == null || !a.Participants.Any(p => p.Role != "assessor" && !String.IsNullOrWhiteSpace(p.Name))) m.Add("п. 35г: не указаны лица, участвовавшие в оценке и предоставлявшие исходные данные.");
            if (a.SourceData == null || a.SourceData.Length == 0) m.Add("п. 35е: не отмечены исходные данные.");
            if (a.Methods == null || a.Methods.Length == 0) m.Add("п. 35ж: не отмечены методы оценки.");
            if (a.Instruments == null || a.Instruments.Length == 0) m.Add("п. 35ж: не отмечены инструменты оценки.");
            if (String.IsNullOrWhiteSpace(a.PlanResponsible) || String.IsNullOrWhiteSpace(a.PlanDeadline)) m.Add("п. 35н: не указаны ответственный и срок выполнения мероприятий.");
            if (a.AssessmentKind == "external" && String.IsNullOrWhiteSpace(a.ExternalLicense)) m.Add("п. 6: для внешней оценки не указана лицензия организации.");
            return m.ToArray();
        }
    }
}
