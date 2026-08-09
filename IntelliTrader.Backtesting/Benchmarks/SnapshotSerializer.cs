using System;
using System.IO;
using System.Collections.Generic;
using IntelliTrader.Backtesting;

namespace BacktestingBenchmarkSuite
{
    /// <summary>
    /// Высокопроизводительный бинарный сериализатор для снимков (snapshots) сигналов и тикеров.
    /// Разработан взамен ZeroFormatter для предотвращения ошибок BadImageFormatException на .NET 8.0/10.0.
    /// </summary>
    public static class SnapshotSerializer
    {
        // --- ЗАПИСЬ СИГНАЛОВ ---

        public static void SerializeSignals(BinaryWriter writer, IEnumerable<SignalData> signals)
        {
            if (signals == null)
            {
                writer.Write(0);
                return;
            }

            // Записываем количество элементов вначале, чтобы затем быстро считать
            var list = signals as IList<SignalData> ?? new List<SignalData>(signals);
            writer.Write(list.Count);

            foreach (var sig in list)
            {
                WriteNullableString(writer, sig.Name);
                WriteNullableString(writer, sig.Pair);
                WriteNullableLong(writer, sig.Volume);
                WriteNullableDouble(writer, sig.VolumeChange);
                WriteNullableDecimal(writer, sig.Price);
                WriteNullableDecimal(writer, sig.PriceChange);
                WriteNullableDouble(writer, sig.Rating);
                WriteNullableDouble(writer, sig.RatingChange);
                WriteNullableDouble(writer, sig.Volatility);
            }
        }

        public static List<SignalData> DeserializeSignals(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            var list = new List<SignalData>(count);

            for (int i = 0; i < count; i++)
            {
                var sig = new SignalData
                {
                    Name = ReadNullableString(reader) ?? "",
                    Pair = ReadNullableString(reader) ?? "",
                    Volume = ReadNullableLong(reader),
                    VolumeChange = ReadNullableDouble(reader),
                    Price = ReadNullableDecimal(reader),
                    PriceChange = ReadNullableDecimal(reader),
                    Rating = ReadNullableDouble(reader),
                    RatingChange = ReadNullableDouble(reader),
                    Volatility = ReadNullableDouble(reader)
                };
                list.Add(sig);
            }

            return list;
        }

        // --- ЗАПИСЬ ТИКЕРОВ ---

        public static void SerializeTickers(BinaryWriter writer, IEnumerable<TickerData> tickers)
        {
            if (tickers == null)
            {
                writer.Write(0);
                return;
            }

            var list = tickers as IList<TickerData> ?? new List<TickerData>(tickers);
            writer.Write(list.Count);

            foreach (var tick in list)
            {
                writer.Write(tick.Pair ?? "");
                writer.Write(tick.BidPrice);
                writer.Write(tick.AskPrice);
                writer.Write(tick.LastPrice);
            }
        }

        public static List<TickerData> DeserializeTickers(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            var list = new List<TickerData>(count);

            for (int i = 0; i < count; i++)
            {
                var tick = new TickerData
                {
                    Pair = reader.ReadString(),
                    BidPrice = reader.ReadDecimal(),
                    AskPrice = reader.ReadDecimal(),
                    LastPrice = reader.ReadDecimal()
                };
                list.Add(tick);
            }

            return list;
        }

        // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ СЕРИАЛИЗАЦИИ ---

        private static void WriteNullableString(BinaryWriter writer, string? val)
        {
            if (val == null)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                writer.Write(val);
            }
        }

        private static string? ReadNullableString(BinaryReader reader)
        {
            if (!reader.ReadBoolean()) return null;
            return reader.ReadString();
        }

        private static void WriteNullableLong(BinaryWriter writer, long? val)
        {
            if (!val.HasValue)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                writer.Write(val.Value);
            }
        }

        private static long? ReadNullableLong(BinaryReader reader)
        {
            if (!reader.ReadBoolean()) return null;
            return reader.ReadInt64();
        }

        private static void WriteNullableDouble(BinaryWriter writer, double? val)
        {
            if (!val.HasValue)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                writer.Write(val.Value);
            }
        }

        private static double? ReadNullableDouble(BinaryReader reader)
        {
            if (!reader.ReadBoolean()) return null;
            return reader.ReadDouble();
        }

        private static void WriteNullableDecimal(BinaryWriter writer, decimal? val)
        {
            if (!val.HasValue)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                writer.Write(val.Value);
            }
        }

        private static decimal? ReadNullableDecimal(BinaryReader reader)
        {
            if (!reader.ReadBoolean()) return null;
            return reader.ReadDecimal();
        }
    }
}
