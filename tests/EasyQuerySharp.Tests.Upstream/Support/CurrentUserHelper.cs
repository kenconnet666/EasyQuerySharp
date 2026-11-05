namespace EasyQuerySharp.Tests.Upstream.Support;

/// <summary>对应 Java：logicdel/CurrentUserHelper，当前用户与租户随异步流程传递（各测试互不影响）。</summary>
public static class CurrentUserHelper
{
    private static readonly AsyncLocal<string?> User = new();
    private static readonly AsyncLocal<string?> Tenant = new();

    /// <summary>当前用户 id。</summary>
    public static string? UserId
    {
        get => User.Value;
        set => User.Value = value;
    }

    /// <summary>当前租户 id。</summary>
    public static string? TenantId
    {
        get => Tenant.Value;
        set => Tenant.Value = value;
    }
}
