using UnityEngine;

/// <summary>
/// 任何能给玩家提供“惯性/动量”的物体都要实现这个接口
/// (比如：移动平台、旋转齿轮、传送带)
/// </summary>
public interface IVelocityProvider
{
    // 获取该物体在某个具体世界坐标点的速度
    Vector2 GetVelocityAtPoint(Vector3 worldPoint);
}