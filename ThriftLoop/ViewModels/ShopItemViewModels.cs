using System.ComponentModel.DataAnnotations;
using ThriftLoop.Constants;

namespace ThriftLoop.ViewModels;

// ════════════════════════════════════════════════════════════════════════════
//  CREATE
// ════════════════════════════════════════════════════════════════════════════

/// <summary>GET payload for Create. Carries shop context for the breadcrumb.</summary>
public class ShopItemCreateViewModel
{
    public int ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
}

/// <summary>JSON body for POST /Shop/Create.</summary>
public class ShopItemCreateDto
{
    [Required] public int ShopId { get; set; }

    [Required, MaxLength(100)] public string Title { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string Description { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string Category { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string Condition { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public List<ShopVariantCreateDto> Variants { get; set; } = new();
}

public class ShopVariantCreateDto
{
    [Required, MaxLength(50)] public string Name { get; set; } = string.Empty;
    [Required, MinLength(1)] public List<ShopSkuCreateDto> Skus { get; set; } = new();
}

public class ShopSkuCreateDto
{
    [MaxLength(20)] public string? Size { get; set; }
    [Required, Range(0.01, 999999.99)] public decimal Price { get; set; }
    [Required, Range(1, 9999)] public int Quantity { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
//  DETAILS
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Drives the public shop item details page.</summary>
public class ShopItemDetailsViewModel
{
    public int ItemId { get; set; }
    public int ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string? ShopLogoUrl { get; set; }
    public bool IsOwner { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;

    /// <summary>Lowest price across all available SKUs — shown as "From ₱X".</summary>
    public decimal StartingPrice { get; set; }

    public List<string> ImageUrls { get; set; } = new();
    public List<ShopItemVariantViewModel> Variants { get; set; } = new();
}

public class ShopItemVariantViewModel
{
    public int VariantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ShopItemSkuViewModel> Skus { get; set; } = new();
}

public class ShopItemSkuViewModel
{
    public int SkuId { get; set; }
    public string? Size { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public bool IsAvailable { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
//  EDIT
// ════════════════════════════════════════════════════════════════════════════

/// <summary>GET payload for Edit. Pre-populates the dynamic variant builder.</summary>
public class ShopItemEditViewModel
{
    public int ItemId { get; set; }
    public int ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;

    public List<string> ImageUrls { get; set; } = new();
    public List<ShopItemVariantViewModel> Variants { get; set; } = new();

    public static IReadOnlyList<string> Categories => ItemConstants.Categories;
    public static IReadOnlyList<string> Conditions => ItemConstants.Conditions;
    public static IReadOnlyList<string> Sizes => ItemConstants.Sizes;
}

/// <summary>JSON body for POST /Shop/Edit.</summary>
public class ShopItemEditDto
{
    [Required] public int ShopItemId { get; set; }

    [Required, MaxLength(100)] public string Title { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string Description { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string Category { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string Condition { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public List<ShopVariantEditDto> Variants { get; set; } = new();
}

public class ShopVariantEditDto
{
    /// <summary>Null for new variants being added during edit.</summary>
    public int? VariantId { get; set; }

    [Required, MaxLength(50)] public string Name { get; set; } = string.Empty;
    [Required, MinLength(1)] public List<ShopSkuEditDto> Skus { get; set; } = new();
}

public class ShopSkuEditDto
{
    /// <summary>Null for new SKUs being added during edit.</summary>
    public int? SkuId { get; set; }

    [MaxLength(20)] public string? Size { get; set; }
    [Required, Range(0.01, 999999.99)] public decimal Price { get; set; }
    [Required, Range(1, 9999)] public int Quantity { get; set; }
}