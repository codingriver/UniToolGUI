using System;

/// <summary>
/// 单实例检测接口。
/// </summary>
public interface ISingleInstance
{
    /// <summary>尝试获取单实例锁，返回 false 表示已有实例在运行</summary>
    bool TryAcquire(string mutexName = null);
    void Release();
    bool IsAnotherInstanceRunning(string mutexName = null);
}
