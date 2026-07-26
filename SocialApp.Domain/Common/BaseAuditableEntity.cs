namespace SocialApp.Domain.Common;

/// <summary>
/// Base class cho mọi entity trong hệ thống.
/// Cung cấp audit fields (CreatedAt, UpdatedAt) và soft-delete (DeletedAt).
/// AppDbContext tự động set các field này trong SaveChangesAsync.
/// </summary>
public abstract class BaseAuditableEntity
{
    /// <summary>Primary key — dùng Guid để tránh enumerable attack.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Thời điểm tạo (UTC). AppDbContext set tự động, không set thủ công.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Thời điểm cập nhật gần nhất (UTC). AppDbContext set tự động.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Soft-delete: null = chưa xoá, có giá trị = đã xoá.
    /// Global query filter trong AppDbContext tự động lọc những bản ghi này.
    /// </summary>
    public DateTime? DeletedAt { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsDeleted => DeletedAt.HasValue;
}