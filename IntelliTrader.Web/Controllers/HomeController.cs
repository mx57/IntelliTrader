using IntelliTrader.Core;
using IntelliTrader.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Primitives;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

namespace IntelliTrader.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private static readonly ConcurrentDictionary<string, (DateTime LastWriteTime, List<TradeResult> Trades)> _tradesCache = new ConcurrentDictionary<string, (DateTime LastWriteTime, List<TradeResult> Trades)>();

        #region Authentication

        [AllowAnonymous]
        public async Task<IActionResult> Login()
        {
            var coreService = Application.Resolve<ICoreService>();
            if (coreService.Config.PasswordProtected)
            {
                var model = new LoginViewModel
                {
                    RememberMe = true
                };
                return View(model);
            }
            else
            {
                return await PerformLogin(true);
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var coreService = Application.Resolve<ICoreService>();
                var isValid = !coreService.Config.PasswordProtected || ComputeMD5Hash(model.Password).Equals(coreService.Config.Password, StringComparison.InvariantCultureIgnoreCase);
                if (!isValid)
                {
                    ModelState.AddModelError("Password", "Invalid Password");
                    return View(model);
                }
                else
                {
                    return await PerformLogin(model.RememberMe);
                }
            }
            else
            {
                return View(model);
            }
        }

        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        private async Task<IActionResult> PerformLogin(bool persistent)
        {
            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
            var name = "user";
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, name));
            identity.AddClaim(new Claim(ClaimTypes.Name, name));
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties { IsPersistent = persistent });

            if (Request.Query.TryGetValue("ReturnUrl", out StringValues url))
            {
                return RedirectToAction(url);
            }
            else
            {
                return RedirectToAction(nameof(Index));
            }
        }

        private string ComputeMD5Hash(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentNullException(nameof(input));
            }

            using (var md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(input);
                byte[] hash = md5.ComputeHash(inputBytes);

                var sb = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }

        #endregion Authentication

        public IActionResult Index()
        {
            return Dashboard();
        }

        public IActionResult Dashboard()
        {
            var coreService = Application.Resolve<ICoreService>();
            var webService = Application.Resolve<IWebService>();
            var model = new DashboardViewModel
            {
                InstanceName = coreService.Config.InstanceName,
                Version = coreService.Version,
                ReadOnlyMode = webService.Config.ReadOnlyMode
            };
            return View(nameof(Dashboard), model);
        }

        public IActionResult Market()
        {
            var coreService = Application.Resolve<ICoreService>();
            var webService = Application.Resolve<IWebService>();
            var model = new MarketViewModel
            {
                InstanceName = coreService.Config.InstanceName,
                Version = coreService.Version,
                ReadOnlyMode = webService.Config.ReadOnlyMode
            };
            return View(model);
        }

        public IActionResult Stats()
        {
            var coreService = Application.Resolve<ICoreService>();
            var webService = Application.Resolve<IWebService>();
            var tradingService = Application.Resolve<ITradingService>();

            var accountInitialBalance = tradingService.Config.VirtualTrading ? tradingService.Config.VirtualAccountInitialBalance : tradingService.Config.AccountInitialBalance;

            // Use AccountInitialBalanceDate from config if it's set to a non-default value, otherwise default to 30 days ago
            DateTimeOffset accountInitialBalanceDate = tradingService.Config.AccountInitialBalanceDate;
            if (accountInitialBalanceDate == default(DateTimeOffset) || accountInitialBalanceDate.Year < 2010)
            {
                accountInitialBalanceDate = DateTimeOffset.Now.AddDays(-30).Date;
            }

            var model = new StatsViewModel
            {
                InstanceName = coreService.Config.InstanceName,
                Version = coreService.Version,
                ReadOnlyMode = webService.Config.ReadOnlyMode,
                TimezoneOffset = coreService.Config.TimezoneOffset,
                AccountInitialBalance = accountInitialBalance,
                AccountBalance = tradingService.Account.GetTotalBalance(),
                Market = tradingService.Config.Market,
                Balances = new Dictionary<DateTimeOffset, decimal>(),
                Trades = GetTrades()
            };

            decimal accumulatedProfit = 0;
            var orderedDates = model.Trades.Keys.OrderBy(d => d).ToList();

            foreach (var date in orderedDates)
            {
                var dailyProfit = model.Trades[date].Where(tr => !tr.IsSwap).Sum(tr => tr.Profit);

                // Only start accumulating profit from the initial balance date
                if (date >= accountInitialBalanceDate.Date)
                {
                    accumulatedProfit += dailyProfit;
                }

                model.Balances[date] = accountInitialBalance + accumulatedProfit;
            }

            return View(model);
        }

        public IActionResult Rules()
        {
            var allTades = GetTrades();
            var signalRuleStats = new Dictionary<string, RuleStats>();
            var tradingRuleStats = new Dictionary<string, RuleStats>();

            foreach (var trade in allTades.Values.SelectMany(t => t))
            {
                if (trade.IsSuccessful)
                {
                    // Signal Rules
                    var signalRule = trade.Metadata?.SignalRule;
                    UpdateRuleStats(signalRuleStats, signalRule, trade);

                    // Trading Rules
                    var tradingRules = trade.Metadata?.TradingRules;
                    if (tradingRules != null)
                    {
                        foreach (var tradingRule in tradingRules)
                        {
                            UpdateRuleStats(tradingRuleStats, tradingRule, trade);
                        }
                    }
                }
            }

            var coreService = Application.Resolve<ICoreService>();
            var webService = Application.Resolve<IWebService>();
            var model = new RulesViewModel
            {
                InstanceName = coreService.Config.InstanceName,
                Version = coreService.Version,
                ReadOnlyMode = webService.Config.ReadOnlyMode,
                SignalRuleStats = signalRuleStats,
                TradingRuleStats = tradingRuleStats
            };

            return View(model);
        }

        private void UpdateRuleStats(Dictionary<string, RuleStats> stats, string ruleName, TradeResult trade)
        {
            if (string.IsNullOrWhiteSpace(ruleName)) return;

            if (!stats.TryGetValue(ruleName, out RuleStats ruleStats))
            {
                ruleStats = new RuleStats();
                stats.Add(ruleName, ruleStats);
            }

            int orderCount = trade.OrderDates?.Count ?? 0;

            if (!trade.IsSwap)
            {
                ruleStats.TotalCost += trade.Cost;
                ruleStats.TotalProfit += trade.Profit;
                decimal totalInvestment = trade.Cost + (trade.Metadata?.AdditionalCosts ?? 0);
                if (totalInvestment > 0)
                {
                    decimal margin = trade.Profit / totalInvestment * 100;
                    if (orderCount == 1)
                    {
                        ruleStats.Margin.Add(margin);
                    }
                    else if (orderCount > 1)
                    {
                        ruleStats.MarginDCA.Add(margin);
                    }
                }
            }
            else
            {
                ruleStats.TotalSwaps++;
            }

            ruleStats.TotalTrades++;
            ruleStats.TotalOrders += orderCount;
            ruleStats.TotalFees += trade.FeesTotal;

            if (orderCount > 0)
            {
                ruleStats.Age.Add((trade.SellDate - trade.OrderDates.Min()).TotalDays);
                ruleStats.DCA.Add((orderCount - 1) + (trade.Metadata?.AdditionalDCALevels ?? 0));
            }
        }

        public IActionResult Trades(DateTimeOffset id)
        {
            var coreService = Application.Resolve<ICoreService>();
            var webService = Application.Resolve<IWebService>();
            var model = new TradesViewModel()
            {
                InstanceName = coreService.Config.InstanceName,
                Version = coreService.Version,
                ReadOnlyMode = webService.Config.ReadOnlyMode,
                TimezoneOffset = coreService.Config.TimezoneOffset,
                Date = id,
                Trades = GetTrades(id).Values.FirstOrDefault() ?? new List<TradeResult>()
            };

            return View(model);
        }

        public IActionResult Settings()
        {
            var coreService = Application.Resolve<ICoreService>();
            var webService = Application.Resolve<IWebService>();
            var tradingService = Application.Resolve<ITradingService>();
            var allConfigurableServices = Application.Resolve<IEnumerable<IConfigurableService>>();

            var model = new SettingsViewModel()
            {
                InstanceName = coreService.Config.InstanceName,
                Version = coreService.Version,
                ReadOnlyMode = webService.Config.ReadOnlyMode,
                BuyEnabled = tradingService.Config.BuyEnabled,
                BuyDCAEnabled = tradingService.Config.BuyDCAEnabled,
                SellEnabled = tradingService.Config.SellEnabled,
                TradingSuspended = tradingService.IsTradingSuspended,
                HealthCheckEnabled = coreService.Config.HealthCheckEnabled,
                Configs = allConfigurableServices.Where(s => !s.GetType().Name.Contains(Constants.ServiceNames.BacktestingService)).OrderBy(s => s.ServiceName).ToDictionary(s => s.ServiceName, s => Application.ConfigProvider.GetSectionJson(s.ServiceName))
            };

            return View(model);
        }

        public IActionResult Log()
        {
            var coreService = Application.Resolve<ICoreService>();
            var webService = Application.Resolve<IWebService>();
            var loggingService = Application.Resolve<ILoggingService>();

            var model = new LogViewModel()
            {
                InstanceName = coreService.Config.InstanceName,
                Version = coreService.Version,
                ReadOnlyMode = webService.Config.ReadOnlyMode,
                LogEntries = loggingService.GetLogEntries().Reverse().Take(500)
            };

            return View(model);
        }

        public IActionResult Help(string lang = "en")
        {
            var coreService = Application.Resolve<ICoreService>();
            string helpFilePath;

            if (lang == "ru")
            {
                helpFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Static", "Help", "index.ru.md");
            }
            else
            {
                helpFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Static", "Help", "index.md");
            }

            var model = new HelpViewModel
            {
                InstanceName = coreService.Config.InstanceName,
                Version = coreService.Version,
                HelpContent = System.IO.File.ReadAllText(helpFilePath),
                Language = lang
            };
            return View(model);
        }

        [HttpGet]
        public IActionResult PollLogs(string type = "general", int maxLines = 100)
        {
            try
            {
                maxLines = Math.Min(Math.Max(maxLines, 1), 1000);
                string pattern = "general".Equals(type, StringComparison.OrdinalIgnoreCase) ? "*-general.txt" : "*-trades.txt";
                string filePath = GetLatestLogFilePath(pattern);

                if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                {
                    return Json(new { lines = new List<string>(), message = "No log file found." });
                }

                var lines = ReadLastLines(filePath, maxLines);
                return Json(new { lines });
            }
            catch (Exception ex)
            {
                return Json(new { lines = new List<string>(), error = ex.Message });
            }
        }

        private string GetLatestLogFilePath(string pattern)
        {
            var logsPath = Path.Combine(Directory.GetCurrentDirectory(), "log");
            if (!Directory.Exists(logsPath)) return null;

            return Directory.EnumerateFiles(logsPath, pattern)
                .OrderByDescending(f => f)
                .FirstOrDefault();
        }

        private List<string> ReadLastLines(string filePath, int maxLines)
        {
            var lines = new List<string>();
            if (!System.IO.File.Exists(filePath))
            {
                return lines;
            }

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long position = fs.Length;
                if (position == 0) return lines;

                const int bufferSize = 4096;
                byte[] buffer = new byte[bufferSize];
                int newlineCount = 0;

                while (position > 0 && newlineCount < maxLines + 1)
                {
                    int toRead = (int)Math.Min(bufferSize, position);
                    position -= toRead;
                    fs.Seek(position, SeekOrigin.Begin);
                    int read = fs.Read(buffer, 0, toRead);

                    for (int i = read - 1; i >= 0; i--)
                    {
                        if (buffer[i] == 10) // '\n'
                        {
                            newlineCount++;
                            if (newlineCount > maxLines)
                            {
                                position = position + i + 1;
                                break;
                            }
                        }
                    }
                }

                fs.Seek(position, SeekOrigin.Begin);
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    string content = sr.ReadToEnd();
                    var allLines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    return allLines.Where(l => !string.IsNullOrWhiteSpace(l)).Skip(Math.Max(0, allLines.Length - maxLines)).ToList();
                }
            }
        }

        public IActionResult Status()
        {
            var loggingService = Application.Resolve<ILoggingService>();
            var tradingService = Application.Resolve<ITradingService>();
            var signalsService = Application.Resolve<ISignalsService>();
            var healthCheckService = Application.Resolve<IHealthCheckService>();

            var status = new
            {
                Balance = tradingService.Account.GetBalance(),
                GlobalRating = signalsService.GetGlobalRating()?.ToString("0.000") ?? "N/A",
                TrailingBuys = tradingService.GetTrailingBuys(),
                TrailingSells = tradingService.GetTrailingSells(),
                TrailingSignals = signalsService.GetTrailingSignals(),
                TradingSuspended = tradingService.IsTradingSuspended,
                HealthChecks = healthCheckService.GetHealthChecks().OrderBy(c => c.Name),
                LogEntries = loggingService.GetLogEntries().Reverse().Take(5)
            };
            return Json(status);
        }

        public IActionResult SignalNames()
        {
            var signalsService = Application.Resolve<ISignalsService>();
            return Json(signalsService.GetSignalNames());
        }

        [HttpPost]
        public IActionResult TradingPairs()
        {
            var coreService = Application.Resolve<ICoreService>();
            var tradingService = Application.Resolve<ITradingService>();

            var tradingPairs = from tradingPair in tradingService.Account.GetTradingPairs()
                               let pairConfig = tradingService.GetPairConfig(tradingPair.Pair)
                               select new
                               {
                                   Name = tradingPair.Pair,
                                   DCA = tradingPair.DCALevel,
                                   TradingViewName = $"{tradingService.Config.Exchange.ToUpperInvariant()}:{tradingPair.Pair}",
                                   Margin = tradingPair.CurrentMargin.ToString("0.00"),
                                   Target = pairConfig.SellMargin.ToString("0.00"),
                                   CurrentPrice = tradingPair.CurrentPrice.ToString("0.00000000"),
                                   CurrentSpread = tradingPair.CurrentSpread.ToString("0.00"),
                                   BoughtPrice = tradingPair.AveragePrice.ToString("0.00000000"),
                                   Cost = tradingPair.Cost.ToString("0.00000000"),
                                   CurrentCost = tradingPair.CurrentCost.ToString("0.00000000"),
                                   Amount = tradingPair.Amount.ToString("0.########"),
                                   OrderDates = tradingPair.OrderDates.Select(d => d.ToOffset(TimeSpan.FromHours(coreService.Config.TimezoneOffset)).ToString("yyyy-MM-dd HH:mm:ss")),
                                   tradingPair.OrderIds,
                                   Age = tradingPair.CurrentAge.ToString("0.00"),
                                   CurrentRating = tradingPair.Metadata.CurrentRating?.ToString("0.000") ?? "N/A",
                                   BoughtRating = tradingPair.Metadata.BoughtRating?.ToString("0.000") ?? "N/A",
                                   SignalRule = tradingPair.Metadata.SignalRule ?? "N/A",
                                   tradingPair.Metadata.SwapPair,
                                   TradingRules = pairConfig.Rules,
                                   IsTrailingSell = tradingService.GetTrailingSells().Contains(tradingPair.Pair),
                                   IsTrailingBuy = tradingService.GetTrailingBuys().Contains(tradingPair.Pair),
                                   LastBuyMargin = tradingPair.Metadata.LastBuyMargin?.ToString("0.00") ?? "N/A",
                                   Config = pairConfig
                               };

            return Json(tradingPairs);
        }

        [HttpPost]
        public IActionResult MarketPairs(List<string> signalsFilter)
        {
            var coreService = Application.Resolve<ICoreService>();
            var tradingService = Application.Resolve<ITradingService>();
            var signalsService = Application.Resolve<ISignalsService>();

            var allSignals = signalsService.GetAllSignals();
            if (allSignals != null)
            {
                if (signalsFilter.Count > 0)
                {
                    allSignals = allSignals.Where(s => signalsFilter.Contains(s.Name));
                }

                var groupedSignals = allSignals.GroupBy(s => s.Pair).ToDictionary(g => g.Key, g => g.AsEnumerable());

                var marketPairs = from signalGroup in groupedSignals
                                  let pair = signalGroup.Key
                                  let pairConfig = tradingService.GetPairConfig(pair)
                                  select new
                                  {
                                      Name = pair,
                                      TradingViewName = $"{tradingService.Config.Exchange.ToUpperInvariant()}:{pair}",
                                      VolumeList = signalGroup.Value.Select(s => new { s.Name, s.Volume }),
                                      VolumeChangeList = signalGroup.Value.Select(s => new { s.Name, s.VolumeChange }),
                                      Price = tradingService.GetPrice(pair).ToString("0.00000000"),
                                      PriceChangeList = signalGroup.Value.Select(s => new { s.Name, s.PriceChange }),
                                      RatingList = signalGroup.Value.Select(s => new { s.Name, s.Rating }),
                                      RatingChangeList = signalGroup.Value.Select(s => new { s.Name, s.RatingChange }),
                                      VolatilityList = signalGroup.Value.Select(s => new { s.Name, s.Volatility }),
                                      Spread = tradingService.Exchange.GetPriceSpread(pair).ToString("0.00"),
                                      ArbitrageList = from market in Enum.GetNames(typeof(ArbitrageMarket)).Where(m => m != tradingService.Config.Market)
                                                      let arbitrage = tradingService.Exchange.GetArbitrage(pair, tradingService.Config.Market, new List<ArbitrageMarket> { Enum.Parse<ArbitrageMarket>(market) })
                                                      select new
                                                      {
                                                          Name = $"{arbitrage.Market}-{arbitrage.Type.ToString()[0]}",
                                                          Arbitrage = arbitrage.IsAssigned ? arbitrage.Percentage.ToString("0.00") : "N/A"
                                                      },
                                      SignalRules = signalsService.GetTrailingInfo(pair)?.Select(ti => ti.Rule.Name) ?? new string[0],
                                      HasTradingPair = tradingService.Account.HasTradingPair(pair),
                                      Config = pairConfig
                                  };

                return Json(marketPairs);
            }
            else
            {
                return Json(null);
            }
        }

        [HttpPost]
        public IActionResult Settings(SettingsViewModel model)
        {
            if (!Application.Resolve<IWebService>().Config.ReadOnlyMode)
            {
                var coreService = Application.Resolve<ICoreService>();
                var tradingService = Application.Resolve<ITradingService>();

                coreService.Config.HealthCheckEnabled = model.HealthCheckEnabled;
                tradingService.Config.BuyEnabled = model.BuyEnabled;
                tradingService.Config.BuyDCAEnabled = model.BuyDCAEnabled;
                tradingService.Config.SellEnabled = model.SellEnabled;

                if (model.TradingSuspended)
                {
                    tradingService.SuspendTrading();
                }
                else
                {
                    tradingService.ResumeTrading();
                }
                return Settings();
            }
            else
            {
                return Settings();
            }
        }

        [HttpPost]
        public IActionResult SaveConfig()
        {
            string configName = Request.Form["name"].ToString();
            string configDefinition = Request.Form["definition"].ToString();

            if (!Application.Resolve<IWebService>().Config.ReadOnlyMode && !String.IsNullOrWhiteSpace(configName) && !String.IsNullOrWhiteSpace(configDefinition))
            {
                Application.ConfigProvider.SetSectionJson(configName, configDefinition);
                return new OkResult();
            }
            else
            {
                return new BadRequestResult();
            }
        }

        [HttpPost]
        public IActionResult Sell()
        {
            string pair = Request.Form["pair"].ToString();
            if (!Application.Resolve<IWebService>().Config.ReadOnlyMode && pair != null && decimal.TryParse(Request.Form["amount"], out decimal amount) && amount > 0)
            {
                var tradingService = Application.Resolve<ITradingService>();
                tradingService.Sell(new SellOptions(pair)
                {
                    Amount = amount,
                    ManualOrder = true
                });
                return new OkResult();
            }
            else
            {
                return new BadRequestResult();
            }
        }

        [HttpPost]
        public IActionResult Buy()
        {
            string pair = Request.Form["pair"].ToString();
            if (!Application.Resolve<IWebService>().Config.ReadOnlyMode && !String.IsNullOrWhiteSpace(pair) && decimal.TryParse(Request.Form["amount"], out decimal amount) && amount > 0)
            {
                var tradingService = Application.Resolve<ITradingService>();
                tradingService.Buy(new BuyOptions(pair)
                {
                    Amount = amount,
                    IgnoreExisting = true,
                    ManualOrder = true
                });
                return new OkResult();
            }
            else
            {
                return new BadRequestResult();
            }
        }

        [HttpPost]
        public IActionResult BuyDefault()
        {
            string pair = Request.Form["pair"].ToString();
            if (!Application.Resolve<IWebService>().Config.ReadOnlyMode && !String.IsNullOrWhiteSpace(pair))
            {
                var signalsService = Application.Resolve<ISignalsService>();
                var tradingService = Application.Resolve<ITradingService>();
                tradingService.Buy(new BuyOptions(pair)
                {
                    MaxCost = tradingService.GetPairConfig(pair).BuyMaxCost,
                    IgnoreExisting = true,
                    ManualOrder = true,
                    Metadata = new OrderMetadata
                    {
                        BoughtGlobalRating = signalsService.GetGlobalRating()
                    }
                });
                return new OkResult();
            }
            else
            {
                return new BadRequestResult();
            }
        }

        [HttpPost]
        public IActionResult Swap()
        {
            string pair = Request.Form["pair"].ToString();
            string swap = Request.Form["swap"].ToString();
            if (!Application.Resolve<IWebService>().Config.ReadOnlyMode && !String.IsNullOrWhiteSpace(pair) && !String.IsNullOrWhiteSpace(swap))
            {
                var tradingService = Application.Resolve<ITradingService>();
                tradingService.Swap(new SwapOptions(pair, swap, new OrderMetadata())
                {
                    ManualOrder = true
                });
                return new OkResult();
            }
            else
            {
                return new BadRequestResult();
            }
        }

        public IActionResult RefreshAccount()
        {
            if (!Application.Resolve<IWebService>().Config.ReadOnlyMode)
            {
                var tradingService = Application.Resolve<ITradingService>();
                tradingService.Account.Refresh();
                return new OkResult();
            }
            else
            {
                return new BadRequestResult();
            }
        }

        public IActionResult RestartServices()
        {
            if (!Application.Resolve<IWebService>().Config.ReadOnlyMode)
            {
                var coreService = Application.Resolve<ICoreService>();
                coreService.Restart();
                return new OkResult();
            }
            else
            {
                return new BadRequestResult();
            }
        }

        [HttpGet]
        public IActionResult PollLogs(string type = "trades", int maxLines = 100)
        {
            maxLines = Math.Max(1, Math.Min(maxLines, 1000));
            var logsPath = Path.Combine(Directory.GetCurrentDirectory(), "log");
            if (!Directory.Exists(logsPath))
            {
                return Json(new { success = false, message = "Logs directory does not exist." });
            }

            string searchPattern = type == "general" ? "*-general.txt" : "*-trades.txt";
            var file = Directory.EnumerateFiles(logsPath, searchPattern, SearchOption.TopDirectoryOnly)
                                 .OrderByDescending(f => f)
                                 .FirstOrDefault();

            if (file == null)
            {
                return Json(new { success = false, message = $"No log files found for type '{type}'." });
            }

            var lines = ReadLastLines(file, maxLines);
            return Json(new { success = true, lines = lines });
        }

        private List<string> ReadLastLines(string filePath, int maxLines)
        {
            var lines = new List<string>();
            if (!System.IO.File.Exists(filePath))
            {
                return lines;
            }

            const int bufferSize = 4096;
            byte[] buffer = new byte[bufferSize];
            var linePositions = new List<long>();

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long fileLength = fs.Length;
                long position = fileLength;

                while (position > 0 && linePositions.Count <= maxLines)
                {
                    int toRead = (int)Math.Min(bufferSize, position);
                    position -= toRead;
                    fs.Position = position;
                    int read = fs.Read(buffer, 0, toRead);

                    for (int i = read - 1; i >= 0; i--)
                    {
                        if (buffer[i] == (byte)'\n')
                        {
                            long absoluteOffset = position + i;
                            linePositions.Add(absoluteOffset);
                            if (linePositions.Count > maxLines)
                            {
                                break;
                            }
                        }
                    }
                }

                linePositions.Reverse();
                int startIndex = 0;
                if (linePositions.Count > maxLines)
                {
                    startIndex = linePositions.Count - maxLines;
                }

                long lastPos = startIndex > 0 ? linePositions[startIndex - 1] + 1 : 0;

                for (int i = startIndex; i < linePositions.Count; i++)
                {
                    long nextPos = linePositions[i];
                    long length = nextPos - lastPos;
                    if (length > 0)
                    {
                        fs.Position = lastPos;
                        byte[] lineBytes = new byte[length];
                        fs.Read(lineBytes, 0, (int)length);
                        string line = Encoding.UTF8.GetString(lineBytes).TrimEnd('\r', '\n');
                        lines.Add(line);
                    }
                    else
                    {
                        lines.Add(string.Empty);
                    }
                    lastPos = nextPos + 1;
                }

                if (lastPos < fileLength)
                {
                    long length = fileLength - lastPos;
                    fs.Position = lastPos;
                    byte[] lineBytes = new byte[length];
                    fs.Read(lineBytes, 0, (int)length);
                    string line = Encoding.UTF8.GetString(lineBytes).TrimEnd('\r', '\n');
                    lines.Add(line);
                }
            }

            if (lines.Count > maxLines)
            {
                lines = lines.Skip(lines.Count - maxLines).ToList();
            }

            return lines;
        }

        private Dictionary<DateTimeOffset, List<TradeResult>> GetTrades(DateTimeOffset? date = null)
        {
            var coreService = Application.Resolve<ICoreService>();
            var logsPath = Path.Combine(Directory.GetCurrentDirectory(), "log");
            var tradeResultPattern = new Regex($"{nameof(TradeResult)} (?<data>\\{{.*\\}})", RegexOptions.Compiled);
            var trades = new Dictionary<DateTimeOffset, List<TradeResult>>();

            if (Directory.Exists(logsPath))
            {
                foreach (var tradesLogFilePath in Directory.EnumerateFiles(logsPath, "*-trades.txt", SearchOption.TopDirectoryOnly))
                {
                    DateTime lastWriteTime = System.IO.File.GetLastWriteTime(tradesLogFilePath);

                    if (!_tradesCache.TryGetValue(tradesLogFilePath, out var cached) || cached.LastWriteTime != lastWriteTime)
                    {
                        var fileTrades = new List<TradeResult>();
                        IEnumerable<string> logLines = Utils.ReadAllLinesWriteSafe(tradesLogFilePath);
                        foreach (var logLine in logLines)
                        {
                            var match = tradeResultPattern.Match(logLine);
                            if (match.Success)
                            {
                                var data = match.Groups["data"].ToString();
                                var json = Utils.FixInvalidJson(data.Replace(nameof(OrderMetadata), ""))
                                    .Replace("AveragePricePaid", nameof(ITradeResult.AveragePrice)); // Old property migration

                                TradeResult tradeResult = JsonConvert.DeserializeObject<TradeResult>(json);
                                if (tradeResult.IsSuccessful && tradeResult.Metadata?.IsTransitional != true)
                                {
                                    fileTrades.Add(tradeResult);
                                }
                            }
                        }
                        cached = (lastWriteTime, fileTrades);
                        _tradesCache[tradesLogFilePath] = cached;
                    }

                    foreach (var tradeResult in cached.Trades)
                    {
                        DateTimeOffset tradeDate = tradeResult.SellDate.ToOffset(TimeSpan.FromHours(coreService.Config.TimezoneOffset)).Date;
                        if (date == null || date == tradeDate)
                        {
                            if (!trades.ContainsKey(tradeDate))
                            {
                                trades.Add(tradeDate, new List<TradeResult>());
                            }
                            trades[tradeDate].Add(tradeResult);
                        }
                    }
                }
            }
            return trades;
        }
    }
}
