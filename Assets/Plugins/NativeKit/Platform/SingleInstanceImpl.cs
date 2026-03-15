/// <summary>空实现单实例（不支持的平台，始终返回允许运行）。</summary>
public class NullSingleInstance : ISingleInstance
{
    public static readonly NullSingleInstance Instance = new NullSingleInstance();
    public bool TryAcquire(string mutexName = null) => true;
    public void Release() { }
    public bool IsAnotherInstanceRunning(string mutexName = null) => false;
}

/// <summary>原生单实例实现（委托给 WindowsSingleInstance）。</summary>
public class NativeSingleInstance : ISingleInstance
{
    public static readonly NativeSingleInstance Instance = new NativeSingleInstance();
    public bool TryAcquire(string mutexName = null) => WindowsSingleInstance.TryAcquire(mutexName);
    public void Release()                           => WindowsSingleInstance.Release();
    public bool IsAnotherInstanceRunning(string mutexName = null) => WindowsSingleInstance.IsAnotherInstanceRunning(mutexName);
}
