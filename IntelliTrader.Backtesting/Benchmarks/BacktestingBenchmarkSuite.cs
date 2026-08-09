using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using IntelliTrader.Backtesting;
using IntelliTrader.Core;
using IntelliTrader.Signals.Base;

namespace IntelliTrader.Backtesting.Benchmarks
{
    public class BacktestingBenchmarkSuite
    {
        private const string TempDirectoryName = "temp_snapshots_benchmark";
        private static string _tempPath = "";

        public static int Main(string[] args)
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine("⚡ IntelliTrader Backtesting Performance Benchmark Suite ⚡");
            Console.WriteLine("=========================================================");

            int snapshotCount = 500;
            if (args.Length > 0 && int.TryParse(args[0], out int customCount))
            {
                snapshotCount = customCount;
            }

            _tempPath = Path.Combine(Directory.GetCurrentDirectory(), TempDirectoryName);

            try
            {
                // Step 1: Generate Mock High-Fidelity Data Representing Multi-Month Histories
                Console.WriteLine($"[*] Generating {snapshotCount} mock snapshots in temporal directory...");
                GenerateMockSnapshots(_tempPath, snapshotCount);

                // Step 2: Warm-up Phase
                Console.WriteLine("[*] Starting Warm-up Phase (JIT compilation & caching)...");
                RunBenchmarkLoop(_tempPath, isWarmUp: true);
                Console.WriteLine("[+] Warm-up completed successfully.");

                // Step 3: Run Benchmark Iterations
                int benchmarkRuns = 5;
                Console.WriteLine($"[*] Running {benchmarkRuns} timed iterations for robust measurements...");

                var runTimes = new List<double>();
                var memoryAllocated = new List<long>();
                var runReports = new List<RunDetail>();

                for (int run = 1; run <= benchmarkRuns; run++)
                {
                    Console.Write($"    - Run #{run}/{benchmarkRuns}... ");

                    // Force collection before run to have clean baseline
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    long memBefore = GC.GetTotalMemory(forceFullCollection: false);
                    int gc0Before = GC.CollectionCount(0);
                    int gc1Before = GC.CollectionCount(1);
                    int gc2Before = GC.CollectionCount(2);

                    var stopwatch = Stopwatch.StartNew();
                    long bytesProcessed = RunBenchmarkLoop(_tempPath, isWarmUp: false);
                    stopwatch.Stop();

                    long memAfter = GC.GetTotalMemory(forceFullCollection: false);
                    int gc0After = GC.CollectionCount(0);
                    int gc1After = GC.CollectionCount(1);
                    int gc2After = GC.CollectionCount(2);

                    double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
                    long allocated = Math.Max(0, memAfter - memBefore);

                    runTimes.Add(elapsedMs);
                    memoryAllocated.Add(allocated);

                    var detail = new RunDetail
                    {
                        RunNumber = run,
                        DurationMs = elapsedMs,
                        MemoryAllocatedBytes = allocated,
                        GCGen0Count = gc0After - gc0Before,
                        GCGen1Count = gc1After - gc1Before,
                        GCGen2Count = gc2After - gc2Before,
                        BytesProcessed = bytesProcessed
                    };
                    runReports.Add(detail);

                    Console.WriteLine($"Done in {elapsedMs:F2} ms (Allocated: {allocated / 1024.0 / 1024.0:F2} MB, Throughput: {(bytesProcessed / 1024.0 / 1024.0) / (elapsedMs / 1000.0):F2} MB/s)");
                }

                // Step 4: Calculate aggregate statistics
                var stats = CalculateStats(runTimes, memoryAllocated, snapshotCount, runReports[0].BytesProcessed);

                // Step 5: Save Reports (Markdown and JSON)
                string reportsDir = Path.Combine(Directory.GetCurrentDirectory(), "IntelliTrader.Backtesting", "Benchmarks", "reports");
                if (!Directory.Exists(reportsDir))
                {
                    Directory.CreateDirectory(reportsDir);
                }

                string reportTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string mdReportPath = Path.Combine(reportsDir, $"benchmark_report_{reportTimestamp}.md");
                string latestMdReportPath = Path.Combine(reportsDir, "benchmark_report.md");
                string jsonReportPath = Path.Combine(reportsDir, "benchmark_report.json");

                GenerateMarkdownReport(mdReportPath, stats, runReports, snapshotCount);
                // Also overwrite the latest benchmark_report.md for easy automated reading
                File.Copy(mdReportPath, latestMdReportPath, overwrite: true);

                GenerateJsonReport(jsonReportPath, stats, runReports, snapshotCount);

                Console.WriteLine("\n=========================================================");
                Console.WriteLine("🎉 Benchmark Suite Executed Successfully!");
                Console.WriteLine("=========================================================");
                Console.WriteLine($"Average Duration  : {stats.AverageDurationMs:F2} ms");
                Console.WriteLine($"Median Duration   : {stats.MedianDurationMs:F2} ms");
                Console.WriteLine($"Min Duration      : {stats.MinDurationMs:F2} ms");
                Console.WriteLine($"Max Duration      : {stats.MaxDurationMs:F2} ms");
                Console.WriteLine($"Throughput (files): {stats.ThroughputFilesPerSec:F2} snapshots/sec");
                Console.WriteLine($"Throughput (size) : {stats.ThroughputMBPerSec:F2} MB/sec");
                Console.WriteLine("=========================================================");
                Console.WriteLine($"Markdown Report saved to: {latestMdReportPath}");
                Console.WriteLine($"JSON Report saved to    : {jsonReportPath}");
                Console.WriteLine("=========================================================");

                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[!] Critical Error in Benchmark Suite: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                return 1;
            }
            finally
            {
                // Ensure absolute cleanup of the temporary folder
                try
                {
                    if (Directory.Exists(_tempPath))
                    {
                        Console.WriteLine("[*] Cleaning up temporary snapshot files...");
                        Directory.Delete(_tempPath, recursive: true);
                        Console.WriteLine("[+] Clean up completed successfully.");
                    }
                }
                catch (Exception cleanupEx)
                {
                    Console.WriteLine($"[!] Warning during cleanup: {cleanupEx.Message}");
                }
            }
        }

