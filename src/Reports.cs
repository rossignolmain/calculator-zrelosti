using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace MaturityApp
{
    // These exporters write Office Open XML directly; Microsoft Office is not required.
    public static class Reports
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string NsWord = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private const string NsSheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        public static void ExportWord(Assessment a, string path)
        {
            if (a == null) throw new ArgumentNullException("a");
            Summary summary = Calc.Summary(a);
            AssessmentAnalysis analysis = Analysis.Build(a);
            byte[] chart = ChartPng(a);
            string doc = WordDocument(a, summary, analysis);
            AtomicWrite(path, delegate(Stream output)
            {
                using (ZipArchive zip = new ZipArchive(output, ZipArchiveMode.Create, true))
                {
                    Add(zip, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/><Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/><Override PartName=\"/word/footer1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml\"/><Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/><Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/></Types>");
                    Add(zip, "_rels/.rels", PackageRels("word/document.xml", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"));
                    Add(zip, "word/_rels/document.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rStyles\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/><Relationship Id=\"rFooter\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer\" Target=\"footer1.xml\"/><Relationship Id=\"rChart\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"media/profile.png\"/></Relationships>");
                    Add(zip, "word/document.xml", doc);
                    Add(zip, "word/styles.xml", WordStyles());
                    Add(zip, "word/footer1.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:ftr xmlns:w=\"" + NsWord + "\"><w:p><w:pPr><w:pStyle w:val=\"Footer\"/><w:jc w:val=\"right\"/></w:pPr><w:r><w:t>Оценка зрелости ИБ · Страница </w:t></w:r><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> PAGE </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p></w:ftr>");
                    Add(zip, "word/media/profile.png", chart);
                    Add(zip, "docProps/core.xml", CoreProperties("Отчёт об оценке зрелости информационной безопасности", a.Assessor));
                    Add(zip, "docProps/app.xml", AppProperties());
                }
            });
        }

        public static void ExportExcel(Assessment a, string path)
        {
            if (a == null) throw new ArgumentNullException("a");
            Summary summary = Calc.Summary(a);
            AssessmentAnalysis analysis = Analysis.Build(a);
            string[] names = { "Сводка", "Сведения по п. 35", "Dij и Pзн", "Ответы", "Рекомендации", "Профиль требований", "План улучшений", "Общие дефициты" };
            string[] sheets = { SummarySheet(a, summary), ReportInfoSheet(a, summary), DijSheet(a), AnswersSheet(a), RecommendationsSheet(a), CriterionProfileSheet(analysis), DeficitPlanSheet(analysis, a), GroupedActionsSheet(analysis) };
            if (a.Passport != null)
            {
                Array.Resize(ref names, names.Length + 1);
                Array.Resize(ref sheets, sheets.Length + 1);
                names[names.Length - 1] = "Описание объекта";
                sheets[sheets.Length - 1] = PassportSheet(a);
            }
            AtomicWrite(path, delegate(Stream output)
            {
                using (ZipArchive zip = new ZipArchive(output, ZipArchiveMode.Create, true))
                {
                    Add(zip, "[Content_Types].xml", SpreadsheetContentTypes(sheets.Length));
                    Add(zip, "_rels/.rels", PackageRels("xl/workbook.xml", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"));
                    Add(zip, "xl/workbook.xml", WorkbookXml(names));
                    Add(zip, "xl/_rels/workbook.xml.rels", WorkbookRelationships(sheets.Length));
                    Add(zip, "xl/styles.xml", ExcelStyles());
                    for (int i = 0; i < sheets.Length; i++) Add(zip, "xl/worksheets/sheet" + (i + 1) + ".xml", sheets[i]);
                    Add(zip, "docProps/core.xml", CoreProperties("Оценка зрелости информационной безопасности", a.Assessor));
                    Add(zip, "docProps/app.xml", AppProperties());
                }
            });
        }

        public static byte[] ChartPng(Assessment a)
        {
            const int width = 1400, height = 570, left = 80, right = 30, top = 58, bottom = 112;
            using (Bitmap bmp = new Bitmap(width, height))
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font label = new Font("Arial", 13))
            using (Font small = new Font("Arial", 11))
            using (Font title = new Font("Arial", 18, FontStyle.Bold))
            using (Pen grid = new Pen(Color.FromArgb(220, 228, 235)))
            using (Pen targetPen = new Pen(Color.FromArgb(197, 131, 47), 3))
            using (Brush blue = new SolidBrush(Color.FromArgb(26, 95, 130)))
            using (Brush grey = new SolidBrush(Color.FromArgb(198, 204, 211)))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawString("Профиль зрелости по направлениям", title, Brushes.Black, left, 12);
                float plotH = height - top - bottom, plotW = width - left - right;
                for (int level = 0; level <= 4; level++)
                {
                    float y = top + plotH - plotH * level / 4f;
                    g.DrawLine(grid, left, y, width - right, y);
                    g.DrawString(level.ToString(Inv), label, Brushes.DimGray, 42, y - 10);
                }
                int count = Data.DirectionNames.Length;
                float step = plotW / count, barW = Math.Min(30, step * .53f);
                for (int i = 0; i < count; i++)
                {
                    DirectionAssessment d = GetDirection(a, i);
                    float x = left + step * (i + .5f);
                    if (d.Included)
                    {
                        DirectionResult r = Calc.Direction(d);
                        float h = plotH * r.Level / 4f;
                        if (h < 3) h = 3;
                        g.FillRectangle(blue, x - barW / 2, top + plotH - h, barW, h);
                        float ty = top + plotH - plotH * d.Target / 4f;
                        g.DrawLine(targetPen, x - barW / 2 - 5, ty, x + barW / 2 + 5, ty);
                    }
                    else g.FillRectangle(grey, x - barW / 2, top + plotH - 7, barW, 7);
                    string n = (i + 1).ToString(Inv);
                    SizeF size = g.MeasureString(n, small);
                    g.DrawString(n, small, Brushes.DimGray, x - size.Width / 2, top + plotH + 10);
                }
                int yLegend = height - 60;
                g.FillRectangle(blue, left, yLegend, 22, 15);
                g.DrawString("Текущий уровень", label, Brushes.Black, left + 32, yLegend - 3);
                g.DrawLine(targetPen, 365, yLegend + 7, 395, yLegend + 7);
                g.DrawString("Целевой уровень", label, Brushes.Black, 405, yLegend - 3);
                g.FillRectangle(grey, 710, yLegend, 22, 15);
                g.DrawString("Исключено из оценки", label, Brushes.Black, 742, yLegend - 3);
                g.DrawString("Номера направлений соответствуют таблице ниже. Для незаполненных направлений результат предварительный.", small, Brushes.DimGray, left, height - 29);
                using (MemoryStream ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); return ms.ToArray(); }
            }
        }

        private static string WordDocument(Assessment a, Summary s, AssessmentAnalysis analysis)
        {
            StringBuilder b = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document xmlns:w=\"" + NsWord + "\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><w:body>");
            // Structure follows section VI of the methodology: item 35 letters а-н, signatures per item 36.
            if (!String.IsNullOrWhiteSpace(a.ApproverName) || !String.IsNullOrWhiteSpace(a.ApproverPosition))
                b.Append(ApprovalBlock(a.ApproverPosition, a.ApproverName));
            b.Append(P("ОТЧЁТ", "Title"));
            b.Append(P("об оценке уровня зрелости деятельности в области защиты информации (обеспечения безопасности значимых объектов КИИ)", "Subtitle"));
            AppendOptionalWord(b, "Номер документа", a.DocumentNumber);
            b.Append(P(s.Complete ? "Оценка заполнена" : "ПРЕДВАРИТЕЛЬНЫЙ ОТЧЁТ · есть незаполненные ответы", s.Complete ? "Status" : "Warning"));
            b.Append(P("Оператор: " + Empty(a.Organization), null));
            b.Append(P("Объект (область) оценки: " + Empty(a.SystemName), null));
            b.Append(P("Дата проведения оценки: " + DateText(a.Date), null));
            b.Append(P("Вид оценки: " + KindText(a), null));
            b.Append(P("Уровень зрелости Узи = " + (s.Count == 0 ? "не рассчитан" : s.Average.ToString("0.00", Inv) + " — " + Calc.LevelName((int)Math.Floor(s.Average))), "Heading2"));
            string[] missing = ReportFacts.Missing(a);
            if (missing.Length > 0)
            {
                b.Append(P("Отчёт неполон по разделу VI Методики:", "Warning"));
                foreach (string m in missing) b.Append(P("• " + m, "Small"));
            }
            b.Append(PageBreak());

            b.Append(P("1. Сведения об операторе (п. 35 а)", "Heading1"));
            b.Append(TableStart(new int[] { 3200, 6300 }));
            b.Append(TableRow(new string[] { "Сведение", "Значение" }, true));
            b.Append(TableRow(new string[] { "Наименование оператора", Empty(a.Organization) }, false));
            if (!String.IsNullOrWhiteSpace(a.OrganizationShort)) b.Append(TableRow(new string[] { "Краткое наименование", a.OrganizationShort }, false));
            if (!String.IsNullOrWhiteSpace(a.OrganizationAddress)) b.Append(TableRow(new string[] { "Адрес", a.OrganizationAddress }, false));
            if (!String.IsNullOrWhiteSpace(a.City)) b.Append(TableRow(new string[] { "Город", a.City }, false));
            b.Append(TableRow(new string[] { "Объект (область) оценки", Empty(a.SystemName) }, false));
            foreach (ProtectionObject o in a.Objects) b.Append(TableRow(new string[] { "Тип объекта защиты", ObjectCatalog.Text(o) }, false));
            b.Append(TableRow(new string[] { "Рекомендуемый минимальный целевой уровень (п. 11)", CategoryLongText(a.Category) }, false));
            b.Append("</w:tbl>");
            b.Append(P("Действующие требования по защите информации (обеспечению безопасности значимых объектов КИИ)", "Heading2"));
            AppendList(b, ReportCatalog.Texts(ReportCatalog.Requirements, a.Requirements), "Не указаны.");
            if (a.Passport != null) AppendPassportWord(b, a);

            b.Append(P("2. Дата проведения оценки (п. 35 б)", "Heading1"));
            b.Append(P(DateText(a.Date), null));

            b.Append(P("3. Лица, проводившие оценку (п. 35 в)", "Heading1"));
            List<ReportPerson> assessors = Assessors(a);
            AppendPeople(b, assessors, false);
            if (a.AssessmentKind == "external") b.Append(P("Внешняя оценка. Лицензия организации на деятельность в области защиты конфиденциальной информации: " + Empty(a.ExternalLicense) + ".", null));

            b.Append(P("4. Лица, участвовавшие в проведении оценки, в том числе предоставлявшие исходные данные (п. 35 г)", "Heading1"));
            AppendPeople(b, a.Participants.Where(p => p.Role != "assessor").ToList(), true);

            b.Append(P("5. Сведения о результатах предыдущей оценки (п. 35 д)", "Heading1"));
            AppendPreviousWord(b, a, s);

            b.Append(P("6. Исходные данные (п. 35 е)", "Heading1"));
            AppendList(b, ReportCatalog.Texts(ReportCatalog.SourceData, a.SourceData), "Не указаны.");
            if (!String.IsNullOrWhiteSpace(a.SourceDataNote)) b.Append(P(a.SourceDataNote, null));
            b.Append(P("Подтверждающие документы по каждому направлению приведены в разделе 10.", "Small"));

            b.Append(P("7. Методы и инструменты оценки (п. 35 ж)", "Heading1"));
            b.Append(P("Методы", "Heading2"));
            AppendList(b, ReportCatalog.Texts(ReportCatalog.Methods, a.Methods), "Не указаны.");
            b.Append(P("Инструменты", "Heading2"));
            AppendList(b, ReportCatalog.Texts(ReportCatalog.Instruments, a.Instruments), "Не указаны.");
            if (!String.IsNullOrWhiteSpace(a.ToolsNote)) b.Append(P(a.ToolsNote, null));

            b.Append(P("8. Перечень направлений деятельности (областей оценки) (п. 35 з)", "Heading1"));
            b.Append(P("Включено в область оценки: " + s.Count + " из " + Data.DirectionNames.Length + ". Неприменимые направления исключены по п. 13 Методики с указанием основания.", null));
            b.Append(TableStart(new int[] { 450, 4300, 1300, 3450 }));
            b.Append(TableRow(new string[] { "№", "Направление", "Область оценки", "Основание исключения" }, true));
            for (int i = 0; i < Data.DirectionNames.Length; i++)
            {
                DirectionAssessment d = GetDirection(a, i);
                b.Append(TableRow(new string[] { (i + 1).ToString(Inv), Data.DirectionNames[i], d.Included ? "Включено" : "Исключено", d.Included ? "" : Empty(d.Evidence) }, false));
            }
            b.Append("</w:tbl>");

            b.Append(P("9. Степень выполнения видов требований Dij и значения Pзн (п. 35 и)", "Heading1"));
            b.Append(P("Dij = wj × aj (п. 29); Pi = Σ Dij по восьми видам требований (п. 30). Столбцы D1–D8: документирование, выполнение, инструменты, квалификация, контроль, обучение, внешний аудит, актуализация.", "Small"));
            b.Append(TableStart(new int[] { 420, 2600, 700, 700, 700, 700, 700, 700, 700, 700, 780 }));
            b.Append(TableRow(new string[] { "№", "Направление", "D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8", "Pзн" }, true));
            for (int i = 0; i < Data.DirectionNames.Length; i++)
            {
                DirectionAssessment d = GetDirection(a, i); if (!d.Included) continue;
                DirectionResult r = Calc.Direction(d);
                string[] cells = new string[11]; cells[0] = (i + 1).ToString(Inv); cells[1] = Data.DirectionNames[i];
                for (int c = 0; c < 8; c++) { double? x = GetAnswer(d, c); cells[c + 2] = x.HasValue ? (x.Value * Data.Criteria[c].Weight).ToString("0.###", Inv) : "—"; }
                cells[10] = r.Score.ToString("0.000", Inv);
                b.Append(TableRow(cells, false));
            }
            b.Append("</w:tbl>");

            b.Append(P("10. Текущий уровень зрелости по направлениям (п. 35 к)", "Heading1"));
            for (int i = 0; i < Data.DirectionNames.Length; i++)
            {
                DirectionAssessment d = GetDirection(a, i); if (!d.Included) continue;
                DirectionResult r = Calc.Direction(d);
                b.Append(P((i + 1) + ". " + Data.DirectionNames[i], "Heading2"));
                b.Append(TableStart(new int[] { 400, 2300, 650, 650, 5500 }));
                b.Append(TableRow(new string[] { "j", "Вид требования", "a", "Dij", "Реализация требования (таблица 2)" }, true));
                for (int c = 0; c < Data.Criteria.Length; c++)
                {
                    double? answer = GetAnswer(d, c);
                    b.Append(TableRow(new string[] { (c + 1).ToString(Inv), Data.Criteria[c].Name, answer.HasValue ? answer.Value.ToString("0.##", Inv) : "—", answer.HasValue ? (answer.Value * Data.Criteria[c].Weight).ToString("0.###", Inv) : "—", AnswerText(c, answer) }, false));
                }
                b.Append("</w:tbl>");
                b.Append(P("Подтверждающие документы, инструментальные средства, отчётная документация: " + Empty(d.Evidence), null));
                b.Append(P("Заключение: Pзн = " + r.Score.ToString("0.000", Inv) + "; по таблице 3 Методики текущий уровень зрелости направления Узит = " + r.Level + " («" + Calc.LevelName(r.Level) + "»)" + (r.Complete ? "." : ". Результат предварительный: незаполненные виды требований учтены как 0."), r.Complete ? null : "Warning"));
            }
            b.Append(P("Расчёт уровня зрелости Узи (п. 10)", "Heading2"));
            b.Append(P(UziFormula(a, s), null));

            b.Append(P("11. Графическая интерпретация (п. 35 л)", "Heading1"));
            b.Append(P("Диаграмма профиля: текущий уровень Узитi и целевой уровень Узицi по каждому направлению (п. 17).", "Small"));
            b.Append(ChartParagraph());
            b.Append(TableStart(new int[] { 450, 4900, 1050, 1050, 1050, 1000 }));
            b.Append(TableRow(new string[] { "№", "Направление", "Узит", "Узиц", "Разрыв", "Pзн" }, true));
            for (int i = 0; i < Data.DirectionNames.Length; i++)
            {
                DirectionAssessment d = GetDirection(a, i); if (!d.Included) continue; DirectionResult r = Calc.Direction(d);
                b.Append(TableRow(new string[] { (i + 1).ToString(Inv), Data.DirectionNames[i], r.Level.ToString(Inv), d.Target.ToString(Inv), r.Gap.ToString(Inv), r.Score.ToString("0.000", Inv) }, false));
            }
            b.Append("</w:tbl>");

            b.Append(P("12. Направления, по которым не достигнуты целевые значения (п. 35 м)", "Heading1"));
            AppendUnmetWord(b, a);

            b.Append(P("13. Рекомендации по совершенствованию защиты информации (п. 35 н)", "Heading1"));
            AppendPlanWord(b, a, analysis);

            if (!String.IsNullOrWhiteSpace(a.Notes)) { b.Append(P("Примечания к оценке", "Heading2")); b.Append(P(a.Notes, null)); }

            b.Append(P("Подписи (п. 36)", "Heading1"));
            b.Append(P("Отчёт подписывается специалистами, проводившими оценку, и утверждается руководителем либо ответственным лицом за защиту информации. Оператор несёт ответственность за качество и объективность проведённой оценки.", "Small"));
            List<ReportPerson> signers = assessors.Where(p => !String.IsNullOrWhiteSpace(p.Name) || !String.IsNullOrWhiteSpace(p.Position)).ToList();
            if (signers.Count == 0) b.Append(SignatureBlock("Оценку проводил", "", ""));
            foreach (ReportPerson p in signers) b.Append(SignatureBlock("Оценку проводил", p.Position, p.Name));

            AppendAnalysisWord(b, analysis);
            b.Append(P("Приложение В. Основание расчёта и ограничения", "Heading1"));
            b.Append(P(MethodNote(), "Small"));
            b.Append(P("Методика оценки уровня зрелости, утверждённая ФСТЭК России " + Data.MethodDate + ". Алгоритм сверен с публичным калькулятором " + Data.SourceUrl + ".", "Small"));
            b.Append("<w:sectPr><w:footerReference w:type=\"default\" r:id=\"rFooter\"/><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"1000\" w:right=\"850\" w:bottom=\"1000\" w:left=\"1000\" w:header=\"400\" w:footer=\"400\"/><w:cols w:space=\"708\"/></w:sectPr></w:body></w:document>");
            return b.ToString();
        }

        private static string KindText(Assessment a)
        {
            return a.AssessmentKind == "external" ? "внешняя (с привлечением организации, имеющей лицензию; п. 6 Методики)" : "внутренняя (самостоятельная; п. 6 Методики)";
        }
        private static string CategoryLongText(int category)
        {
            switch (category)
            {
                case 1: return "ГИС 3 класса, иные ИС госорганов, ИСПДн УЗ-3/4, ЗОКИИ 3 категории — рекомендуемый целевой уровень не ниже 1 (п. 11)";
                case 2: return "ГИС 2 класса, ИСПДн УЗ-2, ЗОКИИ 2 категории — рекомендуемый целевой уровень не ниже 2 (п. 11)";
                case 3: return "ГИС 1 класса, ИСПДн УЗ-1, ЗОКИИ 1 категории — рекомендуемый целевой уровень не ниже 3 (п. 11)";
                default: return "Не определены — целевой уровень установлен оператором";
            }
        }
        private static void AppendList(StringBuilder b, string[] items, string empty)
        {
            if (items.Length == 0) { b.Append(P(empty, "Warning")); return; }
            for (int i = 0; i < items.Length; i++) b.Append(P((i + 1) + ") " + items[i], null));
        }
        private static List<ReportPerson> Assessors(Assessment a)
        {
            List<ReportPerson> list = new List<ReportPerson> { new ReportPerson { Role = "assessor", Name = a.Assessor, Position = a.AssessorPosition, Organization = a.AssessorOrganization, Contact = a.AssessorContact } };
            list.AddRange(a.Participants.Where(p => p.Role == "assessor"));
            return list;
        }
        private static void AppendPeople(StringBuilder b, List<ReportPerson> people, bool withRole)
        {
            people = people.Where(p => !String.IsNullOrWhiteSpace(p.Name) || !String.IsNullOrWhiteSpace(p.Position)).ToList();
            if (people.Count == 0) { b.Append(P("Не указаны.", "Warning")); return; }
            b.Append(withRole ? TableStart(new int[] { 2100, 1900, 1900, 1900, 1700 }) : TableStart(new int[] { 2500, 2400, 2400, 2200 }));
            b.Append(TableRow(withRole ? new string[] { "ФИО", "Должность", "Организация", "Контакты", "Роль" } : new string[] { "ФИО", "Должность", "Организация", "Контакты" }, true));
            foreach (ReportPerson p in people)
                b.Append(TableRow(withRole ? new string[] { Empty(p.Name), Empty(p.Position), Empty(p.Organization), Empty(p.Contact), ReportCatalog.RoleText(p.Role) } : new string[] { Empty(p.Name), Empty(p.Position), Empty(p.Organization), Empty(p.Contact) }, false));
            b.Append("</w:tbl>");
        }
        private static void AppendPreviousWord(StringBuilder b, Assessment a, Summary s)
        {
            if (a.Previous == null) { b.Append(P("Предыдущая оценка уровня зрелости не проводилась (сведения отсутствуют).", null)); return; }
            DynamicsResult dyn = ReportFacts.Dynamics(a);
            b.Append(P("Дата предыдущей оценки: " + DateText(a.Previous.Date) + ". Достигнутое значение Узи: " + a.Previous.Uzi.ToString("0.00", Inv) + " — " + Calc.LevelName((int)Math.Floor(a.Previous.Uzi)) + "." + (String.IsNullOrWhiteSpace(a.Previous.Source) ? "" : " Источник: " + a.Previous.Source + "."), null));
            b.Append(P("Текущее значение Узи: " + (s.Count == 0 ? "не рассчитано" : s.Average.ToString("0.00", Inv)) + ". Вывод по п. 33 Методики: " + dyn.Title.ToLower() + ". " + dyn.Explanation, "Heading3"));
            if (a.Previous.Directions != null)
            {
                b.Append(TableStart(new int[] { 450, 5150, 1300, 1300, 1300 }));
                b.Append(TableRow(new string[] { "№", "Направление", "Было", "Стало", "Изменение" }, true));
                foreach (PreviousDirection before in a.Previous.Directions)
                {
                    DirectionAssessment now = GetDirection(a, before.Id - 1); int after = Calc.Direction(now).Level;
                    string was = before.Included ? before.Level.ToString(Inv) : "исключено", became = now.Included ? after.ToString(Inv) : "исключено";
                    string change = !before.Included || !now.Included ? (before.Included == now.Included ? "—" : "изменена область") : after > before.Level ? "рост" : after < before.Level ? "снижение" : "без изменений";
                    b.Append(TableRow(new string[] { before.Id.ToString(Inv), Data.DirectionNames[before.Id - 1], was, became, change }, false));
                }
                b.Append("</w:tbl>");
            }
            if (dyn.CriterionDecreases.Count > 0)
            {
                b.Append(P("Снижение по видам требований", "Heading3"));
                foreach (string line in dyn.CriterionDecreases) b.Append(P("• " + line, null));
            }
        }
        private static string UziFormula(Assessment a, Summary s)
        {
            if (s.Count == 0) return "Область оценки пуста: Узи не рассчитывается.";
            List<string> levels = new List<string>(); int sum = 0;
            foreach (DirectionAssessment d in a.Directions.Where(x => x.Included).OrderBy(x => x.Id)) { int l = Calc.Direction(d).Level; sum += l; levels.Add(l.ToString(Inv)); }
            return "Узи = ΣУзитi / N = (" + String.Join(" + ", levels) + ") / " + s.Count + " = " + sum + " / " + s.Count + " = " + s.Average.ToString("0.00", Inv) + ". По таблице 1 Методики уровень зрелости — «" + Calc.LevelName((int)Math.Floor(s.Average)) + "»" + (s.Complete ? "." : ". Расчёт предварительный.");
        }
        private static void AppendUnmetWord(StringBuilder b, Assessment a)
        {
            bool any = false;
            foreach (DirectionAssessment d in a.Directions.Where(x => x.Included).OrderBy(x => x.Id))
            {
                DirectionResult r = Calc.Direction(d); if (r.Level >= d.Target) continue;
                any = true;
                b.Append(P(d.Id + ". " + Data.DirectionNames[d.Id - 1] + ": Узит = " + r.Level + ", Узиц = " + d.Target, "Heading3"));
                double[] need = Calc.Minima[d.Target - 1]; bool listed = false;
                for (int j = 0; j < 8; j++)
                {
                    double weighted = d.Answers[j].GetValueOrDefault() * Data.Criteria[j].Weight;
                    if (weighted + 1e-9 >= need[j]) continue;
                    listed = true;
                    b.Append(P("• " + Data.Criteria[j].Name + ": D = " + weighted.ToString("0.###", Inv) + (d.Answers[j].HasValue ? "" : " (не оценено)") + ", требуется не менее " + need[j].ToString("0.###", Inv) + " (таблица 3).", null));
                }
                if (!listed) b.Append(P("• Обязательные значения видов требований выполнены, но Pзн = " + r.Score.ToString("0.000", Inv) + " ниже порога " + Calc.Thresholds[d.Target - 1].ToString("0.000", Inv) + ".", null));
            }
            if (!any) b.Append(P("Целевые значения достигнуты по всем направлениям области оценки.", null));
        }
        private static void AppendPlanWord(StringBuilder b, Assessment a, AssessmentAnalysis analysis)
        {
            b.Append(P("Мероприятия сформированы по видам требований, не обеспечивающим целевой уровень (таблица 3 Методики).", "Small"));
            int unanswered = analysis.Deficits.Count(d => d.Type == "assess");
            if (unanswered > 0) b.Append(P("Не оценено видов требований: " + unanswered + ". До их оценки перечень мероприятий неполный.", "Warning"));
            DeficitRecord[] rows = ReportFacts.PlanRows(a, analysis);
            if (rows.Length == 0) { b.Append(P(!analysis.HasScope ? "Нет включённых направлений." : unanswered > 0 ? "По оценённым видам требований мероприятий не требуется." : "Мероприятия по достижению целевых уровней не требуются. При достижении целей определяются новые целевые уровни.", null)); return; }
            b.Append(TableStart(new int[] { 420, 2300, 3700, 1100, 1250, 830 }));
            b.Append(TableRow(new string[] { "№", "Направление и вид требования", "Мероприятие", "Сейчас / требуется", "Ответственный", "Срок" }, true));
            for (int i = 0; i < rows.Length; i++)
            {
                DeficitRecord d = rows[i]; PlanItem plan = ReportFacts.PlanFor(a, d.Key);
                b.Append(TableRow(new string[] { (i + 1).ToString(Inv), d.DirectionId + ". " + d.DirectionName + "\n" + d.CriterionName + "\n" + ActionLabel(d.Type), ConditionAndAction(d.ConditionText, d.Action), NullableNumber(d.Current) + " / " + NullableNumber(d.Required), plan.Responsible.Length > 0 ? plan.Responsible : "____________", plan.Deadline.Length > 0 ? plan.Deadline : "____________" }, false));
            }
            b.Append("</w:tbl>");
        }
        private static string ApprovalBlock(string position, string name)
        {
            string right = "<w:pPr><w:jc w:val=\"right\"/></w:pPr>";
            StringBuilder b = new StringBuilder();
            foreach (string line in new[] { "УТВЕРЖДАЮ", position, "____________ " + (name ?? ""), "«___» ____________ 20___ г." })
                if (!String.IsNullOrWhiteSpace(line)) b.Append("<w:p>" + right + "<w:r><w:t xml:space=\"preserve\">" + WordText(line) + "</w:t></w:r></w:p>");
            return b.ToString();
        }

        private static void AppendPassportWord(StringBuilder b, Assessment a)
        {
            SystemPassport p = SystemPassports.Snapshot(a.Passport);
            b.Append(P("Описание объекта", "Heading2"));
            b.Append(P("Сведения введены оценщиком и не определяют оценки зрелости, класс или категорию.", "Small"));
            b.Append(P("Состав", "Heading3"));
            if (p.Components.Length == 0) b.Append(P("Не указан.", "Warning"));
            else
            {
                b.Append(TableStart(new int[] { 3400, 1100, 5000 }));
                b.Append(TableRow(new string[] { "Компонент", "Количество", "Примечание" }, true));
                foreach (PassportComponent c in p.Components) b.Append(TableRow(new string[] { Empty(c.Kind), c.Count.ToString(Inv), c.Note }, false));
                b.Append("</w:tbl>");
            }
            b.Append(P("Внешние взаимодействия", "Heading3"));
            if (p.NoConnections) b.Append(P("Внешних взаимодействий нет.", null));
            else if (p.Connections.Length == 0) b.Append(P("Не указаны.", "Warning"));
            else
            {
                b.Append(TableStart(new int[] { 2500, 1700, 1800, 1700, 1800 }));
                b.Append(TableRow(new string[] { "Система / сеть", "Способ", "Канал", "Через что", "Примечание" }, true));
                foreach (PassportConnection c in p.Connections) b.Append(TableRow(new string[] { Empty(c.System), SystemPassports.Label(SystemPassports.Methods, c.Method), SystemPassports.Label(SystemPassports.Channels, c.Channel), c.Via, c.Note }, false));
                b.Append("</w:tbl>");
            }
            if (p.Segments.Length > 0)
            {
                b.Append(P("Сегменты сети", "Heading3"));
                b.Append(TableStart(new int[] { 2700, 3400, 3400 }));
                b.Append(TableRow(new string[] { "Сегмент", "Назначение", "Примечание" }, true));
                foreach (PassportSegment g in p.Segments) b.Append(TableRow(new string[] { Empty(g.Name), g.Purpose, g.Note }, false));
                b.Append("</w:tbl>");
            }
            b.Append(P("Средства защиты информации", "Heading3"));
            if (p.Tools.Length == 0) b.Append(P("Не указаны.", "Warning"));
            else
            {
                b.Append(TableStart(new int[] { 2900, 3300, 3300 }));
                b.Append(TableRow(new string[] { "Класс", "Средство", "Примечание (версия, сертификат)" }, true));
                foreach (PassportTool t in p.Tools) b.Append(TableRow(new string[] { SystemPassports.Label(SystemPassports.ToolClasses, t.Class), Empty(t.Product), t.Note }, false));
                b.Append("</w:tbl>");
            }
            b.Append(P("Применяемые технологии", "Heading3"));
            b.Append(TableStart(new int[] { 5000, 4500 }));
            b.Append(TableRow(new string[] { "Технология", "Применяется" }, true));
            foreach (KeyValuePair<string, string> fact in PassportFacts(p)) b.Append(TableRow(new string[] { fact.Key, fact.Value }, false));
            b.Append("</w:tbl>");
            string[] unknown = SystemPassports.UnknownFields(p);
            if (unknown.Length > 0) b.Append(P("Не уточнено: " + String.Join("; ", unknown) + ". Отсутствие сведений не означает отсутствия технологии.", "Small"));
            if (!String.IsNullOrWhiteSpace(p.Notes)) { b.Append(P("Особенности объекта", "Heading3")); b.Append(P(p.Notes, null)); }
        }

        private static KeyValuePair<string, string>[] PassportFacts(SystemPassport p)
        {
            List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("Доменная инфраструктура", SystemPassports.BooleanText(p.Domain)),
                new KeyValuePair<string, string>("Доступ в интернет", SystemPassports.BooleanText(p.Internet)),
                new KeyValuePair<string, string>("Беспроводной доступ (Wi-Fi)", SystemPassports.BooleanText(p.Wireless)),
                new KeyValuePair<string, string>("Мобильные устройства", SystemPassports.BooleanText(p.Mobile)),
                new KeyValuePair<string, string>("Удалённый доступ (администрирование, подрядчики, работники)", SystemPassports.BooleanText(p.RemoteAdministration)),
                new KeyValuePair<string, string>("Собственная разработка ПО", SystemPassports.BooleanText(p.OwnDevelopment)),
                new KeyValuePair<string, string>("Искусственный интеллект", SystemPassports.BooleanText(p.ArtificialIntelligence))
            };
            if (p.Wireless == true) facts.Insert(3, new KeyValuePair<string, string>("Wi-Fi отделён от сегментов объекта", SystemPassports.BooleanText(p.WirelessIsolated)));
            return facts.ToArray();
        }

        private static string PassportSheet(Assessment a)
        {
            SystemPassport p = SystemPassports.Snapshot(a.Passport);
            StringBuilder rows = new StringBuilder();
            List<string> merges = new List<string> { "A1:D1", "A2:D2" };
            rows.Append(Row(1, T("A1", "ОПИСАНИЕ ОБЪЕКТА", 1), 30));
            rows.Append(Row(2, T("A2", "Сведения введены оценщиком и не определяют оценки зрелости, класс или категорию. Неуточнённая характеристика не означает отсутствия технологии.", 5), 40));
            int row = 4;
            AppendPassportRow(rows, merges, ref row, "Организация", Empty(a.Organization));
            AppendPassportRow(rows, merges, ref row, "Объект оценки", Empty(a.SystemName));
            foreach (ProtectionObject o in a.Objects) AppendPassportRow(rows, merges, ref row, "Тип объекта защиты", ObjectCatalog.Text(o));
            Action<string, string[]> table = (title, head) => { row++; rows.Append(Row(row, T("A" + row, title, 4), 26)); merges.Add("A" + row + ":D" + row); row++; rows.Append(Row(row, HeaderCells(head, row), 30)); row++; };
            table("СОСТАВ", new[] { "Компонент", "Количество", "Примечание", "" });
            foreach (PassportComponent c in p.Components) { rows.Append(Row(row, T("A" + row, c.Kind, 0) + N("B" + row, c.Count, 3) + T("C" + row, c.Note, 0), 32)); merges.Add("C" + row + ":D" + row); row++; }
            table("ВНЕШНИЕ ВЗАИМОДЕЙСТВИЯ", new[] { "Система / сеть", "Способ", "Канал", "Через что / примечание" });
            if (p.NoConnections) { rows.Append(Row(row, T("A" + row, "Внешних взаимодействий нет", 0), 26)); row++; }
            foreach (PassportConnection c in p.Connections) { rows.Append(Row(row, T("A" + row, c.System, 0) + T("B" + row, SystemPassports.Label(SystemPassports.Methods, c.Method), 0) + T("C" + row, SystemPassports.Label(SystemPassports.Channels, c.Channel), 0) + T("D" + row, (c.Via.Length > 0 ? "Через: " + c.Via + (c.Note.Length > 0 ? "; " : "") : "") + c.Note, 0), 32)); row++; }
            if (p.Segments.Length > 0)
            {
                table("СЕГМЕНТЫ СЕТИ", new[] { "Сегмент", "Назначение", "Примечание", "" });
                foreach (PassportSegment g in p.Segments) { rows.Append(Row(row, T("A" + row, g.Name, 0) + T("B" + row, g.Purpose, 0) + T("C" + row, g.Note, 0), 32)); merges.Add("C" + row + ":D" + row); row++; }
            }
            table("СРЕДСТВА ЗАЩИТЫ ИНФОРМАЦИИ", new[] { "Класс", "Средство", "Примечание", "" });
            foreach (PassportTool t in p.Tools) { rows.Append(Row(row, T("A" + row, SystemPassports.Label(SystemPassports.ToolClasses, t.Class), 0) + T("B" + row, t.Product, 0) + T("C" + row, t.Note, 0), 32)); merges.Add("C" + row + ":D" + row); row++; }
            row++;
            foreach (KeyValuePair<string, string> fact in PassportFacts(p)) AppendPassportRow(rows, merges, ref row, fact.Key, fact.Value);
            string[] unknown = SystemPassports.UnknownFields(p);
            AppendPassportRow(rows, merges, ref row, "Не уточнено", unknown.Length == 0 ? "Все характеристики уточнены." : String.Join("\n", unknown), unknown.Length == 0 ? 32 : Math.Min(300, 28 + unknown.Length * 18));
            if (!String.IsNullOrWhiteSpace(p.Notes)) AppendPassportRow(rows, merges, ref row, "Особенности объекта", p.Notes, 90);
            StringBuilder mergeXml = new StringBuilder("<mergeCells count=\"" + merges.Count + "\">");
            foreach (string range in merges) mergeXml.Append("<mergeCell ref=\"" + range + "\"/>");
            mergeXml.Append("</mergeCells>");
            return Sheet(rows, "A1:D" + (row - 1), "<col min=\"1\" max=\"1\" width=\"38\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"24\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"30\" customWidth=\"1\"/><col min=\"4\" max=\"4\" width=\"30\" customWidth=\"1\"/>", 2, "", mergeXml.ToString(), false);
        }

        private static void AppendPassportRow(StringBuilder rows, List<string> merges, ref int row, string label, string value, int height = 42)
        {
            rows.Append(Row(row, T("A" + row, label, 4) + T("B" + row, value, 0), height));
            merges.Add("B" + row + ":D" + row);
            row++;
        }

        private static void AppendAnalysisWord(StringBuilder b, AssessmentAnalysis analysis)
        {
            b.Append(PageBreak());
            b.Append(P("Приложение А. Профиль видов требований", "Heading1"));
            b.Append(P("Средняя оценка рассчитана только по заполненным ответам включённых направлений. Пропуски не входят в среднее; выбранный 0 входит. Эти средние не являются показателем зрелости Узи. Покрытие показывает долю направлений с ответом по виду требования. Известные дефициты учитывают выбранные оценки ниже требуемого минимума; пропуски показаны отдельно.", "Small"));
            b.Append(TableStart(new int[] { 400, 3200, 1100, 1300, 1300, 2000 }));
            b.Append(TableRow(new string[] { "№", "Вид требования", "Средняя оценка", "Ответы / включено", "Покрытие, %", "Известные дефициты" }, true));
            foreach (CriterionProfile c in analysis.Criteria)
                b.Append(TableRow(new string[] { (c.CriterionIndex + 1).ToString(Inv), c.Name, NullableNumber(c.Average), c.Answered + " / " + c.Included, (c.Coverage * 100).ToString("0.##", Inv), c.KnownDeficits.ToString(Inv) }, false));
            b.Append("</w:tbl>");
            if (!analysis.HasScope) b.Append(P("Нет включённых направлений. Для расчёта профиля включите направления и заполните ответы.", "Warning"));

            b.Append(P("Приложение Б. Общие дефициты по направлениям", "Heading1"));
            b.Append(P("Одинаковые требования сгруппированы по виду требования, типу действия и требуемой оценке. Число направлений помогает выбрать общие мероприятия. Номера соответствуют сводной таблице; результат и подтверждающие материалы проверяются по каждому направлению.", "Small"));
            b.Append(P(Analysis.PriorityExplanation, "Small"));
            if (analysis.GroupedActions.Length == 0) b.Append(P("Общих мероприятий по введённым данным нет.", null));
            else
            {
                b.Append(TableStart(new int[] { 450, 2200, 1300, 800, 1500, 3050 }));
                b.Append(TableRow(new string[] { "Приоритет", "Вид требования", "Тип / требуется", "Направлений", "№ направлений", "Полное условие и мероприятие" }, true));
                foreach (GroupedAction g in analysis.GroupedActions)
                    b.Append(TableRow(new string[] { g.Priority.ToString(Inv), g.CriterionName, ActionLabel(g.Type) + "\n" + NullableNumber(g.Required), g.DirectionCount.ToString(Inv), DirectionNumbers(g.DirectionIds), ConditionAndAction(g.ConditionText, g.Action) + (g.BlockingTarget ? "" : "\nНе влияет на минимум цели.") }, false));
                b.Append("</w:tbl>");
            }
        }

        private static string CriterionProfileSheet(AssessmentAnalysis analysis)
        {
            StringBuilder rows = new StringBuilder();
            rows.Append(Row(1, T("A1", "ПРОФИЛЬ ВОСЬМИ КРИТЕРИЕВ", 1), 30));
            rows.Append(Row(2, T("A2", "Средние рассчитаны только по заполненным ответам включённых направлений и не являются показателем зрелости Узи. Пропуски не входят в среднее; выбранный 0 входит. Покрытие = ответы / включённые направления. Известные дефициты — выбранные оценки ниже требуемого минимума; пропуски показаны отдельно.", 5), 58));
            rows.Append(Row(4, HeaderCells(new string[] { "№", "Вид требования", "Вес", "Средняя оценка", "Средняя взвешенная", "Ответов", "Включено", "Пропусков", "Нулевых ответов", "Покрытие, %", "Известные дефициты" }, 4), 42));
            int row = 5;
            foreach (CriterionProfile c in analysis.Criteria)
            {
                rows.Append(Row(row, N("A" + row, c.CriterionIndex + 1, 3) + T("B" + row, c.Name, 0) + N("C" + row, c.Weight, 2) + OptionalNumber("D" + row, c.Average) + OptionalNumber("E" + row, c.WeightedAverage) + N("F" + row, c.Answered, 3) + N("G" + row, c.Included, 3) + N("H" + row, c.Missing, 3) + N("I" + row, c.ZeroCount, 3) + N("J" + row, c.Coverage * 100, 2) + N("K" + row, c.KnownDeficits, 3), 48));
                row++;
            }
            return Sheet(rows, "A1:K" + (row - 1), "<col min=\"1\" max=\"1\" width=\"8\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"42\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"10\" customWidth=\"1\"/><col min=\"4\" max=\"5\" width=\"17\" customWidth=\"1\"/><col min=\"6\" max=\"11\" width=\"14\" customWidth=\"1\"/>", 4, "A4:K" + (row - 1), TwoTitleMerges("K"), true);
        }

        private static string DeficitPlanSheet(AssessmentAnalysis analysis, Assessment a)
        {
            StringBuilder rows = new StringBuilder();
            rows.Append(Row(1, T("A1", "ПЛАН ДОСТИЖЕНИЯ ЦЕЛЕВЫХ УРОВНЕЙ", 1), 30));
            rows.Append(Row(2, T("A2", "«Оценить» — проверить фактическое состояние и выбрать подтверждённый ответ. «Улучшить» — выполнить условие и подтвердить результат. Пропуск при нулевом минимуме не требует выставлять 0. Строки «Сумма баллов» — контроль суммы, а не отдельные работы сверх видов требований: сумма обязательных минимумов текущей таблицы 3 равна порогу P. Ответственный и срок — из данных для отчёта (п. 35 н Методики).", 5), 65));
            rows.Append(Row(4, HeaderCells(new string[] { "№ направления", "Направление", "Вид требования", "Сейчас", "Требуется", "Тип действия", "Влияет на цель", "Полное условие", "Мероприятие", "Исполнитель", "Срок" }, 4), 42));
            int row = 5;
            foreach (DeficitRecord d in analysis.Deficits)
            {
                rows.Append(Row(row, N("A" + row, d.DirectionId, 3) + T("B" + row, d.DirectionName, 0) + T("C" + row, d.CriterionName, 0) + OptionalNumber("D" + row, d.Current) + OptionalNumber("E" + row, d.Required) + T("F" + row, ActionLabel(d.Type), d.Type == "assess" ? 7 : 0) + T("G" + row, d.BlockingTarget ? "Да" : "Нет", 0) + T("H" + row, d.ConditionText ?? "", 0) + T("I" + row, d.Action ?? "", 0) + T("J" + row, ReportFacts.PlanFor(a, d.Key).Responsible, 0) + T("K" + row, ReportFacts.PlanFor(a, d.Key).Deadline, 0), 120));
                row++;
            }
            if (row == 5)
            {
                rows.Append(Row(row, T("I" + row, analysis.HasScope ? "Недостатков и незаполненных ответов не выявлено." : "Нет включённых направлений.", 0), 45));
                row++;
            }
            return Sheet(rows, "A1:K" + (row - 1), "<col min=\"1\" max=\"1\" width=\"12\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"38\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"34\" customWidth=\"1\"/><col min=\"4\" max=\"5\" width=\"12\" customWidth=\"1\"/><col min=\"6\" max=\"7\" width=\"16\" customWidth=\"1\"/><col min=\"8\" max=\"9\" width=\"70\" customWidth=\"1\"/><col min=\"10\" max=\"10\" width=\"28\" customWidth=\"1\"/><col min=\"11\" max=\"11\" width=\"18\" customWidth=\"1\"/>", 4, "A4:K" + (row - 1), TwoTitleMerges("K"), true);
        }

        private static string GroupedActionsSheet(AssessmentAnalysis analysis)
        {
            StringBuilder rows = new StringBuilder();
            rows.Append(Row(1, T("A1", "ОБЩИЕ ДЕФИЦИТЫ ПО НАПРАВЛЕНИЯМ", 1), 30));
            rows.Append(Row(2, T("A2", "Одинаковые требования сгруппированы по виду требования, типу действия и требуемой оценке. Номера направлений соответствуют листу «Сводка». Результат подтверждается по каждому направлению. " + Analysis.PriorityExplanation, 5), 80));
            rows.Append(Row(4, HeaderCells(new string[] { "Приоритет", "Вид требования", "Тип действия", "Требуется", "Направлений", "№ направлений", "Влияет на цель", "Полное условие и мероприятие", "Исполнитель", "Срок" }, 4), 42));
            int row = 5;
            foreach (GroupedAction g in analysis.GroupedActions)
            {
                rows.Append(Row(row, N("A" + row, g.Priority, 3) + T("B" + row, g.CriterionName, 0) + T("C" + row, ActionLabel(g.Type), g.Type == "assess" ? 7 : 0) + OptionalNumber("D" + row, g.Required) + N("E" + row, g.DirectionCount, 3) + T("F" + row, DirectionNumbers(g.DirectionIds), 0) + T("G" + row, g.BlockingTarget ? "Да" : "Нет", 0) + T("H" + row, ConditionAndAction(g.ConditionText, g.Action), 0) + T("I" + row, "", 0) + T("J" + row, "", 0), 120));
                row++;
            }
            if (row == 5) { rows.Append(Row(row, T("H" + row, "Общих мероприятий по введённым данным нет.", 0), 45)); row++; }
            return Sheet(rows, "A1:J" + (row - 1), "<col min=\"1\" max=\"1\" width=\"13\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"36\" customWidth=\"1\"/><col min=\"3\" max=\"5\" width=\"15\" customWidth=\"1\"/><col min=\"6\" max=\"6\" width=\"35\" customWidth=\"1\"/><col min=\"7\" max=\"7\" width=\"16\" customWidth=\"1\"/><col min=\"8\" max=\"8\" width=\"100\" customWidth=\"1\"/><col min=\"9\" max=\"9\" width=\"28\" customWidth=\"1\"/><col min=\"10\" max=\"10\" width=\"18\" customWidth=\"1\"/>", 4, "A4:J" + (row - 1), TwoTitleMerges("J"), true);
        }

        private static string ActionLabel(string type)
        {
            if (type == "assess") return "Оценить";
            if (type == "improve") return "Улучшить";
            if (type == "score") return "Сумма баллов";
            return type ?? "";
        }
        private static string NullableNumber(double? value) { return value.HasValue ? value.Value.ToString("0.###", Inv) : "—"; }
        private static string OptionalNumber(string cell, double? value) { return value.HasValue ? N(cell, value.Value, 2) : T(cell, "—", 8); }
        private static string ConditionAndAction(string condition, string action)
        {
            if (String.IsNullOrWhiteSpace(condition)) return action ?? "";
            if (String.IsNullOrWhiteSpace(action) || String.Equals(condition.Trim(), action.Trim(), StringComparison.Ordinal)) return condition;
            return "Условие: " + condition + "\nМероприятие: " + action;
        }
        private static string DirectionNumbers(int[] ids)
        {
            if (ids == null || ids.Length == 0) return "";
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < ids.Length; i++) { if (i > 0) b.Append(", "); b.Append(ids[i].ToString(Inv)); }
            return b.ToString();
        }
        private static string TwoTitleMerges(string lastColumn)
        {
            return "<mergeCells count=\"2\"><mergeCell ref=\"A1:" + lastColumn + "1\"/><mergeCell ref=\"A2:" + lastColumn + "2\"/></mergeCells>";
        }

        private static string SummarySheet(Assessment a, Summary s)
        {
            StringBuilder rows = new StringBuilder();
            List<string> mergedRanges = new List<string> { "A1:I1", "A2:B2", "C2:I2", "A3:B3", "C3:I3", "A4:B4", "C4:D4", "F4:G4" };
            rows.Append(Row(1, T("A1", "ОЦЕНКА ЗРЕЛОСТИ ИНФОРМАЦИОННОЙ БЕЗОПАСНОСТИ", 1), 30));
            rows.Append(Row(2, T("A2", "Организация", 4) + T("C2", Empty(a.Organization), 0), 32));
            rows.Append(Row(3, T("A3", "Система / объект", 4) + T("C3", Empty(a.SystemName), 0), 32));
            rows.Append(Row(4, T("A4", "Оценку выполнил", 4) + T("C4", Empty(a.Assessor), 0) + T("E4", "Дата", 4) + T("F4", DateText(a.Date), 0) + T("H4", "Категория", 4) + (a.Category == 0 ? T("I4", "Не определена", 0) : N("I4", a.Category, 3)), 32));
            int nextRow = 5;
            AppendOptionalExcel(rows, mergedRanges, ref nextRow, "Краткое наименование", a.OrganizationShort);
            AppendOptionalExcel(rows, mergedRanges, ref nextRow, "Адрес организации", a.OrganizationAddress);
            AppendOptionalExcel(rows, mergedRanges, ref nextRow, "Город", a.City);
            AppendOptionalExcel(rows, mergedRanges, ref nextRow, "Должность составителя", a.AssessorPosition);
            AppendOptionalExcel(rows, mergedRanges, ref nextRow, "Номер документа", a.DocumentNumber);
            AppendOptionalExcel(rows, mergedRanges, ref nextRow, "Для утверждения: ФИО", a.ApproverName);
            AppendOptionalExcel(rows, mergedRanges, ref nextRow, "Для утверждения: должность", a.ApproverPosition);
            int statusRow = nextRow++, countRow = nextRow++, averageRow = nextRow++, headerRow = nextRow;
            rows.Append(Row(statusRow, T("A" + statusRow, s.Complete ? "Оценка заполнена" : "ПРЕДВАРИТЕЛЬНО: есть незаполненные ответы", s.Complete ? 6 : 7), 26));
            rows.Append(Row(countRow, T("A" + countRow, "Направлений: " + s.Count + ". Ответов: " + s.Answered + " / " + s.Total + ". Цель достигнута: " + s.Met + " / " + s.Count + ".", 4), 30));
            rows.Append(Row(averageRow, T("A" + averageRow, "Средний уровень: " + s.Average.ToString("0.00", Inv) + ". Средняя цель: " + s.TargetAverage.ToString("0.00", Inv) + ". Общий уровень: " + ((int)Math.Floor(s.Average)) + " — " + Calc.LevelName((int)Math.Floor(s.Average)) + ".", 5), 30));
            mergedRanges.Add("A" + statusRow + ":I" + statusRow);
            mergedRanges.Add("A" + countRow + ":I" + countRow);
            mergedRanges.Add("A" + averageRow + ":I" + averageRow);
            rows.Append(Row(headerRow, HeaderCells(new string[] { "№", "Направление", "Статус", "P", "Уровень", "Цель", "Разрыв", "Ответы", "Всего" }, headerRow), 30));
            for (int i = 0; i < Data.DirectionNames.Length; i++)
            {
                int row = headerRow + 1 + i; DirectionAssessment d = GetDirection(a, i); DirectionResult r = Calc.Direction(d); int st = d.Included ? 0 : 8;
                string cells = N("A" + row, i + 1, 3) + T("B" + row, Data.DirectionNames[i], st) + T("C" + row, d.Included ? (r.Complete ? "Заполнено" : "Частично") : "Исключено", d.Included ? (r.Complete ? 6 : 7) : 8);
                cells += d.Included ? N("D" + row, r.Score, 9) + N("E" + row, r.Level, 3) : T("D" + row, "—", 8) + T("E" + row, "—", 8);
                cells += N("F" + row, d.Target, 3) + (d.Included ? N("G" + row, r.Gap, r.Gap > 0 ? 10 : 3) + N("H" + row, r.Answered, 3) + N("I" + row, Data.Criteria.Length, 3) : T("G" + row, "—", 8) + T("H" + row, "—", 8) + T("I" + row, "—", 8));
                rows.Append(Row(row, cells, 42));
            }
            int end = headerRow + Data.DirectionNames.Length;
            rows.Append(Row(end + 2, T("A" + (end + 2), "Примечания: " + Empty(a.Notes), 0), 70));
            rows.Append(Row(end + 4, T("A" + (end + 4), MethodNote(), 5), 75));
            rows.Append(Row(end + 5, T("A" + (end + 5), "Источник: " + Data.SourceUrl + " · Версия методики: " + Data.MethodDate, 5), 30));
            mergedRanges.Add("A" + (end + 2) + ":I" + (end + 2));
            mergedRanges.Add("A" + (end + 4) + ":I" + (end + 4));
            mergedRanges.Add("A" + (end + 5) + ":I" + (end + 5));
            StringBuilder merges = new StringBuilder("<mergeCells count=\"" + mergedRanges.Count + "\">");
            foreach (string range in mergedRanges) merges.Append("<mergeCell ref=\"" + range + "\"/>");
            merges.Append("</mergeCells>");
            return Sheet(rows, "A1:I" + (end + 5), "<col min=\"1\" max=\"1\" width=\"8\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"53\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"18\" customWidth=\"1\"/><col min=\"4\" max=\"9\" width=\"13\" customWidth=\"1\"/>", headerRow, "A" + headerRow + ":I" + end, merges.ToString(), false);
        }

        private static string ReportInfoSheet(Assessment a, Summary s)
        {
            StringBuilder rows = new StringBuilder();
            List<string> merges = new List<string> { "A1:E1" };
            rows.Append(Row(1, T("A1", "СВЕДЕНИЯ ОБ ОЦЕНКЕ ПО П. 35 МЕТОДИКИ", 1), 30));
            int row = 3;
            Action<string, string> line = (label, value) => { rows.Append(Row(row, T("A" + row, label, 4) + T("B" + row, value, 0), Math.Min(300, 24 + 16 * (value ?? "").Split('\n').Length))); merges.Add("B" + row + ":E" + row); row++; };
            line("а) Оператор", Empty(a.Organization));
            line("а) Объект (область) оценки", Empty(a.SystemName));
            line("а) Класс / категория", CategoryLongText(a.Category));
            line("а) Действующие требования", Joined(ReportCatalog.Texts(ReportCatalog.Requirements, a.Requirements)));
            line("б) Дата оценки", DateText(a.Date));
            line("Вид оценки (п. 6)", KindText(a) + (a.AssessmentKind == "external" ? ". Лицензия: " + Empty(a.ExternalLicense) : ""));
            DynamicsResult dyn = ReportFacts.Dynamics(a);
            line("д) Предыдущая оценка", a.Previous == null ? "Не проводилась" : DateText(a.Previous.Date) + ": Узи " + a.Previous.Uzi.ToString("0.00", Inv) + ". " + dyn.Title + ". " + dyn.Explanation);
            line("е) Исходные данные", Joined(ReportCatalog.Texts(ReportCatalog.SourceData, a.SourceData)) + (String.IsNullOrWhiteSpace(a.SourceDataNote) ? "" : "\n" + a.SourceDataNote));
            line("ж) Методы", Joined(ReportCatalog.Texts(ReportCatalog.Methods, a.Methods)));
            line("ж) Инструменты", Joined(ReportCatalog.Texts(ReportCatalog.Instruments, a.Instruments)) + (String.IsNullOrWhiteSpace(a.ToolsNote) ? "" : "\n" + a.ToolsNote));
            line("к) Расчёт Узи", UziFormula(a, s));
            line("н) Ответственный / срок", Empty(a.PlanResponsible) + " / " + Empty(a.PlanDeadline));
            string[] missing = ReportFacts.Missing(a);
            if (missing.Length > 0) line("Не заполнено", String.Join("\n", missing));
            row++;
            rows.Append(Row(row, T("A" + row, "в), г) ЛИЦА, ПРОВОДИВШИЕ И УЧАСТВОВАВШИЕ В ОЦЕНКЕ", 4), 26)); merges.Add("A" + row + ":E" + row); row++;
            rows.Append(Row(row, HeaderCells(new string[] { "Роль", "ФИО", "Должность", "Организация", "Контакты" }, row), 30)); row++;
            foreach (ReportPerson p in Assessors(a).Concat(a.Participants.Where(x => x.Role != "assessor")))
            {
                if (String.IsNullOrWhiteSpace(p.Name) && String.IsNullOrWhiteSpace(p.Position)) continue;
                rows.Append(Row(row, T("A" + row, ReportCatalog.RoleText(p.Role), 0) + T("B" + row, p.Name, 0) + T("C" + row, p.Position, 0) + T("D" + row, p.Organization, 0) + T("E" + row, p.Contact, 0), 36)); row++;
            }
            StringBuilder mergeXml = new StringBuilder("<mergeCells count=\"" + merges.Count + "\">");
            foreach (string range in merges) mergeXml.Append("<mergeCell ref=\"" + range + "\"/>");
            mergeXml.Append("</mergeCells>");
            return Sheet(rows, "A1:E" + (row - 1), "<col min=\"1\" max=\"1\" width=\"30\" customWidth=\"1\"/><col min=\"2\" max=\"5\" width=\"34\" customWidth=\"1\"/>", 1, "", mergeXml.ToString(), false);
        }
        private static string Joined(string[] items) { return items.Length == 0 ? "Не указаны" : String.Join("\n", items.Select((x, i) => (i + 1) + ") " + x)); }

        private static string DijSheet(Assessment a)
        {
            StringBuilder rows = new StringBuilder();
            rows.Append(Row(1, T("A1", "СТЕПЕНЬ ВЫПОЛНЕНИЯ ВИДОВ ТРЕБОВАНИЙ Dij И Pзн (п. 35 и)", 1), 30));
            rows.Append(Row(2, T("A2", "Dij = wj × aj (п. 29), Pзн = Σ Dij (п. 30), Узит — по таблице 3. Включены только направления области оценки.", 5), 30));
            string[] head = new string[13]; head[0] = "№"; head[1] = "Направление";
            for (int j = 0; j < 8; j++) head[j + 2] = "D" + (j + 1) + " " + Data.Criteria[j].Name;
            head[10] = "Pзн"; head[11] = "Узит"; head[12] = "Узиц";
            rows.Append(Row(4, HeaderCells(head, 4), 45));
            int row = 5;
            for (int i = 0; i < Data.DirectionNames.Length; i++)
            {
                DirectionAssessment d = GetDirection(a, i); if (!d.Included) continue;
                DirectionResult r = Calc.Direction(d);
                StringBuilder cells = new StringBuilder(N("A" + row, i + 1, 3) + T("B" + row, Data.DirectionNames[i], 0));
                for (int j = 0; j < 8; j++)
                {
                    string cell = ((char)('C' + j)).ToString() + row; double? x = GetAnswer(d, j);
                    cells.Append(x.HasValue ? N(cell, x.Value * Data.Criteria[j].Weight, 9) : T(cell, "—", 8));
                }
                cells.Append(N("K" + row, r.Score, 9) + N("L" + row, r.Level, 3) + N("M" + row, d.Target, 3));
                rows.Append(Row(row, cells.ToString(), 36)); row++;
            }
            return Sheet(rows, "A1:M" + Math.Max(4, row - 1), "<col min=\"1\" max=\"1\" width=\"6\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"44\" customWidth=\"1\"/><col min=\"3\" max=\"10\" width=\"13\" customWidth=\"1\"/><col min=\"11\" max=\"13\" width=\"9\" customWidth=\"1\"/>", 4, "A4:M" + Math.Max(4, row - 1), TwoTitleMerges("M"), true);
        }

        private static string AnswersSheet(Assessment a)
        {
            StringBuilder rows = new StringBuilder();
            rows.Append(Row(1, HeaderCells(new string[] { "№ направления", "Направление", "Участие", "№ вида требования", "Вид требования", "Вес", "Баллы", "Выбранный ответ", "Доказательства и комментарии" }, 1), 36));
            int row = 2;
            for (int i = 0; i < Data.DirectionNames.Length; i++)
            {
                DirectionAssessment d = GetDirection(a, i);
                for (int c = 0; c < Data.Criteria.Length; c++, row++)
                {
                    double? answer = GetAnswer(d, c);
                    string cells = N("A" + row, i + 1, 3) + T("B" + row, Data.DirectionNames[i], 0) + T("C" + row, d.Included ? "Включено" : "Исключено", d.Included ? 0 : 8) + N("D" + row, c + 1, 3) + T("E" + row, Data.Criteria[c].Name, 0) + N("F" + row, Data.Criteria[c].Weight, 2) + (answer.HasValue ? N("G" + row, answer.Value, 2) : T("G" + row, "Не заполнено", 7)) + T("H" + row, AnswerText(c, answer), 0) + T("I" + row, d.Evidence ?? "", 0);
                    rows.Append(Row(row, cells, 70));
                }
            }
            return Sheet(rows, "A1:I" + (row - 1), "<col min=\"1\" max=\"1\" width=\"12\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"40\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"15\" customWidth=\"1\"/><col min=\"4\" max=\"4\" width=\"12\" customWidth=\"1\"/><col min=\"5\" max=\"5\" width=\"36\" customWidth=\"1\"/><col min=\"6\" max=\"7\" width=\"11\" customWidth=\"1\"/><col min=\"8\" max=\"8\" width=\"68\" customWidth=\"1\"/><col min=\"9\" max=\"9\" width=\"62\" customWidth=\"1\"/>", 1, "A1:I" + (row - 1), "", true);
        }

        private static string RecommendationsSheet(Assessment a)
        {
            StringBuilder rows = new StringBuilder();
            rows.Append(Row(1, HeaderCells(new string[] { "№", "Направление", "Уровень", "Цель", "Разрыв", "Рекомендация" }, 1), 30));
            int row = 2;
            for (int i = 0; i < Data.DirectionNames.Length; i++)
            {
                DirectionAssessment d = GetDirection(a, i); if (!d.Included) continue;
                DirectionResult r = Calc.Direction(d);
                List<string> recommendations = r.Recommendations;
                if (recommendations == null || recommendations.Count == 0) continue;
                foreach (string recommendation in recommendations)
                {
                    rows.Append(Row(row, N("A" + row, i + 1, 3) + T("B" + row, Data.DirectionNames[i], 0) + N("C" + row, r.Level, 3) + N("D" + row, d.Target, 3) + N("E" + row, r.Gap, r.Gap > 0 ? 10 : 3) + T("F" + row, recommendation, 0), 75));
                    row++;
                }
            }
            if (row == 2) { rows.Append(Row(2, T("F2", "По введённым данным дополнительных рекомендаций нет.", 0), 40)); row++; }
            return Sheet(rows, "A1:F" + (row - 1), "<col min=\"1\" max=\"1\" width=\"8\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"44\" customWidth=\"1\"/><col min=\"3\" max=\"5\" width=\"12\" customWidth=\"1\"/><col min=\"6\" max=\"6\" width=\"110\" customWidth=\"1\"/>", 1, "A1:F" + (row - 1), "", false);
        }

        private static string SpreadsheetContentTypes(int count)
        {
            StringBuilder b = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            for (int i = 1; i <= count; i++) b.Append("<Override PartName=\"/xl/worksheets/sheet" + i + ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            b.Append("<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/><Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/></Types>");
            return b.ToString();
        }
        private static string WorkbookXml(string[] names)
        {
            StringBuilder b = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"" + NsSheet + "\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><bookViews><workbookView/></bookViews><sheets>");
            for (int i = 0; i < names.Length; i++) b.Append("<sheet name=\"" + E(names[i]) + "\" sheetId=\"" + (i + 1) + "\" r:id=\"r" + (i + 1) + "\"/>");
            b.Append("</sheets><calcPr calcId=\"191029\" fullCalcOnLoad=\"1\"/></workbook>");
            return b.ToString();
        }
        private static string WorkbookRelationships(int count)
        {
            StringBuilder b = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int i = 1; i <= count; i++) b.Append("<Relationship Id=\"r" + i + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet" + i + ".xml\"/>");
            b.Append("<Relationship Id=\"rStyles\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
            return b.ToString();
        }

        private static string Sheet(StringBuilder rows, string dimension, string cols, int freezeRows, string filter, string merges, bool landscape)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"" + NsSheet + "\"><sheetPr><pageSetUpPr fitToPage=\"1\"/></sheetPr><dimension ref=\"" + dimension + "\"/><sheetViews><sheetView workbookViewId=\"0\" showGridLines=\"0\"><pane ySplit=\"" + freezeRows + "\" topLeftCell=\"A" + (freezeRows + 1) + "\" activePane=\"bottomLeft\" state=\"frozen\"/><selection pane=\"bottomLeft\" activeCell=\"A" + (freezeRows + 1) + "\" sqref=\"A" + (freezeRows + 1) + "\"/></sheetView></sheetViews><sheetFormatPr defaultRowHeight=\"18\"/><cols>" + cols + "</cols><sheetData>" + rows + "</sheetData>" + (String.IsNullOrEmpty(filter) ? "" : "<autoFilter ref=\"" + filter + "\"/>") + merges + "<printOptions horizontalCentered=\"1\"/><pageMargins left=\"0.3\" right=\"0.3\" top=\"0.5\" bottom=\"0.5\" header=\"0.2\" footer=\"0.2\"/><pageSetup paperSize=\"9\" orientation=\"" + (landscape ? "landscape" : "portrait") + "\" fitToWidth=\"1\" fitToHeight=\"0\"/><headerFooter><oddFooter>&amp;LОценка зрелости ИБ&amp;RСтраница &amp;P из &amp;N</oddFooter></headerFooter></worksheet>";
        }

        private static string WordStyles()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:styles xmlns:w=\"" + NsWord + "\"><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Calibri\" w:hAnsi=\"Calibri\" w:cs=\"Calibri\"/><w:sz w:val=\"21\"/><w:lang w:val=\"ru-RU\"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after=\"100\" w:line=\"260\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>"
                + WordStyle("Normal", "Обычный", "", "", true)
                + WordStyle("Title", "Название", "<w:spacing w:before=\"200\" w:after=\"80\"/><w:keepNext/>", "<w:b/><w:color w:val=\"173F56\"/><w:sz w:val=\"42\"/>", false)
                + WordStyle("Subtitle", "Подзаголовок", "<w:spacing w:after=\"230\"/><w:keepNext/>", "<w:color w:val=\"526879\"/><w:sz w:val=\"29\"/>", false)
                + WordStyle("Heading1", "Заголовок 1", "<w:keepNext/><w:spacing w:before=\"260\" w:after=\"130\"/><w:outlineLvl w:val=\"0\"/>", "<w:b/><w:color w:val=\"173F56\"/><w:sz w:val=\"29\"/>", false)
                + WordStyle("Heading2", "Заголовок 2", "<w:keepNext/><w:spacing w:before=\"230\" w:after=\"100\"/><w:outlineLvl w:val=\"1\"/>", "<w:b/><w:color w:val=\"1A5F82\"/><w:sz w:val=\"24\"/>", false)
                + WordStyle("Heading3", "Заголовок 3", "<w:keepNext/><w:spacing w:before=\"130\" w:after=\"70\"/><w:outlineLvl w:val=\"2\"/>", "<w:b/><w:sz w:val=\"21\"/>", false)
                + WordStyle("Small", "Примечание", "<w:spacing w:after=\"100\"/>", "<w:color w:val=\"596875\"/><w:sz w:val=\"18\"/>", false)
                + WordStyle("Status", "Статус", "<w:spacing w:before=\"100\" w:after=\"150\"/>", "<w:b/><w:color w:val=\"2C7055\"/>", false)
                + WordStyle("Warning", "Предварительно", "<w:spacing w:before=\"100\" w:after=\"150\"/>", "<w:b/><w:color w:val=\"98601C\"/>", false)
                + WordStyle("Footer", "Нижний колонтитул", "", "<w:color w:val=\"6A7882\"/><w:sz w:val=\"17\"/>", false) + "</w:styles>";
        }

        private static string WordStyle(string id, string name, string p, string r, bool defaultStyle)
        {
            return "<w:style w:type=\"paragraph\" w:styleId=\"" + id + "\"" + (defaultStyle ? " w:default=\"1\"" : "") + "><w:name w:val=\"" + name + "\"/>" + (defaultStyle ? "" : "<w:basedOn w:val=\"Normal\"/>") + "<w:pPr>" + p + "</w:pPr><w:rPr>" + r + "</w:rPr></w:style>";
        }

        private static string ExcelStyles()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?><styleSheet xmlns=\"" + NsSheet + "\"><numFmts count=\"2\"><numFmt numFmtId=\"164\" formatCode=\"0.00\"/><numFmt numFmtId=\"165\" formatCode=\"0.000\"/></numFmts><fonts count=\"6\"><font><sz val=\"11\"/><name val=\"Calibri\"/><color rgb=\"FF243746\"/></font><font><b/><sz val=\"16\"/><name val=\"Calibri\"/><color rgb=\"FF173F56\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/><color rgb=\"FFFFFFFF\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/><color rgb=\"FF173F56\"/></font><font><sz val=\"10\"/><name val=\"Calibri\"/><color rgb=\"FF667783\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/><color rgb=\"FF98601C\"/></font></fonts><fills count=\"6\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF173F56\"/><bgColor indexed=\"64\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE8F3ED\"/><bgColor indexed=\"64\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFF2D9\"/><bgColor indexed=\"64\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF0F2F4\"/><bgColor indexed=\"64\"/></patternFill></fill></fills><borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style=\"thin\"><color rgb=\"FFD9E2E8\"/></left><right style=\"thin\"><color rgb=\"FFD9E2E8\"/></right><top style=\"thin\"><color rgb=\"FFD9E2E8\"/></top><bottom style=\"thin\"><color rgb=\"FFD9E2E8\"/></bottom><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"11\">"
                + Xf(0, 0, 0, 1, false, "left") + Xf(0, 1, 0, 0, false, "left") + Xf(164, 0, 0, 1, true, "center") + Xf(0, 0, 0, 1, false, "center") + Xf(0, 3, 0, 0, false, "left") + Xf(0, 4, 0, 0, false, "left") + Xf(0, 3, 3, 1, false, "left") + Xf(0, 5, 4, 1, false, "left") + Xf(0, 4, 5, 1, false, "left") + Xf(165, 0, 0, 1, true, "center") + Xf(0, 5, 4, 1, false, "center")
                + "</cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles><dxfs count=\"0\"/><tableStyles count=\"0\" defaultTableStyle=\"TableStyleMedium2\" defaultPivotStyle=\"PivotStyleLight16\"/></styleSheet>";
        }

        private static string Xf(int num, int font, int fill, int border, bool number, string align)
        {
            return "<xf numFmtId=\"" + num + "\" fontId=\"" + font + "\" fillId=\"" + fill + "\" borderId=\"" + border + "\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"" + (number ? " applyNumberFormat=\"1\"" : "") + "><alignment horizontal=\"" + align + "\" vertical=\"top\" wrapText=\"1\"/></xf>";
        }

        private static string HeaderCells(string[] names, int row)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < names.Length; i++) b.Append("<c r=\"" + ((char)('A' + i)).ToString() + row + "\" s=\"4\" t=\"inlineStr\"><is><t xml:space=\"preserve\">" + E(names[i]) + "</t></is></c>");
            return b.ToString();
        }

        private static string Row(int row, string cells, int height) { return "<row r=\"" + row + "\" ht=\"" + height + "\" customHeight=\"1\">" + cells + "</row>"; }
        private static string T(string address, string value, int style) { return "<c r=\"" + address + "\" s=\"" + style + "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">" + E(TrimCell(value)) + "</t></is></c>"; }
        private static string N(string address, double value, int style) { return "<c r=\"" + address + "\" s=\"" + style + "\"><v>" + (double.IsNaN(value) || double.IsInfinity(value) ? "0" : value.ToString("0.###############", Inv)) + "</v></c>"; }
        private static string TrimCell(string text) { return text != null && text.Length > 32767 ? text.Substring(0, 32764) + "..." : text; }

        private static string P(string text, string style)
        {
            string t = WordText(text);
            return "<w:p>" + (style == null ? "" : "<w:pPr><w:pStyle w:val=\"" + style + "\"/></w:pPr>") + "<w:r><w:t xml:space=\"preserve\">" + t + "</w:t></w:r></w:p>";
        }
        private static string WordText(string text) { return E(text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "</w:t><w:br/><w:t xml:space=\"preserve\">"); }
        private static void AppendOptionalWord(StringBuilder b, string label, string value)
        {
            if (!String.IsNullOrWhiteSpace(value)) b.Append(P(label + ": " + value, null));
        }
        private static void AppendOptionalExcel(StringBuilder rows, List<string> merges, ref int row, string label, string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return;
            rows.Append(Row(row, T("A" + row, label, 4) + T("C" + row, value, 0), 32));
            merges.Add("A" + row + ":B" + row);
            merges.Add("C" + row + ":I" + row);
            row++;
        }
        private static string Person(string position, string name)
        {
            if (String.IsNullOrWhiteSpace(position)) return name ?? "";
            if (String.IsNullOrWhiteSpace(name)) return position;
            return position + ", " + name;
        }
        private static string SignatureBlock(string label, string position, string name)
        {
            StringBuilder b = new StringBuilder(P(label, "Heading2"));
            if (!String.IsNullOrWhiteSpace(position)) b.Append(P(position, null).Replace("<w:p>", "<w:p><w:pPr><w:keepNext/></w:pPr>"));
            if (!String.IsNullOrWhiteSpace(name)) b.Append(P(name, null).Replace("<w:p>", "<w:p><w:pPr><w:keepNext/></w:pPr>"));
            b.Append(P("Подпись ____________________    Дата ____________________", null));
            return b.ToString();
        }
        private static string PageBreak() { return "<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>"; }
        private static string TableStart(int[] widths)
        {
            StringBuilder b = new StringBuilder("<w:tbl><w:tblPr><w:tblW w:w=\"0\" w:type=\"auto\"/><w:tblBorders><w:top w:val=\"single\" w:sz=\"4\" w:color=\"D9E2E8\"/><w:left w:val=\"single\" w:sz=\"4\" w:color=\"D9E2E8\"/><w:bottom w:val=\"single\" w:sz=\"4\" w:color=\"D9E2E8\"/><w:right w:val=\"single\" w:sz=\"4\" w:color=\"D9E2E8\"/><w:insideH w:val=\"single\" w:sz=\"4\" w:color=\"D9E2E8\"/><w:insideV w:val=\"single\" w:sz=\"4\" w:color=\"D9E2E8\"/></w:tblBorders><w:tblCellMar><w:top w:w=\"65\" w:type=\"dxa\"/><w:left w:w=\"65\" w:type=\"dxa\"/><w:bottom w:w=\"65\" w:type=\"dxa\"/><w:right w:w=\"65\" w:type=\"dxa\"/></w:tblCellMar><w:tblLayout w:type=\"fixed\"/></w:tblPr><w:tblGrid>");
            foreach (int w in widths) b.Append("<w:gridCol w:w=\"" + w + "\"/>");
            b.Append("</w:tblGrid>"); return b.ToString();
        }
        private static string TableRow(string[] cells, bool header)
        {
            StringBuilder b = new StringBuilder("<w:tr><w:trPr><w:cantSplit/>" + (header ? "<w:tblHeader/>" : "") + "</w:trPr>");
            foreach (string cell in cells)
            {
                b.Append("<w:tc><w:tcPr><w:vAlign w:val=\"top\"/>" + (header ? "<w:shd w:val=\"clear\" w:fill=\"173F56\"/>" : "") + "</w:tcPr><w:p><w:pPr><w:spacing w:after=\"20\" w:line=\"220\" w:lineRule=\"auto\"/></w:pPr><w:r><w:rPr><w:sz w:val=\"18\"/>" + (header ? "<w:b/><w:color w:val=\"FFFFFF\"/>" : "") + "</w:rPr><w:t xml:space=\"preserve\">" + WordText(cell) + "</w:t></w:r></w:p></w:tc>");
            }
            b.Append("</w:tr>"); return b.ToString();
        }
        private static string ChartParagraph()
        {
            return "<w:p><w:pPr><w:spacing w:before=\"120\" w:after=\"180\"/></w:pPr><w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"5920000\" cy=\"2410286\"/><wp:docPr id=\"1\" name=\"Профиль зрелости\" descr=\"Текущий и целевой уровни по 21 направлению\"/><wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=\"1\"/></wp:cNvGraphicFramePr><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic><pic:nvPicPr><pic:cNvPr id=\"1\" name=\"profile.png\"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"rChart\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"5920000\" cy=\"2410286\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>";
        }

        private static DirectionAssessment GetDirection(Assessment a, int i)
        {
            if (a.Directions != null)
                foreach (DirectionAssessment d in a.Directions) if (d != null && d.Id == i + 1) return d;
            return new DirectionAssessment { Id = i + 1, Included = false, Target = 1, Answers = new double?[Data.Criteria.Length], Evidence = "" };
        }
        private static double? GetAnswer(DirectionAssessment d, int c) { return d.Answers != null && c < d.Answers.Length ? d.Answers[c] : null; }
        private static string AnswerText(int criterion, double? answer)
        {
            if (!answer.HasValue) return "Не заполнено";
            foreach (AnswerOption option in Data.Criteria[criterion].Options) if (Math.Abs(option.Value - answer.Value) < .000001) return option.Text;
            return "Значение: " + answer.Value.ToString("0.###", Inv);
        }
        private static string Empty(string value) { return String.IsNullOrWhiteSpace(value) ? "Не указано" : value; }
        private static string DateText(string value)
        {
            DateTime date;
            return DateTime.TryParseExact(value, "yyyy-MM-dd", Inv, DateTimeStyles.None, out date) ? date.ToString("dd.MM.yyyy", Inv) : Empty(value);
        }
        private static string CategoryText(int category) { return category == 0 ? "Не определена" : category.ToString(Inv); }
        private static string MethodNote() { return "Алгоритм воспроизведён по публичному калькулятору. Интервал 0,200 ≤ P < 0,225 отнесён к уровню 0. Результаты основаны на введённых данных и не подтверждают соответствие требованиям сами по себе. При незаполненных ответах отчёт предварительный: пропуски учитываются как 0. Исключённые направления не участвуют в сводных расчётах. Общий уровень равен целой части среднего уровня."; }
        private static string E(string value)
        {
            if (value == null) return "";
            StringBuilder b = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\t' || c == '\n' || c == '\r' || (c >= 0x20 && c <= 0xD7FF) || (c >= 0xE000 && c <= 0xFFFD)) b.Append(c);
                else if (Char.IsHighSurrogate(c) && i + 1 < value.Length && Char.IsLowSurrogate(value[i + 1])) { b.Append(c); b.Append(value[++i]); }
            }
            return SecurityElement.Escape(b.ToString());
        }
        private static string PackageRels(string target, string type) { return "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rDoc\" Type=\"" + type + "\" Target=\"" + target + "\"/><Relationship Id=\"rCore\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/><Relationship Id=\"rApp\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/></Relationships>"; }
        private static string CoreProperties(string title, string author) { return "<?xml version=\"1.0\" encoding=\"UTF-8\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><dc:title>" + E(title) + "</dc:title><dc:creator>" + E(author ?? "") + "</dc:creator><cp:lastModifiedBy>MaturityApp</cp:lastModifiedBy><dcterms:created xsi:type=\"dcterms:W3CDTF\">" + DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv) + "</dcterms:created></cp:coreProperties>"; }
        private static string AppProperties() { return "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\"><Application>MaturityApp</Application><AppVersion>1.0</AppVersion></Properties>"; }
        private static void Add(ZipArchive zip, string name, string content) { Add(zip, name, new UTF8Encoding(false).GetBytes(content)); }
        private static void Add(ZipArchive zip, string name, byte[] content)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (Stream s = entry.Open()) s.Write(content, 0, content.Length);
        }
        private static void AtomicWrite(string path, Action<Stream> write)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("Укажите путь к файлу.", "path");
            string destination = Path.GetFullPath(path), directory = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(directory);
            string temp = Path.Combine(directory, "." + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { write(stream); stream.Flush(true); }
                if (File.Exists(destination)) File.Replace(temp, destination, null); else File.Move(temp, destination);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
