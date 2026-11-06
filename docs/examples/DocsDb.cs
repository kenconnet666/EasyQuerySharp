namespace EasyQuerySharp.Docs;

/// <summary>卡片用的辅助方法，不是 EasyQuerySharp 的 API。</summary>
public static class DocsDb
{
    /// <summary>
    /// 用同一个数据源、方言、监听器和拦截器按新的配置再建一个 EasyDb。<see cref="EasyDbOptions"/> 交给 EasyDb 之后就不能改，
    /// 演示某个配置项时用它；实际项目里配置项写在创建 EasyDb 的地方。
    /// </summary>
    public static EasyDb Reconfigure(EasyDb db, Action<EasyDbOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(configure);
        return new EasyDb(o =>
        {
            o.Use(db.Options.Dialect, db.Options.DataSource);
            foreach (var listener in db.Options.Listeners) o.AddListener(listener);
            foreach (var interceptor in db.Options.Interceptors.All) o.AddInterceptor(interceptor);
            configure(o);
        });
    }
}
