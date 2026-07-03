import os

def test_rules_service_optimization() -> None:
    """
    Verify that RulesService.cs has been optimized to cache price and spread
    before the condition loop.
    """
    filepath = "IntelliTrader.Rules/Services/RulesService.cs"
    assert os.path.exists(filepath), f"File {filepath} not found"

    with open(filepath, 'r') as f:
        content = f.read()

    # Check if we are caching price and spread
    assert "decimal currentPrice = tradingService.GetPrice(pair);" in content
    assert "decimal currentSpread = tradingService.Exchange.GetPriceSpread(pair);" in content

    # Check if the loop uses the cached variables instead of calling the service
    # The original pattern was tradingService.GetPrice(pair) < condition.MinPrice
    # The optimized pattern should be currentPrice < condition.MinPrice

    assert "currentPrice < condition.MinPrice" in content
    assert "currentPrice > condition.MaxPrice" in content
    assert "currentSpread < condition.MinSpread" in content
    assert "currentSpread > condition.MaxSpread" in content

    # Ensure no redundant calls are left in the if condition
    assert "tradingService.GetPrice(pair) < condition.MinPrice" not in content

def test_trailing_safety_options_exists() -> None:
    """
    Verify that the new TrailingSafetyOptions model exists and contains expected properties.
    """
    filepath = "IntelliTrader.Trading/Models/TrailingSafetyOptions.cs"
    assert os.path.exists(filepath), f"File {filepath} not found"

    with open(filepath, 'r') as f:
        content = f.read()

    assert "public decimal MaxTrailingSpread { get; set; }" in content
    assert "public bool PauseOnHighSpread { get; set; }" in content

if __name__ == "__main__":
    # Manual run for debugging
    try:
        test_rules_service_optimization()
        test_trailing_safety_options_exists()
        print("All audit tests passed!")
    except AssertionError as e:
        print(f"Audit test failed: {e}")
        exit(1)
