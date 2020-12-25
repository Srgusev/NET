//Программа для мониторинга в сети в фон. режиме.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO; 
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Net.Sockets;
using System.Net.NetworkInformation;

namespace Test_Network
{
    struct net_state
    {
        public bool inet_ok; // Флаг доступности сети
        public bool http_ok; // Флаг теста http
        public Dictionary<String, int> avg_rtts; // Словарь пинга до хостов
        public double packet_loss; // Потеря пакетов
        public DateTime measure_time; // Дата, время
        public int router_rtt; // 
    }
    class Program
    {
        static String HTTP_TEST_HOST; // HTTP сервер, соединение до которого будем тестировать
        static int HTTP_TEST_PORT; // Порт HTTP сервера
        static int HTTP_TIMEOUT; // Таймаут подключения
        static int PING_COUNT; // Количество пакетов пинга
        static int PING_DELAY; // Ожидание перед отправкой следующего пакета пинга
        static int PING_TIMEOUT; // Таймаут пинга
        static List<String> PING_HOSTS;  // Хосты, пинг до которых меряем
        static int MEASURE_DELAY; // Время между проверками
        static String ROUTER_IP; // IP роутера
        static double MAX_PKT_LOSS; // Максимально допустимый Packet loss
        static String OUT_FILE; // Выходной файл CSV
        static bool WRITE_CSV; // Писать ли CSV
        static String CSV_PATTERN; // Шаблон для записи в CSV
        // Промежуточные переменные
        static bool prev_inet_ok = true;
        static DateTime first_fail_time;
        static long total_time = 0;
        static int pkt_sent = 0;
        static int success_pkts = 0;
        static int exited_threads = 0;
        static Dictionary<string, int> measure_results = new Dictionary<string, int>();


