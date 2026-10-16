using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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
            string[] names = { "Сводка", "Ответы", "Рекомендации", "Профиль критериев", "План улучшений", "Общие дефициты" };
            string[] sheets = { SummarySheet(a, summary), AnswersSheet(a), RecommendationsSheet(a), CriterionProfileSheet(analysis), DeficitPlanSheet(analysis), GroupedActionsSheet(analysis) };
            if (a.Passport != null)
            {
                Array.Resize(ref names, names.Length + 1);
                Array.Resize(ref sheets, sheets.Length + 1);
                names[names.Length - 1] = "Паспорт объекта";
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
            b.Append(P("ОЦЕНКА ЗРЕЛОСТИ", "Title"));
            b.Append(P("информационной безопасности", "Subtitle"));
            AppendOptionalWord(b, "Номер документа", a.DocumentNumber);
            b.Append(P(s.Complete ? "Оценка заполнена" : "ПРЕДВАРИТЕЛЬНЫЙ ОТЧЁТ · есть незаполненные ответы", s.Complete ? "Status" : "Warning"));
            b.Append(P("Организация: " + Empty(a.Organization), null));
            AppendOptionalWord(b, "Краткое наименование", a.OrganizationShort);
            AppendOptionalWord(b, "Адрес организации", a.OrganizationAddress);
            AppendOptionalWord(b, "Город", a.City);
            b.Append(P("Система / объект оценки: " + Empty(a.SystemName), null));
            b.Append(P("Оценку выполнил: " + Empty(a.Assessor), null));
            AppendOptionalWord(b, "Должность составителя", a.AssessorPosition);
            AppendOptionalWord(b, "Для утверждения", Person(a.ApproverPosition, a.ApproverName));
            b.Append(P("Дата оценки: " + DateText(a.Date) + "    Категория: " + CategoryText(a.Category), null));
            if (a.Passport != null)
            {
                b.Append(PageBreak());
                AppendPassportWord(b, a);
                b.Append(PageBreak());
            }
            b.Append(P("1. Сводные результаты", "Heading1"));
            b.Append(P("Включено направлений: " + s.Count + ". Заполнено ответов: " + s.Answered + " из " + s.Total + ". Достигнута цель: " + s.Met + " из " + s.Count + ".", null));
            b.Append(P("Средний текущий уровень: " + s.Average.ToString("0.00", Inv) + "; средний целевой: " + s.TargetAverage.ToString("0.00", Inv) + ". Общий уровень: " + ((int)Math.Floor(s.Average)) + " — " + Calc.LevelName((int)Math.Floor(s.Average)) + ".", null));
            b.Append(P("Показатель P — сумма взвешенных оценок по восьми критериям. Текущий уровень и разрыв до цели рассчитаны для каждого направления отдельно. Исключённые направления не входят в сводные показатели.", "Small"));
            b.Append(ChartParagraph());
            b.Append(TableStart(new int[] { 450, 3750, 1100, 600, 800, 800, 700, 1100 }));
            b.Append(TableRow(new string[] { "№", "Направление", "Статус", "P", "Уровень", "Цель", "Разрыв", "Ответы" }, true));
            for (int i = 0; i < Data.DirectionNames.Length; i++)
            {
                DirectionAssessment d = GetDirection(a, i); DirectionResult r = Calc.Direction(d);
                b.Append(TableRow(new string[] { (i + 1).ToString(Inv), Data.DirectionNames[i], d.Included ? (r.Complete ? "Заполнено" : "Частично") : "Исключено", d.Included ? r.Score.ToString("0.000", Inv) : "—", d.Included ? r.Level.ToString(Inv) : "—", d.Target.ToString(Inv), d.Included ? r.Gap.ToString(Inv) : "—", d.Included ? r.Answered + "/" + Data.Criteria.Length : "—" }, false));
            }
            b.Append("</w:tbl>");
            b.Append(P("Примечания к оценке", "Heading2")); b.Append(P(Empty(a.Notes), null));
            b.Append(PageBreak());
            b.Append(P("2. Детализация направлений", "Heading1"));
            for (int i = 0; i < Data.DirectionNames.Length; i++)
            {
                DirectionAssessment d = GetDirection(a, i); DirectionResult r = Calc.Direction(d);
                b.Append(P((i + 1) + ". " + Data.DirectionNames[i], "Heading2"));
                b.Append(P(d.Included ? "P = " + r.Score.ToString("0.000", Inv) + ". Уровень " + r.Level + " — " + Calc.LevelName(r.Level) + ". Цель: " + d.Target + ". Разрыв: " + r.Gap + ". Ответы: " + r.Answered + "/" + Data.Criteria.Length + (r.Complete ? "." : ". Результат предварительный; незаполненные ответы учитываются как 0.") : "Направление исключено из оценки; ответы и доказательства сохранены для справки.", d.Included && !r.Complete ? "Warning" : null));
                b.Append(TableStart(new int[] { 400, 2900, 700, 750, 4550 }));
                b.Append(TableRow(new string[] { "№", "Критерий", "Вес", "Баллы", "Выбранный ответ" }, true));
                for (int c = 0; c < Data.Criteria.Length; c++)
                {
                    double? answer = GetAnswer(d, c);
                    b.Append(TableRow(new string[] { (c + 1).ToString(Inv), Data.Criteria[c].Name, Data.Criteria[c].Weight.ToString("0.00", Inv), answer.HasValue ? answer.Value.ToString("0.##", Inv) : "—", AnswerText(c, answer) }, false));
                }
                b.Append("</w:tbl>");
                b.Append(P("Доказательства и комментарии", "Heading3")); b.Append(P(Empty(d.Evidence), null));
            }
            b.Append(PageBreak());
            b.Append(P("3. Рекомендации", "Heading1"));
            bool any = false;
            for (int i = 0; i < Data.DirectionNames.Length; i++)
            {
                DirectionAssessment d = GetDirection(a, i); if (!d.Included) continue;
                DirectionResult r = Calc.Direction(d);
                if (r.Recommendations == null || r.Recommendations.Count == 0) continue;
                any = true; b.Append(P((i + 1) + ". " + Data.DirectionNames[i], "Heading2"));
                for (int n = 0; n < r.Recommendations.Count; n++) b.Append(P((n + 1) + ". " + r.Recommendations[n], null));
            }
            if (!any) b.Append(P("По введённым данным дополнительных рекомендаций нет. Сохраняйте доказательства и периодически пересматривайте оценку.", null));
            AppendAnalysisWord(b, analysis);
            b.Append(P("7. Основание и ограничения", "Heading1"));
            b.Append(P(MethodNote(), "Small"));
            b.Append(P("Источник: " + Data.SourceUrl + ". Дата версии методики: " + Data.MethodDate + ".", "Small"));
            if (!String.IsNullOrWhiteSpace(a.Assessor) || !String.IsNullOrWhiteSpace(a.AssessorPosition) || !String.IsNullOrWhiteSpace(a.ApproverName) || !String.IsNullOrWhiteSpace(a.ApproverPosition))
            {
                b.Append(P("Подписи", "Heading1"));
                if (!String.IsNullOrWhiteSpace(a.Assessor) || !String.IsNullOrWhiteSpace(a.AssessorPosition))
                    b.Append(SignatureBlock("Составил", a.AssessorPosition, a.Assessor));
                if (!String.IsNullOrWhiteSpace(a.ApproverName) || !String.IsNullOrWhiteSpace(a.ApproverPosition))
                    b.Append(SignatureBlock("УТВЕРЖДАЮ", a.ApproverPosition, a.ApproverName));
            }
            b.Append("<w:sectPr><w:footerReference w:type=\"default\" r:id=\"rFooter\"/><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"1000\" w:right=\"850\" w:bottom=\"1000\" w:left=\"1000\" w:header=\"400\" w:footer=\"400\"/><w:cols w:space=\"708\"/></w:sectPr></w:body></w:document>");
            return b.ToString();
        }

        private static void AppendPassportWord(StringBuilder b, Assessment a)
        {
            SystemPassport p = a.Passport;
            b.Append(P("Описание объекта и состава", "Heading1"));
            b.Append(P(SystemPassports.Description(p), null));
            b.Append(P("Основа конфигурации: " + SystemPassports.TemplateName(p) + ". Параметры ниже отражают введённые сведения об объекте. Они не определяют оценки зрелости, категорию или класс защищённости.", "Small"));
            b.Append(TableStart(new int[] { 3600, 5900 }));
            b.Append(TableRow(new string[] { "Характеристика", "Сведения" }, true));
            foreach (KeyValuePair<string, string> fact in PassportFacts(p))
                b.Append(TableRow(new string[] { fact.Key, fact.Value }, false));
            b.Append("</w:tbl>");
            b.Append(P("Состав оборудования", "Heading2"));
            b.Append(TableStart(new int[] { 2400, 1000, 6100 }));
            b.Append(TableRow(new string[] { "Компонент", "Количество", "Назначение" }, true));
            foreach (SystemComponent component in SystemPassports.Components(p))
                b.Append(TableRow(new string[] { component.Name, component.Count.ToString(Inv), component.Purpose }, false));
            b.Append("</w:tbl>");
            b.Append(P("Текстовая схема подключения", "Heading2"));
            b.Append(P(SystemPassports.Topology(p), null));
            AppendPassportUnknownWord(b, p);
            if (!String.IsNullOrWhiteSpace(p.Notes))
            {
                b.Append(P("Примечания к объекту", "Heading2"));
                b.Append(P(p.Notes, null));
            }
        }

        private static void AppendPassportUnknownWord(StringBuilder b, SystemPassport p)
        {
            b.Append(P("Неуточнённые характеристики", "Heading2"));
            string[] unknown = SystemPassports.UnknownFields(p);
            if (unknown.Length == 0) b.Append(P("Все характеристики паспорта уточнены.", null));
            else
            {
                b.Append(P("Отсутствие сведений означает, что характеристика требует уточнения. Оно не означает, что технология отсутствует.", "Small"));
                for (int i = 0; i < unknown.Length; i++) b.Append(P((i + 1) + ". " + unknown[i], null));
            }
        }

        private static KeyValuePair<string, string>[] PassportFacts(SystemPassport p)
        {
            return new KeyValuePair<string, string>[] {
                new KeyValuePair<string, string>("Подключаемая система", Empty(p.ExternalSystem)),
                new KeyValuePair<string, string>("Способ работы", SystemPassports.ConnectionText(p.Connection)),
                new KeyValuePair<string, string>("Канал подключения", SystemPassports.ChannelText(p.Channel)),
                new KeyValuePair<string, string>("Домен", SystemPassports.BooleanText(p.Domain)),
                new KeyValuePair<string, string>("Доступ в интернет", SystemPassports.BooleanText(p.Internet)),
                new KeyValuePair<string, string>("Беспроводная сеть (Wi-Fi)", SystemPassports.BooleanText(p.Wireless)),
                new KeyValuePair<string, string>("Мобильные устройства", SystemPassports.BooleanText(p.Mobile)),
                new KeyValuePair<string, string>("Удалённое администрирование", SystemPassports.BooleanText(p.RemoteAdministration)),
                new KeyValuePair<string, string>("Собственная разработка ПО", SystemPassports.BooleanText(p.OwnDevelopment)),
                new KeyValuePair<string, string>("Искусственный интеллект", SystemPassports.BooleanText(p.ArtificialIntelligence)),
                new KeyValuePair<string, string>("Используемые средства защиты", Empty(p.Protection))
            };
        }

        private static string PassportSheet(Assessment a)
        {
            SystemPassport p = a.Passport;
            StringBuilder rows = new StringBuilder();
            List<string> merges = new List<string> { "A1:D1", "A2:D2", "A3:D3" };
            rows.Append(Row(1, T("A1", "ПАСПОРТ ОБЪЕКТА", 1), 30));
            rows.Append(Row(2, T("A2", SystemPassports.Description(p), 0), 80));
            rows.Append(Row(3, T("A3", "Паспорт отражает введённые сведения и не определяет оценки зрелости, категорию или класс защищённости. Неуточнённая характеристика не означает отсутствие технологии.", 5), 50));
            int row = 5;
            AppendPassportRow(rows, merges, ref row, "Организация", Empty(a.Organization));
            AppendPassportRow(rows, merges, ref row, "Система / объект оценки", Empty(a.SystemName));
            AppendPassportRow(rows, merges, ref row, "Основа конфигурации", SystemPassports.TemplateName(p));
            foreach (KeyValuePair<string, string> fact in PassportFacts(p))
                AppendPassportRow(rows, merges, ref row, fact.Key, fact.Value);
            row++;
            rows.Append(Row(row, T("A" + row, "СОСТАВ ОБОРУДОВАНИЯ", 4), 26));
            merges.Add("A" + row + ":D" + row); row++;
            rows.Append(Row(row, HeaderCells(new string[] { "Компонент", "Количество", "Назначение", "" }, row), 30));
            merges.Add("C" + row + ":D" + row); row++;
            foreach (SystemComponent component in SystemPassports.Components(p))
            {
                rows.Append(Row(row, T("A" + row, component.Name, 0) + N("B" + row, component.Count, 3) + T("C" + row, component.Purpose, 0), 42));
                merges.Add("C" + row + ":D" + row); row++;
            }
            row++;
            AppendPassportRow(rows, merges, ref row, "Текстовая схема подключения", SystemPassports.Topology(p), 90);
            string[] unknown = SystemPassports.UnknownFields(p);
            AppendPassportRow(rows, merges, ref row, "Неуточнённые характеристики", unknown.Length == 0 ? "Все характеристики паспорта уточнены." : String.Join("\n", unknown), unknown.Length == 0 ? 42 : Math.Min(300, 28 + unknown.Length * 18));
            if (!String.IsNullOrWhiteSpace(p.Notes)) AppendPassportRow(rows, merges, ref row, "Примечания к объекту", p.Notes, 90);
            StringBuilder mergeXml = new StringBuilder("<mergeCells count=\"" + merges.Count + "\">");
            foreach (string range in merges) mergeXml.Append("<mergeCell ref=\"" + range + "\"/>");
            mergeXml.Append("</mergeCells>");
            return Sheet(rows, "A1:D" + (row - 1), "<col min=\"1\" max=\"1\" width=\"38\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"14\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"40\" customWidth=\"1\"/><col min=\"4\" max=\"4\" width=\"28\" customWidth=\"1\"/>", 3, "", mergeXml.ToString(), false);
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
            b.Append(P("4. Профиль критериев", "Heading1"));
            b.Append(P("Средняя оценка рассчитана только по заполненным ответам включённых направлений. Пропуски не входят в среднее; выбранный 0 входит. Эти средние не являются показателем зрелости Узи. Покрытие показывает долю направлений с ответом по критерию. Известные дефициты учитывают выбранные оценки ниже требуемого минимума; пропуски показаны отдельно.", "Small"));
            b.Append(TableStart(new int[] { 400, 3200, 1100, 1300, 1300, 2000 }));
            b.Append(TableRow(new string[] { "№", "Критерий", "Средняя оценка", "Ответы / включено", "Покрытие, %", "Известные дефициты" }, true));
            foreach (CriterionProfile c in analysis.Criteria)
                b.Append(TableRow(new string[] { (c.CriterionIndex + 1).ToString(Inv), c.Name, NullableNumber(c.Average), c.Answered + " / " + c.Included, (c.Coverage * 100).ToString("0.##", Inv), c.KnownDeficits.ToString(Inv) }, false));
            b.Append("</w:tbl>");
            if (!analysis.HasScope) b.Append(P("Нет включённых направлений. Для расчёта профиля включите направления и заполните ответы.", "Warning"));

            b.Append(P("5. План достижения целевых уровней", "Heading1"));
            b.Append(P("План сформирован из текущих ответов и целевого уровня каждого направления. «Оценить» означает проверить фактическое состояние и выбрать подтверждённый ответ. «Улучшить» означает выполнить указанное условие и подтвердить результат. Исполнитель и срок оставлены для заполнения. Пропуск при нулевом минимуме не требует выставлять 0 и отдельно помечен как не влияющий на минимум цели.", "Small"));
            b.Append(P("Строки «Порог P» показывают контрольный суммарный показатель. Это не отдельные работы сверх мероприятий по критериям: в текущей таблице 3 сумма обязательных минимумов равна порогу P.", "Small"));
            if (analysis.Deficits.Length == 0)
                b.Append(P(analysis.HasScope ? "Недостатков и незаполненных ответов по включённым направлениям не выявлено." : "План не сформирован: нет включённых направлений.", null));
            else
            {
                b.Append(TableStart(new int[] { 450, 2400, 1250, 1100, 3000, 1100 }));
                b.Append(TableRow(new string[] { "№", "Направление и критерий", "Сейчас / требуется", "Тип", "Полное условие и мероприятие", "Исполнитель / срок" }, true));
                for (int i = 0; i < analysis.Deficits.Length; i++)
                {
                    DeficitRecord d = analysis.Deficits[i];
                    string target = "Сейчас: " + NullableNumber(d.Current) + "\nТребуется: " + NullableNumber(d.Required);
                    string type = ActionLabel(d.Type) + (d.BlockingTarget ? "\nВлияет на цель" : "\nНе влияет на минимум цели");
                    b.Append(TableRow(new string[] { (i + 1).ToString(Inv), d.DirectionId + ". " + d.DirectionName + "\n" + d.CriterionName, target, type, ConditionAndAction(d.ConditionText, d.Action), "Исполнитель:\n____________\nСрок:\n____________" }, false));
                }
                b.Append("</w:tbl>");
            }

            b.Append(P("6. Общие дефициты", "Heading1"));
            b.Append(P("Одинаковые требования сгруппированы по критерию, типу действия и требуемой оценке. Число направлений помогает выбрать общие мероприятия. Номера соответствуют сводной таблице; результат и подтверждающие материалы проверяются по каждому направлению.", "Small"));
            b.Append(P(Analysis.PriorityExplanation, "Small"));
            if (analysis.GroupedActions.Length == 0) b.Append(P("Общих мероприятий по введённым данным нет.", null));
            else
            {
                b.Append(TableStart(new int[] { 450, 2200, 1300, 800, 1500, 3050 }));
                b.Append(TableRow(new string[] { "Приоритет", "Критерий", "Тип / требуется", "Направлений", "№ направлений", "Полное условие и мероприятие" }, true));
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
            rows.Append(Row(4, HeaderCells(new string[] { "№", "Критерий", "Вес", "Средняя оценка", "Средняя взвешенная", "Ответов", "Включено", "Пропусков", "Нулевых ответов", "Покрытие, %", "Известные дефициты" }, 4), 42));
            int row = 5;
            foreach (CriterionProfile c in analysis.Criteria)
            {
                rows.Append(Row(row, N("A" + row, c.CriterionIndex + 1, 3) + T("B" + row, c.Name, 0) + N("C" + row, c.Weight, 2) + OptionalNumber("D" + row, c.Average) + OptionalNumber("E" + row, c.WeightedAverage) + N("F" + row, c.Answered, 3) + N("G" + row, c.Included, 3) + N("H" + row, c.Missing, 3) + N("I" + row, c.ZeroCount, 3) + N("J" + row, c.Coverage * 100, 2) + N("K" + row, c.KnownDeficits, 3), 48));
                row++;
            }
            return Sheet(rows, "A1:K" + (row - 1), "<col min=\"1\" max=\"1\" width=\"8\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"42\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"10\" customWidth=\"1\"/><col min=\"4\" max=\"5\" width=\"17\" customWidth=\"1\"/><col min=\"6\" max=\"11\" width=\"14\" customWidth=\"1\"/>", 4, "A4:K" + (row - 1), TwoTitleMerges("K"), true);
        }

        private static string DeficitPlanSheet(AssessmentAnalysis analysis)
        {
            StringBuilder rows = new StringBuilder();
            rows.Append(Row(1, T("A1", "ПЛАН ДОСТИЖЕНИЯ ЦЕЛЕВЫХ УРОВНЕЙ", 1), 30));
            rows.Append(Row(2, T("A2", "«Оценить» — проверить фактическое состояние и выбрать подтверждённый ответ. «Улучшить» — выполнить условие и подтвердить результат. Пропуск при нулевом минимуме не требует выставлять 0. Строки «Порог P» — контроль суммы, а не отдельные работы сверх критериев: сумма обязательных минимумов текущей таблицы 3 равна порогу P. Исполнитель и срок оставлены пустыми.", 5), 65));
            rows.Append(Row(4, HeaderCells(new string[] { "№ направления", "Направление", "Критерий", "Сейчас", "Требуется", "Тип действия", "Влияет на цель", "Полное условие", "Мероприятие", "Исполнитель", "Срок" }, 4), 42));
            int row = 5;
            foreach (DeficitRecord d in analysis.Deficits)
            {
                rows.Append(Row(row, N("A" + row, d.DirectionId, 3) + T("B" + row, d.DirectionName, 0) + T("C" + row, d.CriterionName, 0) + OptionalNumber("D" + row, d.Current) + OptionalNumber("E" + row, d.Required) + T("F" + row, ActionLabel(d.Type), d.Type == "assess" ? 7 : 0) + T("G" + row, d.BlockingTarget ? "Да" : "Нет", 0) + T("H" + row, d.ConditionText ?? "", 0) + T("I" + row, d.Action ?? "", 0) + T("J" + row, "", 0) + T("K" + row, "", 0), 120));
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
            rows.Append(Row(2, T("A2", "Одинаковые требования сгруппированы по критерию, типу действия и требуемой оценке. Номера направлений соответствуют листу «Сводка». Результат подтверждается по каждому направлению. " + Analysis.PriorityExplanation, 5), 80));
            rows.Append(Row(4, HeaderCells(new string[] { "Приоритет", "Критерий", "Тип действия", "Требуется", "Направлений", "№ направлений", "Влияет на цель", "Полное условие и мероприятие", "Исполнитель", "Срок" }, 4), 42));
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
            if (type == "score") return "Порог P";
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

        private static string AnswersSheet(Assessment a)
        {
            StringBuilder rows = new StringBuilder();
            rows.Append(Row(1, HeaderCells(new string[] { "№ направления", "Направление", "Участие", "№ критерия", "Критерий", "Вес", "Баллы", "Выбранный ответ", "Доказательства и комментарии" }, 1), 36));
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
