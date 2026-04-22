namespace ThriftLoop.Constants;

/// <summary>
/// Single source of truth for wallet, withdrawal, and seeding rules.
/// </summary>
public static class WalletConstants
{
    /// <summary>Starting balance credited to every new wallet (demo / seeding).</summary>
    public const decimal InitialBalance = 0m;

    /// <summary>Minimum amount a seller can request in a single withdrawal.</summary>
    public const decimal MinWithdrawalAmount = 50m;

    /// <summary>Maximum amount a user can top up in a single transaction.</summary>
    public const decimal MaxTopUpAmount = 50_000m;

    /// <summary>How many recent transactions to show on the Wallet dashboard.</summary>
    public const int RecentTransactionCount = 30;
}