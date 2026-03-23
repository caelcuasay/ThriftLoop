namespace ThriftLoop.Enums;

/// <summary>How the seller wants to receive their funds.</summary>
public enum WithdrawalMethod
{
    /// <summary>Transfer to a registered bank account.</summary>
    BankTransfer,

    /// <summary>Physical cash pickup at a partner location.</summary>
    PickupLocation
}

/// <summary>Lifecycle state of a withdrawal request.</summary>
public enum WithdrawalStatus
{
    /// <summary>Submitted by the seller, not yet processed by an admin.</summary>
    Requested,

    /// <summary>Admin has acknowledged the request and is processing it.</summary>
    Processed,

    /// <summary>Funds have been released to the seller.</summary>
    Completed
}