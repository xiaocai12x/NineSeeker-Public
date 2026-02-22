using UnityEngine;

/// <summary>
/// 控制刘海精灵的锚点跟随、压扁、按动画切换贴图并与主发团颜色同步。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HairBangsController : MonoBehaviour
{
    [Header("Anchor Settings")]
    /// <summary>朝右时刘海跟随的锚点。</summary>
    public Transform hairAnchorRight;
    /// <summary>朝左时刘海跟随的锚点。</summary>
    public Transform hairAnchorLeft;

    [Header("Squash")]
    /// <summary>下蹲时 Y 方向压扁比例，刘海略压即可避免露额过多。</summary>
    public float crouchSquashRatio = 0.8f;

    [Header("Live Tuning")]
    /// <summary>相对锚点的位置偏移，用于微调。</summary>
    public Vector2 baseOffset = Vector2.zero;

    [Header("Sprite Mapping")]
    /// <summary>未匹配到任何动画关键字时使用的默认刘海贴图。</summary>
    public Sprite defaultBangs;
    /// <summary>动画关键字到刘海贴图的映射表。</summary>
    public BangsMapping[] spriteMappings;

    [System.Serializable]
    public class BangsMapping
    {
        /// <summary>玩家动画精灵名中包含的关键字。</summary>
        public string animationKeyword;
        /// <summary>该动画对应的刘海贴图。</summary>
        public Sprite bangsSprite;
    }

    [Header("Color Sync")]
    /// <summary>主发团控制器，用于同步发色。</summary>
    public HairController hairMaster;

    /// <summary>父级玩家控制器，用于朝向与状态。</summary>
    private PlayerController player;
    /// <summary>玩家本体精灵，用于读取当前动画贴图名。</summary>
    private SpriteRenderer playerSR;
    /// <summary>刘海自身的精灵渲染器。</summary>
    private SpriteRenderer bangsSR;
    /// <summary>刘海使用的自定义亮度同步材质。</summary>
    private Material bangsMat;

    /// <summary>上一帧玩家贴图名，用于缓存避免重复计算。</summary>
    private string lastSpriteName;

    private void Awake()
    {
        player = GetComponentInParent<PlayerController>();
        playerSR = player.GetComponentInChildren<SpriteRenderer>();
        bangsSR = GetComponent<SpriteRenderer>();

        // 统一使用自定义的 HSV Luminance Shader
        Shader hsvShader = Shader.Find("Custom/Bangs_Luminance_Sync");
        if (hsvShader != null)
        {
            bangsMat = new Material(hsvShader);
            bangsSR.material = bangsMat;
        }

        bangsSR.sortingLayerName = "Player";
        bangsSR.sortingOrder = 10; // 确保在最前面（根据白皮书v2.2调至10）
    }

    private void LateUpdate()
    {
        // 死亡拦截或隐藏拦截
        if (player == null || !bangsSR.enabled) return;

        // 1. 位置同步：选择锚点，锚点为 null 时用主发团或角色 Visuals 位置作 fallback，避免与发团脱节
        Transform activeAnchor = player.FacingDirection > 0 ? hairAnchorRight : hairAnchorLeft;
        Vector2 targetPos;
        if (activeAnchor != null)
            targetPos = (Vector2)activeAnchor.position + baseOffset;
        else if (hairMaster != null)
            targetPos = (Vector2)hairMaster.transform.position + baseOffset;
        else if (player.Visuals != null)
            targetPos = (Vector2)player.Visuals.transform.position + baseOffset;
        else
            targetPos = (Vector2)transform.position + baseOffset;
        transform.position = targetPos;

        // 2. 朝向同步
        bangsSR.flipX = playerSR.flipX;

        // 3. 【核心优化】底图切换逻辑
        UpdateBangsSpriteWithCache();

        // 4. 挤压同步
        float targetY = (player.StateMachine.CurrentState == player.CrouchState) ? crouchSquashRatio : 1f;

        // 使用简单的 Lerp 让它跟头发和尾巴的节奏保持一致
        Vector3 currentScale = transform.localScale;
        float newY = Mathf.Lerp(currentScale.y, targetY, Time.deltaTime * 15f);

        // 保持 X 轴方向 (flipX 也是一种 scale 变换，但这里我们只动 Y，X 保持绝对值的 1)
        // 注意：如果你的 FlipX 逻辑依赖 Scale.x = -1，请根据实际情况调整。
        // 通常 SpriteRenderer 的 FlipX 属性优于直接改 Scale.x。
        transform.localScale = new Vector3(1f, newY, 1f);

        // 5. 颜色同步
        if (hairMaster != null && bangsMat != null)
        {
            bangsMat.SetColor("_BaseColor", hairMaster.CurrentHairColor);
        }
    }

    /// <summary>
    /// 根据玩家当前精灵名匹配 BangsMapping，仅在贴图名变化时切换刘海贴图。
    /// </summary>
    private void UpdateBangsSpriteWithCache()
    {
        if (playerSR.sprite == null) return;

        string currentSpriteName = playerSR.sprite.name;

        // 只有当玩家本体的 Sprite 发生变化时，才重新计算刘海贴图
        if (currentSpriteName == lastSpriteName) return;

        lastSpriteName = currentSpriteName;
        string nameLower = currentSpriteName.ToLower();

        bool matched = false;
        foreach (var mapping in spriteMappings)
        {
            if (nameLower.Contains(mapping.animationKeyword.ToLower()))
            {
                ApplyNewSprite(mapping.bangsSprite);
                matched = true;
                break;
            }
        }

        // 如果没匹配到，回归默认
        if (!matched && defaultBangs != null)
        {
            ApplyNewSprite(defaultBangs);
        }
    }

    /// <summary>
    /// 应用新的刘海贴图并同步到材质主纹理，避免重复设置相同贴图。
    /// </summary>
    /// <param name="newSprite">要切换到的刘海贴图</param>
    private void ApplyNewSprite(Sprite newSprite)
    {
        if (bangsSR.sprite == newSprite) return;

        bangsSR.sprite = newSprite;

        // 【关键修复】：确保自定义材质拿到最新的贴图纹理
        if (bangsMat != null && newSprite != null)
        {
            bangsMat.SetTexture("_MainTex", newSprite.texture);
        }
    }
}
