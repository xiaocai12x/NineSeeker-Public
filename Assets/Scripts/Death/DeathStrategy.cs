using UnityEngine;
using System.Collections;

public abstract class DeathStrategy : ScriptableObject
{
    #region 检视面板字段
    [Header("Base Settings")]
    public float deathDuration = 1.0f;     // 死亡演出持续多久
    public AudioClip hitSFX;          // 受击音效
    public AudioClip deathSFX;        // 死亡音效
    //public GameObject deathVFX;       // 死亡特效预制体

    [Header("Respawn Settings")]
    public float respawnDelay = 0.2f; // 特效播放多久后玩家才显示
    public AudioClip respawnSFX;      // 回溯音效
    //public GameObject respawnVFX;     // 重生特效
    #endregion

    #region 策略接口
    // 1. 玩家怎么死 
    public abstract IEnumerator ExecuteDeath(PlayerController player, Vector3 hitDirection);

    // 2. 屏幕怎么黑 
    public abstract IEnumerator TransitionIn(IDeathRespawnContext ctx);

    // 3. 玩家怎么复活 
    public abstract IEnumerator ExecuteRespawn(PlayerController player, Vector2 spawnPos);

    // 4. 屏幕怎么亮 
    public abstract IEnumerator TransitionOut(IDeathRespawnContext ctx);
    #endregion
}