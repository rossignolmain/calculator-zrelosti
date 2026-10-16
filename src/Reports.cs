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
            byte[] chart = ChartPng(a);
            string doc = WordDocument(a, summary);
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
            string sheet1 = SummarySheet(a, summary), sheet2 = AnswersSheet(a), sheet3 = RecommendationsSheet(a);
            AtomicWrite(path, delegate(Stream output)
            {
                using (ZipArchive zip = new ZipArchive(output, ZipArchiveMode.Create, true))
                {
                    Add(zip, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/worksheets/sheet3.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/><Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/></Types>");
                    Add(zip, "_rels/.rels", PackageRels("xl/workbook.xml", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"));
                    Add(zip, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"" + NsSheet + "\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><bookViews><workbookView/></bookViews><sheets><sheet name=\"Сводка\" sheetId=\"1\" r:id=\"r1\"/><sheet name=\"Ответы\" sheetId=\"2\" r:id=\"r2\"/><sheet name=\"Рекомендации\" sheetId=\"3\" r:id=\"r3\"/></sheets><calcPr calcId=\"191029\" fullCalcOnLoad=\"1\"/></workbook>");
                    Add(zip, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"r1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"r2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/><Relationship Id=\"r3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet3.xml\"/><Relationship Id=\"rStyles\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
                    Add(zip, "xl/styles.xml", ExcelStyles());
                    Add(zip, "xl/worksheets/sheet1.xml", sheet1);
                    Add(zip, "xl/worksheets/sheet2.xml", sheet2);
                    Add(zip, "xl/worksheets/sheet3.xml", sheet3);
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

        private static string WordDocument(Assessment a, Summary s)
        {
            StringBuilder b = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document xmlns:w=\"" + NsWord + "\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><w:body>");
            b.Append(P("ОЦЕНКА ЗРЕЛОСТИ", "Title"));
            b.Append(P("информационной безопасности", "Subtitle"));
            b.Append(P(s.Complete ? "Оценка заполнена" : "ПРЕДВАРИТЕЛЬНЫЙ ОТЧЁТ · есть незаполненные ответы", s.Complete ? "Status" : "Warning"));
            b.Append(P("Организация: " + Empty(a.Organization), null));
            b.Append(P("Система / объект оценки: " + Empty(a.SystemName), null));
            b.Append(P("Оценку выполнил: " + Empty(a.Assessor), null));
            b.Append(P("Дата оценки: " + DateText(a.Date) + "    Категория: " + CategoryText(a.Category), null));
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
            b.Append(P("4. Основание и ограничения", "Heading1"));
            b.Append(P(MethodNote(), "Small"));
            b.Append(P("Источник: " + Data.SourceUrl + ". Дата версии методики: " + Data.MethodDate + ".", "Small"));
            b.Append("<w:sectPr><w:footerReference w:type=\"default\" r:id=\"rFooter\"/><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"1000\" w:right=\"850\" w:bottom=\"1000\" w:left=\"1000\" w:header=\"400\" w:footer=\"400\"/><w:cols w:space=\"708\"/></w:sectPr></w:body></w:document>");
            return b.ToString();
        }

        private static string SummarySheet(Assessment a, Summary s)
        {
            StringBuilder rows = new StringBuilder();
            rows.Append(Row(1, T("A1", "ОЦЕНКА ЗРЕЛОСТИ ИНФОРМАЦИОННОЙ БЕЗОПАСНОСТИ", 1), 30));
            rows.Append(Row(2, T("A2", "Организация", 4) + T("C2", Empty(a.Organization), 0), 32));
            rows.Append(Row(3, T("A3", "Система / объект", 4) + T("C3", Empty(a.SystemName), 0), 32));
            rows.Append(Row(4, T("A4", "Оценку выполнил", 4) + T("C4", Empty(a.Assessor), 0) + T("E4", "Дата", 4) + T("F4", DateText(a.Date), 0) + T("H4", "Категория", 4) + (a.Category == 0 ? T("I4", "Не определена", 0) : N("I4", a.Category, 3)), 32));
            rows.Append(Row(5, T("A5", s.Complete ? "Оценка заполнена" : "ПРЕДВАРИТЕЛЬНО: есть незаполненные ответы", s.Complete ? 6 : 7), 26));
            rows.Append(Row(6, T("A6", "Направлений: " + s.Count + ". Ответов: " + s.Answered + " / " + s.Total + ". Цель достигнута: " + s.Met + " / " + s.Count + ".", 4), 30));
            rows.Append(Row(7, T("A7", "Средний уровень: " + s.Average.ToString("0.00", Inv) + ". Средняя цель: " + s.TargetAverage.ToString("0.00", Inv) + ". Общий уровень: " + ((int)Math.Floor(s.Average)) + " — " + Calc.LevelName((int)Math.Floor(s.Average)) + ".", 5), 30));
            rows.Append(Row(8, HeaderCells(new string[] { "№", "Направление", "Статус", "P", "Уровень", "Цель", "Разрыв", "Ответы", "Всего" }, 8), 30));
            for (int i = 0; i < Data.DirectionNames.Length; i++)
            {
                int row = 9 + i; DirectionAssessment d = GetDirection(a, i); DirectionResult r = Calc.Direction(d); int st = d.Included ? 0 : 8;
                string cells = N("A" + row, i + 1, 3) + T("B" + row, Data.DirectionNames[i], st) + T("C" + row, d.Included ? (r.Complete ? "Заполнено" : "Частично") : "Исключено", d.Included ? (r.Complete ? 6 : 7) : 8);
                cells += d.Included ? N("D" + row, r.Score, 9) + N("E" + row, r.Level, 3) : T("D" + row, "—", 8) + T("E" + row, "—", 8);
                cells += N("F" + row, d.Target, 3) + (d.Included ? N("G" + row, r.Gap, r.Gap > 0 ? 10 : 3) + N("H" + row, r.Answered, 3) + N("I" + row, Data.Criteria.Length, 3) : T("G" + row, "—", 8) + T("H" + row, "—", 8) + T("I" + row, "—", 8));
                rows.Append(Row(row, cells, 42));
            }
            int end = 8 + Data.DirectionNames.Length;
            rows.Append(Row(end + 2, T("A" + (end + 2), "Примечания: " + Empty(a.Notes), 0), 70));
            rows.Append(Row(end + 4, T("A" + (end + 4), MethodNote(), 5), 75));
            rows.Append(Row(end + 5, T("A" + (end + 5), "Источник: " + Data.SourceUrl + " · Версия методики: " + Data.MethodDate, 5), 30));
            string merges = "<mergeCells count=\"14\"><mergeCell ref=\"A1:I1\"/><mergeCell ref=\"A2:B2\"/><mergeCell ref=\"C2:I2\"/><mergeCell ref=\"A3:B3\"/><mergeCell ref=\"C3:I3\"/><mergeCell ref=\"A4:B4\"/><mergeCell ref=\"C4:D4\"/><mergeCell ref=\"F4:G4\"/><mergeCell ref=\"A5:I5\"/><mergeCell ref=\"A6:I6\"/><mergeCell ref=\"A7:I7\"/><mergeCell ref=\"A" + (end + 2) + ":I" + (end + 2) + "\"/><mergeCell ref=\"A" + (end + 4) + ":I" + (end + 4) + "\"/><mergeCell ref=\"A" + (end + 5) + ":I" + (end + 5) + "\"/></mergeCells>";
            return Sheet(rows, "A1:I" + (end + 5), "<col min=\"1\" max=\"1\" width=\"8\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"53\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"18\" customWidth=\"1\"/><col min=\"4\" max=\"9\" width=\"13\" customWidth=\"1\"/>", 8, "A8:I" + end, merges, false);
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

        private static string Sheet(StringBuilder rows, string dimension, string cols, int freezeRows, string filter, string merges, bool landscape)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"" + NsSheet + "\"><sheetPr><pageSetUpPr fitToPage=\"1\"/></sheetPr><dimension ref=\"" + dimension + "\"/><sheetViews><sheetView workbookViewId=\"0\" showGridLines=\"0\"><pane ySplit=\"" + freezeRows + "\" topLeftCell=\"A" + (freezeRows + 1) + "\" activePane=\"bottomLeft\" state=\"frozen\"/><selection pane=\"bottomLeft\" activeCell=\"A" + (freezeRows + 1) + "\" sqref=\"A" + (freezeRows + 1) + "\"/></sheetView></sheetViews><sheetFormatPr defaultRowHeight=\"18\"/><cols>" + cols + "</cols><sheetData>" + rows + "</sheetData><autoFilter ref=\"" + filter + "\"/>" + merges + "<printOptions horizontalCentered=\"1\"/><pageMargins left=\"0.3\" right=\"0.3\" top=\"0.5\" bottom=\"0.5\" header=\"0.2\" footer=\"0.2\"/><pageSetup paperSize=\"9\" orientation=\"" + (landscape ? "landscape" : "portrait") + "\" fitToWidth=\"1\" fitToHeight=\"0\"/><headerFooter><oddFooter>&amp;LОценка зрелости ИБ&amp;RСтраница &amp;P из &amp;N</oddFooter></headerFooter></worksheet>";
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
            string t = E(text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "</w:t><w:br/><w:t xml:space=\"preserve\">");
            return "<w:p>" + (style == null ? "" : "<w:pPr><w:pStyle w:val=\"" + style + "\"/></w:pPr>") + "<w:r><w:t xml:space=\"preserve\">" + t + "</w:t></w:r></w:p>";
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
                b.Append("<w:tc><w:tcPr><w:vAlign w:val=\"top\"/>" + (header ? "<w:shd w:val=\"clear\" w:fill=\"173F56\"/>" : "") + "</w:tcPr><w:p><w:pPr><w:spacing w:after=\"20\" w:line=\"220\" w:lineRule=\"auto\"/></w:pPr><w:r><w:rPr><w:sz w:val=\"18\"/>" + (header ? "<w:b/><w:color w:val=\"FFFFFF\"/>" : "") + "</w:rPr><w:t xml:space=\"preserve\">" + E(cell) + "</w:t></w:r></w:p></w:tc>");
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
