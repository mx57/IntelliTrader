using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Globalization;

namespace BacktestingBenchmarkSuite
{
    public static class ReportGenerator
    {
        public static void GenerateHtmlReport(string outputPath, BenchmarkResult result)
        {
            // Сбор информации о системе
            string os = RuntimeInformation.OSDescription;
            string architecture = RuntimeInformation.OSArchitecture.ToString();
            string framework = RuntimeInformation.FrameworkDescription;
            int processorCount = Environment.ProcessorCount;
            string machineName = Environment.MachineName;

            // Расчеты для визуализации бенчмарка (SVG-графики)
            double maxThroughput = Math.Max(
                result.SerializationThroughput,
                Math.Max(result.DeserializationThroughput, result.ProcessingThroughput)
            );

            double serBarWidth = maxThroughput > 0 ? (result.SerializationThroughput / maxThroughput) * 100.0 : 0.0;
            double deserBarWidth = maxThroughput > 0 ? (result.DeserializationThroughput / maxThroughput) * 100.0 : 0.0;
            double procBarWidth = maxThroughput > 0 ? (result.ProcessingThroughput / maxThroughput) * 100.0 : 0.0;

            // Форматирование JSON массивов для передачи во фронтенд
            var tradesJsonBuilder = new StringBuilder();
            tradesJsonBuilder.Append("[");
            for (int i = 0; i < result.Trades.Count; i++)
            {
                var t = result.Trades[i];
                tradesJsonBuilder.Append($@"{{""pair"":""{t.Pair}"",""type"":""{t.Type}"",""price"":{t.Price.ToString(CultureInfo.InvariantCulture)},""amount"":{t.Amount.ToString(CultureInfo.InvariantCulture)},""cost"":{t.Cost.ToString(CultureInfo.InvariantCulture)},""profitPct"":{t.ProfitPct.ToString(CultureInfo.InvariantCulture)},""profitUsdt"":{t.ProfitUsdt.ToString(CultureInfo.InvariantCulture)},""timestamp"":""{t.Timestamp:yyyy-MM-dd HH:mm:ss}"",""balance"":{t.ResultingBalance.ToString(CultureInfo.InvariantCulture)}}}");
                if (i < result.Trades.Count - 1) tradesJsonBuilder.Append(",");
            }
            tradesJsonBuilder.Append("]");
            string tradesJson = tradesJsonBuilder.ToString();

            var equityJsonBuilder = new StringBuilder();
            equityJsonBuilder.Append("[");
            for (int i = 0; i < result.EquityCurve.Count; i++)
            {
                var eq = result.EquityCurve[i];
                equityJsonBuilder.Append($@"{{""time"":""{eq.Timestamp:yyyy-MM-dd HH:mm:ss}"",""balance"":{eq.Balance.ToString(CultureInfo.InvariantCulture)}}}");
                if (i < result.EquityCurve.Count - 1) equityJsonBuilder.Append(",");
            }
            equityJsonBuilder.Append("]");
            string equityJson = equityJsonBuilder.ToString();

            string html = """
<!DOCTYPE html>
<html lang="ru">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Отчет о производительности бэктестинга — IntelliTrader</title>
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    <style>
        :root {
            --bg-color: #0b0f19;
            --card-bg: #151d30;
            --card-border: #222f4c;
            --accent-color: #2563eb;
            --accent-hover: #3b82f6;
            --success-color: #10b981;
            --success-bg: rgba(16, 185, 129, 0.1);
            --danger-color: #ef4444;
            --danger-bg: rgba(239, 68, 68, 0.1);
            --warning-color: #f59e0b;
            --warning-bg: rgba(245, 158, 11, 0.1);
            --text-primary: #f1f5f9;
            --text-secondary: #94a3b8;
            --text-muted: #64748b;
            --border-color: #1e293b;
        }

        body {
            background-color: var(--bg-color);
            color: var(--text-primary);
            font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            margin: 0;
            padding: 0;
            line-height: 1.5;
        }

        .container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 40px 20px;
        }

        header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            border-bottom: 1px solid var(--border-color);
            padding-bottom: 24px;
            margin-bottom: 32px;
            flex-wrap: wrap;
            gap: 20px;
        }

        .header-title h1 {
            font-size: 2rem;
            font-weight: 800;
            margin: 0;
            background: linear-gradient(135deg, #3b82f6 0%, #10b981 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }

        .header-title p {
            color: var(--text-secondary);
            margin: 4px 0 0 0;
            font-size: 0.95rem;
        }

        .timestamp-badge {
            background-color: var(--card-bg);
            border: 1px solid var(--card-border);
            padding: 8px 16px;
            border-radius: 8px;
            font-size: 0.85rem;
            color: var(--text-secondary);
            font-family: monospace;
        }

        /* Навигация по вкладкам */
        .tabs {
            display: flex;
            gap: 12px;
            margin-bottom: 32px;
            border-bottom: 1px solid var(--border-color);
            padding-bottom: 12px;
        }

        .tab-btn {
            background: none;
            border: none;
            color: var(--text-secondary);
            padding: 10px 20px;
            font-size: 0.95rem;
            font-weight: 600;
            cursor: pointer;
            border-radius: 6px;
            transition: all 0.2s ease;
        }

        .tab-btn:hover {
            color: var(--text-primary);
            background-color: rgba(255, 255, 255, 0.05);
        }

        .tab-btn.active {
            color: #fff;
            background-color: var(--accent-color);
        }

        .tab-content {
            display: none;
        }

        .tab-content.active {
            display: block;
        }

        /* Сетка показателей */
        .metrics-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
            gap: 20px;
            margin-bottom: 32px;
        }

        .metric-card {
            background-color: var(--card-bg);
            border: 1px solid var(--card-border);
            border-radius: 12px;
            padding: 20px;
            transition: transform 0.2s ease, box-shadow 0.2s ease;
        }

        .metric-card:hover {
            transform: translateY(-2px);
            box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.3);
        }

        .metric-label {
            font-size: 0.85rem;
            font-weight: 600;
            color: var(--text-secondary);
            text-transform: uppercase;
            letter-spacing: 0.05em;
        }

        .metric-value {
            font-size: 1.75rem;
            font-weight: 700;
            margin-top: 8px;
            font-family: 'Courier New', Courier, monospace;
        }

        .text-success { color: var(--success-color); }
        .text-danger { color: var(--danger-color); }
        .text-warning { color: var(--warning-color); }
        .text-info { color: #3b82f6; }

        /* Графики */
        .chart-card {
            background-color: var(--card-bg);
            border: 1px solid var(--card-border);
            border-radius: 12px;
            padding: 24px;
            margin-bottom: 32px;
        }

        .chart-header {
            margin-bottom: 20px;
        }

        .chart-title {
            font-size: 1.15rem;
            font-weight: 700;
            margin: 0;
        }

        /* Интерактивная таблица */
        .table-controls {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 20px;
            flex-wrap: wrap;
            gap: 16px;
        }

        .search-input {
            background-color: var(--bg-color);
            border: 1px solid var(--card-border);
            color: var(--text-primary);
            padding: 10px 16px;
            border-radius: 8px;
            font-size: 0.9rem;
            width: 300px;
            outline: none;
            transition: border-color 0.2s;
        }

        .search-input:focus {
            border-color: var(--accent-color);
        }

        .filter-group {
            display: flex;
            gap: 10px;
        }

        .filter-select {
            background-color: var(--bg-color);
            border: 1px solid var(--card-border);
            color: var(--text-primary);
            padding: 10px 14px;
            border-radius: 8px;
            font-size: 0.9rem;
            outline: none;
            cursor: pointer;
        }

        .data-table-wrapper {
            overflow-x: auto;
            border-radius: 12px;
            border: 1px solid var(--card-border);
            background-color: var(--card-bg);
        }

        .data-table {
            width: 100%;
            border-collapse: collapse;
            text-align: left;
            font-size: 0.9rem;
        }

        .data-table th {
            background-color: rgba(255, 255, 255, 0.02);
            padding: 14px 18px;
            font-weight: 600;
            color: var(--text-secondary);
            border-bottom: 1px solid var(--card-border);
            text-transform: uppercase;
            font-size: 0.75rem;
            letter-spacing: 0.05em;
        }

        .data-table td {
            padding: 14px 18px;
            border-bottom: 1px solid var(--card-border);
            vertical-align: middle;
        }

        .data-table tbody tr:last-child td {
            border-bottom: none;
        }

        .data-table tbody tr:hover {
            background-color: rgba(255, 255, 255, 0.01);
        }

        .badge {
            display: inline-block;
            padding: 4px 8px;
            border-radius: 6px;
            font-size: 0.75rem;
            font-weight: 700;
            text-transform: uppercase;
        }

        .badge-buy {
            background-color: rgba(59, 130, 246, 0.15);
            color: #60a5fa;
            border: 1px solid rgba(59, 130, 246, 0.2);
        }

        .badge-sell-tp {
            background-color: var(--success-bg);
            color: var(--success-color);
            border: 1px solid rgba(16, 185, 129, 0.2);
        }

        .badge-sell-sl {
            background-color: var(--danger-bg);
            color: var(--danger-color);
            border: 1px solid rgba(239, 68, 68, 0.2);
        }

        .badge-dca {
            background-color: var(--warning-bg);
            color: var(--warning-color);
            border: 1px solid rgba(245, 158, 11, 0.2);
        }

        /* Пагинация */
        .pagination {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-top: 20px;
        }

        .pagination-info {
            font-size: 0.85rem;
            color: var(--text-secondary);
        }

        .pagination-buttons {
            display: flex;
            gap: 8px;
        }

        .page-btn {
            background-color: var(--card-bg);
            border: 1px solid var(--card-border);
            color: var(--text-primary);
            padding: 8px 14px;
            border-radius: 6px;
            cursor: pointer;
            font-size: 0.85rem;
            transition: all 0.2s;
        }

        .page-btn:hover:not(:disabled) {
            background-color: var(--border-color);
            border-color: var(--text-secondary);
        }

        .page-btn:disabled {
            opacity: 0.4;
            cursor: not-allowed;
        }

        /* Стилизация графиков производительности */
        .chart-bar-group {
            margin-bottom: 20px;
        }

        .chart-bar-label {
            display: flex;
            justify-content: space-between;
            margin-bottom: 6px;
            font-weight: 500;
        }

        .chart-bar-outer {
            background-color: #0c101d;
            border-radius: 6px;
            height: 24px;
            width: 100%;
            overflow: hidden;
            border: 1px solid var(--card-border);
        }

        .chart-bar-inner {
            height: 100%;
            border-radius: 5px;
            transition: width 1s ease-in-out;
        }

        .bar-ser {
            background: linear-gradient(90deg, #f87171, #ef4444);
            width: {SER_BAR_WIDTH}%;
        }

        .bar-deser {
            background: linear-gradient(90deg, #60a5fa, #3b82f6);
            width: {DESER_BAR_WIDTH}%;
        }

        .bar-proc {
            background: linear-gradient(90deg, #34d399, #10b981);
            width: {PROC_BAR_WIDTH}%;
        }

        /* Сетки системной информации */
        .info-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
            gap: 20px;
            margin-top: 32px;
        }

        .info-table {
            width: 100%;
            border-collapse: collapse;
        }

        .info-table td {
            padding: 12px 16px;
            border-bottom: 1px solid var(--card-border);
            font-size: 0.9rem;
        }

        .info-table td:first-child {
            color: var(--text-secondary);
            font-weight: 500;
            width: 40%;
        }

        .info-table td:last-child {
            font-family: monospace;
            font-weight: 600;
        }

        .tips-list {
            padding-left: 20px;
            margin: 0;
        }

        .tips-list li {
            margin-bottom: 12px;
            color: #cbd5e1;
        }

        footer {
            text-align: center;
            margin-top: 50px;
            color: var(--text-secondary);
            font-size: 0.9rem;
            border-top: 1px solid var(--border-color);
            padding-top: 20px;
        }
    </style>
</head>
<body>
    <div class="container">
        <header>
            <div class="header-title">
                <h1>Отчет о бэктестинге IntelliTrader</h1>
                <p>Результаты симуляции торговых стратегий и замеры производительности</p>
            </div>
            <div class="timestamp-badge">
                Сгенерировано: {GENERATED_TIME}
            </div>
        </header>

        <div class="tabs">
            <button class="tab-btn active" onclick="switchTab('backtest')">📊 Результаты Торговли</button>
            <button class="tab-btn" onclick="switchTab('performance')">⚡ Производительность & Лимиты</button>
            <button class="tab-btn" onclick="switchTab('trades')">📜 Журнал Сделок</button>
        </div>

        <!-- ВКЛАДКА БЭКТЕСТИНГА -->
        <div id="tab-backtest" class="tab-content active">
            <div class="metrics-grid">
                <div class="metric-card">
                    <div class="metric-label">Начальный Баланс</div>
                    <div class="metric-value" style="color: var(--text-primary);">{INITIAL_BALANCE} USDT</div>
                </div>
                <div class="metric-card">
                    <div class="metric-label">Конечный Баланс</div>
                    <div class="metric-value" style="color: #38bdf8;">{FINAL_BALANCE} USDT</div>
                </div>
                <div class="metric-card">
                    <div class="metric-label">Общая Доходность</div>
                    <div class="metric-value {TOTAL_PROFIT_CLASS}">
                        {TOTAL_PROFIT_PCT}%
                    </div>
                </div>
                <div class="metric-card">
                    <div class="metric-label">Макс. Просадка</div>
                    <div class="metric-value text-danger">{MAX_DRAWDOWN_PCT}%</div>
                </div>
                <div class="metric-card">
                    <div class="metric-label">Всего Сделок / Win Rate</div>
                    <div class="metric-value text-info" style="font-size: 1.5rem;">
                        {TOTAL_TRADES} / {WIN_RATE_PCT}%
                    </div>
                </div>
                <div class="metric-card">
                    <div class="metric-label">Profit Factor</div>
                    <div class="metric-value text-warning">{PROFIT_FACTOR}</div>
                </div>
            </div>

            <!-- График баланса/эквити -->
            <div class="chart-card">
                <div class="chart-header">
                    <h3 class="chart-title">Динамика портфеля (Equity Curve)</h3>
                </div>
                <div style="height: 350px; position: relative;">
                    <canvas id="equityChart"></canvas>
                </div>
            </div>

            <!-- Разделение по парам -->
            <div class="chart-card">
                <div class="chart-header">
                    <h3 class="chart-title">Показатели по торговым парам</h3>
                </div>
                <div class="data-table-wrapper">
                    <table class="data-table">
                        <thead>
                            <tr>
                                <th>Пара</th>
                                <th>Всего сделок</th>
                                <th>Прибыльные сделки</th>
                                <th>Win Rate %</th>
                                <th>Чистый Профит (USDT)</th>
                            </tr>
                        </thead>
                        <tbody id="pairsTableBody">
                            <!-- Заполняется динамически посредством JS -->
                        </tbody>
                    </table>
                </div>
            </div>
        </div>

        <!-- ВКЛАДКА ПРОИЗВОДИТЕЛЬНОСТИ (Оригинальные бенчмарки) -->
        <div id="tab-performance" class="tab-content">
            <div class="metrics-grid">
                <div class="metric-card">
                    <div class="metric-label">Снимков обработано</div>
                    <div class="metric-value text-info">{SNAPSHOT_COUNT}</div>
                </div>
                <div class="metric-card">
                    <div class="metric-label">Период симуляции</div>
                    <div class="metric-value">{SIMULATED_MONTHS} мес.</div>
                </div>
                <div class="metric-card">
                    <div class="metric-label">Размер датасета</div>
                    <div class="metric-value">{TOTAL_SIZE_MB} МБ</div>
                </div>
                <div class="metric-card">
                    <div class="metric-label">Ускорение Бэктеста</div>
                    <div class="metric-value text-success">{SPEEDUP_FACTOR}x</div>
                </div>
            </div>

            <!-- Сравнение пропускной способности -->
            <div class="chart-card">
                <div class="chart-header">
                    <h3 class="chart-title">Пропускная способность фаз обработки (снимков в секунду)</h3>
                </div>

                <div class="chart-bar-group">
                    <div class="chart-bar-label">
                        <span>Сериализация (Запись снимков в бинарный поток)</span>
                        <span class="text-danger">{SERIALIZATION_THROUGHPUT} оп/сек</span>
                    </div>
                    <div class="chart-bar-outer">
                        <div class="chart-bar-inner bar-ser"></div>
                    </div>
                </div>

                <div class="chart-bar-group">
                    <div class="chart-bar-label">
                        <span>Десериализация (Загрузка снимков в C# объекты)</span>
                        <span class="text-info">{DESERIALIZATION_THROUGHPUT} оп/сек</span>
                    </div>
                    <div class="chart-bar-outer">
                        <div class="chart-bar-inner bar-deser"></div>
                    </div>
                </div>

                <div class="chart-bar-group">
                    <div class="chart-bar-label">
                        <span>Движок Бэктестинга (Логика торговых правил)</span>
                        <span class="text-success">{PROCESSING_THROUGHPUT} оп/сек</span>
                    </div>
                    <div class="chart-bar-outer">
                        <div class="chart-bar-inner bar-proc"></div>
                    </div>
                </div>
            </div>

            <div class="info-grid">
                <!-- Память -->
                <div class="chart-card" style="margin-bottom: 0;">
                    <div class="chart-header">
                        <h3 class="chart-title">Использование памяти и GC</h3>
                    </div>
                    <table class="info-table">
                        <tr>
                            <td>Выделено в потоке:</td>
                            <td>{BYTES_ALLOCATED} МБ</td>
                        </tr>
                        <tr>
                            <td>Сборщик Gen 0:</td>
                            <td>{GC_GEN_0}</td>
                        </tr>
                        <tr>
                            <td>Сборщик Gen 1:</td>
                            <td>{GC_GEN_1}</td>
                        </tr>
                        <tr>
                            <td>Сборщик Gen 2:</td>
                            <td>{GC_GEN_2}</td>
                        </tr>
                    </table>
                </div>

                <!-- Система -->
                <div class="chart-card" style="margin-bottom: 0;">
                    <div class="chart-header">
                        <h3 class="chart-title">Информация об окружении</h3>
                    </div>
                    <table class="info-table">
                        <tr>
                            <td>Имя хоста:</td>
                            <td>{MACHINE_NAME}</td>
                        </tr>
                        <tr>
                            <td>Операционная система:</td>
                            <td>{OS} ({ARCHITECTURE})</td>
                        </tr>
                        <tr>
                            <td>Рантайм .NET:</td>
                            <td>{FRAMEWORK}</td>
                        </tr>
                        <tr>
                            <td>Процессор (ядер):</td>
                            <td>{PROCESSOR_COUNT} Cores</td>
                        </tr>
                    </table>
                </div>
            </div>
        </div>

        <!-- ВКЛАДКА ЖУРНАЛА СДЕЛОК -->
        <div id="tab-trades" class="tab-content">
            <div class="chart-card">
                <div class="table-controls">
                    <input type="text" id="searchInput" class="search-input" placeholder="Поиск по паре или типу...">

                    <div class="filter-group">
                        <select id="typeFilter" class="filter-select">
                            <option value="ALL">Все типы</option>
                            <option value="BUY">Покупки (BUY)</option>
                            <option value="SELL">Продажи (SELL)</option>
                            <option value="DCA">Усреднения (DCA)</option>
                        </select>
                        <select id="outcomeFilter" class="filter-select">
                            <option value="ALL">Все исходы</option>
                            <option value="WIN">Прибыльные</option>
                            <option value="LOSS">Убыточные</option>
                        </select>
                        <select id="pageSizeSelect" class="filter-select">
                            <option value="10">10 строк</option>
                            <option value="25">25 строк</option>
                            <option value="50">50 строк</option>
                        </select>
                    </div>
                </div>

                <div class="data-table-wrapper">
                    <table class="data-table">
                        <thead>
                            <tr>
                                <th>Время</th>
                                <th>Торговая Пара</th>
                                <th>Тип Ордера</th>
                                <th>Цена (USDT)</th>
                                <th>Количество</th>
                                <th>Стоимость (USDT)</th>
                                <th>Прибыль (%)</th>
                                <th>Баланс (USDT)</th>
                            </tr>
                        </thead>
                        <tbody id="tradesTableBody">
                            <!-- Заполняется динамически посредством JS -->
                        </tbody>
                    </table>
                </div>

                <div class="pagination">
                    <div class="pagination-info" id="paginationInfo">
                        Записи 0-0 из 0
                    </div>
                    <div class="pagination-buttons">
                        <button class="page-btn" id="btnPrev" disabled>◀ Назад</button>
                        <button class="page-btn" id="btnNext" disabled>Вперед ▶</button>
                    </div>
                </div>
            </div>
        </div>

        <footer>
            <p>Разработано в рамках пакета IntelliTrader.Backtesting.Benchmarks. Сборка Bolt ⚡.</p>
        </footer>
    </div>

    <script>
        // Инициализация переданных данных из C#
        const trades = {TRADES_JSON};
        const equityPoints = {EQUITY_JSON};

        function switchTab(tabId) {
            // Переключение кнопок
            document.querySelectorAll('.tab-btn').forEach(btn => btn.classList.remove('active'));
            const targetBtn = Array.from(document.querySelectorAll('.tab-btn')).find(btn => btn.innerText.toLowerCase().includes(tabId === 'backtest' ? 'результаты' : tabId === 'performance' ? 'производительность' : 'журнал'));
            if (targetBtn) targetBtn.classList.add('active');

            // Переключение контента
            document.querySelectorAll('.tab-content').forEach(content => content.classList.remove('active'));
            document.getElementById('tab-' + tabId).classList.add('active');
        }

        // Рендеринг графика Equity
        document.addEventListener('DOMContentLoaded', () => {
            const ctx = document.getElementById('equityChart').getContext('2d');
            new Chart(ctx, {
                type: 'line',
                data: {
                    labels: equityPoints.map(p => p.time),
                    datasets: [{
                        label: 'Баланс портфеля (USDT)',
                        data: equityPoints.map(p => p.balance),
                        borderColor: '#10b981',
                        backgroundColor: 'rgba(16, 185, 129, 0.05)',
                        borderWidth: 2.5,
                        fill: true,
                        tension: 0.15,
                        pointRadius: equityPoints.length > 300 ? 0 : 2,
                        pointHoverRadius: 6,
                        pointBackgroundColor: '#10b981'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    scales: {
                        x: {
                            grid: { color: 'rgba(255, 255, 255, 0.04)' },
                            ticks: { color: '#94a3b8', maxTicksLimit: 12 }
                        },
                        y: {
                            grid: { color: 'rgba(255, 255, 255, 0.04)' },
                            ticks: { color: '#94a3b8' }
                        }
                    },
                    plugins: {
                        legend: { display: false },
                        tooltip: {
                            backgroundColor: '#151d30',
                            titleColor: '#f1f5f9',
                            bodyColor: '#38bdf8',
                            borderColor: '#222f4c',
                            borderWidth: 1,
                            padding: 10,
                            callbacks: {
                                label: function(context) {
                                    return 'Баланс: ' + context.parsed.y.toFixed(2) + ' USDT';
                                }
                            }
                        }
                    }
                }
            });

            // Вычисление и вывод статистики по парам
            calculatePairsStats();

            // Рендеринг таблицы сделок
            renderTradesTable();
        });

        function calculatePairsStats() {
            const pairsStats = {};
            trades.forEach(t => {
                if (!t.type.startsWith('SELL')) return;
                if (!pairsStats[t.pair]) {
                    pairsStats[t.pair] = { trades: 0, wins: 0, profit: 0 };
                }
                pairsStats[t.pair].trades++;
                if (t.profitUsdt > 0) pairsStats[t.pair].wins++;
                pairsStats[t.pair].profit += t.profitUsdt;
            });

            const pairsTableBody = document.getElementById('pairsTableBody');
            pairsTableBody.innerHTML = '';

            const keys = Object.keys(pairsStats);
            if (keys.length === 0) {
                pairsTableBody.innerHTML = '<tr><td colspan="5" style="text-align: center; color: var(--text-muted); padding: 20px;">Нет закрытых сделок для расчета</td></tr>';
                return;
            }

            keys.forEach(p => {
                const stat = pairsStats[p];
                const winRate = stat.trades > 0 ? (stat.wins / stat.trades * 100).toFixed(1) : '0.0';
                const profitClass = stat.profit >= 0 ? 'text-success' : 'text-danger';
                const sign = stat.profit >= 0 ? '+' : '';
                pairsTableBody.innerHTML += `
                    <tr>
                        <td><strong>${p}</strong></td>
                        <td>${stat.trades}</td>
                        <td>${stat.wins}</td>
                        <td>${winRate}%</td>
                        <td class="${profitClass}"><strong>${sign}${stat.profit.toFixed(2)} USDT</strong></td>
                    </tr>
                `;
            });
        }

        // Логика пагинации и поиска журнала сделок
        let currentPage = 1;
        let rowsPerPage = 10;
        let filteredTrades = [...trades];

        function renderTradesTable() {
            const start = (currentPage - 1) * rowsPerPage;
            const end = start + rowsPerPage;
            const paginatedTrades = filteredTrades.slice(start, end);

            const tbody = document.getElementById('tradesTableBody');
            tbody.innerHTML = '';

            if (paginatedTrades.length === 0) {
                tbody.innerHTML = '<tr><td colspan="8" style="text-align: center; color: var(--text-muted); padding: 30px;">Сделки не найдены</td></tr>';
                document.getElementById('btnPrev').disabled = true;
                document.getElementById('btnNext').disabled = true;
                document.getElementById('paginationInfo').innerText = 'Записи 0-0 из 0';
                return;
            }

            paginatedTrades.forEach((t, index) => {
                let badgeClass = 'badge-buy';
                if (t.type.includes('Take Profit')) badgeClass = 'badge-sell-tp';
                else if (t.type.includes('Stop Loss')) badgeClass = 'badge-sell-sl';
                else if (t.type.includes('DCA')) badgeClass = 'badge-dca';

                const profitClass = t.profitUsdt > 0 ? 'text-success' : (t.profitUsdt < 0 ? 'text-danger' : '');
                const profitText = t.profitUsdt !== 0
                    ? `${t.profitUsdt > 0 ? '+' : ''}${t.profitUsdt.toFixed(2)} USDT (${t.profitPct.toFixed(2)}%)`
                    : '—';

                tbody.innerHTML += `
                    <tr>
                        <td style="font-family: monospace; color: var(--text-secondary);">${t.timestamp}</td>
                        <td><strong>${t.pair}</strong></td>
                        <td><span class="badge ${badgeClass}">${t.type}</span></td>
                        <td style="font-family: monospace;">${t.price.toFixed(4)}</td>
                        <td style="font-family: monospace;">${t.amount.toFixed(6)}</td>
                        <td style="font-family: monospace;">${t.cost.toFixed(2)}</td>
                        <td class="${profitClass}" style="font-family: monospace; font-weight: 600;">${profitText}</td>
                        <td style="font-family: monospace; font-weight: 600;">${t.balance.toFixed(2)}</td>
                    </tr>
                `;
            });

            document.getElementById('paginationInfo').innerText = `Записи ${start + 1}-${Math.min(end, filteredTrades.length)} из ${filteredTrades.length}`;
            document.getElementById('btnPrev').disabled = currentPage === 1;
            document.getElementById('btnNext').disabled = end >= filteredTrades.length;
        }

        function filterTrades() {
            const query = document.getElementById('searchInput').value.toLowerCase();
            const typeFilter = document.getElementById('typeFilter').value;
            const outcomeFilter = document.getElementById('outcomeFilter').value;

            filteredTrades = trades.filter(t => {
                const matchesSearch = t.pair.toLowerCase().includes(query) || t.type.toLowerCase().includes(query);

                let matchesType = true;
                if (typeFilter === 'BUY') matchesType = t.type.startsWith('BUY');
                else if (typeFilter === 'SELL') matchesType = t.type.startsWith('SELL');
                else if (typeFilter === 'DCA') matchesType = t.type.includes('DCA');

                let matchesOutcome = true;
                if (outcomeFilter === 'WIN') matchesOutcome = t.type.startsWith('SELL') && t.profitUsdt > 0;
                else if (outcomeFilter === 'LOSS') matchesOutcome = t.type.startsWith('SELL') && t.profitUsdt <= 0;

                return matchesSearch && matchesType && matchesOutcome;
            });

            currentPage = 1;
            renderTradesTable();
        }

        // Слушатели событий управления таблицей
        document.getElementById('searchInput').addEventListener('input', filterTrades);
        document.getElementById('typeFilter').addEventListener('change', filterTrades);
        document.getElementById('outcomeFilter').addEventListener('change', filterTrades);
        document.getElementById('pageSizeSelect').addEventListener('change', (e) => {
            rowsPerPage = parseInt(e.target.value);
            currentPage = 1;
            renderTradesTable();
        });

        document.getElementById('btnPrev').addEventListener('click', () => {
            if (currentPage > 1) {
                currentPage--;
                renderTradesTable();
            }
        });

        document.getElementById('btnNext').addEventListener('click', () => {
            if ((currentPage * rowsPerPage) < filteredTrades.length) {
                currentPage++;
                renderTradesTable();
            }
        });
    </script>
</body>
</html>
""";

            // Выполнение замен placeholders
            html = html
                .Replace("{GENERATED_TIME}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .Replace("{SER_BAR_WIDTH}", serBarWidth.ToString(CultureInfo.InvariantCulture))
                .Replace("{DESER_BAR_WIDTH}", deserBarWidth.ToString(CultureInfo.InvariantCulture))
                .Replace("{PROC_BAR_WIDTH}", procBarWidth.ToString(CultureInfo.InvariantCulture))
                .Replace("{INITIAL_BALANCE}", result.InitialBalance.ToString("N2", CultureInfo.InvariantCulture))
                .Replace("{FINAL_BALANCE}", result.FinalBalance.ToString("N2", CultureInfo.InvariantCulture))
                .Replace("{TOTAL_PROFIT_PCT}", (result.TotalProfitPct >= 0 ? "+" : "") + result.TotalProfitPct.ToString("F2", CultureInfo.InvariantCulture))
                .Replace("{TOTAL_PROFIT_CLASS}", result.TotalProfitPct >= 0 ? "text-success" : "text-danger")
                .Replace("{MAX_DRAWDOWN_PCT}", result.MaxDrawdownPct.ToString("F2", CultureInfo.InvariantCulture))
                .Replace("{TOTAL_TRADES}", result.TotalTrades.ToString())
                .Replace("{WIN_RATE_PCT}", result.WinRatePct.ToString("F1", CultureInfo.InvariantCulture))
                .Replace("{PROFIT_FACTOR}", result.ProfitFactor.ToString("F2", CultureInfo.InvariantCulture))
                .Replace("{SNAPSHOT_COUNT}", result.SnapshotCount.ToString("N0"))
                .Replace("{SIMULATED_MONTHS}", result.SimulatedMonths.ToString())
                .Replace("{TOTAL_SIZE_MB}", result.TotalSizeMb.ToString("F2", CultureInfo.InvariantCulture))
                .Replace("{SPEEDUP_FACTOR}", result.SpeedupFactor.ToString("N0"))
                .Replace("{SERIALIZATION_THROUGHPUT}", result.SerializationThroughput.ToString("N0"))
                .Replace("{DESERIALIZATION_THROUGHPUT}", result.DeserializationThroughput.ToString("N0"))
                .Replace("{PROCESSING_THROUGHPUT}", result.ProcessingThroughput.ToString("N0"))
                .Replace("{BYTES_ALLOCATED}", (result.BytesAllocated / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture))
                .Replace("{GC_GEN_0}", result.GcCollectionsGen0.ToString())
                .Replace("{GC_GEN_1}", result.GcCollectionsGen1.ToString())
                .Replace("{GC_GEN_2}", result.GcCollectionsGen2.ToString())
                .Replace("{MACHINE_NAME}", machineName)
                .Replace("{OS}", os)
                .Replace("{ARCHITECTURE}", architecture)
                .Replace("{FRAMEWORK}", framework)
                .Replace("{PROCESSOR_COUNT}", processorCount.ToString())
                .Replace("{TRADES_JSON}", tradesJson)
                .Replace("{EQUITY_JSON}", equityJson);

            File.WriteAllText(outputPath, html);
        }
    }
}
