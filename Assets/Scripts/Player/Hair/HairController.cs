using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HairController : MonoBehaviour
{
    [Header("Build Support")]
    [Tooltip("手动拖入 URP Sprite Unlit Shader 确保不被剥离")]
    public Shader hairShader;

    [Header("Build Fix: Accessories")]
    [Tooltip("把所有在包里变白的部件（耳朵、刘海）拖入这个数组")]
    public SpriteRenderer[] accessoryRenderers;

    [Header("Anchor Settings")]
    /// <summary>朝右时使用的头发根节点锚点。</summary>
    public Transform hairAnchorRight;
    /// <summary>朝左时使用的头发根节点锚点。</summary>
    public Transform hairAnchorLeft;

    [Header("8x8 Pixel Sprite")]
    /// <summary>头发段贴图，用于材质主纹理。</summary>
    public Sprite hairCircleSprite;

    [Header("Nodes Settings")]
    /// <summary>头发链节点数量，影响段数与长度感。</summary>
    public int segmentCount = 6;
    /// <summary>首段半径，后续段按 0.65 次方递减。</summary>
    public float baseRadius = 0.45f;
    /// <summary>相邻节点标准间距，超过会被约束。</summary>
    public float nodeDistance = 0.16f;

    [Header("Physics Settings")]
    /// <summary>节点跟随上一节点的 Lerp 速度。</summary>
    public float followSpeed = 22f;
    /// <summary>头发下垂重力强度。</summary>
    public float gravity = 12f;
    /// <summary>水平方向阻力，产生迎风感。</summary>
    public float drag = 4f;

    [Header("Juice (Pro 特性)")]
    /// <summary>段长与 nodeDistance 偏差时拉伸系数强度。</summary>
    public float stretchStrength = 0.35f;
    /// <summary>波浪动画角频率。</summary>
    public float waveFrequency = 6f;
    /// <summary>波浪垂直位移幅度。</summary>
    public float waveAmplitude = 0.06f;

    [Header("Crouch Squash (果冻挤压)")]
    [Range(0f, 1f)]
    /// <summary>下蹲时 Y 方向压扁目标比例（如 0.6 即 60%）。</summary>
    public float crouchSquashRatio = 0.6f;
    /// <summary>压扁/回弹插值速度。</summary>
    public float squashSpeed = 15f;

    [Header("Visual Sync")]
    /// <summary>用于读取角色缩放，同步头发半径与压扁。</summary>
    public Transform visualsRoot;

    [Header("Visual Settings (v2.3 Matrix)")]
    /// <summary>可冲刺时的正常发色。</summary>
    public Color colorNormal = new Color32(158, 132, 133, 255);
    /// <summary>体力不足时的灰色。</summary>
    public Color colorUsed = new Color32(158, 158, 158, 255);
    /// <summary>双冲等特殊状态发色。</summary>
    public Color colorDouble = new Color32(158, 90, 126, 255);
    /// <summary>冲刺中高亮色。</summary>
    public Color colorDash = new Color32(158, 41, 41, 255);
    /// <summary>金色等特殊表现。</summary>
    public Color colorGolden = new Color32(168, 144, 81, 255);
    /// <summary>死亡时发色。</summary>
    public Color colorDead = new Color32(133, 133, 133, 255);
    /// <summary>体力回复时的闪烁高亮色。</summary>
    public Color colorRefill = new Color32(126, 173, 189, 255);
    /// <summary>发色向目标插值速度。</summary>
    public float colorLerpSpeed = 15f;

    /// <summary>当前帧插值后的发色，用于 Mesh 与挂件同步。</summary>
    private Color currentHairColor;
    /// <summary>回能时的脉冲放大倍率，>1 时每帧衰减回 1。</summary>
    private float pulseScale = 1f;
    /// <summary>回能闪烁权重，0~1，用于与 colorRefill 混合。</summary>
    private float refillFlashWeight = 0f;
    /// <summary>下蹲压扁当前插值，0~1。</summary>
    private float currentSquash = 1f;

    /// <summary>父级玩家控制器，用于状态与朝向。</summary>
    private PlayerController player;
    /// <summary>玩家精灵渲染器，预留同步用。</summary>
    private SpriteRenderer playerSR;
    /// <summary>本物体 Mesh 过滤器。</summary>
    private MeshFilter meshFilter;
    /// <summary>头发网格实例，每帧重算顶点。</summary>
    private Mesh hairMesh;
    /// <summary>各段节点世界坐标，每帧由 UpdatePositions 更新。</summary>
    private List<Vector2> nodePositions = new List<Vector2>();
    /// <summary>上一帧朝向，用于检测转身并镜像节点。</summary>
    private int lastFacingDirection;

    /// <summary>当前 Mesh 构建用的顶点列表。</summary>
    private List<Vector3> vertices = new List<Vector3>();
    /// <summary>三角形索引。</summary>
    private List<int> triangles = new List<int>();
    /// <summary>UV 坐标。</summary>
    private List<Vector2> uvs = new List<Vector2>();
    /// <summary>顶点色。</summary>
    private List<Color> colors = new List<Color>();

    /// <summary>
    /// 初始化头发网格、材质、节点列表与朝向，仅在运行时执行一次。
    /// </summary>
    private void Awake()
    {
        player = GetComponentInParent<PlayerController>();
        playerSR = player.GetComponentInChildren<SpriteRenderer>();
        meshFilter = GetComponent<MeshFilter>();

        hairMesh = new Mesh();
        hairMesh.name = "CelesteHairMesh_Sync";
        meshFilter.mesh = hairMesh;

        MeshRenderer mr = GetComponent<MeshRenderer>();

        // --- 核心修复：避免在 Build 版使用 Shader.Find ---
        if (hairShader == null)
            hairShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

        Material hairMat = new Material(hairShader);
        if (hairCircleSprite != null)
        {
            hairMat.mainTexture = hairCircleSprite.texture;
        }
        mr.material = hairMat;

        mr.sortingLayerName = "Player";
        mr.sortingOrder = -1;

        nodePositions.Clear();
        for (int i = 0; i < segmentCount; i++) nodePositions.Add(transform.position);
        lastFacingDirection = player.FacingDirection;
        currentHairColor = colorNormal;
    }

    /// <summary>
    /// 每帧更新挤压值、节点位置、头发颜色，生成 Celeste 风格网格并同步挂件颜色。
    /// </summary>
    private void LateUpdate()
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null || !mr.enabled) return;

        // 1. 【核心逻辑】计算挤压值
        // 如果是下蹲状态，目标就是 0.6；否则回弹到 1.0
        float targetSquash = (player.StateMachine.CurrentState == player.CrouchState) ? crouchSquashRatio : 1f;
        currentSquash = Mathf.Lerp(currentSquash, targetSquash, Time.deltaTime * squashSpeed);

        UpdatePositions();
        UpdateHairColor();
        GenerateCelesteStyleMesh();

        // --- 核心修复：每一帧同步挂件颜色 ---
        SyncAccessoryColors();
    }

    /// <summary>
    /// 将当前头发颜色同步到挂件（耳朵、刘海等），修复 Build 线性颜色空间下挂件变白问题。
    /// </summary>
    private void SyncAccessoryColors()
    {
        if (accessoryRenderers == null || accessoryRenderers.Length == 0) return;

        // 处理 Build 版线性颜色空间 (Linear Space) 导致的变白问题
        Color finalSyncColor = currentHairColor;
        if (QualitySettings.activeColorSpace == ColorSpace.Linear)
        {
            finalSyncColor = currentHairColor.linear;
        }

        foreach (var sr in accessoryRenderers)
        {
            if (sr != null)
            {
                sr.color = finalSyncColor;
            }
        }
    }

    /// <summary>
    /// 根据当前锚点与物理参数（重力、阻力、波浪）更新头发链各节点位置，转身时镜像节点。
    /// </summary>
    private void UpdatePositions()
    {
        Transform activeAnchor = player.FacingDirection > 0 ? hairAnchorRight : hairAnchorLeft;
        Vector2 currentAnchorPos = activeAnchor != null ? (Vector2)activeAnchor.position : (Vector2)transform.position;

        float currentFollowSpeed = followSpeed;
        float currentDrag = drag;
        float currentGravity = gravity;

        if (player.IsDead)
        {
            currentFollowSpeed = 5f;
            currentDrag = 1f;
            currentGravity = 25f;
        }

        if (player.FacingDirection != lastFacingDirection)
        {
            for (int i = 1; i < nodePositions.Count; i++)
            {
                float relativeX = nodePositions[i].x - nodePositions[0].x;
                nodePositions[i] = new Vector2(currentAnchorPos.x - relativeX, nodePositions[i].y);
            }
        }
        lastFacingDirection = player.FacingDirection;

        nodePositions[0] = currentAnchorPos;

        for (int i = 1; i < segmentCount; i++)
        {
            Vector2 force = new Vector2(-player.FacingDirection * currentDrag, -currentGravity);
            float wave = Mathf.Sin(Time.time * waveFrequency + i * 0.8f) * waveAmplitude * (i / (float)segmentCount);
            force.y += wave * 50f;

            Vector2 target = nodePositions[i - 1] + force * Time.deltaTime;
            nodePositions[i] = Vector2.Lerp(nodePositions[i], target, Time.deltaTime * currentFollowSpeed);

            float d = Vector2.Distance(nodePositions[i], nodePositions[i - 1]);
            if (d > nodeDistance)
                nodePositions[i] = nodePositions[i - 1] + (nodePositions[i] - nodePositions[i - 1]).normalized * nodeDistance;
        }
    }

    /// <summary>
    /// 清空顶点缓冲，先绘制描边再绘制主色两遍，最后将数据写入 Mesh 用于渲染。
    /// </summary>
    private void GenerateCelesteStyleMesh()
    {
        vertices.Clear(); triangles.Clear(); uvs.Clear(); colors.Clear();

        DrawHairPass(0.125f, Color.black);

        // Mesh 顶点色也需要处理 Linear
        Color meshColor = currentHairColor;
        if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            meshColor = currentHairColor.linear;

        DrawHairPass(0f, meshColor);

        hairMesh.Clear();
        hairMesh.vertices = vertices.ToArray();
        hairMesh.uv = uvs.ToArray();
        hairMesh.triangles = triangles.ToArray();
        hairMesh.colors = colors.ToArray();
    }

    /// <summary>
    /// 按段绘制一遍头发（描边或主色），计算每段半径与拉伸后调用 AddAdvancedQuad 追加四边形。
    /// </summary>
    /// <param name="outlineSize">描边外扩半径，主色传 0</param>
    /// <param name="passColor">本遍使用的颜色</param>
    private void DrawHairPass(float outlineSize, Color passColor)
    {
        float characterScaleX = visualsRoot != null ? visualsRoot.localScale.x : 1f;
        float characterScaleY = visualsRoot != null ? visualsRoot.localScale.y : 1f;

        for (int i = 0; i < segmentCount; i++)
        {
            float r = baseRadius * Mathf.Pow(0.65f, i);
            r = Mathf.Max(0.05f, r);

            float squashFactor = (characterScaleX + characterScaleY) * 0.5f;
            float finalRadius = r * squashFactor * pulseScale;

            float angle = 0;
            float velocityMult = 1f;
            if (i > 0)
            {
                float dist = Vector2.Distance(nodePositions[i], nodePositions[i - 1]);
                float speed = dist / nodeDistance;
                velocityMult = 1f + (speed - 1f) * stretchStrength;
            }

            AddAdvancedQuad(nodePositions[i], finalRadius + outlineSize, passColor, angle, velocityMult);
        }
    }


    /// <summary>
    /// 在指定位置添加一个头发段四边形，使用不同 X/Y 半径与压扁系数生成顶点并追加到当前 Mesh 缓冲。
    /// 注释该方法后头发将不会被渲染。
    /// </summary>
    /// <param name="worldPos">四边形中心的世界坐标</param>
    /// <param name="radius">基础半径</param>
    /// <param name="color">顶点色</param>
    /// <param name="angle">绕 Z 轴旋转角度（度）</param>
    /// <param name="stretch">拉伸系数，影响四边形宽高比</param>
    private void AddAdvancedQuad(Vector2 worldPos, float radius, Color color, float angle, float stretch)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        int vIndex = vertices.Count;

        float halfSizeX = radius * stretch;
        float halfSizeY = radius / (stretch * 0.7f + 0.3f);


        float finalHalfSizeY = halfSizeY * currentSquash;
        float finalHalfSizeX = halfSizeX * (1f + (1f - currentSquash) * 0.5f); // 稍微变宽一点点补偿

        Vector3 v0 = new Vector3(-finalHalfSizeX, -finalHalfSizeY, 0);
        Vector3 v1 = new Vector3(finalHalfSizeX, -finalHalfSizeY, 0);
        Vector3 v2 = new Vector3(-finalHalfSizeX, finalHalfSizeY, 0);
        Vector3 v3 = new Vector3(finalHalfSizeX, finalHalfSizeY, 0);

        Quaternion rot = Quaternion.Euler(0, 0, angle);
        vertices.Add(localPos + rot * v0);
        vertices.Add(localPos + rot * v1);
        vertices.Add(localPos + rot * v2);
        vertices.Add(localPos + rot * v3);

        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));

        triangles.Add(vIndex); triangles.Add(vIndex + 2); triangles.Add(vIndex + 1);
        triangles.Add(vIndex + 1); triangles.Add(vIndex + 2); triangles.Add(vIndex + 3);

        for (int j = 0; j < 4; j++) colors.Add(color);
    }

    /// <summary>
    /// 根据状态插值当前发色，并处理回能闪烁与脉冲缩放衰减。
    /// </summary>
    private void UpdateHairColor()
    {
        Color targetColor = GetPriorityColor();
        currentHairColor = Color.Lerp(currentHairColor, targetColor, Time.deltaTime * colorLerpSpeed);

        if (refillFlashWeight > 0.01f)
        {
            currentHairColor = Color.Lerp(currentHairColor, colorRefill, refillFlashWeight);
            refillFlashWeight = Mathf.MoveTowards(refillFlashWeight, 0f, Time.deltaTime * 5f);
        }

        if (pulseScale > 1f)
        {
            pulseScale = Mathf.MoveTowards(pulseScale, 1f, Time.deltaTime * 4f);
        }
    }

    /// <summary>
    /// 按优先级返回目标发色：死亡 / 冲刺 / 疲劳闪烁 / 可冲刺正常色 / 已用灰色。
    /// </summary>
    private Color GetPriorityColor()
    {
        if (player.IsDead) return colorDead;
        if (player.StateMachine.CurrentState == player.DashState) return colorDash;

        if (player.CurrentStamina < player.playerData.tiredThreshold)
        {
            return (Time.time % 0.2f < 0.1f) ? colorDash : colorUsed;
        }

        return player.CheckIfCanDash() ? colorNormal : colorUsed;
    }

    #region 供外部调用

    /// <summary>
    /// 传送时将所有头发节点瞬移到指定位置并立即刷新网格，避免残影。
    /// </summary>
    /// <param name="position">目标世界坐标</param>
    public void WarpNodes(Vector3 position)
    {
        for (int i = 0; i < nodePositions.Count; i++)
        {
            nodePositions[i] = position;
        }
        GenerateCelesteStyleMesh();
    }

    /// <summary>
    /// 体力回复时由外部调用，触发头发脉冲放大与回能高亮效果。
    /// </summary>
    public void OnRefill()
    {
        pulseScale = 1.35f;
        refillFlashWeight = 1.0f;
    }

    /// <summary>返回当前脉冲缩放值，供外部（如 UI）同步表现。</summary>
    public float GetPulseScale() => pulseScale;

    /// <summary>返回当前插值后的头发颜色，供外部（如挂件、特效）同步。</summary>
    public Color CurrentHairColor => currentHairColor;

    #endregion
}
