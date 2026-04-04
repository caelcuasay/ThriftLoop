// ViewModels/Admin/UserManagementViewModel.cs
using ThriftLoop.Models;
using ThriftLoop.Enums;

namespace ThriftLoop.ViewModels.Admin;

public class UserManagementViewModel
{
    // ── User List ─────────────────────────────────────────────────────────────
    public IReadOnlyList<User> Users { get; set; } = new List<User>();

    // ─── Filtering & Pagination ───────────────────────────────────────────────
    public string? SearchTerm { get; set; }
    public string? RoleFilter { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }

    // ── Available Filters (for dropdowns) ─────────────────────────────────────
    public static IReadOnlyList<string> RoleFilters => new[]
    {
        "All",
        nameof(UserRole.User),
        nameof(UserRole.Seller),
        nameof(UserRole.Admin)
    };

    // ── Computed Properties ───────────────────────────────────────────────────
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    public string GetRoleBadgeClass(UserRole role)
    {
        return role switch
        {
            UserRole.Admin => "badge-admin",
            UserRole.Seller => "badge-seller",
            UserRole.User => "badge-user",
            UserRole.Rider => "badge-rider",
            _ => "badge-default"
        };
    }

    public string GetStatusBadgeClass(bool isDisabled)
    {
        return isDisabled ? "badge-disabled" : "badge-active";
    }

    public string GetStatusText(bool isDisabled)
    {
        return isDisabled ? "Disabled" : "Active";
    }
}