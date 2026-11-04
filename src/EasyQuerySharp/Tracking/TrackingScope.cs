namespace EasyQuerySharp.Tracking;

/// <summary>
/// 追踪作用域（对应 Java 的 TrackManager.begin / release）：<c>using var scope = db.BeginTracking();</c>。
/// 作用域内查出的实体默认被追踪（快照下各列的原始值），按实体更新时只写变化的列；
/// 同一主键的实体再次查询时返回已追踪的那个实例。作用域可以嵌套，最外层释放时清空追踪的实体。
/// </summary>
public sealed class TrackingScope : IDisposable
{
    private readonly EasyDb _db;
    private bool _disposed;

    internal TrackingScope(EasyDb db)
    {
        _db = db;
        db.Tracker.Begin();
    }

    /// <summary>结束这一层作用域。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _db.Tracker.Release();
    }
}
