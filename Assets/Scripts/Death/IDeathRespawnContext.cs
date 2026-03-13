using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 死亡-重生流程所需上下文，由 DeathRespawnManager 实现。
/// 策略在 TransitionIn / TransitionOut 中通过此接口访问转场 UI、音源与玩家引用，不依赖具体 Manager 类。
/// </summary>
public interface IDeathRespawnContext
{
    Image TransitionImage { get; }
    AudioSource AudioSource { get; }
    PlayerController Player { get; }
}
