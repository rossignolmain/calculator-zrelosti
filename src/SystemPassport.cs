using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace MaturityApp
{
    public class PassportComponent { public string Kind = "", Note = ""; public int Count = 1; }
    public class PassportConnection { public string System = "", Method = "", Channel = "", Via = "", Note = ""; } // Via: boundary device, e.g. firewall or crypto gateway
    public class PassportSegment { public string Name = "", Purpose = "", Note = ""; }
    public class PassportTool { public string Class = "other", Product = "", Note = ""; }

    // Technical description of the assessed object. Facts are separate from maturity grades
    // and scope decisions; an unknown nullable flag is never treated as confirmed absence.
    public class SystemPassport
    {
        public int Version = 2;
        public string Notes = "";
        public PassportComponent[] Components = new PassportComponent[0];
        public PassportConnection[] Connections = new PassportConnection[0];
        public bool NoConnections;
        public PassportTool[] Tools = new PassportTool[0];
        public PassportSegment[] Segments = new PassportSegment[0];
        public bool? WirelessIsolated = null; // Wi-Fi separated from the object's segments; meaningful only when Wireless == true
        public bool? Domain = null, Internet = null, Wireless = null, Mobile = null,
            RemoteAdministration = null, OwnDevelopment = null, ArtificialIntelligence = null;
        // Version 1 (1.4-1.6) fields, read once and converted by SystemPassports.Validate.
        public string TemplateId = "", ExternalSystem = "", Connection = "", Channel = "", Protection = "";
        public int Workstations, Routers, Switches, Servers;
    }

    public class SystemComponent { public string Name, Purpose; public int Count; }

    public static class SystemPassports
    {
        const int MaximumFileLength = 1024 * 1024, MaximumRows = 500;
        const string TemplateFormat = "maturity-single-arm";

        public static readonly string[] ComponentSuggestions = {
            "АРМ пользователя", "Сервер", "Виртуальный сервер", "Система хранения данных", "Маршрутизатор", "Коммутатор",
            "Межсетевой экран", "Точка доступа Wi-Fi", "Мобильное устройство", "Принтер / МФУ", "АРМ оператора",
            "Инженерная станция", "ПЛК / контроллер", "Сетевое оборудование АСУ ТП"
        };
        public static readonly string[] ViaSuggestions = { "Межсетевой экран", "Криптошлюз (VPN-шлюз)", "Маршрутизатор", "Однонаправленный шлюз", "Напрямую" };
        public static readonly string[] SegmentSuggestions = { "Сегмент пользователей", "Серверный сегмент", "Сегмент управления", "Технологический сегмент", "ДМЗ", "Гостевой Wi-Fi" };
        public static readonly CatalogItem[] Methods = {
            new CatalogItem("", "Не уточнено"), new CatalogItem("browser", "Через браузер"), new CatalogItem("client", "Клиентская программа"),
            new CatalogItem("exchange", "Автоматический обмен данными"), new CatalogItem("other", "Иной способ")
        };
        public static readonly CatalogItem[] Channels = {
            new CatalogItem("", "Не уточнено"), new CatalogItem("internet", "Интернет"), new CatalogItem("vpn", "Защищённый канал (VPN)"),
            new CatalogItem("dedicated", "Выделенный канал"), new CatalogItem("local", "Локальная сеть организации"), new CatalogItem("other", "Иной канал")
        };
        public static readonly CatalogItem[] ToolClasses = {
            new CatalogItem("crypto", "Криптозащита / VPN"), new CatalogItem("fw", "Межсетевой экран"), new CatalogItem("nsd", "Защита от НСД"),
            new CatalogItem("av", "Антивирусная защита"), new CatalogItem("boot", "Доверенная загрузка"), new CatalogItem("ids", "Обнаружение вторжений"),
            new CatalogItem("scan", "Анализ защищённости"), new CatalogItem("siem", "Мониторинг событий ИБ"), new CatalogItem("backup", "Резервное копирование"),
            new CatalogItem("mobile", "Защита мобильных устройств"), new CatalogItem("other", "Другое")
        };
        // Suggestions only: the user types any product; nothing here implies a product is installed or required.
        public static readonly Dictionary<string, string[]> ProductSuggestions = new Dictionary<string, string[]> {
            { "crypto", new[] { "ViPNet Client", "ViPNet Coordinator", "Континент", "КриптоПро CSP", "С-Терра" } },
            { "fw", new[] { "UserGate", "Континент", "ViPNet xFirewall", "Ideco NGFW" } },
            { "nsd", new[] { "Secret Net Studio", "Dallas Lock" } },
            { "av", new[] { "Kaspersky Endpoint Security", "Dr.Web" } },
            { "boot", new[] { "ПАК «Соболь»", "ViPNet SafeBoot", "Dallas Lock (СДЗ)" } },
            { "ids", new[] { "ViPNet IDS", "Континент СОВ" } },
            { "scan", new[] { "RedCheck", "MaxPatrol", "ScanOVAL", "Сканер-ВС" } },
            { "siem", new[] { "MaxPatrol SIEM", "KUMA", "RuSIEM" } }
        };

        public static SystemPassport CreateEmpty() { return new SystemPassport(); }

        public static void Validate(SystemPassport p)
        {
            if (p == null) return;
            if (p.Version == 1) Migrate(p);
            else if (p.Version != 2) throw new InvalidDataException("Неподдерживаемая версия описания объекта.");
            p.Components = (p.Components ?? new PassportComponent[0]).Where(x => x != null).ToArray();
            p.Connections = (p.Connections ?? new PassportConnection[0]).Where(x => x != null).ToArray();
            p.Tools = (p.Tools ?? new PassportTool[0]).Where(x => x != null).ToArray();
            p.Segments = (p.Segments ?? new PassportSegment[0]).Where(x => x != null).ToArray();
            if (p.Components.Length > MaximumRows || p.Connections.Length > MaximumRows || p.Tools.Length > MaximumRows || p.Segments.Length > MaximumRows) throw new InvalidDataException("Слишком много строк в описании объекта.");
            foreach (PassportSegment g in p.Segments) { g.Name = Text(g.Name); g.Purpose = Text(g.Purpose); g.Note = Text(g.Note); }
            if (p.Wireless != true) p.WirelessIsolated = null; // isolation of absent or unknown Wi-Fi is not a fact
            foreach (PassportComponent c in p.Components)
            {
                if (c.Count < 0 || c.Count > 100000) throw new InvalidDataException("Количество в составе объекта должно быть от 0 до 100000.");
                c.Kind = Text(c.Kind); c.Note = Text(c.Note);
            }
            foreach (PassportConnection c in p.Connections)
            {
                c.Method = c.Method ?? ""; c.Channel = c.Channel ?? "";
                if (!Methods.Any(x => x.Key == c.Method)) throw new InvalidDataException("Неизвестный способ взаимодействия.");
                if (!Channels.Any(x => x.Key == c.Channel)) throw new InvalidDataException("Неизвестный канал взаимодействия.");
                c.System = Text(c.System); c.Via = Text(c.Via); c.Note = Text(c.Note);
            }
            foreach (PassportTool t in p.Tools)
            {
                t.Class = String.IsNullOrEmpty(t.Class) ? "other" : t.Class;
                if (!ToolClasses.Any(x => x.Key == t.Class)) throw new InvalidDataException("Неизвестный класс средства защиты.");
                t.Product = Text(t.Product); t.Note = Text(t.Note);
            }
            if (p.NoConnections && p.Connections.Length > 0) throw new InvalidDataException("Указаны внешние взаимодействия и одновременно отмечено их отсутствие.");
            if (p.Connections.Any(c => c.Channel == "internet")) p.Internet = true; // an Internet channel is itself Internet access
            p.Notes = Text(p.Notes);
        }
        static string Text(string value)
        {
            if (value != null && value.Length > 65536) throw new InvalidDataException("Поле описания объекта слишком длинное.");
            return value ?? "";
        }

        // 1.4-1.6 stored one ARM: fixed equipment counts, one external system and a text list of products.
        static void Migrate(SystemPassport p)
        {
            if (p.Workstations < 0 || p.Routers < 0 || p.Switches < 0 || p.Servers < 0) throw new InvalidDataException("Количество устройств не может быть отрицательным.");
            string legacyConnection = String.IsNullOrEmpty(p.Connection) ? "unknown" : p.Connection, legacyChannel = String.IsNullOrEmpty(p.Channel) ? "unknown" : p.Channel;
            if (!new[] { "unknown", "browser", "client" }.Contains(legacyConnection)) throw new InvalidDataException("Неизвестный способ работы с внешней системой.");
            if (!new[] { "unknown", "internet", "vpn", "isolated" }.Contains(legacyChannel)) throw new InvalidDataException("Неизвестный канал подключения.");
            List<PassportComponent> components = new List<PassportComponent>();
            if (p.Workstations > 0) components.Add(new PassportComponent { Kind = "АРМ пользователя", Count = p.Workstations });
            if (p.Servers > 0) components.Add(new PassportComponent { Kind = "Сервер", Count = p.Servers });
            if (p.Routers > 0) components.Add(new PassportComponent { Kind = "Маршрутизатор", Count = p.Routers });
            if (p.Switches > 0) components.Add(new PassportComponent { Kind = "Коммутатор", Count = p.Switches });
            p.Components = components.ToArray();
            p.Connections = String.IsNullOrWhiteSpace(p.ExternalSystem) ? new PassportConnection[0] : new[] { new PassportConnection {
                System = p.ExternalSystem.Trim(), Method = legacyConnection == "unknown" ? "" : legacyConnection,
                Channel = legacyChannel == "unknown" ? "" : legacyChannel == "isolated" ? "dedicated" : legacyChannel } };
            p.Tools = ParseLegacyProtection(p.Protection);
            p.Version = 2; p.TemplateId = p.ExternalSystem = p.Connection = p.Channel = p.Protection = "";
            p.Workstations = p.Routers = p.Switches = p.Servers = 0;
        }
        static PassportTool[] ParseLegacyProtection(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return new PassportTool[0];
            Dictionary<string, string> labels = new Dictionary<string, string> { { "Криптозащита / сеть", "crypto" }, { "Защита от НСД", "nsd" }, { "Антивирус", "av" }, { "СДЗ", "boot" } };
            List<PassportTool> tools = new List<PassportTool>(); bool structured = true;
            foreach (string part in text.Split(';').Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                int colon = part.IndexOf(':'); string cls;
                if (colon < 0 || !labels.TryGetValue(part.Substring(0, colon).Trim(), out cls)) { structured = false; break; }
                string product = part.Substring(colon + 1).Trim();
                if (product.Length > 0 && !product.Equals("не уточнено", StringComparison.OrdinalIgnoreCase)) tools.Add(new PassportTool { Class = cls, Product = product });
            }
            return structured ? tools.ToArray() : new[] { new PassportTool { Class = "other", Product = text.Trim() } };
        }

        public static SystemPassport Snapshot(SystemPassport p)
        {
            if (p == null) return null;
            JavaScriptSerializer s = Serializer();
            SystemPassport copy = s.Deserialize<SystemPassport>(s.Serialize(p));
            Validate(copy);
            return copy;
        }
        public static void Apply(Assessment a, SystemPassport p)
        {
            if (a == null) throw new ArgumentNullException("a");
            a.Passport = Snapshot(p);
        }

        public static string Label(CatalogItem[] catalog, string key) { CatalogItem item = catalog.FirstOrDefault(x => x.Key == (key ?? "")); return item == null ? key : item.Text; }
        public static string BooleanText(bool? value) { return !value.HasValue ? "Не уточнено" : value.Value ? "Да" : "Нет"; }
        public static string ConnectionText(PassportConnection c)
        {
            List<string> parts = new List<string>();
            if (c.Method.Length > 0) parts.Add(Label(Methods, c.Method));
            if (c.Channel.Length > 0) parts.Add(Label(Channels, c.Channel));
            if (c.Via.Length > 0) parts.Add("через: " + c.Via);
            return (c.System.Length > 0 ? c.System : "Система не указана") + (parts.Count > 0 ? " (" + String.Join(", ", parts) + ")" : "") + (c.Note.Length > 0 ? " — " + c.Note : "");
        }
        public static string ToolText(PassportTool t) { return Label(ToolClasses, t.Class) + ": " + (t.Product.Length > 0 ? t.Product : "не указано") + (t.Note.Length > 0 ? " (" + t.Note + ")" : ""); }

        public static string Description(SystemPassport p)
        {
            if (p == null) return "Описание объекта не заполнено";
            SystemPassport q = Snapshot(p);
            string composition = q.Components.Length == 0 ? "состав не указан" : String.Join("; ", q.Components.Select(c => (c.Kind.Length > 0 ? c.Kind : "Компонент") + " — " + c.Count.ToString(CultureInfo.InvariantCulture)));
            string links = q.NoConnections ? "внешних взаимодействий нет" : q.Connections.Length == 0 ? "внешние взаимодействия не указаны" : String.Join("; ", q.Connections.Select(ConnectionText));
            string segments = q.Segments.Length == 0 ? "" : " Сегменты: " + String.Join("; ", q.Segments.Select(g => g.Name.Length > 0 ? g.Name : "без названия")) + ".";
            return "Состав: " + composition + ". Внешние взаимодействия: " + links + "." + segments;
        }
        public static SystemComponent[] Components(SystemPassport p)
        {
            if (p == null) return new SystemComponent[0];
            return Snapshot(p).Components.Select(c => new SystemComponent { Name = c.Kind.Length > 0 ? c.Kind : "Компонент", Count = c.Count, Purpose = c.Note }).ToArray();
        }
        public static string[] UnknownFields(SystemPassport p)
        {
            if (p == null) return new string[0];
            SystemPassport q = Snapshot(p);
            List<string> fields = new List<string>();
            if (q.Components.Length == 0) fields.Add("Состав объекта");
            if (!q.NoConnections && q.Connections.Length == 0) fields.Add("Внешние взаимодействия (или отметка об их отсутствии)");
            if (q.Connections.Any(c => c.Channel.Length == 0)) fields.Add("Канал внешнего взаимодействия");
            if (q.Tools.Length == 0) fields.Add("Средства защиты информации");
            if (!q.Domain.HasValue) fields.Add("Домен");
            if (!q.Internet.HasValue) fields.Add("Доступ в интернет");
            if (!q.Wireless.HasValue) fields.Add("Беспроводной доступ");
            if (q.Wireless == true && !q.WirelessIsolated.HasValue) fields.Add("Отделение Wi-Fi от сегментов объекта");
            if (!q.Mobile.HasValue) fields.Add("Мобильные устройства");
            if (!q.RemoteAdministration.HasValue) fields.Add("Удалённый доступ");
            if (!q.OwnDevelopment.HasValue) fields.Add("Собственная разработка ПО");
            if (!q.ArtificialIntelligence.HasValue) fields.Add("Искусственный интеллект");
            return fields.ToArray();
        }

        // Legacy .arm.json files (1.4-1.6) are still readable so old shared folders keep working.
        public static SystemPassport ParseTemplate(string json)
        {
            if (String.IsNullOrWhiteSpace(json) || json.Length > MaximumFileLength) throw new InvalidDataException("Пустой или слишком большой файл описания объекта.");
            try
            {
                JavaScriptSerializer serializer = Serializer();
                Dictionary<string, object> root = serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (root == null || !(root.ContainsKey("Format") && root["Format"] as string == TemplateFormat) || !root.ContainsKey("Passport"))
                    throw new InvalidDataException("Нужен файл описания объекта прежней версии (.arm.json).");
                Dictionary<string, object> fields = root["Passport"] as Dictionary<string, object>;
                string[] allowed = typeof(SystemPassport).GetFields().Select(f => f.Name).ToArray();
                if (fields == null || fields.Keys.Any(k => !allowed.Contains(k))) throw new InvalidDataException("Основа может содержать только описание объекта.");
                SystemPassport p = serializer.Deserialize<SystemPassport>(serializer.Serialize(fields));
                Validate(p);
                return p;
            }
            catch (InvalidDataException) { throw; }
            catch (Exception ex)
            {
                if (!(ex is ArgumentException) && !(ex is InvalidOperationException)) throw;
                throw new InvalidDataException("Не удалось прочитать файл описания объекта: некорректный JSON.", ex);
            }
        }
        public static SystemPassport LoadTemplate(string path)
        {
            if (new FileInfo(path).Length > MaximumFileLength) throw new InvalidDataException("Файл описания объекта слишком большой.");
            return ParseTemplate(File.ReadAllText(path, new UTF8Encoding(false, true)));
        }
        public static void SaveTemplate(SystemPassport p, string path)
        {
            if (p == null) throw new InvalidDataException("Сначала опишите объект.");
            string json = Serializer().Serialize(new Dictionary<string, object> { { "Format", TemplateFormat }, { "Version", 1 }, { "Passport", Snapshot(p) } });
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
