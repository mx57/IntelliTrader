using System;

namespace IntelliTrader.Core
{
    /// <summary>
    /// Options for spread-aware trailing safety checks.
    /// </summary>
    public class TrailingSafetyOptions
    {
        /// <summary>
        /// Gets or sets the maximum allowed spread percentage to continue trailing.
        /// If the spread exceeds this value, trailing may be paused or stopped.
        /// </summary>
        public decimal MaxTrailingSpread { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to pause trailing when spread is high.
        /// </summary>
        public bool PauseOnHighSpread { get; set; }

        /// <summary>
        /// Gets or sets the minimum price change percentage required to update trailing stop
        /// even if spread is high.
        /// </summary>
        public decimal MinPriceChangeWithHighSpread { get; set; }
    }
}
