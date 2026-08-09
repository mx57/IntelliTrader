using System;
using System.IO;

namespace BacktestingBenchmarkSuite
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("======================================================================");
            Console.WriteLine("⚡ ПОТОК БЕНЧМАРКОВ БЭКТЕСТИНГА INTELLITRADER (Bolt Edition) ⚡");
            Console.WriteLine("======================================================================");
            Console.ResetColor();

            // Значения по умолчанию
            int durationMonths = 3;
            int snapshotsCount = 5000;
            string outputPath = "benchmark_report.html";

            // Парсинг параметров
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "--duration-months" || args[i] == "-m") && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int m)) durationMonths = m;
                }
                else if ((args[i] == "--snapshots-count" || args[i] == "-s") && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int s)) snapshotsCount = s;
                }
                else if ((args[i] == "--output" || args[i] == "-o") && i + 1 < args.Length)
                {
                    outputPath = args[i + 1];
                }
                else if (args[i] == "--help" || args[i] == "-h")
                {
                    Console.WriteLine("Использование:");
                    Console.WriteLine("  dotnet run --project BacktestingBenchmarkSuite.csproj [опции]");
                    Console.WriteLine();
                    Console.WriteLine("Опции:");
                    Console.WriteLine("  -m, --duration-months <int>  Количество месяцев для симуляции (по умолчанию: 3)");
                    Console.WriteLine("  -s, --snapshots-count <int>  Количество снимков рынка (по умолчанию: 5000)");
                    Console.WriteLine("  -o, --output <path>          Путь к генерируемому отчету HTML (по умолчанию: benchmark_report.html)");
                    return 0;
                }
            }

            Console.WriteLine($"[*] Параметры запуска:");
            Console.WriteLine($"  - Количество снимков: {snapshotsCount:N0}");
            Console.WriteLine($"  - Период симуляции: {durationMonths} мес.");
            Console.WriteLine($"  - Файл отчета: {outputPath}");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[*] Фаза 1: Генерация детерминированного датасета снимков...");
            Console.ResetColor();

            // Запуск бенчмарка
            BenchmarkResult result;
            try
            {
                result = BenchmarkRunner.Run(snapshotsCount, durationMonths);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[!] Произошла критическая ошибка при запуске бенчмарка: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                return 1;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[+] Генерация и прогон бэктеста успешно завершены!");
            Console.WriteLine();
            Console.ResetColor();

            // Вывод красивой таблицы на консоль
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("----------------------------------------------------------------------");
            Console.WriteLine("📊 РЕЗУЛЬТАТЫ БЕНЧМАРКА СКОРОСТИ");
            Console.WriteLine("----------------------------------------------------------------------");
            Console.ResetColor();

            Console.WriteLine($"  Всего снимков обработано: {result.SnapshotCount:N0}");
            Console.WriteLine($"  Общий размер снимков:     {result.TotalSizeMb:F2} МБ");
            Console.WriteLine();
            Console.WriteLine($"  Сериализация (Запись):");
            Console.WriteLine($"    - Время выполнения:     {result.SerializationTimeMs:F2} мс");
            Console.WriteLine($"    - Пропускная способность: {result.SerializationThroughput:N0} снимков/сек");
            Console.WriteLine();
            Console.WriteLine($"  Десериализация (Чтение):");
            Console.WriteLine($"    - Время выполнения:     {result.DeserializationTimeMs:F2} мс");
            Console.WriteLine($"    - Пропускная способность: {result.DeserializationThroughput:N0} снимков/сек");
            Console.WriteLine($"    - Скорость данных:       {result.DeserializationSpeedMbSec:F2} МБ/сек");
            Console.WriteLine();
            Console.WriteLine($"  Эмуляция расчетного ядра бэктестинга:");
            Console.WriteLine($"    - Время выполнения:     {result.ProcessingTimeMs:F2} мс");
            Console.WriteLine($"    - Пропускная способность: {result.ProcessingThroughput:N0} снимков/сек");
            Console.WriteLine($"    - Множитель скорости:   {result.SpeedupFactor:N0}x (быстрее реального времени)");
            Console.WriteLine();
            Console.WriteLine($"  Использование ресурсов:");
            Console.WriteLine($"    - Выделено памяти:      {(result.BytesAllocated / (1024.0 * 1024.0)):F2} МБ");
            Console.WriteLine($"    - GC Сборки (Gen 0/1/2): {result.GcCollectionsGen0} / {result.GcCollectionsGen1} / {result.GcCollectionsGen2}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("----------------------------------------------------------------------");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[*] Фаза 2: Экспорт результатов в HTML-отчет {outputPath}...");
            Console.ResetColor();

            try
            {
                ReportGenerator.GenerateHtmlReport(outputPath, result);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[+] Интерактивный отчет успешно сохранен в: {Path.GetFullPath(outputPath)}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[!] Не удалось сохранить HTML-отчет: {ex.Message}");
                Console.ResetColor();
                return 2;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine("⚡ Бенчмаркинг полностью завершен! Все показатели в норме. ⚡");
            Console.ResetColor();
            return 0;
        }
    }
}