        static void Main(string[] args)
        {
            var config = JsonConvert.DeserializeObject<Dictionary<String, Object>>(File.ReadAllText("setting.json"));

            HTTP_TEST_HOST = (String)config["http_test_host"];
            PING_HOSTS = ((JArray)config["ping_hosts"]).ToObject<List<String>>();
            ROUTER_IP = (String)config["router_ip"];
            HTTP_TEST_PORT = int.Parse((String)config["http_test_port"]);
            HTTP_TIMEOUT = int.Parse((String)config["http_timeout"]);
            PING_COUNT = int.Parse((String)config["ping_count"]);
            PING_TIMEOUT = int.Parse((String)config["ping_timeout"]);
            PING_DELAY = int.Parse((String)config["ping_packet_delay"]);
            MEASURE_DELAY = int.Parse((String)config["measure_delay"]);
            OUT_FILE = (String)config["out_file"];
            WRITE_CSV = bool.Parse((String)config["w_csv"]);
            CSV_PATTERN = (String)config["out_format"];
            MAX_PKT_LOSS = double.Parse((String)config["nq_max_loss"]);

            Console.WriteLine("Load config complit...");

            String CSV_HEADER = CSV_PATTERN
                .Replace("FTIME", "Data")
                .Replace("IUP", "Internet up")
                .Replace("AVGRTT", "Average ping (ms)")
                .Replace("ROUTERRTT", "Ping to router (ms)")
                .Replace("LOSS", "Packet loss, %")
                .Replace("HTTP", "HTTP OK")
                .Replace("STIME", "Time");

            foreach (var host in PING_HOSTS)
            {
                CSV_HEADER = CSV_HEADER.Replace("RN", $"Ping to {host};RN");
            }
            CSV_HEADER = CSV_HEADER.Replace("RN", "\r\n");
            if (WRITE_CSV) // Если запись включена в настройках создать файл и записать заголовки.
            {
                if (!File.Exists(OUT_FILE)) File.WriteAllText(OUT_FILE, CSV_HEADER);
            }

            while (true)
            { 
                Monit();
                Thread.Sleep(MEASURE_DELAY);
            }
            
        }
        static void Save_log(net_state snapshot)
        {
            if (WRITE_CSV) // Если запись логов включена
            {

                String rtts = "";
                int avg_rtt = 0;
                foreach (var ci in PING_HOSTS)
                {
                    rtts += $"{snapshot.avg_rtts[ci]};";
                    avg_rtt += snapshot.avg_rtts[ci];
                }
                avg_rtt = avg_rtt / PING_HOSTS.Count;
                File.AppendAllText(OUT_FILE, CSV_PATTERN
                    .Replace("FTIME", snapshot.measure_time.ToShortDateString())
                    .Replace("IUP", snapshot.inet_ok.ToString())
                    .Replace("AVGRTT", avg_rtt.ToString())
                    .Replace("ROUTERRTT", snapshot.router_rtt.ToString())
                    .Replace("LOSS", snapshot.packet_loss.ToString())
                    .Replace("HTTP", snapshot.http_ok.ToString())
                    .Replace("STIME", snapshot.measure_time.ToShortTimeString())
                    .Replace("RN", $"{rtts}\r\n"));
            }
        }
        static void Monit()
        {
            // Создаем экземпляр измерений.
            net_state snapshot = new net_state();
            snapshot.inet_ok = true;
            snapshot.measure_time = DateTime.Now;
            // Проверяем доступность роутера
            Ping ping = new Ping();
            var prr = ping.Send(ROUTER_IP, PING_TIMEOUT);
            // В CSV файле все поля должны быть заполнены. Если роутер не пингуестся заполняем их параметром PING_TIMEOUT
            snapshot.router_rtt = prr.Status == IPStatus.Success ? (int)prr.RoundtripTime : PING_TIMEOUT;
            if (prr.Status != IPStatus.Success)
            {
                snapshot.avg_rtts = new Dictionary<string, int>();
                snapshot.http_ok = false;
                snapshot.inet_ok = false;
                snapshot.packet_loss = 1;
                foreach (var ci in PING_HOSTS)
                {
                    snapshot.avg_rtts.Add(ci, PING_TIMEOUT);
                }
                Console.WriteLine("Router was unreachable.");
                Save_log(snapshot);
                return;
            }
            snapshot.inet_ok = true;
            // Проверяем доступность HTTP
            try
            {
                snapshot.http_ok = true;
                TcpClient tc = new TcpClient();
                tc.BeginConnect(HTTP_TEST_HOST, HTTP_TEST_PORT, null, null);
                Thread.Sleep(HTTP_TIMEOUT);
                // Если подключиться не удалось
                if (!tc.Connected)
                {
                    snapshot.http_ok = false;
                }
                tc.Dispose();
            }
            catch { snapshot.http_ok = false; snapshot.inet_ok = false; }
            //Теперь пингуем заданые хосты
            exited_threads = 0;
            pkt_sent = 0;
            success_pkts = 0;
            total_time = 0;
            measure_results = new Dictionary<string, int>();
            // Перебираем все хосты и запускаем пинг в отдельном потоке.
            foreach (var ci in PING_HOSTS)
            {
                Thread thread = new Thread(new ParameterizedThreadStart(PingTest));
                thread.Start(ci);
            }
            while (exited_threads < PING_HOSTS.Count) continue;
            //Анализируем результаты пинга
            snapshot.avg_rtts = measure_results;
            snapshot.packet_loss = (double)(pkt_sent - success_pkts) / pkt_sent;
            snapshot.inet_ok = !(
                snapshot.http_ok == false ||
                ((double)total_time / success_pkts >= 0.75 * PING_TIMEOUT) ||
                snapshot.packet_loss >= MAX_PKT_LOSS ||
                snapshot.router_rtt == PING_TIMEOUT);
            Save_log(snapshot);
            if (prev_inet_ok && !snapshot.inet_ok)
            {
                //Интернет был , но теперь неудачу
                prev_inet_ok = false;
                first_fail_time = DateTime.Now;
            }
            else if (!prev_inet_ok && snapshot.inet_ok)
            {
                String t_s = new TimeSpan(DateTime.Now.Ticks - first_fail_time.Ticks).ToString(@"hh\:mm\:ss");

                prev_inet_ok = true;
            }
        }
        static void PingTest(Object arg)
        {
            String host = (String)arg;
            int pkts_lost_row = 0;
            int local_success = 0;
            long local_time = 0;
            Ping ping = new Ping();
            // Запускаем пинг заданное количество раз.
            for (int i = 0; i < PING_COUNT; i++)
            {
                // Если потеряно 3 пакеты, записываем результаты и выходим из цикла
                if (pkts_lost_row == 3)
                {
                    measure_results.Add(host, (int)(local_time / (local_success == 0 ? 1 : local_success)));
                    exited_threads++;
                    return;
                }
                try
                {
                    var result = ping.Send(host, PING_TIMEOUT);
                    // Если пинг прошел
                    if (result.Status == IPStatus.Success)
                    {
                        pkts_lost_row = 0;
                        local_success++;
                        // RoundtripTime Возвращает количество миллисекунд, затраченных на отправку Эхо-запроса
                        local_time += result.RoundtripTime;
                        total_time += result.RoundtripTime;
                        pkt_sent++;
                        success_pkts++;
                    }
                    switch (result.Status)
                    {
                        case IPStatus.Success: break; //Already handled 
                        case IPStatus.BadDestination:

                            measure_results.Add(host, -1);
                            exited_threads++;
                            return;
                        case IPStatus.DestinationHostUnreachable:
                        case IPStatus.DestinationNetworkUnreachable:
                        case IPStatus.DestinationUnreachable:

                            measure_results.Add(host, -1);
                            exited_threads++;
                            return;
                        case IPStatus.TimedOut:
                            pkts_lost_row++;
                            pkt_sent++;
                            break;
                        default:

                            measure_results.Add(host, -1);
                            exited_threads++;
                            return;
                    }
                }
                catch (Exception xc)
                {

                    exited_threads++;
                    measure_results.Add(host, -1);
                    return;
                }
            }
            measure_results.Add(host, (int)(local_time / (local_success == 0 ? 1 : local_success)));
            exited_threads++;
            return;
        }
    }
}
