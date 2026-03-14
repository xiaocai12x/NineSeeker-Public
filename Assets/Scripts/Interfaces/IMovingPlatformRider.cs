using UnityEngine;

public interface IMovingPlatformRider
{
    /// <summary>
    /// 被平台强制移动（携带或推挤）。
    /// 注意：必须使用 Rigidbody 修改位置以保持物理同步。
    /// </summary>
    /// <param name="delta">位移向量</param>
    void ManualMove(Vector2 delta);

    /// <summary>
    /// 预判：如果往这个方向被推，会不会撞墙？
    /// 用于平台判断是否应该挤死玩家。
    /// </summary>
    /// <param name="pushDirection">推挤的方向（归一化向量）</param>
    /// <param name="dist">推挤距离</param>
    /// <returns>true = 后面有墙 (死胡同)</returns>
    bool CheckWallForCrush(Vector2 pushDirection, float dist);

    /// <summary>
    /// 触发挤压死亡逻辑
    /// </summary>
    void DieByCrush(DeathStrategy strategy);
}