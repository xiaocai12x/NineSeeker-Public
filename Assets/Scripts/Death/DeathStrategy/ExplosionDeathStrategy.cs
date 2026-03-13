using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Death Strategy/Explosion (Iris - 8 Balls)")]
public class ExplosionDeathStrategy : DeathStrategy
{
    #region 检视面板字段
    [Header("Iris Resources")]
    public Material irisMaterial;

    [Header("Timing (Celeste Rhythm)")]
    [Tooltip("受击瞬间的顿帧时间 (Freeze Frame)")]
    public float hitStopDuration = 0.1f;
    [Tooltip("收缩转场速度")]
    public float shrinkDuration = 0.3f;
    [Tooltip("扩张转场速度")]
    public float expandDuration = 0.4f;

    [Header("Iris Settings")]
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Range(-2f, 2f)]
    public float centerOffsetY = 0.5f;
    #endregion

    #region 运行时状态
    private Material runtimeMaterial;
    #endregion

    #region 死亡流程
    public override IEnumerator ExecuteDeath(PlayerController player, Vector3 dir)
    {
        AudioSource audio = player.GetComponent<AudioSource>();
        var effect = player.GetComponent<PlayerDeathEffect>();

        // Phase 1: Impact
        if (hitSFX != null && audio != null) audio.PlayOneShot(hitSFX);

        if (player.RB != null)
        {
            player.RB.linearVelocity = Vector2.zero;
            player.RB.simulated = false;
        }

        if (FXManager.Instance != null) FXManager.Instance.ShakeDirectional(dir, 2.0f);
        yield return new WaitForSecondsRealtime(hitStopDuration);

        // Phase 2: Explode
        if (deathSFX != null && audio != null) audio.PlayOneShot(deathSFX);

        if (effect != null)
        {
            // 【关键修改开始】：获取当前头发颜色，而不是用默认白色
            Color explodeColor = Color.white; // 默认兜底色

            // 尝试从 HairController 获取颜色（这是最准的，包含冲刺红、金发等状态）
            var hair = player.GetComponentInChildren<HairController>();
            if (hair != null)
            {
                explodeColor = hair.CurrentHairColor;
            }
            // 如果没有头发脚本，尝试获取 SpriteRenderer 的颜色
            else if (player.SR != null)
            {
                explodeColor = player.SR.color;
            }

            // 传入计算好的颜色
            effect.PlayExplode(player.transform.position, explodeColor);
            // 【关键修改结束】
        }

        player.SetVisualState(false);
        yield return new WaitForSecondsRealtime(0.15f);
    }
    #endregion

    #region 转场进入
    public override IEnumerator TransitionIn(IDeathRespawnContext ctx)
    {
        if (irisMaterial == null) yield break;

        runtimeMaterial = new Material(irisMaterial);
        ctx.TransitionImage.material = runtimeMaterial;
        ctx.TransitionImage.enabled = true;

        float aspect = (float)Screen.width / Screen.height;
        float maxRadius = Mathf.Sqrt(aspect * aspect + 1f);

        float timer = 0f;
        while (timer < shrinkDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / shrinkDuration);

            if (ctx.Player != null)
                UpdateShaderCenter(ctx.Player.transform.position);

            float r = Mathf.Lerp(maxRadius, 0f, transitionCurve.Evaluate(t));
            runtimeMaterial.SetFloat("_Radius", r);
            yield return null;
        }
        runtimeMaterial.SetFloat("_Radius", 0f);
    }
    #endregion

    #region 复活流程
    public override IEnumerator ExecuteRespawn(PlayerController player, Vector2 spawnPos)
    {
        // 1. 基础物理重置
        if (player.RB != null)
        {
            player.RB.linearVelocity = Vector2.zero;
            player.RB.angularVelocity = 0f;
            player.RB.simulated = false;
        }

        // 2. 位置重置
        player.transform.position = spawnPos;
        player.transform.rotation = Quaternion.identity;
        player.transform.localScale = Vector3.one;

        // 3. 【关键】保持隐身，等待黑幕完全退去后再表演
        player.SetVisualState(false);

        // 这里不再调用 effect.PlayReformRoutine
        yield return null;
    }
    #endregion

    #region 转场退出
    public override IEnumerator TransitionOut(IDeathRespawnContext ctx)
    {
        float aspect = (float)Screen.width / Screen.height;
        float maxRadius = Mathf.Sqrt(aspect * aspect + 1f);

        float timer = 0f;
        while (timer < expandDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / expandDuration);

            if (ctx.Player != null) UpdateShaderCenter(ctx.Player.transform.position);

            float r = Mathf.Lerp(0f, maxRadius, transitionCurve.Evaluate(t));
            runtimeMaterial.SetFloat("_Radius", r);

            yield return null;
        }

        if (ctx?.Player != null)
        {
            var effect = ctx.Player.GetComponent<PlayerDeathEffect>();

            if (respawnSFX != null && ctx.AudioSource != null)
                ctx.AudioSource.PlayOneShot(respawnSFX);

            if (effect != null)
            {
                Color normalColor = new Color(0.62f, 0.52f, 0.52f, 1f);
                bool done = false;
                yield return ctx.Player.StartCoroutine(effect.PlayReformRoutine(
                    ctx.Player.transform.position,
                    normalColor,
                    () => done = true
                ));
                while (!done) yield return null;
            }

            ctx.Player.SetVisualState(true);
            ctx.Player.ReviveInternal();
        }

        if (ctx?.TransitionImage != null) ctx.TransitionImage.enabled = false;
    }
    #endregion

    #region 内部辅助方法
    private void UpdateShaderCenter(Vector3 worldPos)
    {
        if (Camera.main == null || runtimeMaterial == null) return;
        Vector3 targetPos = worldPos;
        targetPos.y += centerOffsetY;

        Vector3 viewportPos = Camera.main.WorldToViewportPoint(targetPos);
        runtimeMaterial.SetVector("_Center", new Vector2(viewportPos.x, viewportPos.y));
    }
    #endregion
}