        private static void GenerateMockSnapshots(string basePath, int count)
        {
            string signalsPath = Path.Combine(basePath, "Signals");
            string tickersPath = Path.Combine(basePath, "Tickers");

            Directory.CreateDirectory(signalsPath);
            Directory.CreateDirectory(tickersPath);

            var pairs = new[] { "BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "ADAUSDT", "XRPUSDT", "DOTUSDT", "DOGEUSDT", "LTCUSDT", "LINKUSDT" };
            var random = new Random(42); // deterministic seed

            for (int i = 0; i < count; i++)
            {
                // Generate high-fidelity SignalData
                var signalsList = new List<SignalData>();
                foreach (var pair in pairs)
                {
                    signalsList.Add(new SignalData
                    {
                        Name = "RSI_Trailing",
                        Pair = pair,
                        Volume = random.Next(1000, 1000000),
                        VolumeChange = random.NextDouble() * 10 - 5,
                        Price = (decimal)(random.NextDouble() * 50000 + 10),
                        PriceChange = (decimal)(random.NextDouble() * 4 - 2),
                        Rating = random.NextDouble() * 2 - 1,
                        RatingChange = random.NextDouble() * 0.4 - 0.2,
                        Volatility = random.NextDouble() * 5
                    });
                }

                // Generate high-fidelity TickerData
                var tickersList = new List<TickerData>();
                foreach (var pair in pairs)
                {
                    decimal price = (decimal)(random.NextDouble() * 50000 + 10);
                    tickersList.Add(new TickerData
                    {
                        Pair = pair,
                        BidPrice = price * 0.999m,
                        AskPrice = price * 1.001m,
                        LastPrice = price
                    });
                }

                // Serialize using High Performance Custom Binary Serializer
                byte[] signalBytes = SerializeSignals(signalsList);
                byte[] tickerBytes = SerializeTickers(tickersList);

                string fileSuffix = $"{i:D6}.bin";
                File.WriteAllBytes(Path.Combine(signalsPath, $"signals_{fileSuffix}"), signalBytes);
                File.WriteAllBytes(Path.Combine(tickersPath, $"tickers_{fileSuffix}"), tickerBytes);
            }
        }

        private static long RunBenchmarkLoop(string basePath, bool isWarmUp)
        {
            string signalsPath = Path.Combine(basePath, "Signals");
            string tickersPath = Path.Combine(basePath, "Tickers");

            var signalFiles = Directory.GetFiles(signalsPath, "*.bin").OrderBy(f => f).ToList();
            var tickerFiles = Directory.GetFiles(tickersPath, "*.bin").OrderBy(f => f).ToList();

            long totalBytes = 0;
            int limit = isWarmUp ? Math.Min(50, signalFiles.Count) : signalFiles.Count;

            for (int i = 0; i < limit; i++)
            {
                // Simulate loading signals snapshot
                byte[] signalBytes = File.ReadAllBytes(signalFiles[i]);
                totalBytes += signalBytes.Length;

                var signalData = DeserializeSignals(signalBytes);
                // Evaluate/Force iteration
                foreach (var s in signalData)
                {
                    var sig = s.ToSignal();
                    var name = sig.Name;
                    var val = sig.Price;
                }

                // Simulate loading tickers snapshot
                byte[] tickerBytes = File.ReadAllBytes(tickerFiles[i]);
                totalBytes += tickerBytes.Length;

                var tickerData = DeserializeTickers(tickerBytes);
                // Evaluate/Force iteration
                foreach (var t in tickerData)
                {
                    var tick = t.ToTicker();
                    var pair = tick.Pair;
                    var last = tick.LastPrice;
                }
            }

            return totalBytes;
        }

        // Custom High-Performance Binary Serialization for Signals
        public static byte[] SerializeSignals(IEnumerable<SignalData> signals)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(signals.Count());
                foreach (var s in signals)
                {
                    bw.Write(s.Name ?? string.Empty);
                    bw.Write(s.Pair ?? string.Empty);
                    bw.Write(s.Volume ?? 0L);
                    bw.Write(s.VolumeChange ?? 0.0);
                    bw.Write(s.Price ?? 0m);
                    bw.Write(s.PriceChange ?? 0m);
                    bw.Write(s.Rating ?? 0.0);
                    bw.Write(s.RatingChange ?? 0.0);
                    bw.Write(s.Volatility ?? 0.0);
                }
                return ms.ToArray();
            }
        }

        public static List<SignalData> DeserializeSignals(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            using (var br = new BinaryReader(ms))
            {
                int count = br.ReadInt32();
                var list = new List<SignalData>(count);
                for (int i = 0; i < count; i++)
                {
                    list.Add(new SignalData
                    {
                        Name = br.ReadString(),
                        Pair = br.ReadString(),
                        Volume = br.ReadInt64(),
                        VolumeChange = br.ReadDouble(),
                        Price = br.ReadDecimal(),
                        PriceChange = br.ReadDecimal(),
                        Rating = br.ReadDouble(),
                        RatingChange = br.ReadDouble(),
                        Volatility = br.ReadDouble()
                    });
                }
                return list;
            }
        }

        // Custom High-Performance Binary Serialization for Tickers
        public static byte[] SerializeTickers(IEnumerable<TickerData> tickers)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(tickers.Count());
                foreach (var t in tickers)
                {
                    bw.Write(t.Pair ?? string.Empty);
                    bw.Write(t.BidPrice);
                    bw.Write(t.AskPrice);
                    bw.Write(t.LastPrice);
                }
                return ms.ToArray();
            }
        }

        public static List<TickerData> DeserializeTickers(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            using (var br = new BinaryReader(ms))
            {
                int count = br.ReadInt32();
                var list = new List<TickerData>(count);
                for (int i = 0; i < count; i++)
                {
                    list.Add(new TickerData
                    {
                        Pair = br.ReadString(),
                        BidPrice = br.ReadDecimal(),
                        AskPrice = br.ReadDecimal(),
                        LastPrice = br.ReadDecimal()
                    });
                }
                return list;
            }
        }

        private static BenchmarkStats CalculateStats(List<double> runTimes, List<long> memoryAllocated, int snapshotCount, long bytesProcessed)
        {
            var stats = new BenchmarkStats();
            stats.AverageDurationMs = runTimes.Average();

            var sortedTimes = runTimes.OrderBy(t => t).ToList();
            int mid = sortedTimes.Count / 2;
            stats.MedianDurationMs = (sortedTimes.Count % 2 != 0) ? sortedTimes[mid] : (sortedTimes[mid - 1] + sortedTimes[mid]) / 2.0;

            stats.MinDurationMs = runTimes.Min();
            stats.MaxDurationMs = runTimes.Max();

            double sumOfSquares = runTimes.Select(val => (val - stats.AverageDurationMs) * (val - stats.AverageDurationMs)).Sum();
            stats.StdDevDurationMs = Math.Sqrt(sumOfSquares / runTimes.Count);

            stats.AverageMemoryAllocatedBytes = (long)memoryAllocated.Average();

            double averageSec = stats.AverageDurationMs / 1000.0;
            stats.ThroughputFilesPerSec = snapshotCount / averageSec;
            stats.ThroughputMBPerSec = (bytesProcessed / 1024.0 / 1024.0) / averageSec;

            return stats;
        }

        private static void GenerateMarkdownReport(string outputPath, BenchmarkStats stats, List<RunDetail> runs, int snapshotCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# IntelliTrader Backtesting Benchmark Report");
            sb.AppendLine();
            sb.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"OS Environment: {Environment.OSVersion}");
            sb.AppendLine($"Processor Count: {Environment.ProcessorCount}");
            sb.AppendLine($"Runtime Version: {Environment.Version}");
            sb.AppendLine();
            sb.AppendLine("## Summary Statistics");
            sb.AppendLine();
            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("| --- | --- |");
            sb.AppendLine($"| **Total Snapshots Evaluated** | {snapshotCount} signals & {snapshotCount} tickers |");
            sb.AppendLine($"| **Average Execution Time** | {stats.AverageDurationMs:F2} ms |");
            sb.AppendLine($"| **Median Execution Time** | {stats.MedianDurationMs:F2} ms |");
            sb.AppendLine($"| **Minimum Execution Time** | {stats.MinDurationMs:F2} ms |");
            sb.AppendLine($"| **Maximum Execution Time** | {stats.MaxDurationMs:F2} ms |");
            sb.AppendLine($"| **Standard Deviation** | {stats.StdDevDurationMs:F2} ms |");
            sb.AppendLine($"| **Throughput** | {stats.ThroughputFilesPerSec:F2} snapshots/sec |");
            sb.AppendLine($"| **Data Throughput** | {stats.ThroughputMBPerSec:F2} MB/sec |");
            sb.AppendLine($"| **Avg Memory Allocation** | {stats.AverageMemoryAllocatedBytes / 1024.0 / 1024.0:F2} MB |");
            sb.AppendLine();
            sb.AppendLine("## Iteration Details");
            sb.AppendLine();
            sb.AppendLine("| Run # | Duration (ms) | Allocated Memory (MB) | GC Gen0 | GC Gen1 | GC Gen2 |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- |");

            foreach (var run in runs)
            {
                sb.AppendLine($"| {run.RunNumber} | {run.DurationMs:F2} | {run.MemoryAllocatedBytes / 1024.0 / 1024.0:F2} | {run.GCGen0Count} | {run.GCGen1Count} | {run.GCGen2Count} |");
            }
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine("*Performance benchmark executed autonomously by Bolt ⚡.*");

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        private static void GenerateJsonReport(string outputPath, BenchmarkStats stats, List<RunDetail> runs, int snapshotCount)
        {
            var report = new
            {
                Metadata = new
                {
                    GeneratedAt = DateTime.UtcNow.ToString("o"),
                    OS = Environment.OSVersion.ToString(),
                    ProcessorCount = Environment.ProcessorCount,
                    Runtime = Environment.Version.ToString(),
                    SnapshotCount = snapshotCount
                },
                Statistics = stats,
                Runs = runs
            };

            string json = JsonConvert.SerializeObject(report, Formatting.Indented);
            File.WriteAllText(outputPath, json, Encoding.UTF8);
        }
    }

    public class RunDetail
    {
        public int RunNumber { get; set; }
        public double DurationMs { get; set; }
        public long MemoryAllocatedBytes { get; set; }
        public int GCGen0Count { get; set; }
        public int GCGen1Count { get; set; }
        public int GCGen2Count { get; set; }
        public long BytesProcessed { get; set; }
    }

    public class BenchmarkStats
    {
        public double AverageDurationMs { get; set; }
        public double MedianDurationMs { get; set; }
        public double MinDurationMs { get; set; }
        public double MaxDurationMs { get; set; }
        public double StdDevDurationMs { get; set; }
        public long AverageMemoryAllocatedBytes { get; set; }
        public double ThroughputFilesPerSec { get; set; }
        public double ThroughputMBPerSec { get; set; }
    }
}
